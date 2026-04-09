namespace EliteDataCollector.UI.Services
{
    /// <summary>
    /// Manages real-time journal data extraction and aggregation.
    /// Subscribes to JournalMonitor events and aggregates player metrics.
    /// </summary>
    public class JournalDataService
    {
        private EliteDataCollector.Core.Services.JournalMonitor? _journalMonitor;
        private long _currentCredits;
        private string? _currentSystemName;
        private string? _currentStarport;
        private DateTime _lastUpdate;

        public event EventHandler<JournalDataChangedEventArgs>? DataChanged;

        public long CurrentCredits => _currentCredits;
        public string? CurrentSystemName => _currentSystemName;
        public string? CurrentStarport => _currentStarport;
        public DateTime LastUpdate => _lastUpdate;

        public void Initialize(EliteDataCollector.Core.Services.JournalMonitor journalMonitor)
        {
            _journalMonitor = journalMonitor;
            _journalMonitor.JournalLineRead += OnJournalLineRead;
        }

        private void OnJournalLineRead(object? sender, EliteDataCollector.Core.Services.JournalLineEventArgs args)
        {
            try
            {
                // Parse relevant journal events
                switch (args.EventType)
                {
                    case "Location":
                    case "FSDJump":
                        ParseLocationEvent(args);
                        break;

                    case "Cargo":
                        ParseCargoEvent(args);
                        break;

                    case "Materials":
                        ParseMaterialsEvent(args);
                        break;

                    case "Credits":
                        ParseCreditsEvent(args);
                        break;

                    case "PowerplayCollect":
                    case "PowerplayDeliver":
                    case "PowerplayVote":
                    case "PowerplayFastTrack":
                        NotifyDataChanged(new JournalDataChangedEventArgs 
                        { 
                            EventType = args.EventType,
                            DataType = "Powerplay"
                        });
                        break;
                }

                _lastUpdate = DateTime.UtcNow;
            }
            catch
            {
                // Silently ignore parsing errors
            }
        }

        private void ParseLocationEvent(EliteDataCollector.Core.Services.JournalLineEventArgs args)
        {
            if (args.ParsedEvent == null) return;

            try
            {
                using var doc = args.ParsedEvent;
                var root = doc.RootElement;

                if (root.TryGetProperty("StarSystem", out var systemProp))
                {
                    _currentSystemName = systemProp.GetString();
                }

                if (root.TryGetProperty("StationName", out var starportProp))
                {
                    _currentStarport = starportProp.GetString();
                }

                NotifyDataChanged(new JournalDataChangedEventArgs 
                { 
                    EventType = args.EventType,
                    DataType = "Location"
                });
            }
            catch { }
        }

        private void ParseCargoEvent(EliteDataCollector.Core.Services.JournalLineEventArgs args)
        {
            // Cargo event parsing placeholder
        }

        private void ParseMaterialsEvent(EliteDataCollector.Core.Services.JournalLineEventArgs args)
        {
            // Materials event parsing placeholder
        }

        private void ParseCreditsEvent(EliteDataCollector.Core.Services.JournalLineEventArgs args)
        {
            if (args.ParsedEvent == null) return;

            try
            {
                using var doc = args.ParsedEvent;
                var root = doc.RootElement;

                if (root.TryGetProperty("Credits", out var creditsProp) && creditsProp.TryGetInt64(out var credits))
                {
                    _currentCredits = credits;
                    NotifyDataChanged(new JournalDataChangedEventArgs 
                    { 
                        EventType = "Credits",
                        DataType = "Credits",
                        LongValue = credits
                    });
                }
            }
            catch { }
        }

        private void NotifyDataChanged(JournalDataChangedEventArgs args)
        {
            DataChanged?.Invoke(this, args);
        }

        public class JournalDataChangedEventArgs : EventArgs
        {
            public string EventType { get; set; } = string.Empty;
            public string DataType { get; set; } = string.Empty;
            public long? LongValue { get; set; }
            public string? StringValue { get; set; }
        }
    }
}

