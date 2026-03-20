# Architecture Diagrams & Visual Reference

## Dependency Graph

```
                         ┌──────────────────┐
                         │    MainCore      │
                         │  (Orchestrator)  │
                         └────────┬─────────┘
                                  │
                 ┌────────────────┼────────────────┐
                 │                │                │
                 ↓                ↓                ↓
        ┌──────────────────┐ ┌──────────────┐ ┌──────────────────┐
        │ GameProcess      │ │ Journal      │ │ CAPI             │
        │ Monitor          │ │ Monitor      │ │ Auth             │
        │ (watches for     │ │ (reads       │ │ (OAuth2 tokens)  │
        │  game launch)    │ │  journal)    │ │                  │
        └────────┬─────────┘ └──────┬───────┘ └────────┬─────────┘
                 │                  │                   │
                 │  Events          │  Events          │
                 │                  │                   │
                 ↓                  ↓                   ↓
        ┌─ GameLaunched          JournalLineRead    Tokens
        ├─ GameExited            (EventType, raw)   (valid/invalid)
        │                        │
        └─→ MainCore listens     └─→ MainCore routes to modules
            and responds              (future)

                 ↙                    ↖
         ┌──────────────────────────────────────┐
         │    Squadron Validator                │
         │  (checks if authorized)              │
         └──────────────────────────────────────┘
                      ↓
         ┌──────────────────────────────────────┐
         │     Output Writer                    │
         │   (Console, File, GUI, etc.)         │
         └──────────────────────────────────────┘
```

## State Transition Diagram

```
                      ┌─────────────────────────┐
                      │ NOT_INITIALIZED         │
                      │                         │
                      │ Only action: Init       │
                      └────────────┬────────────┘
                                   │
                    InitializeAsync() called
                                   │
                                   ↓
                      ┌─────────────────────────┐
                      │ INITIALIZED_IDLE        │
                      │                         │
                      │ Game monitor running    │
                      │ Journal monitor stopped │
                      │ _isRunning = false      │
                      └────────────┬────────────┘
                                   │
              GameLaunched event detected
                   StartAsync() called
                                   │
                                   ↓
                      ┌─────────────────────────┐
                      │ INITIALIZED_RUNNING     │
                      │                         │
                      │ Game monitor running    │
                      │ Journal monitor active  │
                      │ _isRunning = true       │
                      │ Collecting data!        │
                      └────────────┬────────────┘
                                   │
               GameExited event detected
                   StopAsync() called
                                   │
                                   ↓
                      [Back to INITIALIZED_IDLE]

At any point: ShutdownAsync() → NOT_INITIALIZED + cleanup
```

## Event Flow During Game Session

```
TIME            EVENT                    MAINCORE STATE
────────────────────────────────────────────────────────────

T=0             App starts
                InitializeAsync()        → IDLE

T=1             [User launches game]
                GameProcessMonitor detects launch

T=2             GameLaunched event fires → OnGameLaunched()
                StartAsync() called       → RUNNING
                JournalMonitor starts

T=3             Game writes to journal
                "{ \"event\": \"FSDJump\", ... }"

T=4             JournalLineRead event fires → OnJournalLineRead()
                Event type: "FSDJump"

T=5             [More journal events...]
                JournalLineRead → OnJournalLineRead()
                JournalLineRead → OnJournalLineRead()
                (Repeats constantly)

T=500           [User exits game]
                GameProcessMonitor detects exit

T=501           GameExited event fires → OnGameExited()
                StopAsync() called       → IDLE
                JournalMonitor stops

T=502           [App waiting for next game launch...]

T=9999          User closes app
                ShutdownAsync()          → NOT_INITIALIZED
                GameProcessMonitor stops
                Cleanup complete
```

## Service Interface Hierarchy

```
public interface IGameProcessMonitor
┣━ Task StartAsync()
┣━ Task StopAsync()
┗━ event EventHandler? GameLaunched
   event EventHandler? GameExited


public interface IJournalMonitor
┣━ Task StartAsync()
┣━ Task StopAsync()
┗━ event EventHandler<JournalLineEventArgs>? JournalLineRead
   └─ Contains: RawLine, ParsedEvent, EventType


public interface ICapiAuth
┣━ Task InitializeAsync()
┣━ Task<string?> GetAccessTokenAsync()
┣━ Task RefreshTokenAsync()
┣━ Task<bool> AuthenticateAsync()
┗━ bool HasStoredCredentials()


public interface ISquadronValidator
┣━ Task InitializeAsync()
┣━ Task<bool> ValidateAsync()
┗━ string? GetValidatedSquadron()


public interface IOutputWriter
┣━ void WriteLine(string message)
┗━ void WriteLine(string format, params object[] args)
```

