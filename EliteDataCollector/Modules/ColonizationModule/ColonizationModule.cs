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
    /// Purpose: Track colonization progress in Elite Dangerous by monitoring:
    /// - Current system location (via FSDJump and Location events)
    /// - BGS faction states and influence
    /// - PowerPlay power and allegiance
    /// - Structure/project progress
    ///
    /// Data Flow:
    /// 1. Player jumps to system → FSDJump event → Update _currentSystem
    /// 2. Check if current system is in target list
    /// 3. Extract BGS faction data and PowerPlay allegiance
    /// 4. Upload to Supabase via SupabaseClient service
    /// 5. Repeat for each system visit
    ///
    /// Teaching: This is the first game loop module, demonstrating:
    /// - GameLoopModule interface implementation
    /// - Dependency injection of services
    /// - JSON event parsing and filtering
    /// - State management (tracking current system)
    /// - Async/await for background operations
    /// - Error resilience (never throw from event handlers)
    /// </summary>
    public class ColonizationModule : GameLoopModule
    {
        // ========== CONSTANTS ==========

        private const string MODULE_NAME = "Colonization";
        private const string MODULE_DESC = "Monitors and reports colonization progress to Supabase";

        /// <summary>
        /// Important events to process. All others are silently skipped.
        /// This filtering reduces CPU usage and keeps logs clean.
        /// </summary>
        private static readonly HashSet<string> IMPORTANT_EVENTS = new(StringComparer.Ordinal)
        {
            "Location",          // Player entered a station/surface
            "FSDJump",           // Player jumped to new system
            "StructureBuy",      // Colonization project started
            "StructureRepair",   // Project being worked on
            "StructureSell",     // Project being cancelled/transferred
            "StructureTransfer"  // Project transferred to another faction
        };

        // ========== INJECTED SERVICES ==========

        /// <summary>Supabase client for database operations</summary>
        private SupabaseClient _supabaseClient = null!;

        /// <summary>Optional output writer for logging</summary>
        private OutputWriter? _outputWriter;

        // ========== MODULE STATE ==========

        /// <summary>Currently detected system name (updated on Location/FSDJump)</summary>
        private string? _currentSystem = null;

        /// <summary>Currently detected system address (game provides this)</summary>
        private long _currentSystemAddress = 0;

        /// <summary>
        /// Set of target systems to monitor (case-insensitive for robustness)
        /// Loaded once on startup from Supabase.
        /// O(1) lookup performance for filtering.
        /// </summary>
        private HashSet<string> _targetSystems = new(StringComparer.OrdinalIgnoreCase);

        // ========== MODULE PROPERTIES ==========

        public string Name => MODULE_NAME;
        public string Description => MODULE_DESC;

        // ========== LIFECYCLE METHODS ==========

        /// <summary>
        /// Initializes the colonization module.
        ///
        /// Teaching: Module initialization demonstrates:
        /// - Extracting dependencies from IServiceProvider
        /// - Loading configuration on startup
        /// - Setting up initial state
        /// - Error handling (log but continue gracefully)
        ///
        /// This runs once when the app starts, before any events are processed.
        /// </summary>
        public async Task InitializeAsync(IServiceProvider services)
        {
            try
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Initializing...");

                // Extract services from dependency injection container
                _supabaseClient = (SupabaseClient?)services.GetService(typeof(SupabaseClient))
                    ?? throw new InvalidOperationException("SupabaseClient service not registered");

                _outputWriter = (OutputWriter?)services.GetService(typeof(OutputWriter));

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Dependencies injected successfully");

                // Load target systems from Supabase on startup
                await LoadTargetSystems();

                _outputWriter?.WriteLine(
                    $"[{MODULE_NAME}] Initialized successfully. Monitoring {_targetSystems.Count} target systems.");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during initialization: {ex.Message}");
                // Continue anyway - module will process all events if target list fails to load
            }
        }

        /// <summary>
        /// Processes a journal line event from the game.
        ///
        /// Teaching: This is called frequently and must be fast
        /// - Check event type against important events
        /// - Route to appropriate handler
        /// - Never throw (catch all exceptions)
        /// - Log important events for debugging
        ///
        /// Performance: Must complete without blocking because JournalMonitor
        /// continues raising events at game's pace (~10 events/sec during active play).
        /// </summary>
        public async Task OnJournalLineAsync(string line, JsonDocument parsedEvent)
        {
            try
            {
                // Extract event type from parsed JSON
                if (!parsedEvent.RootElement.TryGetProperty("event", out var eventProp))
                    return;

                var eventType = eventProp.GetString();
                if (eventType == null)
                    return;

                // Filter: only process important events
                if (!IMPORTANT_EVENTS.Contains(eventType))
                    return;

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Processing event: {eventType}");

                // Route to appropriate handler based on event type
                if (eventType == "Location" || eventType == "FSDJump")
                {
                    // Update current system tracking
                    await ProcessLocationOrFsdJump(parsedEvent);
                }
                else if (eventType.StartsWith("Structure"))
                {
                    // Process structure/colonization projects
                    await ProcessStructureEvent(parsedEvent, eventType);
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR processing event: {ex.Message}");
                // Continue despite errors - background processing should never crash
            }
        }

        /// <summary>
        /// Processes commander profile updates from CAPI (optional).
        ///
        /// Teaching: Kept for interface compliance. Can be extended later
        /// to extract rank, assets, or other profile-specific data.
        ///
        /// For now, this is a no-op (empty implementation).
        /// </summary>
        public Task OnCapiProfileAsync(JsonDocument profile)
        {
            // Not used in this phase - could extract profile data (rank, credits, etc.) in future
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shuts down the colonization module.
        ///
        /// Teaching: Cleanup on app exit
        /// - Save any pending state
        /// - Close connections
        /// - Release resources
        /// - Never throw
        ///
        /// In this implementation, nothing needs cleanup, but pattern is shown
        /// for modules that might need to save pending uploads or close connections.
        /// </summary>
        public Task ShutdownAsync()
        {
            try
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Shutting down...");
                // No resources to clean up for this module
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during shutdown: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        // ========== PRIVATE HELPER METHODS ==========

        /// <summary>
        /// Loads target systems from Supabase on startup.
        ///
        /// Teaching: Initialization pattern for cached data
        /// - Fetch once on startup (single Supabase query)
        /// - Store in local HashSet for fast lookups
        /// - Handle failure gracefully (continue with empty list)
        ///
        /// Trade-off: Requires app restart to see new target systems.
        /// Benefit: Single query on startup, O(1) lookups throughout app lifetime.
        /// </summary>
        private async Task LoadTargetSystems()
        {
            try
            {
                var systems = await _supabaseClient.GetTargetSystemsAsync();
                _targetSystems = new HashSet<string>(systems, StringComparer.OrdinalIgnoreCase);
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Loaded {_targetSystems.Count} target systems");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] WARNING: Failed to load target systems: {ex.Message}");
                _targetSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Processes Location and FSDJump events to track current system.
        ///
        /// Teaching: Event handler for location events
        /// - Extract system name and address
        /// - Check if in target system (filter before parsing expensive BGS data)
        /// - Parse full system data (name, factions, power, power state)
        /// - Upload to Supabase
        ///
        /// Example JSON (Location event):
        /// {
        ///   "timestamp": "2026-03-21T14:15:30Z",
        ///   "event": "Location",
        ///   "SystemAddress": 123456789,
        ///   "StarSystem": "Sol",
        ///   "SystemAllegiance": "Federation",
        ///   "SystemFaction": { "name": "Federation" },
        ///   "Factions": [
        ///     { "name": "Sol Democrats", "Allegiance": "Federation", "Influence": 0.85, "FactionState": "Boom" },
        ///     { "name": "Sol Liberals", "Allegiance": "Federation", "Influence": 0.15, "FactionState": "None" }
        ///   ],
        ///   "PowerplayState": "Contested",
        ///   "Powers": ["Li Yong-Rui"]
        /// }
        /// </summary>
        private async Task ProcessLocationOrFsdJump(JsonDocument parsedEvent)
        {
            try
            {
                var root = parsedEvent.RootElement;

                // Extract system name and address
                if (!root.TryGetProperty("StarSystem", out var systemNameProp))
                    return;

                var systemName = systemNameProp.GetString();
                if (systemName == null)
                    return;

                _currentSystem = systemName;

                // Extract system address for database key
                if (root.TryGetProperty("SystemAddress", out var addressProp))
                {
                    _currentSystemAddress = addressProp.GetInt64();
                }

                // Filter: Only process if in target system
                if (!_targetSystems.Contains(systemName))
                {
                    _outputWriter?.WriteLine(
                        $"[{MODULE_NAME}] System '{systemName}' not in target list. Skipping.");
                    return;
                }

                _outputWriter?.WriteLine(
                    $"[{MODULE_NAME}] Current system: {systemName} (target system detected)");

                // Parse full system data
                var systemData = ParseSystemData(parsedEvent);

                // Upload to Supabase
                await _supabaseClient.UpsertSystemDataAsync(systemData);

                _outputWriter?.WriteLine(
                    $"[{MODULE_NAME}] System data uploaded: {systemData.Factions?.Count ?? 0} factions");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine(
                    $"[{MODULE_NAME}] ERROR processing location event: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes structure-related events (building, repairing, selling).
        ///
        /// Teaching: Event handler for structure events
        /// - Extract system address and structure data
        /// - Filter: only process if in target system
        /// - Parse structure list
        /// - Upload to Supabase
        /// </summary>
        private async Task ProcessStructureEvent(JsonDocument parsedEvent, string eventType)
        {
            try
            {
                _outputWriter?.WriteLine(
                    $"[{MODULE_NAME}] Processing structure event: {eventType}");

                // Filter: Only process if in target system
                if (string.IsNullOrWhiteSpace(_currentSystem) ||
                    !_targetSystems.Contains(_currentSystem))
                {
                    return;
                }

                // For now, just log. Full implementation would extract structure data
                // and upload to Supabase structures table
                _outputWriter?.WriteLine(
                    $"[{MODULE_NAME}] Structure event in target system '{_currentSystem}'");

                // TODO: Parse structure data and upload
                // var structures = ParseStructures(parsedEvent);
                // await _supabaseClient.UpsertStructuresAsync(_currentSystemAddress, structures);
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine(
                    $"[{MODULE_NAME}] ERROR processing structure event: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses system data from a Location/FSDJump event.
        ///
        /// Teaching: JSON parsing pattern with safe property access
        /// - Use TryGetProperty for safe access (no exceptions on missing fields)
        /// - Extract nullable fields (could be missing in some events)
        /// - Build structured object from parsed data
        /// - Return even if some fields missing (graceful degradation)
        ///
        /// Fields extracted:
        /// - System name and address
        /// - Controlling faction
        /// - PowerPlay power and state
        /// - Faction list with influence and state
        /// </summary>
        private SystemData ParseSystemData(JsonDocument eventJson)
        {
            var root = eventJson.RootElement;

            // Extract system basic info
            var systemName = root.TryGetProperty("StarSystem", out var nameProp)
                ? nameProp.GetString() ?? "Unknown"
                : "Unknown";

            var systemAddress = root.TryGetProperty("SystemAddress", out var addressProp)
                ? addressProp.GetInt64()
                : 0;

            // Extract controlling faction
            var controllingFaction = root.TryGetProperty("SystemFaction", out var factionProp)
                && factionProp.TryGetProperty("name", out var factionNameProp)
                ? factionNameProp.GetString() ?? "Unknown"
                : "Unknown";

            // Extract PowerPlay power (e.g., "Li Yong-Rui", "Aisling Duval")
            var power = "None";
            if (root.TryGetProperty("Powers", out var powersProp) && powersProp.ValueKind == JsonValueKind.Array)
            {
                var firstPower = powersProp.EnumerateArray().FirstOrDefault();
                if (firstPower.ValueKind == JsonValueKind.String)
                {
                    power = firstPower.GetString() ?? "None";
                }
            }

            // Extract PowerPlay state (e.g., "Contested", "Headquarters", "Control")
            var powerState = root.TryGetProperty("PowerplayState", out var stateProp)
                ? stateProp.GetString() ?? "None"
                : "None";

            // Parse faction list
            var factions = ParseFactions(eventJson);

            // Build and return SystemData object
            return new SystemData
            {
                Id = systemAddress,
                SystemName = systemName,
                ControllingFaction = controllingFaction,
                Power = power,
                PowerState = powerState,
                Factions = factions,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Parses faction list from a Location/FSDJump event.
        ///
        /// Teaching: Parsing JSON arrays
        /// - Use EnumerateArray() to iterate JSON array elements
        /// - Extract properties from each faction object
        /// - Handle missing or malformed data gracefully
        /// - Return list of parsed objects
        ///
        /// Faction data includes:
        /// - Name (faction name)
        /// - Influence (0.0 to 1.0, their market share)
        /// - State (e.g., "Boom", "None", "War", "Election")
        /// - Allegiance (e.g., "Federation", "Empire", "Independent")
        /// </summary>
        private List<FactionInfluence> ParseFactions(JsonDocument eventJson)
        {
            var factions = new List<FactionInfluence>();

            try
            {
                var root = eventJson.RootElement;

                if (!root.TryGetProperty("Factions", out var factionsArray))
                    return factions;

                if (factionsArray.ValueKind != JsonValueKind.Array)
                    return factions;

                foreach (var factionElement in factionsArray.EnumerateArray())
                {
                    try
                    {
                        var name = factionElement.TryGetProperty("name", out var nameProp)
                            ? nameProp.GetString() ?? "Unknown"
                            : "Unknown";

                        var influence = factionElement.TryGetProperty("Influence", out var influenceProp)
                            ? influenceProp.GetDouble()
                            : 0.0;

                        var state = factionElement.TryGetProperty("FactionState", out var stateProp)
                            ? stateProp.GetString() ?? "None"
                            : "None";

                        var allegiance = factionElement.TryGetProperty("Allegiance", out var allegianceProp)
                            ? allegianceProp.GetString() ?? "Unknown"
                            : "Unknown";

                        factions.Add(new FactionInfluence
                        {
                            Name = name,
                            Influence = (float)influence,
                            State = state,
                            Allegiance = allegiance
                        });
                    }
                    catch
                    {
                        // Skip malformed faction, continue with next
                        continue;
                    }
                }
            }
            catch
            {
                // If parsing fails, return whatever we got so far
            }

            return factions;
        }
    }
}
