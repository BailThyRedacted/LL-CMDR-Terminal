using System;
using System.Text.Json;
using System.Threading.Tasks;
using EliteDataCollector.Core.Interfaces;

namespace ExplorationModule
{
    public class ExplorationModule : IGameLoopModule
    {
        public string Name => "Exploration";
        public string Description => "Identifies valuable planets and exobio signals.";

        public async Task InitializeAsync(IServiceProvider services)
        {
            // Get services like IOutputWriter from the service provider
            // Load configuration, etc.
        }

        public async Task OnJournalLineAsync(string line, JsonDocument parsedEvent)
        {
            // Process journal events (e.g., Scan, ScanOrganic)
        }

        public async Task OnCapiProfileAsync(JsonDocument profile)
        {
            // Not needed for now, but required by interface
        }

        public async Task ShutdownAsync()
        {
            // Clean up
        }
    }
}