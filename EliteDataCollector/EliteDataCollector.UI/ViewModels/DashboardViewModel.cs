using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EliteDataCollector.UI.Services;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace EliteDataCollector.UI.ViewModels
{
    /// <summary>
    /// Dashboard view model - manages display metrics, real-time updates from journal,
    /// and periodic Supabase data refresh.
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly JournalDataService _journalDataService;
        private readonly DashboardSettingsService _settingsService;
        private readonly EliteDataCollector.Core.Services.SupabaseClient _supabaseClient;
        private readonly DispatcherQueue _dispatcherQueue;
        private Timer? _supabaseRefreshTimer;

        [ObservableProperty]
        private long credits;

        [ObservableProperty]
        private string currentLocation = "Unknown";

        [ObservableProperty]
        private string currentStarport = "N/A";

        [ObservableProperty]
        private int powerplayMerits;

        [ObservableProperty]
        private ObservableCollection<JournalEventDisplay> recentActivity = new();

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string lastUpdateTime = "Never";

        public ObservableCollection<MetricToggle> AvailableMetrics { get; } = new();

        public DashboardViewModel(
            JournalDataService journalDataService,
            DashboardSettingsService settingsService,
            EliteDataCollector.Core.Services.SupabaseClient supabaseClient)
        {
            _journalDataService = journalDataService;
            _settingsService = settingsService;
            _supabaseClient = supabaseClient;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            InitializeMetrics();
        }

        public async Task InitializeAsync()
        {
            // Subscribe to journal data changes
            _journalDataService.DataChanged += OnJournalDataChanged;

            // Initialize dashboard
            await RefreshSupabaseDataAsync();

            // Start periodic Supabase refresh
            var settings = _settingsService.GetSettings();
            _supabaseRefreshTimer = new Timer(
                async _ => await RefreshSupabaseDataAsync(),
                null,
                TimeSpan.FromMinutes(settings.SupabaseRefreshIntervalMinutes),
                TimeSpan.FromMinutes(settings.SupabaseRefreshIntervalMinutes));
        }

        private void InitializeMetrics()
        {
            var settings = _settingsService.GetSettings();

            AvailableMetrics.Add(new MetricToggle { Name = "Credits", IsEnabled = settings.DisplayMetrics.Contains("Credits") });
            AvailableMetrics.Add(new MetricToggle { Name = "CurrentLocation", IsEnabled = settings.DisplayMetrics.Contains("CurrentLocation") });
            AvailableMetrics.Add(new MetricToggle { Name = "FactionInfluence", IsEnabled = settings.DisplayMetrics.Contains("FactionInfluence") });
            AvailableMetrics.Add(new MetricToggle { Name = "PowerplayMerits", IsEnabled = settings.DisplayMetrics.Contains("PowerplayMerits") });
            AvailableMetrics.Add(new MetricToggle { Name = "RecentActivity", IsEnabled = settings.DisplayMetrics.Contains("RecentActivity") });
        }

        private void OnJournalDataChanged(object? sender, JournalDataService.JournalDataChangedEventArgs args)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                Credits = _journalDataService.CurrentCredits;
                CurrentLocation = _journalDataService.CurrentSystemName ?? "Unknown";
                CurrentStarport = _journalDataService.CurrentStarport ?? "N/A";

                if (args.DataType == "Location")
                {
                    AddRecentActivity($"Jumped to {_journalDataService.CurrentSystemName}");
                }

                LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
            });
        }

        private async Task RefreshSupabaseDataAsync()
        {
            try
            {
                IsLoading = true;
                // Placeholder for Supabase data fetch
                // This will be populated when module data becomes available
                await Task.Delay(500); // Simulated network delay
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AddRecentActivity(string activity)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                RecentActivity.Insert(0, new JournalEventDisplay
                {
                    EventType = activity,
                    Timestamp = DateTime.Now
                });

                // Keep only last 10 events
                while (RecentActivity.Count > 10)
                {
                    RecentActivity.RemoveAt(RecentActivity.Count - 1);
                }
            });
        }

        [RelayCommand]
        public async Task RefreshNowAsync()
        {
            await RefreshSupabaseDataAsync();
        }

        public void Dispose()
        {
            _supabaseRefreshTimer?.Dispose();
        }
    }

    public class MetricToggle
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class JournalEventDisplay
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}

