# MainCore Implementation - Design Decisions Summary

## What We Built

We created the **orchestrator** - the conductor of the entire application. This is a state machine that manages all services and their interactions.

### Files Created:

1. **MainCore.cs** - The orchestrator class (main logic)
2. **IGameProcessMonitor.cs** - Interface for detecting game launch/exit
3. **IJournalMonitor.cs** - Interface for reading game journal
4. **ICapiAuth.cs** - Interface for authentication
5. **ISquadronValidator.cs** - Interface for squadron membership check
6. **IOutputWriter.cs** - Interface for logging/output
7. **ConsoleOutputWriter.cs** - Simple console implementation of IOutputWriter

---

## Key Design Decisions Explained

### 1. **Why Interfaces? (Dependency Injection Pattern)**

**Decision**: All services are passed as interfaces to MainCore's constructor.

**Code Example**:
```csharp
public class MainCore
{
    private readonly IGameProcessMonitor _gameMonitor;

    public MainCore(IGameProcessMonitor gameMonitor, ...) // <- Interface, not concrete class
    {
        _gameMonitor = gameMonitor ?? throw new ArgumentNullException(nameof(gameMonitor));
    }
}
```

**Why?**
- **Testing**: Tests can pass fake/mock implementations
- **Flexibility**: Swap implementations without touching MainCore
- **SOLID Principle**: "D" = Dependency Inversion
- **Clarity**: Constructor shows exactly what MainCore needs

**Bad Alternative**:
```csharp
// DON'T do this:
private readonly GameProcessMonitor _gameMonitor = new(); // Tightly coupled
```

---

### 2. **Why Three Async Phases? (InitializeAsync, StartAsync, StopAsync)**

**Decision**: Separate initialization from running state.

**State Machine**:
```
┌─────────────────────────────────────────────┐
│ Not Initialized                             │
│ (Nothing started)                           │
└────────────────────┬────────────────────────┘
                     │
                     │ InitializeAsync()
                     ↓
┌─────────────────────────────────────────────┐
│ Initialized, Idle                           │
│ (Waiting for game launch)                   │
│ - Game monitor watching                     │
│ - Journal monitor NOT watching              │
│ - _isRunning = false                        │
└────────────────────┬────────────────────────┘
                     │
                     │ OnGameLaunched event
                     │ → StartAsync()
                     ↓
┌─────────────────────────────────────────────┐
│ Initialized, Running                       │
│ (Collecting data)                           │
│ - Game monitor watching                     │
│ - Journal monitor watching                  │
│ - _isRunning = true                         │
└────────────────────┬────────────────────────┘
                     │
                     │ OnGameExited event
                     │ → StopAsync()
                     ↓
         [Back to Idle state]
```

**Code**:
```csharp
// Prevent calling StartAsync before InitializeAsync
if (!_isInitialized)
    throw new InvalidOperationException("Must initialize first");

// Prevent calling StartAsync twice
if (_isRunning)
    return;
```

**Why this pattern?**
- **Resource-efficient**: Only watch journal when game is running
- **Safe**: Can't start before initialization completes
- **Clear**: Explicit state transitions make code easier to understand
- **Maintainable**: Easier to debug ("what state were we in?")

---

### 3. **Why Event Handlers? (Loose Coupling)**

**Decision**: Services communicate via events, not direct method calls.

**Example Flow**:
```csharp
// In InitializeAsync():
_gameMonitor.GameLaunched += OnGameLaunched; // Subscribe to event

// In GameProcessMonitor (another class):
// detect EliteDangerous64.exe...
GameLaunched?.Invoke(this, EventArgs.Empty); // Raise event

// This triggers MainCore's handler:
private async void OnGameLaunched(object? sender, EventArgs e)
{
    await StartAsync();
}
```

**Why?**
- **Decoupling**: GameProcessMonitor doesn't know about MainCore
- **Flexibility**: Multiple listeners can subscribe to same event
- **Modularity**: Easy to add new listeners later
- **Testing**: Can raise events manually in tests

