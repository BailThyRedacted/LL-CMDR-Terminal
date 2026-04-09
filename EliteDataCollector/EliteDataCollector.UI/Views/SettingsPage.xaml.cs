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

                // Bind controls
                CreditsCheck.IsChecked = ViewModel.ShowCredits;
                LocationCheck.IsChecked = ViewModel.ShowCurrentLocation;
                InfluenceCheck.IsChecked = ViewModel.ShowFactionInfluence;
                MeritsCheck.IsChecked = ViewModel.ShowPowerplayMerits;
                ActivityCheck.IsChecked = ViewModel.ShowRecentActivity;
                RefreshIntervalBox.Value = ViewModel.SupabaseRefreshIntervalMinutes;
                ContuberniumCheckBox.IsChecked = ViewModel.ContuberniumCheckEnabled;

                // Subscribe to changes
                ViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ViewModel.SettingsSaveMessage))
                    {
                        SaveMessage.Text = ViewModel.SettingsSaveMessage;
                    }
                };
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowCredits = CreditsCheck.IsChecked ?? false;
                ViewModel.ShowCurrentLocation = LocationCheck.IsChecked ?? false;
                ViewModel.ShowFactionInfluence = InfluenceCheck.IsChecked ?? false;
                ViewModel.ShowPowerplayMerits = MeritsCheck.IsChecked ?? false;
                ViewModel.ShowRecentActivity = ActivityCheck.IsChecked ?? false;
                ViewModel.SupabaseRefreshIntervalMinutes = (int)RefreshIntervalBox.Value;
                ViewModel.ContuberniumCheckEnabled = ContuberniumCheckBox.IsChecked ?? false;

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
            }
        }
    }
}