## MainCore Method Call Sequence

```
INITIALIZATION PHASE:
═══════════════════════════════════════

new MainCore(gameMonitor, journalMonitor, capiAuth, validator, output)
    │
    ├─ Null-check all services
    ├─ Store in private readonly fields
    └─ Set _isInitialized = false
       _isRunning = false


InitializeAsync() called:
    │
    ├─ Check: if (_isInitialized) throw
    │
    ├─ Subscribe to events:
    │  ├─ _gameMonitor.GameLaunched += OnGameLaunched
    │  ├─ _gameMonitor.GameExited += OnGameExited
    │  └─ _journalMonitor.JournalLineRead += OnJournalLineRead
    │
    ├─ Await _gameMonitor.StartAsync()
    │  └─ GameProcessMonitor now checking every 5 sec
    │
    ├─ Await _capiAuth.InitializeAsync()
    │  └─ Load stored tokens from secure storage
    │
    ├─ Await _squadronValidator.InitializeAsync()
    │  └─ Load approved squadron names
    │
    ├─ Set _isInitialized = true
    │
    └─ Log: "Initialized. Waiting for game launch."

═══════════════════════════════════════

RUNNING PHASE (When Game Launches):
═══════════════════════════════════════

GameProcessMonitor detects: EliteDangerous64.exe
    │
    └─ Raises: GameLaunched event
        │
        └─ Triggers: MainCore.OnGameLaunched()
            │
            ├─ Log: "Game launched detected"
            │
            └─ Await StartAsync():
                │
                ├─ Check: if (!_isInitialized) throw
                ├─ Check: if (_isRunning) return [idempotent]
                │
                ├─ Await _journalMonitor.StartAsync()
                │  └─ Begin watching journal folder
                │
                ├─ Await _capiAuth.RefreshTokenAsync()
                │  └─ Verify tokens are still valid
                │
                ├─ Await _squadronValidator.ValidateAsync()
                │  └─ Check: Is user in Lavigny's Legion?
                │
                ├─ If validation fails:
                │  └─ Await StopAsync() and return
                │
                ├─ Set _isRunning = true
                │
                └─ Log: "Data collection started"

═══════════════════════════════════════

RUNNING STATE (Continuously):
═══════════════════════════════════════

JournalMonitor reads new line: "{ \"event\": \"Location\" ... }"
    │
    └─ Raises: JournalLineRead event
        │
        ├─ eventArgs.EventType = "Location"
        ├─ eventArgs.RawLine = raw JSON string
        └─ eventArgs.ParsedEvent = JsonDocument (parsed)
            │
            └─ Triggers: MainCore.OnJournalLineRead()
                │
                ├─ Check: if (eventType == important) Log it
                │
                └─ [Future] Route to modules:
                   ├─ Call colonizationModule.OnJournalLineAsync()
                   ├─ Call explorationModule.OnJournalLineAsync()
                   └─ etc.

═══════════════════════════════════════

STOPPING PHASE (When Game Exits):
═══════════════════════════════════════

GameProcessMonitor detects: EliteDangerous64.exe exited
    │
    └─ Raises: GameExited event
        │
        └─ Triggers: MainCore.OnGameExited()
            │
            ├─ Log: "Game exit detected"
            │
            └─ Await StopAsync():
                │
                ├─ Check: if (!_isRunning) return [idempotent]
                │
                ├─ Await _journalMonitor.StopAsync()
                │  └─ Stop watching journal
                │
                ├─ Set _isRunning = false
                │
                └─ Log: "Data collection stopped"

Back to IDLE state (waiting for next game launch)

═══════════════════════════════════════

SHUTDOWN PHASE (App closing):
═══════════════════════════════════════

ShutdownAsync() called:
    │
    ├─ Check: if (_isRunning) await StopAsync()
    │
    ├─ Unsubscribe from events:
    │  ├─ _gameMonitor.GameLaunched -= OnGameLaunched
    │  ├─ _gameMonitor.GameExited -= OnGameExited
    │  └─ _journalMonitor.JournalLineRead -= OnJournalLineRead
    │
    ├─ Await _gameMonitor.StopAsync()
    │  └─ Stop all monitoring
    │
    ├─ Set _isInitialized = false
    │
    └─ Log: "Shutdown complete"

Application is now clean and can exit
```

