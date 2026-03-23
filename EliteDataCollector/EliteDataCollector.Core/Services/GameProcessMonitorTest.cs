using System;
using System.Threading.Tasks;
using EliteDataCollector.Core;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Tests
{
    // NOTE: This test file references GameProcessMonitor implementation and MonitorMode enum
    // which need to be created. Commenting out for now.
    /*
    public class GameProcessMonitorTest
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     GameProcessMonitor Integration Test                        ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║  Launch Elite Dangerous now to test game detection!            ║");
            Console.WriteLine("║  You'll see:                                                   ║");
            Console.WriteLine("║    - Game launched detected (within ~5 seconds of launch)     ║");
            Console.WriteLine("║    - MainCore starting data collection                        ║");
            Console.WriteLine("║    - Game exit detected                                       ║");
            Console.WriteLine("║    - MainCore stopping data collection                        ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║  Press Ctrl+C to stop the test                                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Create output writer
            var output = new ConsoleOutputWriter();

            // Create the real GameProcessMonitor (continuous polling mode, with logging)
            // Constructor parameters:
            // - mode: MonitorMode.ContinuousPolling (default, always watching)
            // - pollIntervalMs: 5000 (5 seconds between checks)
            // - outputWriter: output (for logging)
            var gameMonitor = new GameProcessMonitor(
                mode: MonitorMode.ContinuousPolling,
                pollIntervalMs: 5000,
                outputWriter: output);

            // Create stub implementations for other services (minimal for testing)
            var journalMonitor = new StubJournalMonitor(output);
            var capiAuth = new StubCapiAuth(output);
            var validator = new StubSquadronValidator(output);

            // Create MainCore with real GameProcessMonitor
            var core = new MainCore(gameMonitor, journalMonitor, capiAuth, validator, output);

            try
            {
                // Initialize everything
                await core.InitializeAsync();

                // Keep running until user presses Ctrl+C
                Console.WriteLine();
                Console.WriteLine("Monitoring started. Launch/exit Elite Dangerous to see events...");
                Console.WriteLine("Press Ctrl+C to exit this test.");
                Console.WriteLine();

                // Wait forever (until Ctrl+C)
                await Task.Delay(Timeout.Infinite);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Test interrupted by user.");
            }
            finally
            {
                // Always clean up
                Console.WriteLine();
                Console.WriteLine("Shutting down...");
                await core.ShutdownAsync();
                Console.WriteLine("Test completed.");
            }
        }
    }

    // ========================================================================
    // STUB IMPLEMENTATIONS - Minimal implementations for testing
    // ========================================================================

    /// <summary>
    /// Stub JournalMonitor - Does nothing, just fulfills the interface.
    /// This lets us test GameProcessMonitor without implementing JournalMonitor yet.
    /// </summary>
    public class StubJournalMonitor : JournalMonitor
    {
        private readonly OutputWriter? _output;

        public event EventHandler<JournalLineEventArgs>? JournalLineRead;

        public StubJournalMonitor(OutputWriter? output = null) => _output = output;

        public Task StartAsync()
        {
            _output?.WriteLine("StubJournalMonitor: StartAsync() called");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _output?.WriteLine("StubJournalMonitor: StopAsync() called");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stub CapiAuth - Does nothing, just fulfills the interface.
    /// </summary>
    public class StubCapiAuth : CapiAuth
    {
        private readonly OutputWriter? _output;

        public StubCapiAuth(OutputWriter? output = null) => _output = output;

        public Task InitializeAsync()
        {
            _output?.WriteLine("StubCapiAuth: InitializeAsync() called");
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>("stub-token");

        public Task RefreshTokenAsync()
        {
            _output?.WriteLine("StubCapiAuth: RefreshTokenAsync() called");
            return Task.CompletedTask;
        }

        public Task<bool> AuthenticateAsync() => Task.FromResult(true);

        public bool HasStoredCredentials() => true;
    }

    /// <summary>
    /// Stub SquadronValidator - Always returns true (user is valid).
    /// </summary>
    public class StubSquadronValidator : SquadronValidator
    {
        private readonly OutputWriter? _output;

        public StubSquadronValidator(OutputWriter? output = null) => _output = output;

        public Task InitializeAsync()
        {
            _output?.WriteLine("StubSquadronValidator: InitializeAsync() called");
            return Task.CompletedTask;
        }

        public Task<bool> ValidateAsync()
        {
            _output?.WriteLine("StubSquadronValidator: ValidateAsync() called");
            return Task.FromResult(true);
        }

        public string? GetValidatedSquadron() => "Lavigny's Legion (Test)";
    }
    */
}
