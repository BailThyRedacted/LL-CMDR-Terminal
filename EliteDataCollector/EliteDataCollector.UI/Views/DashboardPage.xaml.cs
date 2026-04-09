using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EliteDataCollector.UI.ViewModels;

namespace EliteDataCollector.UI.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel? ViewModel { get; private set; }

        public DashboardPage()
        {
            this.InitializeComponent();
            this.Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel = App.ServiceProvider?.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
            if (ViewModel != null)
            {
                await ViewModel.InitializeAsync();
                this.DataContext = ViewModel;

                // Bind properties
                UpdateUI();

                // Subscribe to property changes
                ViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ViewModel.Credits))
                        CreditsValue.Text = ViewModel.Credits.ToString("N0");
                    else if (e.PropertyName == nameof(ViewModel.CurrentLocation))
                        LocationValue.Text = ViewModel.CurrentLocation;
                    else if (e.PropertyName == nameof(ViewModel.CurrentStarport))
                        StarportValue.Text = ViewModel.CurrentStarport;
                    else if (e.PropertyName == nameof(ViewModel.LastUpdateTime))
                        LastUpdateLabel.Text = $"Last updated: {ViewModel.LastUpdateTime}";
                    else if (e.PropertyName == nameof(ViewModel.RecentActivity))
                        UpdateActivityList();
                };
            }
        }

        private void UpdateUI()
        {
            if (ViewModel == null) return;
            
            CreditsValue.Text = ViewModel.Credits.ToString("N0");
            LocationValue.Text = ViewModel.CurrentLocation;
            StarportValue.Text = ViewModel.CurrentStarport;
            MeritsValue.Text = ViewModel.PowerplayMerits.ToString();
            LastUpdateLabel.Text = $"Last updated: {ViewModel.LastUpdateTime}";
            UpdateActivityList();
        }

        private void UpdateActivityList()
        {
            if (ViewModel == null) return;
            
            ActivityList.ItemsSource = ViewModel.RecentActivity;
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.RefreshNowCommand.ExecuteAsync(null);
            }
        }
    }
}

