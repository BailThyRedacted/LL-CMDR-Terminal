using System;
using System.Threading.Tasks;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Core
{
    /// <summary>
    /// MainCore is the orchestrator (conductor) of the entire application.
    ///
    /// DESIGN PHILOSOPHY:
    /// ==================
    /// - MainCore coordinates all services but doesn't do the actual work
    /// - Each service has a single responsibility (SOLID: Single Responsibility Principle)
    /// - Services communicate via events, not direct method calls (loose coupling)
    /// - State is validated at each step to prevent invalid transitions
    /// - Errors are handled gracefully with logging at each level
    ///
    /// LIFECYCLE:
    /// ==========
    /// 1. InitializeAsync() - Set up all services, wire event handlers
    /// 2. StartAsync()      - Begin data collection (when game launches)
    /// 3. StopAsync()       - Stop data collection (when game exits)
    /// 4. ShutdownAsync()   - Clean up everything (app closing)
    ///
    /// KEY PATTERNS USED:
    /// ==================
    /// - Dependency Injection: Services are passed to constructor, not created inside
    /// - Event-Driven: Services communicate via events, promoting loose coupling
    /// - State Machine: _isInitialized and _isRunning prevent invalid state transitions
    /// - Async/Await: All I/O is non-blocking to keep app responsive
    /// - Fail-Fast: Exceptions are thrown for precondition violations
    /// - Null-Safe Output: _outputWriter?. handles null gracefully
    /// </summary>
    public class MainCore : IDisposable
    {
        // ====================================================================
        // PRIVATE FIELDS - These are the services that do the actual work
        // ====================================================================

        private readonly GameProcessMonitor _gameMonitor;
        private readonly JournalMonitor _journalMonitor;
        private readonly CapiAuth _capiAuth;
        private readonly SquadronValidator _squadronValidator;
        private readonly OutputWriter? _outputWriter;

        // ====================================================================
        // STATE FIELDS - Track what mode the orchestrator is in
        // ====================================================================

        /// <summary>
        /// True if InitializeAsync() has completed successfully.
        /// Prevents calling Initialize twice or using services before they're ready.
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// True if StartAsync() has been called and game is running.
        /// False means we're in "idle" mode waiting for game to launch.
        /// </summary>
        private bool _isRunning = false;

        // ====================================================================
        // CONSTRUCTOR - Dependency Injection
        // ====================================================================

        /// <summary>
        /// Creates a new MainCore orchestrator.
        ///
        /// WHY EACH PARAMETER?
        /// - gameMonitor: Detects when game launches/exits
        /// - journalMonitor: Reads game journal events
        /// - capiAuth: Manages authentication tokens
        /// - squadronValidator: Checks squadron membership
        /// - outputWriter: Logs messages (can be null for silent mode)
        ///
        /// WHY DEPENDENCY INJECTION?
        /// - Tests can pass mock objects instead of real services
        /// - Services can be reused in multiple contexts
        /// - Follows SOLID principles (D = Dependency Inversion)
        /// - Makes it clear what dependencies this class needs
        /// </summary>
        public MainCore(
            GameProcessMonitor gameMonitor,
            JournalMonitor journalMonitor,
            CapiAuth capiAuth,
            SquadronValidator squadronValidator,
            OutputWriter? outputWriter = null)
        {
            // Null-check all required services
            // Use ?? throw pattern to fail fast with clear error
            _gameMonitor = gameMonitor ?? throw new ArgumentNullException(nameof(gameMonitor));
            _journalMonitor = journalMonitor ?? throw new ArgumentNullException(nameof(journalMonitor));
            _capiAuth = capiAuth ?? throw new ArgumentNullException(nameof(capiAuth));
            _squadronValidator = squadronValidator ?? throw new ArgumentNullException(nameof(squadronValidator));

            // OutputWriter is optional (null is OK for silent operation)
            _outputWriter = outputWriter;
        }

        // ====================================================================
        // PUBLIC LIFECYCLE METHODS
        // ====================================================================

        /// <summary>
        /// Initializes the orchestrator and all services.
        ///
        /// WHAT IT DOES:
        /// 1. Validate that we're not already initialized
        /// 2. Subscribe to service events (before starting services!)
        /// 3. Start the game process monitor
        /// 4. Initialize other services (but don't start them yet)
        /// 5. Mark as initialized
        ///
        /// WHY THESE STEPS?
        /// - Validation prevents duplicate initialization bugs
        /// - Event subscription BEFORE starting services ensures we don't miss events
        /// - Game monitor starts first so we can detect launches
        /// - Other services initialize but stay idle (game might not be running)
        /// - Initialization is async, so we await each step
        ///
        /// THROWS:
        /// - InvalidOperationException if already initialized
        /// - Any exception from services if initialization fails
        /// </summary>
        public async Task InitializeAsync()
        {
            // PRECONDITION CHECK: Can't initialize twice
            if (_isInitialized)
                throw new InvalidOperationException("MainCore is already initialized. Call ShutdownAsync() first if you want to restart.");

            try
            {
                _outputWriter?.WriteLine("MainCore: Initializing...");

                // STEP 1: Subscribe to events BEFORE starting services
                // This ensures we don't miss any events that fire during startup
                _outputWriter?.WriteLine("  - Subscribing to service events...");
                _gameMonitor.GameLaunched += OnGameLaunched;
                _gameMonitor.GameExited += OnGameExited;
                _journalMonitor.JournalLineRead += OnJournalLineRead;

                // STEP 2: Start game process monitor
                // This runs continuously, checking every 5 seconds for the game
                _outputWriter?.WriteLine("  - Starting game process monitor...");
                await _gameMonitor.StartAsync();

                // STEP 3: Initialize other services
                // They're initialized but not "started" - they wait until game launches
                _outputWriter?.WriteLine("  - Initializing CAPI auth...");
                await _capiAuth.InitializeAsync();

                _outputWriter?.WriteLine("  - Initializing squadron validator...");
                await _squadronValidator.InitializeAsync();

                // STEP 4: Mark as initialized
                _isInitialized = true;

                _outputWriter?.WriteLine("MainCore: Initialization complete. Waiting for game launch...");
            }
            catch (Exception ex)
            {
                // Any exception during init is a problem
                _outputWriter?.WriteLine($"MainCore: Initialization FAILED: {ex.Message}");

                // Clean up any partial initialization
                try { await _gameMonitor.StopAsync(); } catch { }

                throw; // Re-throw so caller knows initialization failed
            }
        }

        /// <summary>
        /// Starts active data collection.
        ///
        /// WHAT IT DOES:
        /// 1. Validate that initialization is complete
        /// 2. If already running, do nothing (idempotent)
        /// 3. Start journal monitoring
        /// 4. Refresh authentication tokens
        /// 5. Validate squadron membership
        /// 6. If validation fails, stop immediately
        /// 7. Mark as running
        ///
        /// WHY THESE STEPS?
        /// - We need initialized state before starting
        /// - Idempotent (safe to call multiple times)
        /// - Journal monitor starts before validation (we want to see events)
        /// - Auth refresh ensures tokens aren't stale
        /// - Squadron check is an access control gate
        /// - If squadron fails, we DON'T collect data (security)
        ///
        /// CALLED WHEN:
        /// - Game process is detected (via OnGameLaunched event)
        /// - Can also be called manually to resume collection
        ///
        /// THROWS:
        /// - InvalidOperationException if not initialized
        /// - Any exception from services if startup fails
        /// </summary>
        public async Task StartAsync()
        {
            // PRECONDITION CHECK 1: Must be initialized first
            if (!_isInitialized)
                throw new InvalidOperationException("MainCore must be initialized first. Call InitializeAsync().");

            // IDEMPOTENT CHECK: If already running, do nothing
            if (_isRunning)
            {
                _outputWriter?.WriteLine("MainCore: Already running, skipping Start.");
                return;
            }

            try
            {
                _outputWriter?.WriteLine("MainCore: Starting data collection...");

                // STEP 1: Start journal monitoring
                _outputWriter?.WriteLine("  - Starting journal monitor...");
                await _journalMonitor.StartAsync();

                // STEP 2: Refresh authentication
                // Tokens might have expired while game was closed
                _outputWriter?.WriteLine("  - Refreshing CAPI authentication...");
                try
                {
                    await _capiAuth.RefreshTokenAsync();
                }
                catch (Exception ex)
                {
                    _outputWriter?.WriteLine($"  - Auth refresh failed: {ex.Message}. Checking for stored credentials...");
                    if (!_capiAuth.HasStoredCredentials())
                    {
                        _outputWriter?.WriteLine("  - No stored credentials. User must authenticate first.");
                        await _journalMonitor.StopAsync();
                        return;
                    }
                }

                // STEP 3: Validate squadron membership
                // This is a security gate - unauthorized users don't collect data
                _outputWriter?.WriteLine("  - Validating squadron membership...");
                var isValidMember = await _squadronValidator.ValidateAsync();

                if (!isValidMember)
                {
                    _outputWriter?.WriteLine("  - Squadron validation FAILED. Stopping data collection.");
                    await _journalMonitor.StopAsync();
                    return; // Don't mark as running
                }

                // STEP 4: Mark as running
                _isRunning = true;

                _outputWriter?.WriteLine($"MainCore: Data collection started (Squadron: {_squadronValidator.GetValidatedSquadron()})");
            }
            catch (Exception ex)
            {
                // Unexpected error during start
                _outputWriter?.WriteLine($"MainCore: Start FAILED: {ex.Message}");

                // Attempt cleanup
                try { await _journalMonitor.StopAsync(); } catch { }

                throw;
            }
        }

        /// <summary>
        /// Stops active data collection.
        ///
        /// WHAT IT DOES:
        /// 1. If not running, do nothing (idempotent)
        /// 2. Stop journal monitoring
        /// 3. Mark as not running
        ///
        /// WHY THIS PATTERN?
        /// - Idempotent (safe to call multiple times)
        /// - Only stops journal monitor (game process monitor keeps running)
        /// - Errors are logged but don't propagate (graceful degradation)
        /// - Quick operation (no validation, just cleanup)
        ///
        /// CALLED WHEN:
        /// - Game process exits (via OnGameExited event)
        /// - Can also be called manually to pause collection
        /// - Eventually called during shutdown
        ///
        /// NEVER THROWS:
        /// - Catches and logs all exceptions
        /// - Returns gracefully even if stop fails
        /// </summary>
        public async Task StopAsync()
        {
            // IDEMPOTENT CHECK: If not running, nothing to do
            if (!_isRunning)
                return;

            try
            {
                _outputWriter?.WriteLine("MainCore: Stopping data collection...");

                await _journalMonitor.StopAsync();

                _isRunning = false;

                _outputWriter?.WriteLine("MainCore: Data collection stopped. Waiting for next game launch...");
            }
            catch (Exception ex)
            {
                // Log error but don't throw (we're stopping anyway)
                _outputWriter?.WriteLine($"MainCore: Error during stop: {ex.Message}");
            }
        }

        /// <summary>
        /// Complete shutdown of the orchestrator.
        ///
        /// WHAT IT DOES:
        /// 1. Stop data collection (if running)
        /// 2. Unsubscribe from all events
        /// 3. Stop game process monitor
        /// 4. Mark as not initialized
        ///
        /// WHY UNSUBSCRIBE?
        /// - Prevents event handlers from firing after shutdown
        /// - Allows garbage collector to clean up MainCore
        /// - Prevents "double-firing" if reinitialized
        ///
        /// CALLED WHEN:
        /// - Application is closing
        /// - User explicitly exits via tray menu
        /// - Manual restart is needed
        ///
        /// NEVER THROWS:
        /// - Attempts to clean up all services even if some fail
        /// - Logs errors but continues cleanup
        /// </summary>
        public async Task ShutdownAsync()
        {
            try
            {
                _outputWriter?.WriteLine("MainCore: Shutting down...");

                // STEP 1: Stop data collection if running
                if (_isRunning)
                    await StopAsync();

                // STEP 2: Unsubscribe from events
                _outputWriter?.WriteLine("  - Unsubscribing from service events...");
                _gameMonitor.GameLaunched -= OnGameLaunched;
                _gameMonitor.GameExited -= OnGameExited;
                _journalMonitor.JournalLineRead -= OnJournalLineRead;

                // STEP 3: Stop game process monitor
                _outputWriter?.WriteLine("  - Stopping game process monitor...");
                await _gameMonitor.StopAsync();

                // STEP 4: Mark as not initialized
                _isInitialized = false;

                _outputWriter?.WriteLine("MainCore: Shutdown complete.");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"MainCore: Error during shutdown: {ex.Message}");
                // Don't re-throw; try to shut down cleanly anyway
            }
        }

        // ====================================================================
        // PRIVATE EVENT HANDLERS - React to service events
        // ====================================================================

        /// <summary>
        /// Called when GameProcessMonitor detects game launch.
        ///
        /// WHAT IT DOES:
        /// Just calls StartAsync(), which handles the rest.
        ///
        /// WHY ASYNC VOID?
        /// This is an event handler, which requires void return type.
        /// We fire-and-forget because we want the event to complete immediately.
        /// That said, this is generally discouraged in C#; if we had control,
        /// we'd use async event handlers (async Task).
        ///
        /// IMPORTANT: This is called from GameProcessMonitor's thread,
        /// so any errors here might not be visible unless we log them.
        /// That's why we wrap in try-catch.
        /// </summary>
        private async void OnGameLaunched(object? sender, EventArgs e)
        {
            try
            {
                _outputWriter?.WriteLine(">>> GAME LAUNCHED <<<");
                await StartAsync();
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"MainCore: Error in OnGameLaunched: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when GameProcessMonitor detects game exit.
        ///
        /// WHAT IT DOES:
        /// Just calls StopAsync(), which handles the rest.
        ///
        /// WHY ASYNC VOID?
        /// Same reasoning as OnGameLaunched.
        /// </summary>
        private async void OnGameExited(object? sender, EventArgs e)
        {
            try
            {
                _outputWriter?.WriteLine(">>> GAME EXITED <<<");
                await StopAsync();
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"MainCore: Error in OnGameExited: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when JournalMonitor reads a new journal line.
        ///
        /// WHAT IT DOES (TODAY):
        /// Logs the event type. This is a placeholder.
        ///
        /// WHAT IT WILL DO (FUTURE):
        /// - Route the event to interested modules (Colonization, Exploration, etc.)
        /// - Call module.OnJournalLineAsync() for each module that cares
        /// - Aggregate results and upload to database
        ///
        /// WHY ASYNC VOID?
        /// Same as above. This is a temporary limitation of event handlers.
        ///
        /// CALLED FREQUENTLY:
        /// Every time a new line is written to the journal (often during gameplay).
        /// Must be fast and lightweight.
        /// </summary>
        private async void OnJournalLineRead(object? sender, JournalLineEventArgs e)
        {
            // For now, just log. Later, route to modules.
            // We don't log every line (too spammy), just important ones.
            if (e.EventType == "FSDJump" || e.EventType.Contains("Structure"))
            {
                _outputWriter?.WriteLine($"MainCore: Journal event: {e.EventType}");
            }

            // TODO: In future milestones:
            // - Load all modules
            // - Call module.OnJournalLineAsync(e.RawLine, e.ParsedEvent)
            // - Await all tasks
        }

        // ====================================================================
        // IDisposable PATTERN - Allows using statement
        // ====================================================================

        /// <summary>
        /// Implements IDisposable for use in using statements.
        ///
        /// WHAT IT DOES:
        /// Calls ShutdownAsync().Wait() to synchronously clean up.
        ///
        /// WHY WAIT()?
        /// We need synchronous cleanup for IDisposable.
        /// Calling .Wait() blocks until the async operation completes.
        ///
        /// USAGE:
        /// using (var core = new MainCore(...))
        /// {
        ///     await core.InitializeAsync();
        ///     // ...
        /// } // Dispose() called automatically here
        ///
        /// IMPORTANT:
        /// This is a compromise. Ideally, C# would support IAsyncDisposable everywhere.
        /// But for compatibility, IDisposable is still common.
        /// </summary>
        public void Dispose()
        {
            try
            {
                ShutdownAsync().Wait();
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"MainCore: Error during Dispose: {ex.Message}");
            }
        }
    }
}