## Error Handling Strategy

```
InitializeAsync():
    try
    │   ├─ Subscribe
    │   ├─ Start services
    │   └─ Mark initialized
    │
    catch Exception ex:
        ├─ Log error
        ├─ Attempt cleanup
        └─ Re-throw (caller must handle)

StartAsync():
    try
    │   ├─ Start journal monitor
    │   ├─ Refresh auth
    │   └─ Validate squadron
    │
    catch Exception ex:
        ├─ Log error
        ├─ Attempt cleanup
        └─ Re-throw (caller must handle)

StopAsync():
    try
    │   ├─ Stop journal monitor
    │   └─ Mark stopped
    │
    catch Exception ex:
        ├─ Log error
        └─ DON'T re-throw (graceful failure)

ShutdownAsync():
    try
    │   ├─ Stop if running
    │   ├─ Unsubscribe
    │   └─ Stop services
    │
    catch Exception ex:
        ├─ Log error
        └─ DON'T re-throw (always try to clean up)
```

## Integration Point: Modules

```
MainCore receives JournalLineRead event
    │
    ├─ EventType: "FSDJump"
    ├─ RawLine: full JSON
    └─ ParsedEvent: JsonDocument
        │
        └─ [Future] Load and iterate modules:
            │
            ├─ foreach module in loaded_modules:
            │  │
            │  └─ if (module wants this event):
            │     └─ await module.OnJournalLineAsync(rawLine, parsedEvent)
            │
            ├─ Colonization Module:
            │  └─ "FSDJump" → Extract system data → Upload to Supabase
            │
            ├─ Exploration Module:
            │  └─ "FSDJump" → Extract new discoveries → Upload
            │
            └─ [Other modules...]

Module interface:
    public interface IGameLoopModule
    {
        Task OnJournalLineAsync(string line, JsonDocument parsedEvent);
        Task OnCapiProfileAsync(JsonDocument profile);
    }
```

## Threading Model

```
Main Thread
    │
    ├─ MainCore (async methods)
    │
    ├─ GameProcessMonitor (background polling thread)
    │  └─ Every 5 seconds: raises events on DIFFERENT thread
    │     But event handlers (OnGameLaunched, etc.) are called
    │     on MainCore, so we need to be careful about thread safety
    │
    └─ JournalMonitor (background file watch thread)
       └─ When journal changes: raises events on DIFFERENT thread
          Event handlers called on MainCore

KEY LEARNING: Event handlers are called on the thread that raised them!
So if GameProcessMonitor raises from thread #2, OnGameLaunched
runs on thread #2, not the main thread! That's why we're careful
about async operations and don't assume synchronous behavior.
```

## Design Pattern: Orchestrator vs Mediator

```
ORCHESTRATOR (What we built):
═══════════════════════════════

Services:
├─ GameProcessMonitor
├─ JournalMonitor
├─ CapiAuth
└─ SquadronValidator

        All events flow through MainCore
        MainCore dictates who does what
        Top-down control

Benefits:
- Clear flow of control
- Easy to trace logic
- Obvious state transitions

Drawback:
- MainCore becomes a bottleneck
- Can grow complex with many services


MEDIATOR (Could use later):
═══════════════════════════════

Services communicate through a central mediator
Instead of direct dependencies, they all know about the mediator
Mediator receives messages and dispatches

Benefits:
- More decoupled

Drawdown:
- Mediator can become complex "traffic controller"
- Harder to trace

We chose ORCHESTRATOR because:
- Clear state machine needed
- Number of services is small
- Control flow must be predictable
```

---

## Quick Reference: Which Pattern to Use

| Scenario | Use This | Why |
|----------|----------|-----|
| "How does the game launch trigger data collection?" | Orchestrator pattern | Clear cause-effect |
| "How do I test MainCore?" | Mock interfaces | Swap real for fake |
| "How do I add a new service?" | Implement interface, inject to MainCore | Follows established pattern |
| "What happens if auth fails?" | Exception properly handled | Init throws, Start catches, Stop logs |
| "Can I call StartAsync() twice?" | Yes, it's safe | Idempotent design |
| "What if OutputWriter is null?" | App runs silently | Null-safe operator `?.` |

