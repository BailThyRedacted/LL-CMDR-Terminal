using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Concrete implementation of GameProcessMonitor.
    /// Continuously polls for EliteDangerous64.exe process and raises events when it starts/exits.
    ///
    /// DESIGN NOTES:
    /// - Polls every 5 seconds (configurable)
    /// - Runs async so it never blocks the main thread
    /// - Uses a CancellationToken to gracefully stop monitoring
    /// - Tracks game state to only raise events on transitions (not every poll)
    /// - Logs all activity through OutputWriter for debugging
    /// </summary>
    public class GameProcessMonitorImpl : GameProcessMonitor
    {
        private readonly OutputWriter? _outputWriter;
        private readonly int _pollIntervalMs;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitoringTask;
        private bool _isGameRunning = false;

        public event EventHandler? GameLaunched;
        public event EventHandler? GameExited;

        /// <summary>
        /// Creates a new GameProcessMonitorImpl.
        ///
        /// Parameters:
        /// - outputWriter: Optional logging (null = silent mode)
        /// - pollIntervalMs: How often to check for the game (default 5000ms = 5 seconds)
        /// </summary>
        public GameProcessMonitorImpl(OutputWriter? outputWriter = null, int pollIntervalMs = 5000)
        {
            _outputWriter = outputWriter;
            _pollIntervalMs = pollIntervalMs;
        }

        public async Task StartAsync()
        {
            _outputWriter?.WriteLine("[GameProcessMonitor] Starting game process monitoring...", LogLevel.Info);

            // Create a cancellation token so we can stop the monitoring task later
            _cancellationTokenSource = new CancellationTokenSource();

            // Start the monitoring task (runs in background)
            _monitoringTask = MonitorProcessAsync(_cancellationTokenSource.Token);

            // Wait briefly to let the first poll complete
            await Task.Delay(100);

            _outputWriter?.WriteLine("[GameProcessMonitor] Game process monitoring started", LogLevel.Debug);
        }

        public async Task StopAsync()
        {
            _outputWriter?.WriteLine("[GameProcessMonitor] Stopping game process monitoring...", LogLevel.Info);

            if (_cancellationTokenSource != null)
            {
                // Signal the monitoring task to stop
                _cancellationTokenSource.Cancel();

                // Wait for the task to actually stop
                if (_monitoringTask != null)
                {
                    try
                    {
                        await _monitoringTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when we cancel the token
                        _outputWriter?.WriteLine("[GameProcessMonitor] Monitor task cancelled", LogLevel.Debug);
                    }
                }

                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            _isGameRunning = false;
            _outputWriter?.WriteLine("[GameProcessMonitor] Game process monitoring stopped", LogLevel.Debug);
        }

        /// <summary>
        /// Continuously polls for EliteDangerous64.exe until cancellation is requested.
        /// This runs in a background task so it doesn't block the main thread.
        /// </summary>
        private async Task MonitorProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check if the game process exists
                        bool gameCurrentlyRunning = IsGameRunning();

                        // If state changed, raise appropriate event
                        if (gameCurrentlyRunning && !_isGameRunning)
                        {
                            // Game just launched!
                            _isGameRunning = true;
                            _outputWriter?.WriteLine("[GameProcessMonitor] Game launched detected! (EliteDangerous64.exe)", LogLevel.Info);
                            GameLaunched?.Invoke(this, EventArgs.Empty);
                        }
                        else if (!gameCurrentlyRunning && _isGameRunning)
                        {
                            // Game just exited!
                            _isGameRunning = false;
                            _outputWriter?.WriteLine("[GameProcessMonitor] Game exit detected", LogLevel.Info);
                            GameExited?.Invoke(this, EventArgs.Empty);
                        }

                        // Sleep before next poll
                        await Task.Delay(_pollIntervalMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _outputWriter?.WriteLine($"[GameProcessMonitor] Error during poll: {ex.Message}", LogLevel.Warning);
                        // Continue monitoring even if a single poll fails
                        await Task.Delay(_pollIntervalMs, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cleanup when requested to stop
            }
        }

        /// <summary>
        /// Checks if EliteDangerous64.exe is currently running.
        /// Returns true if found, false otherwise.
        /// </summary>
        private bool IsGameRunning()
        {
            try
            {
                // Look for a process named "EliteDangerous64" (without .exe)
                Process[] processes = Process.GetProcessesByName("EliteDangerous64");
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[GameProcessMonitor] Error checking process: {ex.Message}", LogLevel.Warning);
                return false;
            }
        }
    }
}
