using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EliteDataCollector.Core.Models;
using EliteDataCollector.Core.Services;

namespace ExplorationModule
{
    /// <summary>
    /// Exploration Module - Identifies valuable planets for exobiological sample collection.
    ///
    /// Purpose: Monitor Scan and ScanOrganic journal events to identify high-value exobiology
    /// targets. Playersgains profit by collecting biological samples from planets with:
    /// - Rich atmospheres (Ammonia, Methane, Nitrogen, Water)
    /// - Moderate temperatures (suited for diverse organism life)
    /// - Low gravity (enables exotic specialization)
    /// - Landable status (can actually collect samples)
    ///
    /// Data Flow:
    /// 1. Player scans a planet → Scan event → Extract characteristics
    /// 2. Score planet using ExobiologyScoringEngine (0-100)
    /// 3. If score > 60 (high value), alert player to console
    /// 4. If player scans organisms → ScanOrganic event → Track if bacterium-only
    /// 5. Persist all scans to local JSON file (%APPDATA%\...\scans.json)
    /// 6. Next session: load previous scans for reference
    ///
    /// Teaching: This is the second game loop module, demonstrating:
    /// - GameLoopModule interface implementation
    /// - Dependency injection of services
    /// - JSON event parsing with safe property access
    /// - Multi-factor scoring algorithm
    /// - Local file persistence (no database needed)
    /// - Organism filtering (ignore bacterium-only planets)
    /// - Graceful error handling (event handlers never throw)
    /// </summary>
    public class ExplorationModule : GameLoopModule
    {
        // ========== CONSTANTS ==========

        private const string MODULE_NAME = "Exploration";
        private const string MODULE_DESC = "Identifies valuable planets for exobiology sampling";

        /// <summary>
        /// Important events to process. All others are silently skipped.
        /// - Scan: player scanned any celestial body
        /// - ScanOrganic: player discovered biological signals (organisms)
        /// </summary>
        private static readonly HashSet<string> IMPORTANT_EVENTS = new(StringComparer.Ordinal)
        {
            "Scan",
            "ScanOrganic"
        };

        // ========== INJECTED SERVICES ==========

        /// <summary>Optional output writer for logging and console alerts</summary>
        private OutputWriter? _outputWriter;

        // ========== MODULE STATE ==========

        /// <summary>Currently detected system name (updated on Scan events)</summary>
        private string? _currentSystem = null;

        /// <summary>
        /// Cache of scanned planets in current session, keyed by BodyName.
        /// Allows us to update planet data when ScanOrganic events arrive for the same planet.
        /// Represents: BodyName → PlanetScan with full characteristics
        /// Example: "Sol A" → PlanetScan { SystemName: "Sol", BodyName: "Sol A", ... }
        /// </summary>
        private Dictionary<string, PlanetScan> _planetCache = new(StringComparer.OrdinalIgnoreCase);

        // ========== MODULE PROPERTIES ==========

        public string Name => MODULE_NAME;
        public string Description => MODULE_DESC;

        // ========== LIFECYCLE METHODS ==========

