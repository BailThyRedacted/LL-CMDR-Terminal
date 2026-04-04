using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EliteDataCollector.Core.Models;
using Microsoft.Extensions.Configuration;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Implementation of SupabaseClient service for Supabase PostgreSQL database access.
    ///
    /// Design Decision: Wrapper pattern abstracts away Supabase SDK internals
    /// - Configuration reading (URL, API Key) from appsettings.json
    /// - HTTP request handling and error management
    /// - Error resilience: never throws, always logs and returns gracefully
    /// - Enables testing with mock implementations
    ///
    /// Teaching: This demonstrates the adapter/wrapper pattern:
    /// - SupabaseClient is the interface (contract)
    /// - SupabaseClientImpl implements that contract
    /// - MainCore depends on interface, not concrete implementation
    /// - Allows swapping implementations without changing MainCore
    /// </summary>
    public class SupabaseClientImpl : SupabaseClient
    {
        // ========== CONSTANTS ==========

        private const string CONFIG_SECTION = "Supabase";
        private const string CONFIG_URL = "Url";
        private const string CONFIG_KEY = "PublishableKey";

        // Retry configuration
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY_MS_1 = 1000;    // 1 second
        private const int RETRY_DELAY_MS_2 = 2000;    // 2 seconds
        private const int RETRY_DELAY_MS_3 = 4000;    // 4 seconds

        // JSON serialization options
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,  // Case-insensitive for parsing responses
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // ========== CONFIGURATION FIELDS ==========

        /// <summary>Supabase project URL (e.g., https://abc123.supabase.co)</summary>
        private readonly string _supabaseUrl;

        /// <summary>Supabase publishable key for authentication</summary>
        private readonly string _supabaseKey;

        /// <summary>Optional output writer for logging operations and errors</summary>
        private readonly OutputWriter? _outputWriter;

        /// <summary>Settings manager to retrieve user context (CMDR name, Windows username)</summary>
        private readonly SettingsManager? _settingsManager;

        // ========== CONSTRUCTOR ==========

        /// <summary>
        /// Creates a new Supabase client wrapper.
        ///
        /// Teaching: Constructor demonstrates configuration pattern
        /// - Read from IConfiguration (typically from appsettings.json)
        /// - Store credentials safely as readonly fields
        /// - Log startup for debugging
        /// - Validate configuration exists
        ///
        /// Why inject IConfiguration?
        /// - Decouples config reading from implementation
        /// - Allows different config sources (json, env vars, key vault)
        /// - Testable: can inject mock configuration
        /// - Follows dependency injection best practices
        /// </summary>
        /// <param name="configuration">Configuration provider (reads appsettings.json)</param>
        /// <param name="settingsManager">Settings manager for user context (CMDR name)</param>
        /// <param name="outputWriter">Optional logger for debugging</param>
        public SupabaseClientImpl(IConfiguration configuration, SettingsManager? settingsManager = null, OutputWriter? outputWriter = null)
        {
            _outputWriter = outputWriter;
            _settingsManager = settingsManager;

            // Read Supabase configuration from appsettings.json
            var supabaseConfig = configuration.GetSection(CONFIG_SECTION);

            _supabaseUrl = supabaseConfig[CONFIG_URL] ?? string.Empty;
            _supabaseKey = supabaseConfig[CONFIG_KEY] ?? string.Empty;

            // Validate configuration is present
            if (string.IsNullOrWhiteSpace(_supabaseUrl) || string.IsNullOrWhiteSpace(_supabaseKey))
            {
                _outputWriter?.WriteLine(
                    "WARNING: Supabase configuration incomplete. Check appsettings.json:\n" +
                    $"  Expected sections: {CONFIG_SECTION}:{CONFIG_URL}, {CONFIG_SECTION}:{CONFIG_KEY}");
            }
            else
            {
                _outputWriter?.WriteLine($"SupabaseClient initialized. URL: {_supabaseUrl}");
            }
        }

        // ========== PUBLIC METHODS ==========

        /// <summary>
        /// Fetches the list of target systems from Supabase for colonization monitoring.
        ///
        /// Teaching: REST API pattern with error resilience
        /// - Make HTTP GET request to Supabase
        /// - Parse JSON response
        /// - Return empty list on any error (graceful degradation)
        /// - Log errors for debugging but don't throw
        ///
        /// Curriculum: This demonstrates:
        /// - HttpClient for REST APIs
        /// - JSON deserialization
        /// - Try-catch error handling (never throw from service)
        /// - Logging for observability
        /// - Retry logic with exponential backoff
        /// </summary>
        public async Task<List<string>> GetTargetSystemsAsync()
        {
            if (!IsConfigured())
            {
                _outputWriter?.WriteLine("ERROR: Supabase not configured. Skipping GetTargetSystems.");
                return new List<string>();
            }

            return await RetryAsync(async () =>
            {
                _outputWriter?.WriteLine("Fetching target systems from Supabase...");

                using HttpClient client = new();
                string url = $"{_supabaseUrl}/rest/v1/target_systems?select=name";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthHeaders(request);

                var response = await client.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _outputWriter?.WriteLine($"ERROR: Auth failed fetching target systems: {response.StatusCode}");
                    return new List<string>();  // Don't retry auth errors
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                
                // Parse JSON array of objects: [{"name": "Sol"}, {"name": "Alpha Centauri"}]
                var systems = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, JsonOptions) ?? new();
                var systemNames = systems
                    .Select(s => s.ContainsKey("name") ? s["name"].GetString() ?? "" : "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                _outputWriter?.WriteLine($"Target systems fetched successfully: {systemNames.Count} systems");
                return systemNames;
            });
        }

        /// <summary>
        /// Fetches systems from the ll_presence table (Lavigny's Legion presence systems).
        /// These are the specific systems where LL is active and data should be collected.
        ///
        /// Teaching: RESTful query with filtering
        /// - Uses Supabase select parameter to fetch specific columns
        /// - Gracefully handles missing data or errors
        /// - Retry logic for transient failures
        /// </summary>
        public async Task<List<string>> GetLlPresenceSystemsAsync()
        {
            if (!IsConfigured())
            {
                _outputWriter?.WriteLine("ERROR: Supabase not configured. Skipping GetLlPresenceSystems.");
                return new List<string>();
            }

            return await RetryAsync(async () =>
            {
                _outputWriter?.WriteLine("Fetching LL presence systems from Supabase...");

                using HttpClient client = new();
                string url = $"{_supabaseUrl}/rest/v1/ll_presence?select=system_name";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthHeaders(request);

                var response = await client.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _outputWriter?.WriteLine($"ERROR: Auth failed fetching LL presence systems: {response.StatusCode}");
                    return new List<string>();  // Don't retry auth errors
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                
                // Parse JSON array of objects: [{"system_name": "Sol"}, {"system_name": "Alpha Centauri"}]
                var systems = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, JsonOptions) ?? new();
                var systemNames = systems
                    .Select(s => s.ContainsKey("system_name") ? s["system_name"].GetString() ?? "" : "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                _outputWriter?.WriteLine($"LL presence systems fetched successfully: {systemNames.Count} systems");
                return systemNames;
            });
        }

        /// <summary>
        /// Uploads or updates system data in Supabase (upsert operation).
        ///
        /// Teaching: Upsert pattern for database operations
        /// - If system exists (matched by Id/SystemAddress), update it
        /// - If system doesn't exist, insert new record
        /// - Update timestamp to current time
        /// - Handle errors gracefully (never crash background processing)
        ///
        /// Curriculum: This demonstrates:
        /// - HTTP POST for upsert operations
        /// - JSON serialization of complex objects
        /// - Try-catch with no throws (background safety)
        /// - Logging for debugging
        /// - Retry logic with exponential backoff
        /// - User context (user_id) for RLS filtering
        /// </summary>
        public async Task UpsertSystemDataAsync(SystemData systemData)
        {
            if (!IsConfigured())
            {
                _outputWriter?.WriteLine("ERROR: Supabase not configured. Skipping UpsertSystemData.");
                return;
            }

            if (systemData == null)
            {
                _outputWriter?.WriteLine("ERROR: SystemData is null. Skipping UpsertSystemData.");
                return;
            }

            await RetryAsync(async () =>
            {
                _outputWriter?.WriteLine(
                    $"Upserting system data: {systemData.SystemName} " +
                    $"(Factions: {systemData.Factions?.Count ?? 0}, Structures: {systemData.Structures?.Count ?? 0})");

                using HttpClient client = new();
                
                // Set timestamp to now
                systemData.Timestamp = DateTime.UtcNow;
                
                // Add user_id for RLS filtering
                string userId = await GetUserIdAsync();
                
                // Create request body with snake_case field names matching SQL schema
                var requestBody = new
                {
                    id = systemData.Id,
                    system_name = systemData.SystemName,
                    timestamp = systemData.Timestamp,
                    controlling_faction = systemData.ControllingFaction,
                    power = systemData.Power,
                    power_state = systemData.PowerState,
                    lavigny_influence = systemData.LavignyInfluence,
                    user_id = userId
                };

                string json = JsonSerializer.Serialize(requestBody, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string url = $"{_supabaseUrl}/rest/v1/system_data?on_conflict=id";
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                AddAuthHeaders(request);
                request.Headers.Add("Prefer", "resolution=merge-duplicates");  // Upsert mode
                request.Headers.Add("X-User-Id", userId);  // For RLS filtering

                var response = await client.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _outputWriter?.WriteLine($"ERROR: Auth failed upserting system data: {response.StatusCode}");
                    return new List<string>();  // Don't retry auth errors
                }

                response.EnsureSuccessStatusCode();
                _outputWriter?.WriteLine("System data upserted successfully.");
                return new List<string>();
            });
        }

        /// <summary>
        /// Uploads or updates structure/colonization project data for a system.
        ///
        /// Teaching: Batch upsert pattern
        /// - Upload multiple structures in one operation
        /// - Each structure identified by system_id
        /// - Include user_id for RLS filtering
        /// - Handle errors gracefully
        ///
        /// Curriculum: This demonstrates:
        /// - Handling collections of objects
        /// - Batch operations vs. individual calls
        /// - Error handling in collection processing
        /// - Retry logic with exponential backoff
        /// </summary>
        public async Task UpsertStructuresAsync(long systemAddress, List<Structure> structures)
        {
            if (!IsConfigured())
            {
                _outputWriter?.WriteLine("ERROR: Supabase not configured. Skipping UpsertStructures.");
                return;
            }

            if (structures == null || structures.Count == 0)
            {
                _outputWriter?.WriteLine($"No structures to upsert for system {systemAddress}.");
                return;
            }

            await RetryAsync(async () =>
            {
                _outputWriter?.WriteLine(
                    $"Upserting structures for system {systemAddress}: {structures.Count} structures");

                using HttpClient client = new();
                
                // Add user_id for RLS filtering
                string userId = await GetUserIdAsync();
                
                // Create request body with user_id for each structure
                // Note: SQL schema has structure_type and status (not structure_name or progress_percent)
                var requestBody = structures.Select(s => new
                {
                    id = Guid.NewGuid(),
                    system_id = systemAddress,
                    structure_type = s.Type ?? "",
                    status = $"{s.Name} ({s.ProgressPercent}%)",  // Store name + progress in status field
                    user_id = userId,
                    created_at = DateTime.UtcNow
                }).ToList();

                string json = JsonSerializer.Serialize(requestBody, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                string url = $"{_supabaseUrl}/rest/v1/structures";
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                AddAuthHeaders(request);
                request.Headers.Add("X-User-Id", userId);  // For RLS filtering

                var response = await client.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _outputWriter?.WriteLine($"ERROR: Auth failed upserting structures: {response.StatusCode}");
                    return new List<string>();  // Don't retry auth errors
                }

                response.EnsureSuccessStatusCode();
                _outputWriter?.WriteLine("Structures upserted successfully.");
                return new List<string>();
            });
        }

        /// <summary>
        /// Inserts a single PowerPlay activity record into the powerplay_activities table.
        /// Each call produces a new row — no deduplication.
        /// </summary>
        public async Task InsertPowerplayActivityAsync(PowerplayActivity activity)
        {
            if (!IsConfigured())
            {
                _outputWriter?.WriteLine("ERROR: Supabase not configured. Skipping InsertPowerplayActivity.");
                return;
            }

            if (activity == null)
            {
                _outputWriter?.WriteLine("ERROR: PowerplayActivity is null. Skipping insert.");
                return;
            }

            await RetryAsync(async () =>
            {
                _outputWriter?.WriteLine($"Inserting PowerPlay activity: {activity.EventType} in {activity.SystemName ?? "unknown system"}");

                using HttpClient client = new();

                string userId = await GetUserIdAsync();
                activity.UserId = userId;

                var requestBody = new
                {
                    id = activity.Id,
                    event_type = activity.EventType,
                    power = activity.Power,
                    system_name = activity.SystemName,
                    item_type = activity.ItemType,
                    count = activity.Count,
                    merits = activity.Merits,
                    amount = activity.Amount,
                    votes = activity.Votes,
                    timestamp = activity.Timestamp,
                    user_id = userId
                };

                string json = JsonSerializer.Serialize(requestBody, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{_supabaseUrl}/rest/v1/powerplay_activities";
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                AddAuthHeaders(request);
                request.Headers.Add("X-User-Id", userId);

                var response = await client.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _outputWriter?.WriteLine($"ERROR: Auth failed inserting PowerPlay activity: {response.StatusCode}");
                    return new List<string>();  // Don't retry auth errors
                }

                response.EnsureSuccessStatusCode();
                _outputWriter?.WriteLine($"PowerPlay activity inserted: {activity.EventType}");
                return new List<string>();
            });
        }

        /// <summary>
        /// Upserts an ALD star system record into the powerplay_systems table.
        /// Uses SystemAddress (Id) as the unique key; revisiting a system updates the row.
        /// </summary>
        public async Task UpsertPowerplaySystemAsync(PowerplaySystem system)
        {
            if (!IsConfigured())
            {
                _outputWriter?.WriteLine("ERROR: Supabase not configured. Skipping UpsertPowerplaySystem.");
                return;
            }

            if (system == null)
            {
                _outputWriter?.WriteLine("ERROR: PowerplaySystem is null. Skipping upsert.");
                return;
            }

            await RetryAsync(async () =>
            {
                _outputWriter?.WriteLine($"Upserting PowerPlay system: {system.SystemName} ({system.PowerState})");

                using HttpClient client = new();

                string userId = await GetUserIdAsync();
                system.UserId = userId;
                system.Timestamp = DateTime.UtcNow;

                var requestBody = new
                {
                    id = system.Id,
                    system_name = system.SystemName,
                    power = system.Power,
                    power_state = system.PowerState,
                    timestamp = system.Timestamp,
                    user_id = userId
                };

                string json = JsonSerializer.Serialize(requestBody, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{_supabaseUrl}/rest/v1/powerplay_systems?on_conflict=id";
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                AddAuthHeaders(request);
                request.Headers.Add("Prefer", "resolution=merge-duplicates");
                request.Headers.Add("X-User-Id", userId);

                var response = await client.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _outputWriter?.WriteLine($"ERROR: Auth failed upserting PowerPlay system: {response.StatusCode}");
                    return new List<string>();  // Don't retry auth errors
                }

                response.EnsureSuccessStatusCode();
                _outputWriter?.WriteLine($"PowerPlay system upserted: {system.SystemName}");
                return new List<string>();
            });
        }

        // ========== PRIVATE HELPER METHODS ==========

        /// <summary>
        /// Helper to validate Supabase is properly configured.
        /// Returns true only if both URL and publishable key are present and non-empty.
        /// </summary>
        private bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_supabaseUrl) && !string.IsNullOrWhiteSpace(_supabaseKey);
        }

        /// <summary>
        /// Adds authentication headers to HTTP request.
        /// Supabase uses Bearer token authentication with the publishable key.
        /// </summary>
        private void AddAuthHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _supabaseKey);
            request.Headers.Add("apikey", _supabaseKey);  // Supabase also accepts apikey header
        }

        /// <summary>
        /// Extracts user ID from Windows username and optional CMDR name.
        /// Priority: CMDR Name (if available) → Windows Username (fallback)
        /// Both are joined for uniqueness across systems.
        /// </summary>
        private async Task<string> GetUserIdAsync()
        {
            try
            {
                string windowsUser = Environment.UserName;
                string cmdrName = "";

                // Try to get CMDR name from SettingsManager
                if (_settingsManager != null)
                {
                    try
                    {
                        var settings = await _settingsManager.LoadAsync();
                        cmdrName = settings?.CommanderName ?? "";
                    }
                    catch (Exception ex)
                    {
                        _outputWriter?.WriteLine($"WARNING: Could not load CMDR name from settings: {ex.Message}");
                    }
                }

                // Use CMDR name if available, otherwise Windows username
                string userId = !string.IsNullOrWhiteSpace(cmdrName) ? cmdrName : windowsUser;
                
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _outputWriter?.WriteLine("WARNING: Could not determine user ID. Using 'unknown'.");
                    userId = "unknown";
                }

                _outputWriter?.WriteLine($"User ID resolved to: {userId}");
                return userId;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"ERROR extracting user ID: {ex.Message}. Using 'unknown'.");
                return "unknown";
            }
        }

        /// <summary>
        /// Generic retry helper with exponential backoff.
        /// Attempts operation up to MAX_RETRIES (3) times with increasing delays.
        /// Does NOT retry on auth errors (401/403).
        /// Returns empty list on all errors (graceful degradation).
        /// </summary>
        private async Task<List<string>> RetryAsync(Func<Task<List<string>>> operation)
        {
            int attempt = 0;
            int[] delays = { RETRY_DELAY_MS_1, RETRY_DELAY_MS_2, RETRY_DELAY_MS_3 };

            while (attempt < MAX_RETRIES)
            {
                try
                {
                    return await operation();
                }
                catch (HttpRequestException ex) when (ex.InnerException is System.Net.Http.HttpRequestException)
                {
                    // Network error - check if it's auth-related
                    if (ex.Message.Contains("401") || ex.Message.Contains("403"))
                    {
                        _outputWriter?.WriteLine($"ERROR: Auth failure (attempt {attempt + 1}): {ex.Message}");
                        return new List<string>();  // Don't retry auth errors
                    }

                    attempt++;
                    if (attempt < MAX_RETRIES)
                    {
                        int delayMs = delays[attempt - 1];
                        _outputWriter?.WriteLine($"Transient error (attempt {attempt}). Retrying in {delayMs}ms: {ex.Message}");
                        await Task.Delay(delayMs);
                    }
                    else
                    {
                        _outputWriter?.WriteLine($"ERROR: Max retries ({MAX_RETRIES}) exceeded: {ex.Message}");
                        return new List<string>();
                    }
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt < MAX_RETRIES)
                    {
                        int delayMs = delays[attempt - 1];
                        _outputWriter?.WriteLine($"Error (attempt {attempt}). Retrying in {delayMs}ms: {ex.Message}");
                        await Task.Delay(delayMs);
                    }
                    else
                    {
                        _outputWriter?.WriteLine($"ERROR: Max retries ({MAX_RETRIES}) exceeded: {ex.Message}");
                        return new List<string>();
                    }
                }
            }

            return new List<string>();
        }
    }
}
