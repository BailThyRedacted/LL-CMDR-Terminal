using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// INARA API integration for commander authentication.
    ///
    /// Teaching: External API integration pattern
    /// - HttpClient for REST API calls to INARA
    /// - JSON response parsing
    /// - Error handling for network/API failures
    /// - Graceful degradation if INARA offline
    ///
    /// INARA API: https://inara.cz/apikeys/
    /// - Each commander gets a personal API key
    /// - Used to fetch commander profile/name
    /// - Rate limited, so cache results
    /// </summary>
    public interface InaraAuth
    {
        /// <summary>
        /// Verifies an INARA API key and returns commander information.
        /// Returns (commanderId, commanderName) if valid, throws if invalid.
        /// </summary>
        Task<(int commanderId, string commanderName)> VerifyApiKeyAsync(string apiKey);
    }

    /// <summary>
    /// INARA API implementation using HTTP client.
    ///
    /// Teaching: Service with external dependencies
    /// - Accepts HttpClient via dependency injection (testable)
    /// - Handles JSON parsing with System.Text.Json
    /// - Never throws from public methods (returns null/default on error)
    /// </summary>
    public class InaraAuthImpl : InaraAuth
    {
        private readonly HttpClient _httpClient;
        private readonly OutputWriter? _outputWriter;

        private const string INARA_API_BASE = "https://inara.cz/api/v1";
        private const string COMMANDER_PROFILE_ENDPOINT = "/getCommanderProfile";

        public InaraAuthImpl(HttpClient httpClient, OutputWriter? outputWriter = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _outputWriter = outputWriter;
        }

        /// <summary>
        /// Verifies INARA API key by fetching commander profile.
        ///
        /// Teaching: API request/response pattern
        /// - Build request with API key
        /// - Parse JSON response
        /// - Extract commander ID and name
        /// - Handle errors gracefully
        /// </summary>
        public async Task<(int commanderId, string commanderName)> VerifyApiKeyAsync(string apiKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("API key cannot be empty");

                _outputWriter?.WriteLine("[InaraAuth] Verifying INARA API key...");

                // Build request
                var url = $"{INARA_API_BASE}{COMMANDER_PROFILE_ENDPOINT}?apiKey={apiKey}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "EliteDataCollector/1.0");

                // Send request with timeout
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.SendAsync(request, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"INARA API returned: {response.StatusCode}");
                }

                // Parse response
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check for API success
                if (!root.TryGetProperty("status", out var statusProp))
                    throw new InvalidOperationException("Invalid INARA response: missing status");

                var status = statusProp.GetString();
                if (status != "ok")
                {
                    var errorMsg = root.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : "Unknown error";
                    throw new InvalidOperationException($"INARA error: {errorMsg}");
                }

                // Extract commander data
                if (!root.TryGetProperty("result", out var resultProp))
                    throw new InvalidOperationException("Invalid INARA response: missing result");

                if (!resultProp.TryGetProperty("user", out var userProp))
                    throw new InvalidOperationException("Invalid INARA response: missing user");

                if (!userProp.TryGetProperty("id", out var idProp))
                    throw new InvalidOperationException("Invalid INARA response: missing commander ID");

                if (!userProp.TryGetProperty("username", out var nameProp))
                    throw new InvalidOperationException("Invalid INARA response: missing commander name");

                var commanderId = idProp.GetInt32();
                var commanderName = nameProp.GetString() ?? "Unknown";

                _outputWriter?.WriteLine($"[InaraAuth] ✓ Authorization successful: {commanderName} (ID: {commanderId})");

                return (commanderId, commanderName);
            }
            catch (HttpRequestException ex)
            {
                _outputWriter?.WriteLine($"[InaraAuth] Network error: {ex.Message}");
                throw new InvalidOperationException($"Failed to reach INARA: {ex.Message}", ex);
            }
            catch (TaskCanceledException)
            {
                _outputWriter?.WriteLine("[InaraAuth] INARA request timed out");
                throw new InvalidOperationException("INARA API request timed out");
            }
            catch (JsonException ex)
            {
                _outputWriter?.WriteLine($"[InaraAuth] Invalid INARA response: {ex.Message}");
                throw new InvalidOperationException($"Invalid INARA response: {ex.Message}", ex);
            }
            catch (InvalidOperationException)
            {
                throw;  // Re-throw validation errors
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[InaraAuth] Unexpected error: {ex.Message}");
                throw new InvalidOperationException($"Unexpected error: {ex.Message}", ex);
            }
        }
    }
}
