using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Enum defining how the GameProcessMonitor operates.
    ///
    /// TEACHING: Enums let you define a fixed set of named values.
    /// Better than strings ("continuous") or integers (0/1) because
    /// the compiler checks your spelling and prevents typos.
    /// </summary>
    public enum MonitorMode
    {
        /// <summary>
        /// App runs continuously in background, polling every N seconds.
        /// Best for: Always-on data collection (app starts with system).
        /// Detection: Game launch/exit detected within 5 seconds.
        /// </summary>
        ContinuousPolling,

        /// <summary>
        /// App is launched by Steam, detects game once, then stops.
        /// Best for: Steam integration (launches app only when needed).
        /// Detection: Immediate (single check when app starts).
        /// </summary>
        SteamLaunchTriggered
    }

    /// <summary>
    /// Real implementation of IGameProcessMonitor that detects when Elite Dangerous launches and exits.
    ///
    /// HOW IT WORKS (High Level):
    /// ===========================
    /// This class runs a background "polling loop" that:
    /// 1. Checks if EliteDangerous64.exe is running every N seconds (configurable)
    /// 2. Compares current state to previous state
    /// 3. Raises GameLaunched event when it detects a launch
    /// 4. Raises GameExited event when it detects an exit
    ///
    /// TWO MODES:
    /// ===========
    /// - ContinuousPolling: Classic background monitoring (default)
    /// - SteamLaunchTriggered: Single check on startup (for Steam integration)
    ///
    /// WHY POLLING (Instead of Other Methods)?
    /// ========================================
    /// - Simple: Just check if process exists or not
    /// - Reliable: Works regardless of how game is launched
    /// - Testable: Easy to mock (just return true/false)
    /// - Lightweight: Process.GetProcessesByName() is very fast (~1-2ms)
    /// - No Dependencies: Uses built-in .NET APIs
    ///
    /// THREADING MODEL:
    /// ================
    /// - StartAsync() creates a background task
    /// - This task runs the MonitoringLoop (continuously or once)
    /// - Main app thread doesn't block (it's async)
    /// - Stop is triggered by CancellationToken
    ///
    /// C# CONCEPTS YOU'LL LEARN:
    /// ========================
    /// - Enums: Fixed set of named values
    /// - CancellationToken: Way to signal a background task to stop
    /// - Task & async/await: Non-blocking asynchronous operations
    /// - Process.GetProcessesByName(): Find running processes
    /// - Event raising: Notify subscribers when something happens
    /// - Constructor parameters: Configuration for each instance
    /// </summary>
    public class GameProcessMonitor : IGameProcessMonitor
    {
        // ====================================================================
        // CONSTANTS - Values that never change
        // ====================================================================

        /// <summary>
        /// The exact name of the Elite Dangerous process (without .exe extension).
        /// This name must match EXACTLY what appears in Task Manager.
        /// Used by Process.GetProcessesByName("EliteDangerous64") to find the game.
        /// </summary>
        private const string GAME_PROCESS_NAME = "EliteDangerous64";

        /// <summary>
        /// How often (in milliseconds) to check if the game is running.
        /// 5000 ms = 5 seconds between checks (default).
        ///
        /// PERFORMANCE ANALYSIS:
        /// - Each check takes ~1-2ms (very fast)
        /// - 5 second intervals = ~288 checks per day
        /// - CPU impact: negligible (< 0.1%)
        /// - Response time: Game launch detected within ~5 seconds
        /// - Battery impact: minimal (suitable for background app)
        ///
        /// Can be customized per instance via constructor.
        /// </summary>
        private const int DEFAULT_POLL_INTERVAL_MS = 5000;

        // ====================================================================
        // FIELDS - Instance variables that persist across method calls
        // ====================================================================

        /// <summary>
        /// Which mode this monitor is running in.
        /// ContinuousPolling = Keep polling until StopAsync
        /// SteamLaunchTriggered = Check once and exit
        ///
        /// TEACHING: readonly means this field is set in constructor
        /// and can never be changed after that. This prevents accidents.
        /// </summary>
        private readonly MonitorMode _mode;

        /// <summary>
        /// How often to poll (in milliseconds).
        /// Configurable per instance (default: 5000ms = 5 seconds).
        ///
        /// TEACHING: readonly + constructor parameter = immutable configuration.
        /// Different instances can have different poll intervals.
        /// Example: new GameProcessMonitor(pollIntervalMs: 30000) for 30 seconds
        /// </summary>
        private readonly int _pollIntervalMs;

        /// <summary>
        /// Reference to the output writer for logging (optional).
        ///
        /// WHY OPTIONAL?
        /// - App can run "silently" without logging
        /// - Tests don't need to provide a logger
        /// - Different UIs (console, GUI, service) can provide different loggers
        ///
        /// C# CONCEPT - Nullable Reference Type (?):
        /// The ? means "this can be null"
        /// When we use it, we check with ?. to avoid null reference errors
        /// </summary>
        private readonly IOutputWriter? _outputWriter;
        ///
        /// C# CONCEPT - CancellationToken:
        /// This is a way to say "please stop what you're doing" to a background task.
        /// When we call _cancellationTokenSource.Cancel(), it sets this token to "cancelled".
        /// The monitoring loop checks this token regularly and exits when it's cancelled.
        ///
        /// Why nullable (CancellationTokenSource?)?
        /// - Initially null (no monitoring yet)
        /// - Created when StartAsync() is called
        /// - Disposed when StopAsync() finishes
        /// </summary>
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// The background task that runs the monitoring loop.
        ///
        /// C# CONCEPT - Task:
        /// This is a "promise" that work is being done in the background.
        /// We can await it to know when it completes.
        /// Null = no monitoring task running
        /// </summary>
        private Task? _monitoringTask;

        /// <summary>
        /// Tracks the previous state of the game process.
        ///
        /// HOW STATE CHANGE DETECTION WORKS:
        /// - This variable remembers: "Was the game running last time I checked?"
        /// - Each poll, we compare:
        ///   * Current state: Is it running now? (from IsGameProcessRunning())
        ///   * Previous state: Was it running last time?
        ///   * _wasGameRunning field
        /// - If different: Something changed! Raise event.
        /// - Update _wasGameRunning for next comparison
        ///
        /// EXAMPLE Timeline:
        /// Check 1: Game not running, _wasGameRunning = false → No change, do nothing
        /// Check 2: Game not running, _wasGameRunning = false → No change, do nothing
        /// Check 3: Game IS running, _wasGameRunning = false → CHANGED! Raise GameLaunched, set _wasGameRunning = true
        /// Check 4: Game running, _wasGameRunning = true → No change, do nothing
        /// Check 5: Game not running, _wasGameRunning = true → CHANGED! Raise GameExited, set _wasGameRunning = false
        /// </summary>
        private bool _wasGameRunning = false;

        /// <summary>
        /// True if we're currently polling/monitoring.
        /// Used for idempotency checks (can't start twice).
        ///
        /// TEACHING: Idempotency means "safe to call multiple times".
        /// If StartAsync() is called twice, the second call returns immediately
        /// because _isMonitoring is already true.
        /// </summary>
        private bool _isMonitoring = false;

        // ====================================================================
        // EVENTS - Things that can happen and be observed by others
        // ====================================================================

        /// <summary>
        /// Event that fires when the game process is detected as launched.
        /// MainCore listens to this and calls OnGameLaunched().
        ///
        /// C# CONCEPT - Events:
        /// Events are like notifications:
        /// - This class RAISES the event: GameLaunched?.Invoke(this, EventArgs.Empty)
        /// - MainCore SUBSCRIBES to it: _gameMonitor.GameLaunched += OnGameLaunched
        /// - When we raise it, all subscribers get called automatically
        /// </summary>
        public event EventHandler? GameLaunched;

        /// <summary>
        /// Event that fires when the game process is detected as exited.
        /// MainCore listens to this and calls OnGameExited().
        /// </summary>
        public event EventHandler? GameExited;

        // ====================================================================
        // CONSTRUCTOR - Initialization code that runs when the class is created
        // ====================================================================

        /// <summary>
        /// Creates a new GameProcessMonitor with specified configuration.
        ///
        /// TEACHING: Constructor parameters become configuration for this instance.
        ///
        /// Parameters:
        /// - mode: Which polling mode (ContinuousPolling or SteamLaunchTriggered)
        /// - pollIntervalMs: How often to check (milliseconds, default 5000)
        /// - outputWriter: Optional logger (null = silent)
        ///
        /// EXAMPLE USAGE:
        ///
        /// // Example 1: Default continuous monitoring every 5 seconds
        /// var monitor = new GameProcessMonitor();
        ///
        /// // Example 2: Custom polling interval (30 seconds instead of 5)
        /// var monitor = new GameProcessMonitor(pollIntervalMs: 30000);
        ///
        /// // Example 3: Steam launch mode (single check)
        /// var monitor = new GameProcessMonitor(mode: MonitorMode.SteamLaunchTriggered);
        ///
        /// // Example 4: With logging
        /// var output = new ConsoleOutputWriter();
        /// var monitor = new GameProcessMonitor(
        ///     mode: MonitorMode.ContinuousPolling,
        ///     pollIntervalMs: 5000,
        ///     outputWriter: output);
        /// </summary>
        public GameProcessMonitor(
            MonitorMode mode = MonitorMode.ContinuousPolling,
            int pollIntervalMs = DEFAULT_POLL_INTERVAL_MS,
            IOutputWriter? outputWriter = null)
        {
            // TEACHING: Validation
            // Check that the parameter values are sensible.
            // Fail fast: throw immediately if config is bad.
            if (pollIntervalMs <= 0)
                throw new ArgumentException("Poll interval must be greater than 0 milliseconds", nameof(pollIntervalMs));

            // Store configuration for later use
            _mode = mode;
            _pollIntervalMs = pollIntervalMs;
            _outputWriter = outputWriter; // Can be null (silent mode)

            // Initialize state
            _isMonitoring = false;
            _wasGameRunning = false;
            _cancellationTokenSource = null;
            _monitoringTask = null;

            _outputWriter?.WriteLine($"GameProcessMonitor: Created in {_mode} mode, polling every {_pollIntervalMs}ms for {GAME_PROCESS_NAME}");
        }

        // ====================================================================
        // PUBLIC METHODS - Called by MainCore
        // ====================================================================

        /// <summary>
        /// Starts monitoring for the game process.
        ///
        /// WHAT IT DOES:
        /// 1. If already monitoring, return immediately (idempotent)
        /// 2. Create a cancellation token for clean shutdown
        /// 3. Start MonitoringLoop on a background thread
        /// 4. Return immediately (doesn't wait for loop to finish)
        ///
        /// TEACHING: async Task pattern
        /// - "async" keyword means this method is asynchronous
        /// - "Task" return type means it represents work happening asynchronously
        /// - We typically use "await" to wait for it: await monitor.StartAsync();
        /// - But we can also "fire and forget": monitor.StartAsync(); (not recommended)
        ///
        /// TEACHING: Why return immediately?
        /// If we awaited the loop, the method would never return
        /// (loop runs forever in continuous mode).
        /// Instead, we start the loop and return.
        /// The loop runs in background on a thread pool thread.
        ///
        /// TEACHING: Why idempotent?
        /// Consider this:
        ///   await monitor.StartAsync();
        ///   await monitor.StartAsync(); // Oops, called again!
        ///
        /// Without idempotent check:
        /// - First call: Creates task, starts polling
        /// - Second call: Creates ANOTHER task, starts ANOTHER polling loop!
        ///   (Now we have 2 loops competing, events fire twice... bug!)
        ///
        /// With idempotent check (our code):
        /// - First call: _isMonitoring=false → create task, set _isMonitoring=true
        /// - Second call: _isMonitoring=true → return immediately (safe!)
        /// </summary>
        public async Task StartAsync()
        {
            // IDEMPOTENCY CHECK: If already monitoring, do nothing
            // This prevents accidental double-starts
            if (_isMonitoring)
            {
                _outputWriter?.WriteLine("GameProcessMonitor: Already monitoring, ignoring duplicate StartAsync call");
                return;
            }

            try
            {
                _outputWriter?.WriteLine("GameProcessMonitor: Starting monitoring...");

                // Create a new cancellation token source for this monitoring session
                // We'll use this to signal the loop to stop
                _cancellationTokenSource = new CancellationTokenSource();

                // Start the monitoring loop on a background thread
                // Task.Run means: "Run this on a thread pool thread, don't block caller"
                // MonitoringLoop will run in background while we return
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                // Mark as monitoring
                _isMonitoring = true;

                _outputWriter?.WriteLine($"GameProcessMonitor: Monitoring started ({_mode} mode, {_pollIntervalMs}ms interval)");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"GameProcessMonitor: Error starting: {ex.Message}");
                throw; // Re-throw so caller knows something failed
            }
        }

        /// <summary>
        /// Stops monitoring for the game process.
        ///
        /// WHAT IT DOES:
        /// 1. If not monitoring, return immediately (idempotent)
        /// 2. Signal the cancellation token (tells loop to stop)
        /// 3. Await the monitoring task (wait for loop to finish)
        /// 4. Mark as not monitoring
        ///
        /// TEACHING: Why async Task? This is the right pattern.
        /// async void would be dangerous (can't await, can't track exceptions).
        /// async Task is correct.
        ///
        /// TEACHING: Why never throw?
        /// Stopping is cleanup. Even if something goes wrong,
        /// we want to clean up as much as possible.
        /// Throwing would prevent cleanup of other things.
        /// So we catch and log, then continue cleanup.
        /// This is "graceful degradation" or "best effort cleanup".
        /// </summary>
        public async Task StopAsync()
        {
            // IDEMPOTENCY CHECK: If not monitoring, nothing to stop
            if (!_isMonitoring)
            {
                _outputWriter?.WriteLine("GameProcessMonitor: Not monitoring, ignoring StopAsync call");
                return;
            }

            try
            {
                _outputWriter?.WriteLine("GameProcessMonitor: Stopping monitoring...");

                // Signal the cancellation token (tells MonitoringLoop to exit)
                _cancellationTokenSource?.Cancel();

                // Wait for the monitoring task to finish
                if (_monitoringTask != null)
                {
                    await _monitoringTask;
                }

                // Clean up the cancellation token source
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _monitoringTask = null;

                // Mark as not monitoring
                _isMonitoring = false;

                _outputWriter?.WriteLine("GameProcessMonitor: Monitoring stopped");
            }
            catch (Exception ex)
            {
                // Log but don't throw (we're cleaning up)
                _outputWriter?.WriteLine($"GameProcessMonitor: Error during stop: {ex.Message}");
            }
        }

        // ====================================================================
        // PRIVATE METHODS - Helper methods used internally
        // ====================================================================

        /// <summary>
        /// The main polling loop that runs in background.
        /// Continuously (or once, depending on mode) checks for game process.
        ///
        /// TEACHING: This is the heart of the monitoring.
        /// It runs on a background thread and checks the game status.
        ///
        /// TEACHING: CancellationToken parameter
        /// The loop checks token.IsCancellationRequested regularly.
        /// When StopAsync calls _cancellationTokenSource.Cancel(),
        /// this token gets the signal and loop exits gracefully.
        ///
        /// TEACHING: Mode-specific behavior
        /// - ContinuousPolling: Loop runs until cancelled
        /// - SteamLaunchTriggered: Loop checks once and exits
        ///
        /// FLOW (Continuous Mode):
        /// 1. Loop starts: while (task not cancelled)
        /// 2. Check if game running?
        /// 3. If state changed: raise event
        /// 4. Wait N milliseconds (but can be interrupted by cancellation)
        /// 5. Go to step 2
        ///
        /// FLOW (Steam Mode):
        /// 1. Check if game running?
        /// 2. If state changed: raise event
        /// 3. Exit (no loop)
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            _outputWriter?.WriteLine("GameProcessMonitor: Monitoring loop started.");

            try
            {
                // Initialize state: remember if game was running at start
                // (Usually false if app just started, but could be true if game already running)
                _wasGameRunning = IsGameProcessRunning();
                _outputWriter?.WriteLine($"GameProcessMonitor: Initial game state: {(_wasGameRunning ? "RUNNING" : "NOT RUNNING")}");

                // MODE: Steam Launch Triggered - Check once and return
                if (_mode == MonitorMode.SteamLaunchTriggered)
                {
                    _outputWriter?.WriteLine("GameProcessMonitor: Steam mode - single check...");

                    // Perform one check
                    bool isRunning = IsGameProcessRunning();

                    if (isRunning && !_wasGameRunning)
                    {
                        _outputWriter?.WriteLine("GameProcessMonitor: Game detected running - raising GameLaunched");
                        RaiseGameLaunched();
                    }
                    else if (!isRunning && _wasGameRunning)
                    {
                        _outputWriter?.WriteLine("GameProcessMonitor: Game detected exited - raising GameExited");
                        RaiseGameExited();
                    }
                    else
                    {
                        _outputWriter?.WriteLine($"GameProcessMonitor: Game is {(isRunning ? "running" : "not running")} (no change)");
                    }

                    // Steam mode: done, exit the loop/task
                    return;
                }

                // MODE: Continuous Polling - Loop until cancelled
                _outputWriter?.WriteLine($"GameProcessMonitor: Continuous mode - polling every {_pollIntervalMs}ms");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // TEACHING: Check the current state
                        bool isGameRunningNow = IsGameProcessRunning();

                        // TEACHING: Detect state change
                        if (isGameRunningNow && !_wasGameRunning)
                        {
                            // Game just launched!
                            _outputWriter?.WriteLine("GameProcessMonitor: Game launched detected");
                            RaiseGameLaunched();
                            _wasGameRunning = true;
                        }
                        else if (!isGameRunningNow && _wasGameRunning)
                        {
                            // Game just exited!
                            _outputWriter?.WriteLine("GameProcessMonitor: Game exit detected");
                            RaiseGameExited();
                            _wasGameRunning = false;
                        }
                        // else: no state change, keep polling

                        // TEACHING: Wait before next check (but allow cancellation to interrupt)
                        // Task.Delay is a non-blocking wait (doesn't freeze app)
                        // Passing cancellationToken allows it to exit immediately if cancelled
                        await Task.Delay(_pollIntervalMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // This is expected when StopAsync() cancels the token
                        // Just break out of the loop gracefully
                        _outputWriter?.WriteLine("GameProcessMonitor: Monitoring loop cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Unexpected error in the loop
                        // Log it but continue monitoring (don't crash)
                        _outputWriter?.WriteLine($"GameProcessMonitor: Error in monitoring loop: {ex.Message}. Continuing...");
                        // Continue to next iteration
                    }
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"GameProcessMonitor: Monitoring loop crashed: {ex.Message}");
            }
            finally
            {
                _outputWriter?.WriteLine("GameProcessMonitor: Monitoring loop ended.");
            }
        }

        /// <summary>
        /// Checks if the Elite Dangerous game process is currently running.
        ///
        /// HOW IT WORKS:
        /// 1. Call Process.GetProcessesByName("EliteDangerous64")
        /// 2. This returns an array of all running processes with that name
        /// 3. If array.Length > 0, the game is running
        /// 4. If array.Length == 0, the game is not running
        ///
        /// C# CONCEPT - Process API:
        /// Process is a .NET class that represents a running application.
        /// GetProcessesByName() finds all processes matching a name.
        ///
        /// WHY TRY-CATCH?
        /// If something goes wrong (permissions, system issues), we don't crash.
        /// Just return false (assume game is not running).
        /// The app must be resilient.
        ///
        /// PERFORMANCE:
        /// This is VERY fast (1-2ms). It's safe to call every 5 seconds.
        /// </summary>
        private bool IsGameProcessRunning()
        {
            try
            {
                // Get all processes named "EliteDangerous64"
                // NOTE: Don't include .exe extension
                Process[] processes = Process.GetProcessesByName(GAME_PROCESS_NAME);

                // If we found at least one process, the game is running
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                // Something went wrong (permissions, system issue, etc.)
                // Log and assume game is not running
                _outputWriter?.WriteLine("GameProcessMonitor: Error checking process: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Raises the GameLaunched event.
        ///
        /// C# CONCEPT - Event Raising:
        /// To notify subscribers that something happened, we "invoke" the event.
        /// The syntax is: EventName?.Invoke(this, EventArgs.Empty)
        ///
        /// WHY THE ? OPERATOR?
        /// GameLaunched might be null if nobody is listening.
        /// The ?. operator says "if not null, invoke it; else do nothing"
        /// This prevents "null reference" errors.
        ///
        /// THIS and EventArgs:
        /// - this: "I (GameProcessMonitor) am raising this event"
        /// - EventArgs.Empty: "No additional data to send"
        ///   (In future, we could send data like metadata)
        /// </summary>
        private void RaiseGameLaunched()
        {
            // Invoke the event if anyone is listening
            GameLaunched?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the GameExited event.
        /// Same pattern as RaiseGameLaunched.
        /// </summary>
        private void RaiseGameExited()
        {
            // Invoke the event if anyone is listening
            GameExited?.Invoke(this, EventArgs.Empty);
        }
    }
}
