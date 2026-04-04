using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using EliteDataCollector.Core.Models;
using EliteDataCollector.Core.Services;

namespace PowerplayModule
{
    /// <summary>
    /// PowerPlay Module - Tracks commander PowerPlay activity and ALD system state.
    ///
    /// Purpose:
    ///   1. Record every player PowerPlay action (Collect, Deliver, Vote, Salary,
    ///      FastTrack, Join, Defect, Leave) into the powerplay_activities Supabase table.
    ///   2. Track the PowerPlay state of all Arissa Lavigny-Duval controlled systems
    ///      visited during a session into the powerplay_systems Supabase table.
    ///
    /// Journal events handled:
    ///   Location, FSDJump        — update current system state; upsert if ALD system
    ///   PowerplayCollect         — commodities collected for power
    ///   PowerplayDeliver         — commodities delivered to system
    ///   PowerplayVote            — voted for system expansion/consolidation
    ///   PowerplaySalary          — salary payment received
    ///   PowerplayFastTrack       — merits fast-tracked (credits spent)
    ///   PowerplayJoin            — pledged to a new power
    ///   PowerplayDefect          — defected to another power
    ///   PowerplayLeave           — left a power without pledging another
    ///
    /// Data access: SupabaseClient (injected via IServiceProvider)
    /// Module disabled by default — enable via settings (PowerplayEnabled = true).
    /// </summary>
    public class PowerplayModule : GameLoopModule
    {
        // ========== CONSTANTS ==========

        private const string MODULE_NAME = "Powerplay";
        private const string MODULE_DESC = "Tracks PowerPlay activity and ALD system state, uploads to Supabase";

        /// <summary>
        /// Only ALD-controlled systems are uploaded to powerplay_systems.
        /// Case-sensitive match against the journal "Power" field.
        /// </summary>
        private const string ALD_POWER = "Arissa Lavigny-Duval";

        private static readonly HashSet<string> LOCATION_EVENTS = new(StringComparer.Ordinal)
        {
            "Location",
            "FSDJump"
        };

        private static readonly HashSet<string> ACTIVITY_EVENTS = new(StringComparer.Ordinal)
        {
            "PowerplayCollect",
            "PowerplayDeliver",
            "PowerplayVote",
            "PowerplaySalary",
            "PowerplayFastTrack",
            "PowerplayJoin",
            "PowerplayDefect",
            "PowerplayLeave"
        };

        // ========== INJECTED SERVICES ==========

        private SupabaseClient? _supabaseClient;
        private OutputWriter? _outputWriter;

        // ========== SESSION STATE ==========

        /// <summary>Current star system name, updated on every FSDJump/Location.</summary>
        private string? _currentSystem = null;

        /// <summary>Current PowerPlay power controlling the system, updated on every FSDJump/Location.</summary>
        private string? _currentPower = null;

        // ========== MODULE PROPERTIES ==========

        public string Name => MODULE_NAME;
        public string Description => MODULE_DESC;

        // ========== LIFECYCLE METHODS ==========

        /// <summary>
        /// Initialise the module by extracting service dependencies from the DI container.
        /// </summary>
        public async Task InitializeAsync(IServiceProvider services)
        {
            try
            {
                _outputWriter = (OutputWriter?)services.GetService(typeof(OutputWriter));
                _supabaseClient = (SupabaseClient?)services.GetService(typeof(SupabaseClient));

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Initializing...");

                if (_supabaseClient == null)
                    _outputWriter?.WriteLine($"[{MODULE_NAME}] WARNING: SupabaseClient not available. Data will not be uploaded.");

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Ready. Tracking ALD systems and commander PowerPlay activity.");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during initialization: {ex.Message}");
            }
        }

