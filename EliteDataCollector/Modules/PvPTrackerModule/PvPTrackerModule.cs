using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EliteDataCollector.Core.Services;

namespace PvPTrackerModule
{
    /// <summary>
    /// PvP Tracker Module - Monitors player encounters in Elite Dangerous.
    ///
    /// Purpose:
    /// - Track player encounters (interdictions, kills, deaths, ship scans)
    /// - Log all encounters to a local JSON file for history
    /// - Alert the user when a hostile encounter occurs in a Lavigny's Legion system
    /// - Play Console.Beep() for audio attention on LL-system hostile alerts
    ///
    /// Tracked Journal Events:
    /// - Interdiction       (player interdicted another CMDR)
    /// - Interdicted        (player was interdicted by another CMDR)
    /// - EscapeInterdiction (player escaped an interdiction)
    /// - PVPKill            (player killed another CMDR)
    /// - Died               (player died - check if killer is a CMDR)
    /// - ShipTargeted       (player scanned another ship - check if pilot is a CMDR)
    /// - Location / FSDJump (track current system for context)
    ///
    /// Data Storage:
    /// - Pure JSON file at %APPDATA%\EliteDangerousDataCollector\pvp_encounters.json
    /// - Each encounter is a JSON object in an array
    /// </summary>
    public class PvPTrackerModule : GameLoopModule
    {
        // ========== CONSTANTS ==========

        private const string MODULE_NAME = "PvPTracker";
        private const string MODULE_DESC = "Tracks player encounters and alerts on hostiles in LL systems";
        private const string ENCOUNTERS_FILENAME = "pvp_encounters.json";

        private static readonly HashSet<string> PVP_EVENTS = new(StringComparer.Ordinal)
        {
            "Interdiction",
            "Interdicted",
            "EscapeInterdiction",
            "PVPKill",
            "Died",
            "ShipTargeted",
        };

        private static readonly HashSet<string> LOCATION_EVENTS = new(StringComparer.Ordinal)
        {
            "Location",
            "FSDJump",
        };

        // ========== INJECTED SERVICES ==========

        private OutputWriter? _outputWriter;
        private SupabaseClient? _supabaseClient;

        // ========== MODULE STATE ==========

        private string? _currentSystem;
        private HashSet<string> _llPresenceSystems = new(StringComparer.OrdinalIgnoreCase);
        private List<PvPEncounter> _encounters = new();
        private string _encountersFilePath = string.Empty;
        private readonly object _fileLock = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // ========== MODULE PROPERTIES ==========

        public string Name => MODULE_NAME;
        public string Description => MODULE_DESC;

        // ========== LIFECYCLE METHODS ==========

        public async Task InitializeAsync(IServiceProvider services)
        {
            try
            {
                _outputWriter = (OutputWriter?)services.GetService(typeof(OutputWriter));
                _supabaseClient = (SupabaseClient?)services.GetService(typeof(SupabaseClient));

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Initializing...");

                // Set up encounters file path
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EliteDangerousDataCollector");
                Directory.CreateDirectory(appDataPath);
                _encountersFilePath = Path.Combine(appDataPath, ENCOUNTERS_FILENAME);

                // Load existing encounters from disk
                await LoadEncountersAsync();

                // Load LL presence systems for hostile alerts
                await LoadLlPresenceSystemsAsync();

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Loaded {_encounters.Count} existing encounter(s).");
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Monitoring {_llPresenceSystems.Count} LL presence system(s).");
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Ready to track PvP encounters.");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during initialization: {ex.Message}");
            }
        }

