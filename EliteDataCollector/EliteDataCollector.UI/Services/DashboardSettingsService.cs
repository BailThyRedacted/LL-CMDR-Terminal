using System.Text.Json;
using System.Text.Json.Serialization;

namespace EliteDataCollector.UI.Services
{
    /// <summary>
    /// Manages dashboard settings persistence and retrieval from local JSON file.
    /// Located in %APPDATA%\EliteDangerousDataCollector\dashboard-settings.json
    /// </summary>
    public class DashboardSettingsService
    {
        private readonly string _settingsPath;
        private DashboardSettings _settings;
        private const int CurrentVersion = 1;

        public DashboardSettingsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EliteDangerousDataCollector");

            Directory.CreateDirectory(appDataPath);
            _settingsPath = Path.Combine(appDataPath, "dashboard-settings.json");

            _settings = LoadSettings();
        }

        public DashboardSettings GetSettings() => _settings;

        public void SaveSettings(DashboardSettings settings)
        {
            _settings = settings;
            _settings.Version = CurrentVersion;
            _settings.LastModified = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            File.WriteAllText(_settingsPath, json);
        }

        private DashboardSettings LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                return GetDefaultSettings();
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<DashboardSettings>(json);

                if (settings == null)
                {
                    return GetDefaultSettings();
                }

                // Handle version migration if needed
                if (settings.Version < CurrentVersion)
                {
                    MigrateSettings(settings);
                }

                return settings;
            }
            catch
            {
                // If corrupt, return defaults
                return GetDefaultSettings();
            }
        }

        private void MigrateSettings(DashboardSettings settings)
        {
            // Add migration logic here as schema evolves
            settings.Version = CurrentVersion;
        }

        private static DashboardSettings GetDefaultSettings()
        {
            return new DashboardSettings
            {
                Version = CurrentVersion,
                LastModified = DateTime.UtcNow,
                DisplayMetrics = new List<string>
                {
                    "Credits",
                    "CurrentLocation",
                    "FactionInfluence",
                    "PowerplayMerits",
                    "RecentActivity"
                },
                SupabaseRefreshIntervalMinutes = 5,
                ContuberniumCheckEnabled = true
            };
        }
    }

    /// <summary>
    /// Represents dashboard configuration.
    /// </summary>
    public class DashboardSettings
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }

        [JsonPropertyName("displayMetrics")]
        public List<string> DisplayMetrics { get; set; } = new();

        [JsonPropertyName("supabaseRefreshIntervalMinutes")]
        public int SupabaseRefreshIntervalMinutes { get; set; } = 5;

        [JsonPropertyName("contuberniumCheckEnabled")]
        public bool ContuberniumCheckEnabled { get; set; } = true;
    }
}

