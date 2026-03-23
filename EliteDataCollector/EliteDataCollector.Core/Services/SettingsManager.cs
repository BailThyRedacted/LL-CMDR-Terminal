using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Manages user settings persistence (encrypted credentials + module toggles).
    ///
    /// Teaching: Settings management pattern
    /// - Loads settings from %APPDATA% on startup
    /// - Encrypts sensitive fields (INARA API key) before storing
    /// - Decrypts on load
    /// - Creates file on first run
    /// - Thread-safe for concurrent access
    ///
    /// File location: %APPDATA%\EliteDangerousDataCollector\settings.json
    /// </summary>
    public interface SettingsManager
    {
        /// <summary>Load settings from disk. Creates default settings if file doesn't exist.</summary>
        Task<AppSettings> LoadAsync();

        /// <summary>Save settings to disk (encrypts sensitive fields)</summary>
        Task SaveAsync(AppSettings settings);

        /// <summary>Get path to settings file (for debugging)</summary>
        string GetSettingsPath();
    }

    /// <summary>
    /// Default settings manager implementation using JSON files.
    /// </summary>
    public class SettingsManagerImpl : SettingsManager
    {
        private readonly OutputWriter? _outputWriter;
        private readonly string _settingsDir;
        private readonly string _settingsFile;
        private readonly JsonSerializerOptions _jsonOptions;

        private const string APP_FOLDER = "EliteDangerousDataCollector";
        private const string SETTINGS_FILENAME = "settings.json";

        public SettingsManagerImpl(OutputWriter? outputWriter = null)
        {
            _outputWriter = outputWriter;

            // Settings stored in %APPDATA%\EliteDangerousDataCollector\
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsDir = Path.Combine(appDataPath, APP_FOLDER);
            _settingsFile = Path.Combine(_settingsDir, SETTINGS_FILENAME);

            // JSON options for pretty formatting
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Load settings from disk.
        /// Creates default settings if file doesn't exist (first run).
        /// </summary>
        public async Task<AppSettings> LoadAsync()
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(_settingsDir);

                // If no settings file yet, return defaults
                if (!File.Exists(_settingsFile))
                {
                    _outputWriter?.WriteLine($"[SettingsManager] No settings found. First-time setup required.");
                    return new AppSettings { SetupComplete = false };
                }

                // Read and parse settings file
                var json = await File.ReadAllTextAsync(_settingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                if (settings == null)
                {
                    _outputWriter?.WriteLine($"[SettingsManager] Settings file corrupted, using defaults.");
                    return new AppSettings { SetupComplete = false };
                }

                // Decrypt INARA API key
                try
                {
                    if (!string.IsNullOrEmpty(settings.InaraApiKeyEncrypted))
                    {
                        settings.InaraApiKeyEncrypted = CredentialEncryption.Decrypt(settings.InaraApiKeyEncrypted);
                    }
                }
                catch (Exception ex)
                {
                    _outputWriter?.WriteLine($"[SettingsManager] WARNING: Could not decrypt INARA key: {ex.Message}");
                    _outputWriter?.WriteLine($"[SettingsManager] You may need to re-enter credentials.");
                    settings.SetupComplete = false;
                }

                _outputWriter?.WriteLine($"[SettingsManager] Settings loaded for: {settings.CommanderName}");

                return settings;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[SettingsManager] Error loading settings: {ex.Message}");
                return new AppSettings { SetupComplete = false };
            }
        }

        /// <summary>
        /// Save settings to disk (encrypts INARA API key before writing).
        /// </summary>
        public async Task SaveAsync(AppSettings settings)
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(_settingsDir);

                // Encrypt INARA API key before saving
                var settingsToSave = new AppSettings
                {
                    CommanderId = settings.CommanderId,
                    CommanderName = settings.CommanderName,
                    LastVerified = settings.LastVerified,
                    Modules = settings.Modules,
                    SetupComplete = settings.SetupComplete,
                    InaraApiKeyEncrypted = string.IsNullOrEmpty(settings.InaraApiKeyEncrypted)
                        ? string.Empty
                        : CredentialEncryption.Encrypt(settings.InaraApiKeyEncrypted)
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(settingsToSave, _jsonOptions);

                // Write to disk
                await File.WriteAllTextAsync(_settingsFile, json);

                _outputWriter?.WriteLine($"[SettingsManager] Settings saved for: {settings.CommanderName}");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[SettingsManager] Error saving settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>Get path to settings file (useful for debugging)</summary>
        public string GetSettingsPath() => _settingsFile;
    }
}
