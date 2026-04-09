using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EliteDataCollector.UI.Services;

namespace EliteDataCollector.UI.ViewModels
{
    /// <summary>
    /// Settings view model - manages dashboard configuration and persistence.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly DashboardSettingsService _settingsService;
        private readonly DashboardViewModel _dashboardViewModel;

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

        public SettingsViewModel(
            DashboardSettingsService settingsService,
            DashboardViewModel dashboardViewModel)
        {
            _settingsService = settingsService;
            _dashboardViewModel = dashboardViewModel;

            LoadSettings();
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

        [RelayCommand]
        public void SaveSettings()
        {
            var settings = new DashboardSettings
            {
                SupabaseRefreshIntervalMinutes = SupabaseRefreshIntervalMinutes,
                ContuberniumCheckEnabled = ContuberniumCheckEnabled,
                DisplayMetrics = new List<string>()
            };

            if (ShowCredits) settings.DisplayMetrics.Add("Credits");
            if (ShowCurrentLocation) settings.DisplayMetrics.Add("CurrentLocation");
            if (ShowFactionInfluence) settings.DisplayMetrics.Add("FactionInfluence");
            if (ShowPowerplayMerits) settings.DisplayMetrics.Add("PowerplayMerits");
            if (ShowRecentActivity) settings.DisplayMetrics.Add("RecentActivity");

            _settingsService.SaveSettings(settings);
            SettingsSaveMessage = "Settings saved successfully!";

            // Clear message after 3 seconds
            Task.Delay(3000).ContinueWith(_ => 
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
            SettingsSaveMessage = "Reset to defaults!";

            Task.Delay(3000).ContinueWith(_ => 
            {
                SettingsSaveMessage = string.Empty;
            });
        }
    }
}

