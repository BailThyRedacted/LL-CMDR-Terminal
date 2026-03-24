using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EliteDataCollector.Core.Models;
using EliteDataCollector.Core.Services;

namespace ColonizationModule
{
    /// <summary>
    /// Colonization Module - Monitors BGS (Background Simulation) and PowerPlay state.
    ///
    /// Purpose: Track all visited systems and upload BGS/PowerPlay data:
    /// - Track location (via FSDJump and Location events)
    /// - Extract BGS faction states and influence (especially Lavigny's Legion)
    /// - Extract PowerPlay power and allegiance
    /// - Upload ALL systems to INARA API (for commander's personal record)
    /// - Upload MATCHING systems to Supabase (for central database)
    ///
    /// Data Collection:
    /// - System name and address
    /// - Timestamp of visit
    /// - Controlling faction and influence %
    /// - Lavigny's Legion influence % (tracked separately)
    /// - System allegiance and state
    /// - BGS faction list (all factions, their influence, state)
    /// - PowerPlay: controlling power, power state
    ///
    /// Teaching: Updated GameLoopModule demonstrating:
    /// - INARA authentication integration
    /// - Dual-destination data upload (INARA + Supabase)
    /// - Filtering by system list for Supabase
    /// - BGS-specific data extraction
    /// - PowerPlay tracking
    /// </summary>
    public class ColonizationModule : GameLoopModule
    {
        // ========== CONSTANTS ==========

        private const string MODULE_NAME = "Colonization";
        private const string MODULE_DESC = "Monitors BGS and PowerPlay state, uploads to INARA and Supabase";

        private static readonly HashSet<string> IMPORTANT_EVENTS = new(StringComparer.Ordinal)
        {
            "Location",          // Player entered a station/surface
            "FSDJump",           // Player jumped to new system
        };

        // ========== INJECTED SERVICES ==========

        private SupabaseClient? _supabaseClient;
        private InaraAuth? _inaraAuth;
        private OutputWriter? _outputWriter;

        // ========== MODULE STATE ==========

        private string? _currentSystem = null;
        private long _currentSystemAddress = 0;
        private int _commanderId = 0;
        private HashSet<string> _targetSystems = new(StringComparer.OrdinalIgnoreCase);

        // ========== MODULE PROPERTIES ==========

        public string Name => MODULE_NAME;
        public string Description => MODULE_DESC;

        // ========== LIFECYCLE METHODS ==========

        /// <summary>
        /// Initialize the colonization module.
        /// Extract services and load target systems from Supabase.
        /// </summary>
        public async Task InitializeAsync(IServiceProvider services)
        {
            try
            {
                _outputWriter = (OutputWriter?)services.GetService(typeof(OutputWriter));
                _supabaseClient = (SupabaseClient?)services.GetService(typeof(SupabaseClient));
                _inaraAuth = (InaraAuth?)services.GetService(typeof(InaraAuth));

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Initializing...");

                // Load target systems from Supabase
                await LoadTargetSystems();

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Ready to track BGS and PowerPlay data.");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during initialization: {ex.Message}");
            }
        }

