using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EliteDataCollector.UI.ViewModels;

namespace EliteDataCollector.UI.Views
{
    public sealed partial class PvPTrackerPage : Page
    {
        public PvPTrackerViewModel? ViewModel { get; private set; }

        public PvPTrackerPage()
        {
            this.InitializeComponent();
            this.Loaded += PvPTrackerPage_Loaded;
        }

        private void PvPTrackerPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel = App.ServiceProvider?.GetService(typeof(PvPTrackerViewModel)) as PvPTrackerViewModel;
            if (ViewModel != null)
            {
                this.DataContext = ViewModel;
                EncounterList.ItemsSource = ViewModel.Encounters;

                // Bind summary fields
                UpdateSummary();

                ViewModel.PropertyChanged += (s, args) =>
                {
                    UpdateSummary();
                };
            }
        }

        private void UpdateSummary()
        {
            if (ViewModel == null) return;
            SummaryText.Text = ViewModel.SummaryText;
            TotalCount.Text = ViewModel.TotalEncounters.ToString();
            HostileCount.Text = ViewModel.HostileEncounters.ToString();
            LlAlertCount.Text = ViewModel.LlSystemAlerts.ToString();
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.LoadEncounters();
            UpdateSummary();
        }
    }
}

