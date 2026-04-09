using System.Text;
using System.Net.Http;

namespace EliteDataCollector.UI.Services
{
    /// <summary>
    /// Manages fetching, caching, and serving Contubernium newsletter content.
    /// Fetches from public GitHub repo on 14th and 28th of each month at hourly intervals.
    /// Caches content locally to minimize API calls.
    /// </summary>
    public class ContuberniumService
    {
        private readonly HttpClient _httpClient;
        private string _contuberniumRepoUrl = "https://github.com/placeholder/contubernium"; // Placeholder URL
        private string _cachedContent = string.Empty;
        private DateTime _lastFetchTime = DateTime.MinValue;
        private Timer? _scheduleTimer;

        private const int CacheExpiryHours = 24;
        private readonly string _cacheFilePath;

        public event EventHandler<ContuberniumUpdatedEventArgs>? ContentUpdated;

        public ContuberniumService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EliteDangerousDataCollector");
            Directory.CreateDirectory(appDataPath);
            _cacheFilePath = Path.Combine(appDataPath, "contubernium-cache.md");
        }

        public void SetRepositoryUrl(string repoUrl)
        {
            _contuberniumRepoUrl = repoUrl;
        }

        public string GetCachedContent() => _cachedContent;

        public async Task InitializeAsync()
        {
            // Load cached content
            if (File.Exists(_cacheFilePath))
            {
                try
                {
                    _cachedContent = await File.ReadAllTextAsync(_cacheFilePath);
                }
                catch { }
            }

            // Start scheduler for 14th and 28th
            StartScheduler();

            // Try to fetch immediately if it's the right day
            if (ShouldFetchToday())
            {
                await FetchContentAsync();
            }
        }

        public async Task ManualRefreshAsync()
        {
            await FetchContentAsync();
        }

        private bool ShouldFetchToday()
        {
            var today = DateTime.UtcNow.Day;
            return today == 14 || today == 28;
        }

        private void StartScheduler()
        {
            // Check hourly on 14th and 28th
            _scheduleTimer = new Timer(async _ =>
            {
                if (ShouldFetchToday())
                {
                    // Check if we haven't fetched in the last hour
                    if (DateTime.UtcNow - _lastFetchTime > TimeSpan.FromHours(1))
                    {
                        await FetchContentAsync();
                    }
                }
            }, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        private async Task FetchContentAsync()
        {
            try
            {
                // Construct raw GitHub URL from repo URL
                // If URL is https://github.com/username/repo, convert to raw content URL
                var rawUrl = ConvertToRawGithubUrl(_contuberniumRepoUrl);

                if (string.IsNullOrEmpty(rawUrl))
                {
                    return;
                }

                var response = await _httpClient.GetAsync(rawUrl);
                if (response.IsSuccessStatusCode)
                {
                    _cachedContent = await response.Content.ReadAsStringAsync();
                    _lastFetchTime = DateTime.UtcNow;

                    // Save to cache file
                    await File.WriteAllTextAsync(_cacheFilePath, _cachedContent);

                    ContentUpdated?.Invoke(this, new ContuberniumUpdatedEventArgs 
                    { 
                        Success = true,
                        FetchTime = _lastFetchTime
                    });
                }
            }
            catch (Exception ex)
            {
                ContentUpdated?.Invoke(this, new ContuberniumUpdatedEventArgs 
                { 
                    Success = false,
                    ErrorMessage = ex.Message,
                    FetchTime = DateTime.UtcNow
                });
            }
        }

        private string ConvertToRawGithubUrl(string repoUrl)
        {
            // Convert https://github.com/user/repo to https://raw.githubusercontent.com/user/repo/main/README.md
            if (string.IsNullOrEmpty(repoUrl) || repoUrl == "https://github.com/placeholder/contubernium")
            {
                return string.Empty; // Placeholder URL not yet configured
            }

            try
            {
                var uri = new Uri(repoUrl);
                var parts = uri.AbsolutePath.Trim('/').Split('/');

                if (parts.Length >= 2)
                {
                    var user = parts[0];
                    var repo = parts[1];
                    return $"https://raw.githubusercontent.com/{user}/{repo}/main/README.md";
                }
            }
            catch { }

            return string.Empty;
        }

        public void Dispose()
        {
            _scheduleTimer?.Dispose();
        }
    }

    public class ContuberniumUpdatedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime FetchTime { get; set; }
    }
}