**Bad Alternative**:
```csharp
// DON'T do this (tightly coupled):
mainCore.StartAsync(); // GameProcessMonitor knows about MainCore
```

---

### 4. **Why Idempotency? (Safe to Call Multiple Times)**

**Decision**: StartAsync() and StopAsync() check if already in that state.

**Code**:
```csharp
public async Task StartAsync()
{
    if (_isRunning)
        return; // Already running, do nothing
    // ... start logic ...
}

public async Task StopAsync()
{
    if (!_isRunning)
        return; // Already stopped, do nothing
    // ... stop logic ...
}
```

**Why?**
```
Time 1: Game launches → GameLaunched event → StartAsync()
Time 2: Event fires AGAIN → StartAsync() called again
        → _isRunning is true → return (safe, no double-start)
```

- **Prevents bugs**: Double-starting wouldn't cause crashes
- **Defensive**: Handles unexpected event sequences
- **Simplifies callers**: Don't need to check state before calling

---

### 5. **Why Null-Coalescence for Validation? (Fail Fast)**

**Decision**: Use `?? throw` pattern for required dependencies.

**Code**:
```csharp
_gameMonitor = gameMonitor ?? throw new ArgumentNullException(nameof(gameMonitor));
```

**Why?**
```csharp
// BAD: Wait until later to find out there's a null
private readonly IGameProcessMonitor _gameMonitor;

public MainCore(IGameProcessMonitor gameMonitor)
{
    _gameMonitor = gameMonitor; // Null check happens later!
}

// Good: Fail immediately with clear error
public MainCore(IGameProcessMonitor gameMonitor)
{
    _gameMonitor = gameMonitor ?? throw new ArgumentNullException(...);
    // Now if NULL was passed, we throw immediately with clear message
}
```

- **Early detection**: Error at construction, not 5 steps later
- **Clear message**: Shows which parameter is null
- **No surprises**: Can't accidentally use null reference

---

### 6. **Why Optional Output Writer? (Flexibility)**

**Decision**: OutputWriter is nullable (can pass null for silent mode).

**Code**:
```csharp
private readonly IOutputWriter? _outputWriter; // Nullable

public MainCore(IGameProcessMonitor gameMonitor, ..., IOutputWriter? outputWriter = null)
{
    // outputWriter can be null
}

// Null-safe usage:
_outputWriter?.WriteLine("message"); // Does nothing if null
```

**Why?**
- **Flexibility**: App can run completely silently
- **Testing**: Tests don't need to provide output writer
- **Contexts**: Console app, Windows Service, Tests all work
- **Performance**: No unnecessary output

---

### 7. **Why Event Subscription BEFORE Service Start? (Preventing Race Conditions)**

**Decision**: Subscribe to events before calling StartAsync().

**Code**:
```csharp
public async Task InitializeAsync()
{
    // RIGHT ORDER:
    _gameMonitor.GameLaunched += OnGameLaunched;  // Subscribe FIRST
    _journalMonitor.JournalLineRead += OnJournalLineRead;

    await _gameMonitor.StartAsync(); // Then start
    // If an event fires immediately, we're already subscribed
}
```

**Why?**
```
Timeline:
T=0: Subscribe to event
T=1: Start service
T=2: Service fires event immediately (fast!)
T=3: Our handler is called

vs. BAD:
T=0: Start service
T=1: Service fires event immediately
T=2: [We miss it! We weren't subscribed yet!]
T=3: Subscribe to event (too late)
```

---

### 8. **Why Unsubscribe during Shutdown? (Proper Cleanup)**

**Decision**: Remove event handlers before closing.

**Code**:
```csharp
public async Task ShutdownAsync()
{
    _gameMonitor.GameLaunched -= OnGameLaunched;  // Unsubscribe
    _journalMonitor.JournalLineRead -= OnJournalLineRead;

    await _gameMonitor.StopAsync();
}
```

**Why?**
- **Memory**: Allows garbage collector to clean up MainCore
- **Prevention**: Prevents handlers from firing after shutdown
- **Symmetry**: Subscribe in Init, unsubscribe in Shutdown
- **Reusability**: Can reinitialize MainCore without weird side effects

