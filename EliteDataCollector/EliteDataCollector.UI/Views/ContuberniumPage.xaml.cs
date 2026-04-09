using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EliteDataCollector.UI.ViewModels;

namespace EliteDataCollector.UI.Views
{
    public sealed partial class ContuberniumPage : Page
    {
        public ContuberniumViewModel? ViewModel { get; private set; }

        public ContuberniumPage()
        {
            this.InitializeComponent();
            this.Loaded += ContuberniumPage_Loaded;
        }

        private async void ContuberniumPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel = App.ServiceProvider?.GetService(typeof(ContuberniumViewModel)) as ContuberniumViewModel;
            if (ViewModel != null)
            {
                this.DataContext = ViewModel;
                await ViewModel.InitializeAsync();

                NewsletterContent.Text = ViewModel.NewsletterContent;
                LastUpdateTime.Text = $"Last updated: {ViewModel.LastUpdateTime}";

                ViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ViewModel.NewsletterContent))
                        NewsletterContent.Text = ViewModel.NewsletterContent;
                    else if (e.PropertyName == nameof(ViewModel.LastUpdateTime))
                        LastUpdateTime.Text = $"Last updated: {ViewModel.LastUpdateTime}";
                };
            }
        }

        private async void OnManualRefreshClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.ManualRefreshAsync();
            }
        }
    }
}

