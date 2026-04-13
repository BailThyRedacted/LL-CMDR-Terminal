using CommunityToolkit.Mvvm.ComponentModel;
using EliteDataCollector.UI.Services;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// ViewModel for PvP Tracker page - displays encounter history from pvp_encounters.json.
    /// </summary>
    public partial class PvPTrackerViewModel : ObservableObject
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly string _encountersFilePath;
        private FileSystemWatcher? _fileWatcher;

        [ObservableProperty]
        private string pageTitle = "PvP Tracker";

        [ObservableProperty]
        private string summaryText = "No encounters recorded yet.";

        [ObservableProperty]
        private int totalEncounters;

        [ObservableProperty]
        private int hostileEncounters;

        [ObservableProperty]
        private int llSystemAlerts;

        public ObservableCollection<PvPEncounterDisplay> Encounters { get; } = new();

        public PvPTrackerViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EliteDangerousDataCollector");
            _encountersFilePath = Path.Combine(appDataPath, "pvp_encounters.json");

            LoadEncounters();
            WatchForChanges(appDataPath);
        }

        public void LoadEncounters()
        {
            try
            {
                Encounters.Clear();

                if (!File.Exists(_encountersFilePath))
                {
                    SummaryText = "No encounters recorded yet.";
                    return;
                }

                var json = File.ReadAllText(_encountersFilePath);
                var encounters = JsonSerializer.Deserialize<List<PvPEncounterData>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (encounters == null || encounters.Count == 0)
                {
                    SummaryText = "No encounters recorded yet.";
                    return;
                }

                // Sort newest first
                encounters.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

                TotalEncounters = encounters.Count;
                HostileEncounters = encounters.Count(e => e.IsHostile);
                LlSystemAlerts = encounters.Count(e => e.InLlSystem);

                foreach (var enc in encounters)
                {
                    Encounters.Add(new PvPEncounterDisplay
                    {
                        TimestampDisplay = enc.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        System = enc.System,
                        EventType = enc.EventType,
                        OtherCmdr = enc.OtherCmdr,
                        Outcome = enc.Outcome,
                        IsHostile = enc.IsHostile,
                        InLlSystem = enc.InLlSystem,
                        StatusIcon = enc.InLlSystem ? "🚨" : enc.IsHostile ? "⚠️" : "👀"
                    });
                }

                SummaryText = $"{TotalEncounters} encounter(s) | {HostileEncounters} hostile | {LlSystemAlerts} in LL systems";
            }
            catch (Exception ex)
            {
                SummaryText = $"Error loading encounters: {ex.Message}";
            }
        }

        private void WatchForChanges(string directory)
        {
            try
            {
                if (!Directory.Exists(directory)) return;

                _fileWatcher = new FileSystemWatcher(directory, "pvp_encounters.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += (s, e) =>
                {
                    // Debounce: wait a moment for file write to complete
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        _dispatcherQueue.TryEnqueue(() => LoadEncounters());
                    });
                };
            }
            catch
            {
                // File watching is optional
            }
        }
    }

    /// <summary>
    /// Display model for a PvP encounter in the UI ListView.
    /// </summary>
    public class PvPEncounterDisplay
    {
        public string TimestampDisplay { get; set; } = string.Empty;
        public string System { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string OtherCmdr { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public bool IsHostile { get; set; }
        public bool InLlSystem { get; set; }
        public string StatusIcon { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data model matching the JSON structure written by PvPTrackerModule.
    /// </summary>
    public class PvPEncounterData
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("system")]
        public string System { get; set; } = string.Empty;

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("otherCmdr")]
        public string OtherCmdr { get; set; } = string.Empty;

        [JsonPropertyName("outcome")]
        public string Outcome { get; set; } = string.Empty;

        [JsonPropertyName("isHostile")]
        public bool IsHostile { get; set; }

        [JsonPropertyName("inLlSystem")]
        public bool InLlSystem { get; set; }
    }
}

