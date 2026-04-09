using CommunityToolkit.Mvvm.ComponentModel;
using EliteDataCollector.UI.Services;
using Microsoft.UI.Dispatching;

namespace EliteDataCollector.UI.ViewModels
{
    public partial class ColonizationViewModel : ObservableObject
    {
        [ObservableProperty]
        private string pageTitle = "Colonization Module";

        [ObservableProperty]
        private string content = "Colonization data will appear here as it becomes available.";
    }

    public partial class BgsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string pageTitle = "BGS (Background Simulation)";

        [ObservableProperty]
        private string content = "BGS faction data will appear here as it becomes available.";
    }

    public partial class PowerplayViewModel : ObservableObject
    {
        [ObservableProperty]
        private string pageTitle = "PowerPlay";

        [ObservableProperty]
        private string content = "PowerPlay activity will appear here as it becomes available.";
    }

    public partial class ContuberniumViewModel : ObservableObject
    {
        private readonly ContuberniumService _contuberniumService;
        private readonly DispatcherQueue _dispatcherQueue;

        [ObservableProperty]
        private string pageTitle = "Contubernium Newsletter";

        [ObservableProperty]
        private string newsletterContent = "Loading newsletter...";

        [ObservableProperty]
        private string lastUpdateTime = "Never";

        [ObservableProperty]
        private bool isRefreshing;

        public ContuberniumViewModel(ContuberniumService contuberniumService)
        {
            _contuberniumService = contuberniumService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public async Task InitializeAsync()
        {
            await _contuberniumService.InitializeAsync();
            RefreshContent();

            _contuberniumService.ContentUpdated += (s, args) =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    RefreshContent();
                    LastUpdateTime = args.FetchTime.ToString("yyyy-MM-dd HH:mm:ss");
                    if (!args.Success)
                    {
                        NewsletterContent = $"Failed to fetch newsletter: {args.ErrorMessage}";
                    }
                });
            };
        }

        private void RefreshContent()
        {
            var content = _contuberniumService.GetCachedContent();
            NewsletterContent = string.IsNullOrEmpty(content) 
                ? "No newsletter content available." 
                : content;
        }

        public async Task ManualRefreshAsync()
        {
            IsRefreshing = true;
            try
            {
                await _contuberniumService.ManualRefreshAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }
    }
}