---

### 9. **Why Never Throw from StopAsync? (Graceful Degradation)**

**Decision**: StopAsync catches all exceptions and logs them.

**Code**:
```csharp
public async Task StopAsync()
{
    try
    {
        _outputWriter?.WriteLine("MainCore: Stopping...");
        await _journalMonitor.StopAsync();
    }
    catch (Exception ex)
    {
        _outputWriter?.WriteLine($"Error during stop: {ex.Message}");
        // Don't re-throw!
    }
}
```

**Why?**
```
Scenario: Game exits, OnGameExited fires, calls StopAsync
  If StopAsync throws:
    - Exception propagates to event handler
    - Event system might be confused
    - Worse: next time we try to stop, same error

Better: Log the error but continue
  - App stays in consistent state
  - Can try to stop again
  - User can manually exit via tray menu
```

---

### 10. **Why Console Implementation Separate? (Single Responsibility)**

**Decision**: ConsoleOutputWriter is a separate class.

**Code**:
```csharp
public interface IOutputWriter
{
    void WriteLine(string message);
}

public class ConsoleOutputWriter : IOutputWriter
{
    public void WriteLine(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}

// Later, create different implementations:
// - GuiOutputWriter (writes to text box)
// - FileOutputWriter (writes to file)
// - EventLogOutputWriter (Windows event log)
```

**Why?**
- **Reusability**: Multiple apps can use ConsoleOutputWriter
- **SOLID**: Single Responsibility
- **Testability**: Tests can provide mock output writer
- **Modularity**: Output logic is isolated

---

## How the Lifecycle Works

### Scenario: User Starts App

```
User runs application.exe
    ↓
MainCore.InitializeAsync()
    ├─ Subscribe to GameLaunched, GameExited, JournalLineRead events
    ├─ Start GameProcessMonitor (begins checking for EliteDangerous64.exe every 5 sec)
    ├─ Initialize CapiAuth (loads stored tokens from secure storage)
    ├─ Initialize SquadronValidator
    └─ Set _isInitialized = true

User is now in IDLE state (app is running, waiting for game)
    ↓
[5 seconds pass]
GameProcessMonitor detects: "EliteDangerous64.exe started!"
    ↓
GameProcessMonitor raises GameLaunched event
    ↓
MainCore.OnGameLaunched() is called
    ├─ Await MainCore.StartAsync()
    │   ├─ Start JournalMonitor
    │   ├─ Refresh CAPI auth tokens
    │   ├─ Validate squadron membership
    │   │   └─ If invalid: Stop JournalMonitor and return
    │   └─ Set _isRunning = true
    └─ Log "Data collection started"

Now in RUNNING state (actively collecting data)
    ↓
User plays the game, journal events are written
    ↓
JournalMonitor detects new lines in journal
    ↓
JournalMonitor raises JournalLineRead event for each line
    ↓
MainCore.OnJournalLineRead() is called
    └─ Log important events (FSDJump, Structure events, etc.)

[Later: User exits game]
GameProcessMonitor detects: "EliteDangerous64.exe exited"
    ↓
GameProcessMonitor raises GameExited event
    ↓
MainCore.OnGameExited() is called
    ├─ Await MainCore.StopAsync()
    │   ├─ Stop JournalMonitor
    │   └─ Set _isRunning = false
    └─ Log "Data collection stopped"

Back to IDLE state
    ↓
[If user plays again, repeat from game launch detection]

[Eventually: User closes app]
MainCore.ShutdownAsync()
    ├─ Stop any active collection (if _isRunning)
    ├─ Unsubscribe from all events
    ├─ Stop GameProcessMonitor
    ├─ Set _isInitialized = false
    └─ Log "Shutdown complete"

App is now fully cleaned up
```

---

## What's Next?

### Immediate Next Steps:

1. **Create Mock/Stub Implementations** (for testing)
   - MockGameProcessMonitor - raises events on demand
   - MockJournalMonitor - simulates journal events
   - MockCapiAuth - fake authentication
   - MockSquadronValidator - always returns true/false