        /// <summary>
        /// Process journal line events from the game.
        /// Filter to Location and FSDJump events only.
        /// </summary>
        public async Task OnJournalLineAsync(string line, JsonDocument parsedEvent)
        {
            try
            {
                if (!parsedEvent.RootElement.TryGetProperty("event", out var eventProp))
                    return;

                var eventType = eventProp.GetString();
                if (eventType == null || !IMPORTANT_EVENTS.Contains(eventType))
                    return;

                // Process location events
                if (eventType == "Location" || eventType == "FSDJump")
                {
                    await ProcessLocationOrFsdJump(parsedEvent);
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
            try
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Shutting down...");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during shutdown: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        // ========== PRIVATE EVENT HANDLERS ==========

        /// <summary>
        /// Process Location or FSDJump event.
        /// Extract BGS and PowerPlay data, upload to INARA and (if matching) Supabase.
        /// </summary>
        private async Task ProcessLocationOrFsdJump(JsonDocument parsedEvent)
        {
            try
            {
                var root = parsedEvent.RootElement;

                // Extract system name and address
                if (!root.TryGetProperty("StarSystem", out var systemProp))
                    return;

                var systemName = systemProp.GetString();
                if (systemName == null)
                    return;

                _currentSystem = systemName;

                if (root.TryGetProperty("SystemAddress", out var addressProp))
                {
                    _currentSystemAddress = addressProp.GetInt64();
                }

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Location: {systemName}");

                // Parse system data (BGS and PowerPlay info)
                var systemData = ParseSystemData(parsedEvent);

                // Always upload to INARA (all systems)
                await UploadToInara(systemData);

                // Only upload to Supabase if system is in target list
                if (_targetSystems.Contains(systemName))
                {
                    await UploadToSupabase(systemData);
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR processing location: {ex.Message}");
            }
        }

        // ========== PRIVATE DATA EXTRACTION ==========

        /// <summary>
        /// Parse system data from Location/FSDJump event.
        /// Extract: faction influence, Lavigny's Legion influence, PowerPlay info, etc.
        /// </summary>
        private SystemData ParseSystemData(JsonDocument eventJson)
        {
            var root = eventJson.RootElement;
            var timestamp = DateTime.UtcNow;

            if (root.TryGetProperty("timestamp", out var timestampProp))
            {
                if (DateTime.TryParse(timestampProp.GetString(), out var parsedTime))
                    timestamp = parsedTime;
            }

            var systemName = root.TryGetProperty("StarSystem", out var systemProp)
                ? systemProp.GetString() ?? "Unknown"
                : "Unknown";

            var systemAddress = root.TryGetProperty("SystemAddress", out var addressProp)
                ? addressProp.GetInt64()
                : 0;

            // Extract controlling faction
            var controllingFaction = root.TryGetProperty("SystemFaction", out var factionProp) &&
                                    factionProp.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? "Unknown"
                : "Unknown";

            // Extract PowerPlay info
            var power = root.TryGetProperty("Power", out var powerProp) &&
                       powerProp.ValueKind != JsonValueKind.Null
                ? powerProp.GetString()
                : null;

            var powerState = root.TryGetProperty("PowerplayState", out var stateProp) &&
                            stateProp.ValueKind != JsonValueKind.Null
                ? stateProp.GetString()
                : null;

            // Parse factions (includes Lavigny's Legion influence)
            var factions = ParseFactions(eventJson);

            // Extract Lavigny's Legion influence specifically
            var lavignyInfluence = factions
                .FirstOrDefault(f => f.Name.Contains("Legion", StringComparison.OrdinalIgnoreCase))
                ?.Influence ?? 0.0;

            return new SystemData
            {
                Id = systemAddress,
                SystemName = systemName,
                Timestamp = timestamp,
                ControllingFaction = controllingFaction,
                Power = power ?? "None",
                PowerState = powerState ?? "None",
                Factions = factions,
                Structures = new List<Structure>(),
                // Additional fields (will add to model)
                LavignyInfluence = lavignyInfluence
            };
        }

        /// <summary>
        /// Parse faction list from event.
        /// </summary>
        private List<FactionInfluence> ParseFactions(JsonDocument eventJson)
        {
            var factions = new List<FactionInfluence>();

            var root = eventJson.RootElement;
            if (!root.TryGetProperty("Factions", out var factionsArray))
                return factions;

            foreach (var factionElement in factionsArray.EnumerateArray())
            {
                var name = factionElement.TryGetProperty("Name", out var nameProp)
                    ? nameProp.GetString() ?? "Unknown"
                    : "Unknown";

                var influence = factionElement.TryGetProperty("Influence", out var influenceProp)
                    ? influenceProp.GetDouble()
                    : 0.0;

                var state = factionElement.TryGetProperty("FactionState", out var stateProp)
                    ? stateProp.GetString() ?? "None"
                    : "None";

                var allegiance = factionElement.TryGetProperty("Allegiance", out var alleg)
                    ? alleg.GetString() ?? "Independent"
                    : "Independent";

                factions.Add(new FactionInfluence
                {
                    Name = name,
                    Influence = influence,
                    State = state,
                    Allegiance = allegiance
                });
            }

            return factions;
        }

        // ========== PRIVATE UPLOAD METHODS ==========

        /// <summary>
        /// Upload system data to INARA (all systems).
        /// Uses INARA API for commander's personal record.
        /// </summary>
        private async Task UploadToInara(SystemData systemData)
        {
            try
            {
                if (_inaraAuth == null)
                {
                    _outputWriter?.WriteLine($"[{MODULE_NAME}] INARA service not available, skipping upload");
                    return;
                }

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Uploading to INARA: {systemData.SystemName}");

                // TODO: Implement INARA upload logic
                // Would call: _inaraAuth.SubmitSystemDataAsync(systemData)

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR uploading to INARA: {ex.Message}");
                // Graceful degradation - don't fail the module
            }
        }

        /// <summary>
        /// Upload system data to Supabase (only matching systems).
        /// For systems in target list only.
        /// </summary>
        private async Task UploadToSupabase(SystemData systemData)
        {
            try
            {
                if (_supabaseClient == null)
                {
                    _outputWriter?.WriteLine($"[{MODULE_NAME}] Supabase service not available, skipping upload");
                    return;
                }

                if (!_targetSystems.Contains(systemData.SystemName))
                {
                    _outputWriter?.WriteLine($"[{MODULE_NAME}] System '{systemData.SystemName}' not in target list, skipping Supabase upload");
                    return;
                }

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Uploading to Supabase: {systemData.SystemName}");

                await _supabaseClient.UpsertSystemDataAsync(systemData);

                _outputWriter?.WriteLine($"[{MODULE_NAME}] ✓ Uploaded to Supabase: {systemData.SystemName}");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR uploading to Supabase: {ex.Message}");
                // Graceful degradation - don't fail the module
            }
        }

        // ========== PRIVATE LOADING METHODS ==========

        /// <summary>
        /// Load target systems from Supabase.
        /// These are the systems we monitor and report to Supabase.
        /// </summary>
        private async Task LoadTargetSystems()
        {
            try
            {
                if (_supabaseClient == null)
                {
                    _outputWriter?.WriteLine($"[{MODULE_NAME}] Supabase not configured, tracking all systems (INARA only)");
                    return;
                }

                var systems = await _supabaseClient.GetTargetSystemsAsync();
                _targetSystems = new HashSet<string>(systems, StringComparer.OrdinalIgnoreCase);

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Loaded {_targetSystems.Count} target systems from Supabase");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] WARNING: Could not load target systems: {ex.Message}");
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Continuing - will track all systems for INARA");
                _targetSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
