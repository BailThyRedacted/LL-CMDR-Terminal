using System;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Monitors application idle state (when game is not running and no updates are needed).
    /// Used by UpdateService to determine when it's safe to restart the app.
    /// </summary>
    public interface IdleDetector
    {
        /// <summary>
        /// Checks if the app is currently idle (safe for restart).
        /// App is idle when: game not running AND no active data collection.
        /// </summary>
        Task<bool> IsIdleAsync();

        /// <summary>
        /// Registers a callback to be invoked when the app becomes idle.
        /// </summary>
        void OnBecomeIdle(Func<Task> callback);

        /// <summary>
        /// Updates the last activity timestamp.
        /// Call this when game activity is detected.
        /// </summary>
        void UpdateLastActivityTime();

        /// <summary>
        /// Gets the current idle duration in seconds.
        /// </summary>
        int GetIdleDurationSeconds();
    }

    /// <summary>
    /// Concrete implementation of IdleDetector.
    /// </summary>
    public class IdleDetectorImpl : IdleDetector
    {
        private readonly GameProcessMonitor _gameMonitor;
        private readonly OutputWriter? _outputWriter;
        private DateTime _lastActivityTime = DateTime.UtcNow;
        private bool _isGameRunning = false;
        private const int IdleThresholdSeconds = 300; // 5 minutes
        private Func<Task>? _idleCallback;

        public IdleDetectorImpl(GameProcessMonitor gameMonitor, OutputWriter? outputWriter = null)
        {
            _gameMonitor = gameMonitor ?? throw new ArgumentNullException(nameof(gameMonitor));
            _outputWriter = outputWriter;

            // Subscribe to game state changes
            _gameMonitor.GameLaunched += (sender, args) => OnGameLaunched();
            _gameMonitor.GameExited += (sender, args) => OnGameExited();
        }

        public async Task<bool> IsIdleAsync()
        {
            if (_isGameRunning)
            {
                return false; // Not idle if game is running
            }

            int idleDuration = GetIdleDurationSeconds();
            bool isIdle = idleDuration >= IdleThresholdSeconds;

            if (isIdle && _idleCallback != null)
            {
                _outputWriter?.WriteLine($"[IdleDetector] App is now idle (for {idleDuration}s), invoking callback", LogLevel.Debug);
                await _idleCallback();
            }

            return isIdle;
        }

        public void OnBecomeIdle(Func<Task> callback)
        {
            _idleCallback = callback;
        }

        public void UpdateLastActivityTime()
        {
            _lastActivityTime = DateTime.UtcNow;
            _outputWriter?.WriteLine("[IdleDetector] Activity detected, resetting idle timer", LogLevel.Debug);
        }

        public int GetIdleDurationSeconds()
        {
            return (int)DateTime.UtcNow.Subtract(_lastActivityTime).TotalSeconds;
        }

        private void OnGameLaunched()
        {
            _isGameRunning = true;
            UpdateLastActivityTime();
            _outputWriter?.WriteLine("[IdleDetector] Game launched, app no longer idle", LogLevel.Debug);
        }

        private void OnGameExited()
        {
            _isGameRunning = false;
            UpdateLastActivityTime();
            _outputWriter?.WriteLine("[IdleDetector] Game exited, idle timer started", LogLevel.Debug);
        }
    }
}
