using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EliteDataCollector.UI.ViewModels;

namespace EliteDataCollector.UI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel? ViewModel { get; private set; }

        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel = App.ServiceProvider?.GetService(typeof(SettingsViewModel)) as SettingsViewModel;
            if (ViewModel != null)
            {
                this.DataContext = ViewModel;

                // Show first-run banner if needed
                if (AppContext.IsFirstRun)
                {
                    FirstRunBanner.Visibility = Visibility.Visible;
                }

                // Bind dashboard controls
                CreditsCheck.IsChecked = ViewModel.ShowCredits;
                LocationCheck.IsChecked = ViewModel.ShowCurrentLocation;
                InfluenceCheck.IsChecked = ViewModel.ShowFactionInfluence;
                MeritsCheck.IsChecked = ViewModel.ShowPowerplayMerits;
                ActivityCheck.IsChecked = ViewModel.ShowRecentActivity;
                RefreshIntervalBox.Value = ViewModel.SupabaseRefreshIntervalMinutes;
                ContuberniumCheckBox.IsChecked = ViewModel.ContuberniumCheckEnabled;

                // Bind commander controls
                CommanderNameBox.Text = ViewModel.CommanderName;

                // Bind module toggles
                ColonizationToggle.IsOn = ViewModel.ColonizationEnabled;
                ExplorationToggle.IsOn = ViewModel.ExplorationEnabled;
                PowerplayToggle.IsOn = ViewModel.PowerplayEnabled;
                PvPTrackerToggle.IsOn = ViewModel.PvpTrackerEnabled;

                // Subscribe to changes
                ViewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(ViewModel.SettingsSaveMessage))
                    {
                        SaveMessage.Text = ViewModel.SettingsSaveMessage;
                    }
                    if (args.PropertyName == nameof(ViewModel.CommanderName))
                    {
                        CommanderNameBox.Text = ViewModel.CommanderName;
                    }
                    if (args.PropertyName == nameof(ViewModel.KeyValidationMessage))
                    {
                        KeyValidationMessage.Text = ViewModel.KeyValidationMessage;
                        KeyValidationMessage.Foreground = ViewModel.IsKeyValid
                            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green)
                            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    }
                };
            }
        }

        private void OnValidateKeyClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var key = AuthKeyBox.Text?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                KeyValidationMessage.Text = "Please enter a key.";
                KeyValidationMessage.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                return;
            }

            var (success, message) = ViewModel.ValidateKey(key);
            KeyValidationMessage.Text = message;
            KeyValidationMessage.Foreground = success
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                // Read dashboard controls
                ViewModel.ShowCredits = CreditsCheck.IsChecked ?? false;
                ViewModel.ShowCurrentLocation = LocationCheck.IsChecked ?? false;
                ViewModel.ShowFactionInfluence = InfluenceCheck.IsChecked ?? false;
                ViewModel.ShowPowerplayMerits = MeritsCheck.IsChecked ?? false;
                ViewModel.ShowRecentActivity = ActivityCheck.IsChecked ?? false;
                ViewModel.SupabaseRefreshIntervalMinutes = (int)RefreshIntervalBox.Value;
                ViewModel.ContuberniumCheckEnabled = ContuberniumCheckBox.IsChecked ?? false;

                // Read module toggles
                ViewModel.ColonizationEnabled = ColonizationToggle.IsOn;
                ViewModel.ExplorationEnabled = ExplorationToggle.IsOn;
                ViewModel.PowerplayEnabled = PowerplayToggle.IsOn;
                ViewModel.PvpTrackerEnabled = PvPTrackerToggle.IsOn;

                ViewModel.SaveSettingsCommand.Execute(null);
            }
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ResetToDefaultsCommand.Execute(null);
                
                // Update UI
                CreditsCheck.IsChecked = ViewModel.ShowCredits;
                LocationCheck.IsChecked = ViewModel.ShowCurrentLocation;
                InfluenceCheck.IsChecked = ViewModel.ShowFactionInfluence;
                MeritsCheck.IsChecked = ViewModel.ShowPowerplayMerits;
                ActivityCheck.IsChecked = ViewModel.ShowRecentActivity;
                RefreshIntervalBox.Value = ViewModel.SupabaseRefreshIntervalMinutes;
                ContuberniumCheckBox.IsChecked = ViewModel.ContuberniumCheckEnabled;

                // Update module toggles
                ColonizationToggle.IsOn = ViewModel.ColonizationEnabled;
                ExplorationToggle.IsOn = ViewModel.ExplorationEnabled;
                PowerplayToggle.IsOn = ViewModel.PowerplayEnabled;
                PvPTrackerToggle.IsOn = ViewModel.PvpTrackerEnabled;
            }
        }
    }
}