        /// <summary>
        /// Initializes the exploration module.
        ///
        /// Teaching: Module initialization demonstrates:
        /// - Extracting dependencies from IServiceProvider
        /// - Setting up initial state (empty planet cache)
        ///
        /// This runs once when the app starts, before any events are processed.
        /// </summary>
        public async Task InitializeAsync(IServiceProvider services)
        {
            try
            {
                _outputWriter = (OutputWriter?)services.GetService(typeof(OutputWriter));

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Initializing...");

                // Initialize planet cache (empty at start, fills as we scan)
                _planetCache = new Dictionary<string, PlanetScan>(StringComparer.OrdinalIgnoreCase);

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Initialized successfully. Ready to scan planets.");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR during initialization: {ex.Message}");
                // Continue anyway - module will process all events
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

                // Route to appropriate handler
                if (eventType == "Scan")
                {
                    await ProcessScanEvent(parsedEvent);
                }
                else if (eventType == "ScanOrganic")
                {
                    await ProcessScanOrganicEvent(parsedEvent);
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
            // Not used in this phase - could extract profile data in future
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shuts down the exploration module.
        ///
        /// Teaching: Cleanup on app exit
        /// - Save any pending state
        /// - Close connections
        /// - Release resources
        /// - Never throw
        ///
        /// In this implementation, nothing needs cleanup, but pattern is shown.
        /// </summary>
        public Task ShutdownAsync()
        {
            try
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] Shutting down...");
                // Planet cache will be garbage collected
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
        /// Processes a Scan event (player scanned a planet/celestial body).
        ///
        /// Teaching: Event handler for Scan events
        /// - Extract planet characteristics from JSON
        /// - Score using ExobiologyScoringEngine
        /// - Cache in memory for future ScanOrganic events
        /// - Alert if high-value
        /// - Persist to JSON file
        /// </summary>
        private async Task ProcessScanEvent(JsonDocument parsedEvent)
        {
            try
            {
                var root = parsedEvent.RootElement;

                // Extract system name
                if (root.TryGetProperty("StarSystem", out var systemProp))
                {
                    _currentSystem = systemProp.GetString();
                }

                // Parse planet data
                var planet = ParseScanEvent(parsedEvent);

                // Calculate score
                var score = ExobiologyScoringEngine.ScorePlanet(planet);
                planet.ExobiologyScore = score;

                // Estimate value
                var value = ExobiologyScoringEngine.EstimateValue(score, planet);
                planet.EstimatedValue = value;

                // Cache for future ScanOrganic events
                _planetCache[planet.BodyName] = planet;

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Scanned: {planet.BodyName} - Score: {score}/100");

                // Alert if high value
                await AlertIfValueable(planet);

                // Write to file
                await WriteScanToFile(planet);
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR processing Scan event: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a ScanOrganic event (player discovered biological signals).
        ///
        /// Teaching: Organism tracking
        /// - Extract organism species from event
        /// - Check if it's bacterium (low value) or flora/fauna (high value)
        /// - Mark planet as "bacterium-only" if ONLY bacteria found
        /// </summary>
        private async Task ProcessScanOrganicEvent(JsonDocument parsedEvent)
        {
            try
            {
                var root = parsedEvent.RootElement;

                // Extract body name
                if (!root.TryGetProperty("Body", out var bodyProp))
                    return;

                var bodyName = bodyProp.GetString();
                if (bodyName == null || !_planetCache.ContainsKey(bodyName))
                    return;  // Planet not in cache, skip

                // Extract genus (organism type)
                if (root.TryGetProperty("Genus", out var genusProp))
                {
                    var genus = genusProp.GetString() ?? "";

                    _outputWriter?.WriteLine($"[{MODULE_NAME}] Organism found on {bodyName}: {genus}");

                    // Check if it's bacterium (ignore)
                    if (genus.Contains("Bacterium", StringComparison.OrdinalIgnoreCase))
                    {
                        // Mark as bacterium-only for now (could be flora/fauna later)
                        var planet = _planetCache[bodyName];
                        planet.BacteriumOnly = true;

                        _outputWriter?.WriteLine($"[{MODULE_NAME}] Bacterium detected on {bodyName} - marking as low value");
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR processing ScanOrganic event: {ex.Message}");
            }
        }

        // ========== PRIVATE PARSING METHODS ==========

        /// <summary>
        /// Parses planet data from a Scan event.
        ///
        /// Teaching: JSON parsing pattern with safe property access
        /// - Use TryGetProperty for safe access (no exceptions on missing fields)
        /// - Extract nullable fields (could be missing in some events)
        /// - Build structured object from parsed data
        /// - Return even if some fields missing (graceful degradation)
        ///
        /// Example Scan event JSON:
        /// {
        ///   "timestamp": "2026-03-21T14:15:30Z",
        ///   "event": "Scan",
        ///   "ScanType": "AutoScan",
        ///   "BodyName": "Sol 1",
        ///   "BodyID": 0,
        ///   "StarSystem": "Sol",
        ///   "SystemAddress": 10477373803,
        ///   "BodyType": "Planet",
        ///   "Subclass": "H",
        ///   "Class": "H",  // Planet type code
        ///   "PlanetType": "Water world",
        ///   "Radius": 6371100.0,
        ///   "MassEM": 0.98,
        ///   "SurfaceGravity": 9.81,
        ///   "SurfaceTemperature": 288.15,
        ///   "SurfacePressure": 101325.0,
        ///   "Landable": true,
        ///   "Atmosphere": "Water atmosphere",
        ///   "Volcanism": "No volcanism",
        ///   "Materials": [...],
        ///   "Composition": {...}
        /// }
        /// </summary>
        private PlanetScan ParseScanEvent(JsonDocument eventJson)
        {
            var root = eventJson.RootElement;
            var timestamp = DateTime.UtcNow;

            // Try to extract actual timestamp from event
            if (root.TryGetProperty("timestamp", out var timestampProp))
            {
                if (DateTime.TryParse(timestampProp.GetString(), out var parsedTime))
                    timestamp = parsedTime;
            }

            var systemName = root.TryGetProperty("StarSystem", out var systemProp)
                ? systemProp.GetString() ?? "Unknown"
                : "Unknown";

            var bodyName = root.TryGetProperty("BodyName", out var bodyProp)
                ? bodyProp.GetString() ?? "Unknown"
                : "Unknown";

            var planetType = root.TryGetProperty("PlanetType", out var typeProp)
                ? typeProp.GetString() ?? "Unknown"
                : "Unknown";

            var atmosphere = root.TryGetProperty("Atmosphere", out var atmProp)
                ? atmProp.GetString()
                : null;

            var surfaceTemp = root.TryGetProperty("SurfaceTemperature", out var tempProp)
                ? tempProp.GetDouble()
                : 0;

            // Gravity: convert from m/s² to G (Earth = 9.81 m/s²)
            double gravityG = 0;
            if (root.TryGetProperty("SurfaceGravity", out var gravProp))
            {
                var gravityMs2 = gravProp.GetDouble();
                gravityG = gravityMs2 / 9.81;
            }

            var landable = root.TryGetProperty("Landable", out var landProp)
                && landProp.GetBoolean();

            return new PlanetScan
            {
                SystemName = systemName,
                BodyName = bodyName,
                PlanetType = planetType,
                Atmosphere = atmosphere,
                SurfaceTemperature = surfaceTemp,
                Gravity = gravityG,
                Landable = landable,
                Timestamp = timestamp,
                BacteriumOnly = false  // Unknown until ScanOrganic events arrive
            };
        }

        // ========== PRIVATE ALERT METHODS ==========

        /// <summary>
        /// Alerts the player if a planet is high-value exobiology target.
        ///
        /// Teaching: Alert conditions
        /// - Only alert for high-value planets (score > 60)
        /// - Skip bacterium-only planets (low value)
        /// - Format alert with key characteristics
        /// </summary>
        private async Task AlertIfValueable(PlanetScan planet)
        {
            const int ALERT_THRESHOLD = 60;

            if (planet.ExobiologyScore <= ALERT_THRESHOLD)
                return;

            if (planet.BacteriumOnly)
                return;  // Bacterium-only: not worth alerting

            var tempC = planet.SurfaceTemperature - 273.15;
            var valueM = planet.EstimatedValue / 1_000_000m;

            _outputWriter?.WriteLine(
                $"[{MODULE_NAME}] 🎯 HIGH VALUE: {planet.SystemName} - {planet.BodyName} " +
                $"- Atmosphere: {planet.Atmosphere ?? "None"} " +
                $"- Temp: {planet.SurfaceTemperature:F1}K ({tempC:F1}°C) " +
                $"- Gravity: {planet.Gravity:F2}G " +
                $"- Landable: {(planet.Landable ? "YES" : "NO")} " +
                $"- Score: {planet.ExobiologyScore}/100 " +
                $"- Est. Value: ~{valueM:F1}M credits");

            await Task.CompletedTask;
        }

        // ========== PRIVATE FILE STORAGE ==========

        /// <summary>
        /// Appends a scanned planet to the local scans.json file.
        ///
        /// Teaching: Local file persistence
        /// - Store in %APPDATA%\EliteDangerousDataCollector\scans.json
        /// - JSON array format (easy to parse later)
        /// - Append new scans (don't overwrite previous)
        /// - Create file if doesn't exist
        /// </summary>
        private async Task WriteScanToFile(PlanetScan planet)
        {
            try
            {
                // Build file path
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var scanDir = Path.Combine(appDataPath, "EliteDangerousDataCollector");
                var scanFile = Path.Combine(scanDir, "scans.json");

                // Create directory if doesn't exist
                Directory.CreateDirectory(scanDir);

                // Read existing scans or initialize empty array
                List<PlanetScan> scans = new();
                if (File.Exists(scanFile))
                {
                    var json = File.ReadAllText(scanFile);
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var parsed = JsonSerializer.Deserialize<List<PlanetScan>>(json, options);
                        if (parsed != null)
                            scans = parsed;
                    }
                    catch
                    {
                        // If file is corrupted, start fresh
                        _outputWriter?.WriteLine($"[{MODULE_NAME}] WARNING: Could not parse existing scans.json, starting fresh");
                        scans = new();
                    }
                }

                // Append new scan
                scans.Add(planet);

                // Write back to file
                var options2 = new JsonSerializerOptions { WriteIndented = true };
                var json2 = JsonSerializer.Serialize(scans, options2);
                File.WriteAllText(scanFile, json2);

                _outputWriter?.WriteLine($"[{MODULE_NAME}] Scan recorded: {scanFile}");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[{MODULE_NAME}] ERROR writing scan to file: {ex.Message}");
            }
        }
    }
}