        /// <summary>
        /// Route each journal line to the appropriate handler.
        /// Must return quickly — called for every journal event during gameplay.
        /// </summary>
        public async Task OnJournalLineAsync(string line, JsonDocument parsedEvent)
        {
            try
            {
                if (!parsedEvent.RootElement.TryGetProperty("event", out var eventProp))
                    return;

                var eventType = eventProp.GetString();
                if (eventType == null)
                    return;

                if (LOCATION_EVENTS.Contains(eventType))
                {
                    await HandleLocationEventAsync(parsedEvent);
                }
                else if (ACTIVITY_EVENTS.Contains(eventType))
                {
                    await HandleActivityEventAsync(eventType, parsedEvent);
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR processing event: {ex.Message}");
            }
        }

        public Task OnCapiProfileAsync(JsonDocument profile) => Task.CompletedTask;

        public Task ShutdownAsync()
        {
            _outputWriter?.WriteLine($"[{MODULE_NAME}] Shutting down.");
            return Task.CompletedTask;
        }

        // ========== EVENT HANDLERS ==========

        /// <summary>
        /// Handle FSDJump or Location events.
        /// Always update local system state so subsequent activity events
        /// can include the correct system context.
        /// Upload system data to Supabase if the controlling power is ALD.
        /// </summary>
        private async Task HandleLocationEventAsync(JsonDocument parsedEvent)
        {
            var root = parsedEvent.RootElement;

            // Extract system name
            if (!root.TryGetProperty("StarSystem", out var systemProp))
                return;

            var systemName = systemProp.GetString();
            if (systemName == null)
                return;

            // Extract SystemAddress
            long systemAddress = 0;
            if (root.TryGetProperty("SystemAddress", out var addressProp))
                systemAddress = addressProp.GetInt64();

            // Extract PowerPlay power (nullable in journal)
            string? power = null;
            if (root.TryGetProperty("Power", out var powerProp) &&
                powerProp.ValueKind != JsonValueKind.Null)
            {
                power = powerProp.GetString();
            }

            // Extract PowerPlay state (nullable in journal — PP2.0 uses "PowerplayState")
            string? powerState = null;
            if (root.TryGetProperty("PowerplayState", out var stateProp) &&
                stateProp.ValueKind != JsonValueKind.Null)
            {
                powerState = stateProp.GetString();
            }

            // Update session state so activity events have system context
            _currentSystem = systemName;
            _currentPower = power;

            _outputWriter?.WriteLine($"[{MODULE_NAME}] System: {systemName} | Power: {power ?? "none"} | State: {powerState ?? "none"}");

            // Only upload ALD-controlled systems
            if (!string.Equals(power, ALD_POWER, StringComparison.Ordinal))
                return;

            if (_supabaseClient == null)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Supabase not available, skipping system upload.");
                return;
            }

            var system = new PowerplaySystem
            {
                Id = systemAddress,
                SystemName = systemName,
                Power = power ?? ALD_POWER,
                PowerState = powerState ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            await _supabaseClient.UpsertPowerplaySystemAsync(system);
        }

        /// <summary>
        /// Handle any of the PowerPlay activity events.
        /// Builds a <see cref="PowerplayActivity"/> record using data from the journal
        /// event combined with current session state, then inserts into Supabase.
        /// </summary>
        private async Task HandleActivityEventAsync(string eventType, JsonDocument parsedEvent)
        {
            if (_supabaseClient == null)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Supabase not available, skipping activity insert: {eventType}");
                return;
            }

            var root = parsedEvent.RootElement;

            // Parse timestamp from journal
            var timestamp = DateTime.UtcNow;
            if (root.TryGetProperty("timestamp", out var tsProp) &&
                DateTime.TryParse(tsProp.GetString(), out var parsedTs))
            {
                timestamp = parsedTs;
            }

            // Build activity — fields are event-specific and may be null
            var activity = new PowerplayActivity
            {
                EventType = eventType,
                Power = _currentPower,
                SystemName = _currentSystem,
                Timestamp = timestamp
            };

            // PowerplayCollect / PowerplayDeliver
            //   Journal fields: Type (commodity), Count
            if (eventType == "PowerplayCollect" || eventType == "PowerplayDeliver")
            {
                if (root.TryGetProperty("Type", out var typeProp))
                    activity.ItemType = typeProp.GetString();

                if (root.TryGetProperty("Count", out var countProp))
                    activity.Count = countProp.GetInt32();
            }

            // PowerplayVote
            //   Journal fields: Votes (number of votes cast)
            if (eventType == "PowerplayVote")
            {
                if (root.TryGetProperty("Votes", out var votesProp))
                    activity.Votes = votesProp.GetInt32();
            }

            // PowerplaySalary
            //   Journal fields: Amount (credits received)
            if (eventType == "PowerplaySalary")
            {
                if (root.TryGetProperty("Amount", out var amountProp))
                    activity.Amount = amountProp.GetInt64();
            }

            // PowerplayFastTrack
            //   Journal fields: Amount (credits spent), Merits (merits bought — PP2.0)
            if (eventType == "PowerplayFastTrack")
            {
                if (root.TryGetProperty("Amount", out var ftAmountProp))
                    activity.Amount = ftAmountProp.GetInt64();

                if (root.TryGetProperty("Merits", out var ftMeritsProp))
                    activity.Merits = ftMeritsProp.GetInt32();
            }

            // PowerplayJoin / PowerplayDefect
            //   Journal fields: Power (the power being joined/defected to)
            //   Override Power field from the event itself (more accurate than session state)
            if (eventType == "PowerplayJoin" || eventType == "PowerplayDefect")
            {
                if (root.TryGetProperty("Power", out var joinPowerProp) &&
                    joinPowerProp.ValueKind != JsonValueKind.Null)
                {
                    activity.Power = joinPowerProp.GetString();
                }

                // Update session state immediately
                _currentPower = activity.Power;
            }

            // PowerplayLeave has no extra fields; the Power is already in session state

            _outputWriter?.WriteLine($"[{MODULE_NAME}] Activity: {eventType} | Power: {activity.Power ?? "none"} | System: {activity.SystemName ?? "unknown"}");

            await _supabaseClient.InsertPowerplayActivityAsync(activity);
        }
    }
}
