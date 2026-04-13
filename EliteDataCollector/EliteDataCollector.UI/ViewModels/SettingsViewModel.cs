using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EliteDataCollector.Core.Services;
using EliteDataCollector.UI.Services;

namespace EliteDataCollector.UI.ViewModels
{
    /// <summary>
    /// Settings view model - manages dashboard configuration, module toggles,
    /// and commander setup. Handles both first-run setup and ongoing config.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly DashboardSettingsService _settingsService;
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly SettingsManager _settingsManager;
        private readonly KeyValidator _keyValidator;

        // ===== Dashboard Settings =====

        [ObservableProperty]
        private int supabaseRefreshIntervalMinutes;

        [ObservableProperty]
        private bool contuberniumCheckEnabled;

        [ObservableProperty]
        private bool showCredits;

        [ObservableProperty]
        private bool showCurrentLocation;

        [ObservableProperty]
        private bool showFactionInfluence;

        [ObservableProperty]
        private bool showPowerplayMerits;

        [ObservableProperty]
        private bool showRecentActivity;

        [ObservableProperty]
        private string settingsSaveMessage = string.Empty;

        // ===== Commander Settings =====

        [ObservableProperty]
        private string authenticationKey = string.Empty;

        [ObservableProperty]
        private string commanderName = string.Empty;

        [ObservableProperty]
        private string keyValidationMessage = string.Empty;

        [ObservableProperty]
        private bool isKeyValid;

        // ===== Module Toggles =====

        [ObservableProperty]
        private bool colonizationEnabled;

        [ObservableProperty]
        private bool explorationEnabled;

        [ObservableProperty]
        private bool powerplayEnabled;

        [ObservableProperty]
        private bool pvpTrackerEnabled;

        public SettingsViewModel(
            DashboardSettingsService settingsService,
            DashboardViewModel dashboardViewModel,
            SettingsManager settingsManager,
            KeyValidator keyValidator)
        {
            _settingsService = settingsService;
            _dashboardViewModel = dashboardViewModel;
            _settingsManager = settingsManager;
            _keyValidator = keyValidator;

            LoadSettings();
            _ = LoadAppSettingsAsync();
        }

        private void LoadSettings()
        {
            var settings = _settingsService.GetSettings();

            SupabaseRefreshIntervalMinutes = settings.SupabaseRefreshIntervalMinutes;
            ContuberniumCheckEnabled = settings.ContuberniumCheckEnabled;

            ShowCredits = settings.DisplayMetrics.Contains("Credits");
            ShowCurrentLocation = settings.DisplayMetrics.Contains("CurrentLocation");
            ShowFactionInfluence = settings.DisplayMetrics.Contains("FactionInfluence");
            ShowPowerplayMerits = settings.DisplayMetrics.Contains("PowerplayMerits");
            ShowRecentActivity = settings.DisplayMetrics.Contains("RecentActivity");
        }

        private async Task LoadAppSettingsAsync()
        {
            try
            {
                var appSettings = await _settingsManager.LoadAsync();
                CommanderName = appSettings.CommanderName;
                ColonizationEnabled = appSettings.Modules.ColonizationEnabled;
                ExplorationEnabled = appSettings.Modules.ExplorationEnabled;
                PowerplayEnabled = appSettings.Modules.PowerplayEnabled;
                PvpTrackerEnabled = appSettings.Modules.PvPTrackerEnabled;
                IsKeyValid = appSettings.SetupComplete;
            }
            catch
            {
                // Settings may not exist yet on first run
            }
        }

        /// <summary>
        /// Validate the authentication key and extract commander info.
        /// </summary>
        public (bool success, string message) ValidateKey(string key)
        {
            try
            {
                var (valid, commanderId, cmdName) = _keyValidator.ValidateKey(key);
                if (valid)
                {
                    CommanderName = cmdName;
                    AuthenticationKey = key;
                    IsKeyValid = true;
                    KeyValidationMessage = $"✓ Valid: {cmdName}";
                    return (true, $"✓ Valid: {cmdName}");
                }
                else
                {
                    IsKeyValid = false;
                    KeyValidationMessage = "✗ Invalid key";
                    return (false, "✗ Invalid key");
                }
            }
            catch (Exception ex)
            {
                IsKeyValid = false;
                KeyValidationMessage = $"✗ Error: {ex.Message}";
                return (false, $"✗ Error: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SaveSettings()
        {
            // Save dashboard settings
            var dashSettings = new DashboardSettings
            {
                SupabaseRefreshIntervalMinutes = SupabaseRefreshIntervalMinutes,
                ContuberniumCheckEnabled = ContuberniumCheckEnabled,
                DisplayMetrics = new List<string>()
            };

            if (ShowCredits) dashSettings.DisplayMetrics.Add("Credits");
            if (ShowCurrentLocation) dashSettings.DisplayMetrics.Add("CurrentLocation");
            if (ShowFactionInfluence) dashSettings.DisplayMetrics.Add("FactionInfluence");
            if (ShowPowerplayMerits) dashSettings.DisplayMetrics.Add("PowerplayMerits");
            if (ShowRecentActivity) dashSettings.DisplayMetrics.Add("RecentActivity");

            _settingsService.SaveSettings(dashSettings);

            // Save app settings (commander + modules)
            try
            {
                var appSettings = await _settingsManager.LoadAsync();

                // Update commander info if key was validated
                if (IsKeyValid && !string.IsNullOrEmpty(AuthenticationKey))
                {
                    var (valid, commanderId, cmdName) = _keyValidator.ValidateKey(AuthenticationKey);
                    if (valid)
                    {
                        appSettings.InaraApiKeyEncrypted = AuthenticationKey;
                        appSettings.CommanderId = commanderId;
                        appSettings.CommanderName = cmdName;
                        appSettings.LastVerified = DateTime.UtcNow;
                    }
                }

                appSettings.Modules.ColonizationEnabled = ColonizationEnabled;
                appSettings.Modules.ExplorationEnabled = ExplorationEnabled;
                appSettings.Modules.PowerplayEnabled = PowerplayEnabled;
                appSettings.Modules.PvPTrackerEnabled = PvpTrackerEnabled;
                appSettings.SetupComplete = IsKeyValid;

                await _settingsManager.SaveAsync(appSettings);
            }
            catch (Exception ex)
            {
                SettingsSaveMessage = $"Error saving: {ex.Message}";
                return;
            }

            SettingsSaveMessage = "Settings saved successfully! Restart app for module changes to take effect.";

            // Clear message after 5 seconds
            _ = Task.Delay(5000).ContinueWith(_ =>
            {
                SettingsSaveMessage = string.Empty;
            });
        }

        [RelayCommand]
        public void ResetToDefaults()
        {
            var defaultSettings = new DashboardSettings
            {
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

            _settingsService.SaveSettings(defaultSettings);
            LoadSettings();

            // Reset module toggles to defaults
            ColonizationEnabled = true;
            ExplorationEnabled = true;
            PowerplayEnabled = true;
            PvpTrackerEnabled = true;

            SettingsSaveMessage = "Reset to defaults!";

            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                SettingsSaveMessage = string.Empty;
            });
        }
    }
}