2. **Build GameProcessMonitor** (simpler than journal monitor)
   - Use `Process.GetProcessesByName("EliteDangerous64")`
   - Poll every 5 seconds
   - Raise events when it starts/exits

3. **Build JournalMonitor** (more complex)
   - Find journal folder
   - Watch for new files
   - Read new lines using byte-offset tracking
   - Parse JSON
   - Raise events

4. **Build CapiAuth** (requires reading Frontier CAPI docs)
   - OAuth2 PKCE flow
   - Token storage with DPAPI
   - Token refresh logic

5. **Build SquadronValidator**
   - Call CAPI /profile endpoint
   - Parse squadron field
   - Check against "Lavigny's Legion"

### Testing the Structure:

```csharp
// Example: Test with mocks
var output = new ConsoleOutputWriter();
var gameMonitor = new MockGameProcessMonitor();
var journalMonitor = new MockJournalMonitor();
var capiAuth = new MockCapiAuth();
var validator = new MockSquadronValidator();

var core = new MainCore(gameMonitor, journalMonitor, capiAuth, validator, output);

await core.InitializeAsync();
// Now manually raise events:
gameMonitor.RaiseGameLaunched(); // Simulates game starting
// core.StartAsync() should be called automatically

await Task.Delay(1000);
// Verify _isRunning is true
// Verify journal monitor started

gameMonitor.RaiseGameExited(); // Simulates game exiting
// core.StopAsync() should be called automatically

await Task.Delay(1000);
// Verify _isRunning is false
// Verify journal monitor stopped
```

---

## Summary of Patterns Used

| Pattern | Where | Why |
|---------|-------|-----|
| **Dependency Injection** | Constructor | Testability, flexibility |
| **Interface Segregation** | IGameProcessMonitor, etc. | Loose coupling, modularity |
| **Event-Driven** | OnGameLaunched, etc. | Services don't know about each other |
| **State Machine** | _isInitialized, _isRunning | Clear state transitions, prevents errors |
| **Async/Await** | All I/O operations | Non-blocking, responsive |
| **Null Coalescing** | `??` operator | Fail fast with clear errors |
| **Null-Safe Access** | `_outputWriter?.WriteLine()` | Optional dependencies work safely |
| **Idempotency** | StartAsync, StopAsync | Safe to call multiple times |
| **Fail-Safe** | Try-catch in StopAsync | Graceful degradation |
| **IDisposable** | Dispose method | Works with using statements |

---

## C# Language Features Highlighted

```csharp
// 1. Interfaces (contracts)
public interface IGameProcessMonitor { ... }

// 2. Async/Await (non-blocking)
public async Task StartAsync() { await _service.StartAsync(); }

// 3. Events (loosely coupled communication)
public event EventHandler? GameLaunched;

// 4. Null Coalescing (default values)
_service = parameter ?? throw new ArgumentNullException(...);

// 5. Null-Safe Access (null propagation)
_outputWriter?.WriteLine(...); // Does nothing if null

// 6. Readonly (immutable after assignment)
private readonly IService _service;

// 7. EventArgs (pass data to event handlers)
public class JournalLineEventArgs : EventArgs { ... }

// 8. Try-Catch-Finally (error handling)
try { ... } catch (Exception ex) { ... } finally { ... }

// 9. Switch expressions (C# 8+)
// Not used here, but common in handlers

// 10. Nullable reference types (C# 8+)
private readonly IOutputWriter? _outputWriter; // ? means nullable
```

---

## Compile-Time Checks

The code should compile without errors because:
- All interfaces are properly defined
- All implementations follow the interfaces
- Async/await is properly used
- Nullable annotations are correct
- All dependencies are provided

If you see compilation errors, they're likely:
- Missing using statements
- Mismatched method signatures
- Wrong namespace references

---

**This is the solid foundation. All future services (GameProcessMonitor, JournalMonitor, etc.) will implement these interfaces and integrate with MainCore automatically.**
