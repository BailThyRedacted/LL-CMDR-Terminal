using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Checks for application updates from GitHub Releases.
    /// 
    /// Design Decision: Separate service allows us to:
    /// - Test update logic independently
    /// - Mock HTTP calls in unit tests
    /// - Reuse in multiple contexts (app startup, manual checks, periodic background tasks)
    /// </summary>
    public interface UpdateService
    {
        /// <summary>
        /// Checks GitHub Releases API for a newer version.
        /// Returns null if already on latest version or if check fails gracefully.
        /// </summary>
        Task<UpdateInfo?> CheckForUpdatesAsync();

        /// <summary>
        /// Gets the current installed version string.
        /// </summary>
        string GetCurrentVersion();

        /// <summary>
        /// Gets the URL of the latest release asset (.msi file).
        /// </summary>
        Task<string?> GetLatestReleaseDownloadUrlAsync();
    }

    /// <summary>
    /// Update information from GitHub Releases.
    /// </summary>
    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public DateTime ReleaseDate { get; set; }
        public string TagName { get; set; } = "";
    }

    /// <summary>
    /// Concrete implementation of UpdateService.
    /// Queries GitHub Releases API for the latest .msi asset.
    /// </summary>
    public class UpdateServiceImpl : UpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly OutputWriter? _outputWriter;
        private readonly string _gitHubRepository;
        private readonly string _currentVersion;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private UpdateInfo? _cachedUpdateInfo = null;
        private const int CacheExpirationMinutes = 1440; // 24 hours

        /// <summary>
        /// Creates a new UpdateServiceImpl.
        ///
        /// Parameters:
        /// - httpClient: HttpClient for API calls
        /// - outputWriter: Optional logging
        /// - gitHubRepository: GitHub repo in format "owner/repo" (e.g., "your-username/EliteDataCollector")
        /// - currentVersion: Current app version string (e.g., "1.0.0")
        /// </summary>
        public UpdateServiceImpl(
            HttpClient httpClient,
            OutputWriter? outputWriter,
            string gitHubRepository,
            string currentVersion)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _outputWriter = outputWriter;
            _gitHubRepository = gitHubRepository ?? throw new ArgumentNullException(nameof(gitHubRepository));
            _currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));

            // Set a descriptive user agent to avoid GitHub API rejections
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "EliteDataCollector-AutoUpdater");
            }
        }

        public string GetCurrentVersion() => _currentVersion;

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                // Check cache first
                if (DateTime.UtcNow.Subtract(_lastCheckTime).TotalMinutes < CacheExpirationMinutes && _cachedUpdateInfo != null)
                {
                    _outputWriter?.WriteLine("[UpdateService] Using cached update info", LogLevel.Debug);
                    return _cachedUpdateInfo;
                }

                _outputWriter?.WriteLine("[UpdateService] Checking GitHub for updates...", LogLevel.Info);

                // Query GitHub API for latest release
                string apiUrl = $"https://api.github.com/repos/{_gitHubRepository}/releases/latest";
                
                var response = await _httpClient.GetAsync(apiUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _outputWriter?.WriteLine($"[UpdateService] GitHub API returned {response.StatusCode}", LogLevel.Warning);
                    return null;
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                
                // Parse JSON response
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    var root = doc.RootElement;
                    
                    // Extract tag_name (version)
                    string latestVersion = root.GetProperty("tag_name").GetString() ?? "";
                    
                    // Remove 'v' prefix if present (normalize versions)
                    latestVersion = latestVersion.TrimStart('v');
                    
                    _outputWriter?.WriteLine($"[UpdateService] Latest version on GitHub: {latestVersion}", LogLevel.Debug);
                    _outputWriter?.WriteLine($"[UpdateService] Current version: {_currentVersion}", LogLevel.Debug);

                    // Compare versions
                    if (IsNewerVersion(latestVersion, _currentVersion))
                    {
                        _outputWriter?.WriteLine($"[UpdateService] Update available: {_currentVersion} → {latestVersion}", LogLevel.Info);

                        // Extract download URL
                        string downloadUrl = await ExtractMsiDownloadUrlAsync(root);
                        
                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            _outputWriter?.WriteLine("[UpdateService] Could not find .msi asset in latest release", LogLevel.Warning);
                            return null;
                        }

                        // Extract release notes
                        string releaseNotes = root.GetProperty("body").GetString() ?? "";
                        
                        // Extract release date
                        string releaseDateStr = root.GetProperty("published_at").GetString() ?? "";
                        DateTime.TryParse(releaseDateStr, out DateTime releaseDate);

                        var updateInfo = new UpdateInfo
                        {
                            LatestVersion = latestVersion,
                            DownloadUrl = downloadUrl,
                            ReleaseNotes = releaseNotes,
                            ReleaseDate = releaseDate,
                            TagName = root.GetProperty("tag_name").GetString() ?? ""
                        };

                        // Cache the result
                        _lastCheckTime = DateTime.UtcNow;
                        _cachedUpdateInfo = updateInfo;

                        return updateInfo;
                    }
                    else
                    {
                        _outputWriter?.WriteLine("[UpdateService] Already on latest version", LogLevel.Debug);
                        _lastCheckTime = DateTime.UtcNow;
                        return null;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _outputWriter?.WriteLine($"[UpdateService] Network error checking for updates: {ex.Message}", LogLevel.Warning);
                return null;
            }
            catch (JsonException ex)
            {
                _outputWriter?.WriteLine($"[UpdateService] Error parsing GitHub API response: {ex.Message}", LogLevel.Warning);
                return null;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[UpdateService] Unexpected error checking for updates: {ex.Message}", LogLevel.Warning);
                return null;
            }
        }

        public async Task<string?> GetLatestReleaseDownloadUrlAsync()
        {
            try
            {
                string apiUrl = $"https://api.github.com/repos/{_gitHubRepository}/releases/latest";
                
                var response = await _httpClient.GetAsync(apiUrl);
                
                if (!response.IsSuccessStatusCode)
                    return null;

                string jsonContent = await response.Content.ReadAsStringAsync();
                
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    var root = doc.RootElement;
                    return await ExtractMsiDownloadUrlAsync(root);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the .msi download URL from the GitHub API response.
        /// </summary>
        private async Task<string> ExtractMsiDownloadUrlAsync(JsonElement releaseElement)
        {
            try
            {
                var assets = releaseElement.GetProperty("assets").EnumerateArray();
                
                foreach (var asset in assets)
                {
                    string assetName = asset.GetProperty("name").GetString() ?? "";
                    
                    if (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        string downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        _outputWriter?.WriteLine($"[UpdateService] Found .msi asset: {assetName}", LogLevel.Debug);
                        return downloadUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[UpdateService] Error extracting download URL: {ex.Message}", LogLevel.Debug);
            }

            return "";
        }

        /// <summary>
        /// Compares semantic versions (e.g., "1.0.0" vs "1.0.1").
        /// Returns true if versionA is newer than versionB.
        /// </summary>
        private bool IsNewerVersion(string versionA, string versionB)
        {
            // Normalize versions (remove leading 'v')
            versionA = versionA.TrimStart('v');
            versionB = versionB.TrimStart('v');

            if (!Version.TryParse(versionA, out var verA) || !Version.TryParse(versionB, out var verB))
            {
                _outputWriter?.WriteLine($"[UpdateService] Could not parse versions: {versionA} vs {versionB}", LogLevel.Debug);
                return false;
            }

            return verA > verB;
        }
    }
}