        public async Task OnJournalLineAsync(string line, JsonDocument parsedEvent)
        {
            try
            {
                if (!parsedEvent.RootElement.TryGetProperty("event", out var eventProp))
                    return;

                var eventType = eventProp.GetString();
                if (eventType == null)
                    return;

                // Track current system
                if (LOCATION_EVENTS.Contains(eventType))
                {
                    if (parsedEvent.RootElement.TryGetProperty("StarSystem", out var systemProp))
                    {
                        _currentSystem = systemProp.GetString();
                    }
                    return;
                }

                // Process PvP events
                if (!PVP_EVENTS.Contains(eventType))
                    return;

                var encounter = ParseEncounter(eventType, parsedEvent);
                if (encounter != null)
                {
                    await RecordEncounterAsync(encounter);
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
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Shutting down. {_encounters.Count} encounter(s) recorded this session.");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during shutdown: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        // ========== PRIVATE: EVENT PARSING ==========

        /// <summary>
        /// Parse a PvP-relevant journal event into a PvPEncounter record.
        /// Returns null if the event isn't actually a player encounter (e.g. NPC).
        /// </summary>
        private PvPEncounter? ParseEncounter(string eventType, JsonDocument parsedEvent)
        {
            var root = parsedEvent.RootElement;

            // Extract timestamp
            var timestamp = DateTime.UtcNow;
            if (root.TryGetProperty("timestamp", out var tsProp) &&
                DateTime.TryParse(tsProp.GetString(), out var parsed))
            {
                timestamp = parsed;
            }

            switch (eventType)
            {
                case "Interdiction":
                    return ParseInterdiction(root, timestamp);

                case "Interdicted":
                    return ParseInterdicted(root, timestamp);

                case "EscapeInterdiction":
                    return ParseEscapeInterdiction(root, timestamp);

                case "PVPKill":
                    return ParsePvPKill(root, timestamp);

                case "Died":
                    return ParseDied(root, timestamp);

                case "ShipTargeted":
                    return ParseShipTargeted(root, timestamp);

                default:
                    return null;
            }
        }

        private PvPEncounter? ParseInterdiction(JsonElement root, DateTime timestamp)
        {
            // Interdiction: player interdicted someone
            var isPlayer = root.TryGetProperty("IsPlayer", out var ipProp) && ipProp.GetBoolean();
            if (!isPlayer) return null; // Only track player interdictions

            var targetName = root.TryGetProperty("Interdicted", out var nameProp)
                ? nameProp.GetString() ?? "Unknown"
                : "Unknown";

            var success = root.TryGetProperty("Success", out var sProp) && sProp.GetBoolean();

            return new PvPEncounter
            {
                Timestamp = timestamp,
                System = _currentSystem ?? "Unknown",
                EventType = "Interdiction",
                OtherCmdr = targetName,
                Outcome = success ? "Success" : "Failed",
                IsHostile = true
            };
        }

        private PvPEncounter? ParseInterdicted(JsonElement root, DateTime timestamp)
        {
            // Interdicted: player was interdicted by someone
            var isPlayer = root.TryGetProperty("IsPlayer", out var ipProp) && ipProp.GetBoolean();
            if (!isPlayer) return null;

            var attackerName = root.TryGetProperty("Interdictor", out var nameProp)
                ? nameProp.GetString() ?? "Unknown"
                : "Unknown";

            var submitted = root.TryGetProperty("Submitted", out var sProp) && sProp.GetBoolean();

            return new PvPEncounter
            {
                Timestamp = timestamp,
                System = _currentSystem ?? "Unknown",
                EventType = "Interdicted",
                OtherCmdr = attackerName,
                Outcome = submitted ? "Submitted" : "Fought",
                IsHostile = true
            };
        }

        private PvPEncounter? ParseEscapeInterdiction(JsonElement root, DateTime timestamp)
        {
            // EscapeInterdiction: player escaped
            var isPlayer = root.TryGetProperty("IsPlayer", out var ipProp) && ipProp.GetBoolean();
            if (!isPlayer) return null;

            var attackerName = root.TryGetProperty("Interdictor", out var nameProp)
                ? nameProp.GetString() ?? "Unknown"
                : "Unknown";

            return new PvPEncounter
            {
                Timestamp = timestamp,
                System = _currentSystem ?? "Unknown",
                EventType = "EscapeInterdiction",
                OtherCmdr = attackerName,
                Outcome = "Escaped",
                IsHostile = true
            };
        }

        private PvPEncounter? ParsePvPKill(JsonElement root, DateTime timestamp)
        {
            // PVPKill: player killed another CMDR
            var victimName = root.TryGetProperty("Victim", out var nameProp)
                ? nameProp.GetString() ?? "Unknown"
                : "Unknown";

            return new PvPEncounter
            {
                Timestamp = timestamp,
                System = _currentSystem ?? "Unknown",
                EventType = "PVPKill",
                OtherCmdr = victimName,
                Outcome = "Kill",
                IsHostile = true
            };
        }

        private PvPEncounter? ParseDied(JsonElement root, DateTime timestamp)
        {
            // Died: check if killer is a CMDR (KillerName contains "Cmdr")
            if (!root.TryGetProperty("KillerName", out var killerProp))
                return null;

            var killerName = killerProp.GetString() ?? "";

            // Also check Killers array for wing kills
            if (root.TryGetProperty("Killers", out var killersArray) &&
                killersArray.ValueKind == JsonValueKind.Array)
            {
                var cmdrKillers = new List<string>();
                foreach (var killer in killersArray.EnumerateArray())
                {
                    if (killer.TryGetProperty("Name", out var kName))
                    {
                        var name = kName.GetString() ?? "";
                        if (name.StartsWith("Cmdr", StringComparison.OrdinalIgnoreCase))
                        {
                            cmdrKillers.Add(name.Replace("Cmdr ", "").Replace("CMDR ", ""));
                        }
                    }
                }
                if (cmdrKillers.Count > 0)
                {
                    return new PvPEncounter
                    {
                        Timestamp = timestamp,
                        System = _currentSystem ?? "Unknown",
                        EventType = "Died",
                        OtherCmdr = string.Join(", ", cmdrKillers),
                        Outcome = "Killed by wing",
                        IsHostile = true
                    };
                }
            }

            // Single killer
            if (killerName.StartsWith("Cmdr", StringComparison.OrdinalIgnoreCase))
            {
                return new PvPEncounter
                {
                    Timestamp = timestamp,
                    System = _currentSystem ?? "Unknown",
                    EventType = "Died",
                    OtherCmdr = killerName.Replace("Cmdr ", "").Replace("CMDR ", ""),
                    Outcome = "Killed",
                    IsHostile = true
                };
            }

            return null; // NPC death, skip
        }

        private PvPEncounter? ParseShipTargeted(JsonElement root, DateTime timestamp)
        {
            // ShipTargeted: player scanned another ship
            // Only track if ScanStage >= 1 and PilotName indicates a CMDR
            if (!root.TryGetProperty("ScanStage", out var stageProp))
                return null;

            int scanStage = 0;
            if (stageProp.ValueKind == JsonValueKind.Number)
                scanStage = stageProp.GetInt32();

            if (scanStage < 1) return null;

            if (!root.TryGetProperty("PilotName", out var pilotProp))
                return null;

            var pilotName = pilotProp.GetString() ?? "";

            // Check PilotName_Localised for CMDR prefix (journal format)
            var pilotLocalised = root.TryGetProperty("PilotName_Localised", out var plProp)
                ? plProp.GetString() ?? pilotName
                : pilotName;

            // Skip NPCs - pilot names like "$npc_name;" are NPCs
            if (pilotName.StartsWith("$") || pilotName.StartsWith("#"))
                return null;

            // If PilotName starts with "Cmdr" it's a player
            if (!pilotName.StartsWith("Cmdr", StringComparison.OrdinalIgnoreCase) &&
                !pilotLocalised.StartsWith("CMDR", StringComparison.OrdinalIgnoreCase))
                return null;

            var cmdrName = pilotLocalised
                .Replace("Cmdr ", "").Replace("CMDR ", "")
                .Replace("Cmdr", "").Replace("CMDR", "").Trim();

            if (string.IsNullOrWhiteSpace(cmdrName))
                cmdrName = "Unknown";

            return new PvPEncounter
            {
                Timestamp = timestamp,
                System = _currentSystem ?? "Unknown",
                EventType = "ShipTargeted",
                OtherCmdr = cmdrName,
                Outcome = $"Scanned (Stage {scanStage})",
                IsHostile = false // Scanning is not necessarily hostile
            };
        }

        // ========== PRIVATE: RECORDING & ALERTING ==========

        /// <summary>
        /// Record encounter to the in-memory list and persist to JSON file.
        /// If the encounter is hostile and in an LL system, trigger an alert.
        /// </summary>
        private async Task RecordEncounterAsync(PvPEncounter encounter)
        {
            _encounters.Add(encounter);

            _outputWriter?.WriteLine($"[{MODULE_NAME}] Encounter: {encounter.EventType} - CMDR {encounter.OtherCmdr} in {encounter.System} ({encounter.Outcome})");

            // Check for LL-system hostile alert
            if (encounter.IsHostile && _llPresenceSystems.Contains(encounter.System))
            {
                encounter.InLlSystem = true;

                _outputWriter?.WriteLine("");
                _outputWriter?.WriteLine("╔══════════════════════════════════════════════════════╗");
                _outputWriter?.WriteLine("║           ⚠️  [PVP ALERT] LL SYSTEM HOSTILE          ║");
                _outputWriter?.WriteLine("╠══════════════════════════════════════════════════════╣");
                _outputWriter?.WriteLine($"║  System: {encounter.System,-42} ║");
                _outputWriter?.WriteLine($"║  CMDR:   {encounter.OtherCmdr,-42} ║");
                _outputWriter?.WriteLine($"║  Event:  {encounter.EventType,-42} ║");
                _outputWriter?.WriteLine("╠══════════════════════════════════════════════════════╣");
                _outputWriter?.WriteLine("║  Please report this hostile encounter to leadership! ║");
                _outputWriter?.WriteLine("╚══════════════════════════════════════════════════════╝");
                _outputWriter?.WriteLine("");

                // Audio alert
                try { Console.Beep(800, 300); Console.Beep(1000, 300); Console.Beep(800, 300); }
                catch { /* Console.Beep may not be available in all environments */ }
            }

            // Persist to disk
            await SaveEncountersAsync();
        }

        // ========== PRIVATE: FILE I/O ==========

        private async Task LoadEncountersAsync()
        {
            try
            {
                if (!File.Exists(_encountersFilePath))
                {
                    _encounters = new List<PvPEncounter>();
                    return;
                }

                var json = await File.ReadAllTextAsync(_encountersFilePath);
                _encounters = JsonSerializer.Deserialize<List<PvPEncounter>>(json, _jsonOptions) ?? new List<PvPEncounter>();
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] WARNING: Could not load encounters: {ex.Message}");
                _encounters = new List<PvPEncounter>();
            }
        }

        private async Task SaveEncountersAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_encounters, _jsonOptions);

                // Thread-safe write
                lock (_fileLock)
                {
                    File.WriteAllText(_encountersFilePath, json);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] WARNING: Could not save encounters: {ex.Message}");
            }
        }

        private async Task LoadLlPresenceSystemsAsync()
        {
            try
            {
                if (_supabaseClient == null)
                {
                    _outputWriter?.WriteLine($"[{MODULE_NAME}] Supabase not configured, LL-system alerts disabled");
                    return;
                }

                var systems = await _supabaseClient.GetLlPresenceSystemsAsync();
                _llPresenceSystems = new HashSet<string>(systems, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] WARNING: Could not load LL presence systems: {ex.Message}");
                _llPresenceSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    // ========== DATA MODEL ==========

    /// <summary>
    /// Represents a single PvP encounter record persisted to JSON.
    /// </summary>
    public class PvPEncounter
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("system")]
        public string System { get; set; } = "Unknown";

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("otherCmdr")]
        public string OtherCmdr { get; set; } = "Unknown";

        [JsonPropertyName("outcome")]
        public string Outcome { get; set; } = string.Empty;

        [JsonPropertyName("isHostile")]
        public bool IsHostile { get; set; }

        [JsonPropertyName("inLlSystem")]
        public bool InLlSystem { get; set; }
    }
}

