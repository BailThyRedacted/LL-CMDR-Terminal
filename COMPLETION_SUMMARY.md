# MainCore Implementation Complete ✅

## What You Now Have

### Core Architecture Files Created:

```
EliteDataCollector.Core/
├── MainCore.cs                          # The orchestrator (conductor)
├── ConsoleOutputWriter.cs               # Console logging implementation
└── Services/
    ├── IGameProcessMonitor.cs           # Detects game launch/exit
    ├── IJournalMonitor.cs               # Reads game journal
    ├── ICapiAuth.cs                     # Frontier authentication
    ├── ISquadronValidator.cs            # Squadron membership check
    └── IOutputWriter.cs                 # Logging abstraction
```

### Documentation Files Created:

- `ORCHESTRATOR_GUIDE.md` - Step-by-step tutorial (read this first to understand concepts)
- `DESIGN_DECISIONS.md` - Deep dive into why each design decision was made

---

## The Orchestrator Explained in 90 Seconds

Think of MainCore like an **air traffic controller** at an airport:

```
Runway (GameProcessMonitor)
  └─ Detects: "Flight landed!" → raises GameLaunched event
     ↓
Tower (MainCore)
  ├─ Hears the notification
  ├─ Checks: "Are we allowed to land?" (validates pilots/squadron)
  ├─ Signals: "Start collecting baggage" (starts JournalMonitor)
  └─ Starts coordinating ground operations
     ↓
Baggage Handlers (JournalMonitor)
  └─ Begin offloading cargo (reading journal events)

Later: Flight departs
  └─ Runway detects: "Flight took off!" → raises GameExited event
     ↓
Tower (MainCore)
  ├─ Hears the notification
  ├─ Signals: "Stop collecting baggage" (stops JournalMonitor)
  └─ Waits for next arrival
```

---

## How MainCore Works (State Machine)

### State 1: Not Initialized
```csharp
var core = new MainCore(gameMonitor, journalMonitor, capiAuth, validator);
// Still waiting for initialization
```

### State 2: Initialize
```csharp
await core.InitializeAsync();
// ✓ Subscribes to events
// ✓ Starts game process monitor
// ✓ Initializes services
// Now waiting for game launch (IDLE state)
```

### State 3: Game Launches
```
GameProcessMonitor detects EliteDangerous64.exe
  → Raises GameLaunched event
  → MainCore.OnGameLaunched() called
  → Calls StartAsync()
    ├─ Starts journal monitoring
    ├─ Refreshes authentication
    ├─ Validates squadron
    └─ Set _isRunning = true (RUNNING state)
```

### State 4: Collecting Data
```
JournalMonitor detects new journal lines
  → Raises JournalLineRead event for each line
  → MainCore.OnJournalLineRead() called
  → Logs/routes to modules
  [This repeats constantly while game is running]
```

### State 5: Game Exits
```
GameProcessMonitor detects EliteDangerous64.exe exited
  → Raises GameExited event
  → MainCore.OnGameExited() called
  → Calls StopAsync()
    ├─ Stops journal monitoring
    └─ Set _isRunning = false (back to IDLE)
```

### State 6: Shutdown
```csharp
await core.ShutdownAsync();
// ✓ Stops any running collection
// ✓ Unsubscribes from all events
// ✓ Stops game process monitor
// Application can now exit
```

---

## Key Decisions & Their Benefits

| Decision | Benefit | Example |
|----------|---------|---------|
| **Interfaces for all services** | Can swap implementations anytime | Tests use MockGameProcessMonitor |
| **Event-driven communication** | Services don't depend on each other | GameProcessMonitor doesn't know about MainCore |
| **Separate Init/Start/Stop** | Efficient and safe | Don't watch journal until game actually runs |
| **State tracking (_isInitialized, _isRunning)** | Prevents invalid operations | Can't call StartAsync() before InitializeAsync() |
| **Null-safe output (?.)** | App can run silently if needed | Teams can use with/without logging |
| **Idempotent Start/Stop** | Safe to call multiple times | Won't break if event fires twice |
| **Async/await everywhere** | App never freezes | UI stays responsive during I/O |

---

## C# Concepts You've Learned

### 1. Interfaces (Contracts)
```csharp
public interface IGameProcessMonitor
{
    Task StartAsync();
    event EventHandler? GameLaunched;
}
```
**Real implementation** can be:
- Real: `GameProcessMonitor`
- Fake (for testing): `MockGameProcessMonitor`
- Different: `WmiGameProcessMonitor`

### 2. Dependency Injection
```csharp
public MainCore(IGameProcessMonitor gameMonitor, ...)
{
    _gameMonitor = gameMonitor; // Passed in, not created
}
```
**Benefit**: Can test with fakes, change implementations easily

### 3. Events
```csharp
public event EventHandler? GameLaunched;
GameLaunched?.Invoke(this, EventArgs.Empty);

// Someone listens:
_gameMonitor.GameLaunched += OnGameLaunched;
```
**Benefit**: Loose coupling, multiple listeners possible

### 4. Async/Await
```csharp
public async Task InitializeAsync()
{
    await _gameMonitor.StartAsync();
    // Continue only after StartAsync completes
}
```
**Benefit**: Non-blocking I/O, responsive app

### 5. State Machine
```csharp
if (!_isInitialized) throw new InvalidOperationException(...);
if (_isRunning) return; // Idempotent
_isRunning = true; // Mark state change
```
**Benefit**: Clear state transitions, prevents bugs

### 6. Null-Coalescing & Null-Safe Access
```csharp
_gameMonitor = gameMonitor ?? throw new ArgumentNullException(...);
_outputWriter?.WriteLine(...); // Safe even if null
```
**Benefit**: Fail fast, handle nulls gracefully

---

## What's Missing (To Be Built Next)

These interfaces are **defined** but need **implementations**:

1. **IGameProcessMonitor** → GameProcessMonitor
   - Monitor `EliteDangerous64.exe` process
   - Raise events when it starts/exits

2. **IJournalMonitor** → JournalMonitor
   - Watch journal folder for new files
   - Read new lines efficiently
   - Parse JSON
   - Raise events with event data

3. **ICapiAuth** → CapiAuth
   - OAuth2 PKCE authentication flow
   - Token storage with encryption (DPAPI)
   - Token refresh logic

4. **ISquadronValidator** → SquadronValidator
   - Check authenticated user's squadron
   - Verify it matches "Lavigny's Legion"
   - Handle periodic rechecks

5. **Module Loader**
   - Discover modules in `Modules` folder
   - Load them dynamically
   - Pass events to them

---

## Testing the Structure

You can test MainCore's logic with mocks right now:

```csharp
// Create mocks (fake implementations)
var gameMonitor = new MockGameProcessMonitor();
var journalMonitor = new MockJournalMonitor();
var capiAuth = new MockCapiAuth();
var validator = new MockSquadronValidator();
var output = new ConsoleOutputWriter();

// Create MainCore with mocks
var core = new MainCore(gameMonitor, journalMonitor, capiAuth, validator, output);

// Initialize
await core.InitializeAsync();

// Simulate game launching
gameMonitor.RaiseGameLaunched();
await Task.Delay(100);

// Check state
Assert.IsTrue(core._isRunning); // (would need to make _isRunning public)

// Simulate game exiting
gameMonitor.RaiseGameExited();
await Task.Delay(100);

// Check state
Assert.IsFalse(core._isRunning);

// Cleanup
await core.ShutdownAsync();
```

---

## Next Steps (Priority Order)

### Step 1: Create Mock Implementations (Today)
**Why**: Test MainCore logic without building real services
**Files**:
- `MockGameProcessMonitor.cs`
- `MockJournalMonitor.cs`
- `MockCapiAuth.cs`
- `MockSquadronValidator.cs`

**Difficulty**: ⭐ Easy

### Step 2: Build GameProcessMonitor (Tomorrow)
**Why**: Simplest real service, teaches process monitoring
**Key Code**:
```csharp
Process[] procs = Process.GetProcessesByName("EliteDangerous64");
// Raise GameLaunched if found
// Raise GameExited if not found
```

**Difficulty**: ⭐⭐ Medium

### Step 3: Build JournalMonitor (Later this week)
**Why**: Core of the app, more complex
**Key Code**:
- `FileSystemWatcher` for file changes
- Track byte offsets
- Parse JSON
- Raise events

**Difficulty**: ⭐⭐⭐ Hard

### Step 4: Build CapiAuth (Next week)
**Why**: Integration with Frontier API
**Key Code**:
- OAuth2 PKCE flow
- DPAPI token encryption

**Difficulty**: ⭐⭐⭐⭐ Harder

### Step 5: Build SquadronValidator (Next week)
**Why**: Final access control layer
**Key Code**:
- Call `/profile` endpoint
- Parse response
- Check squadron name

**Difficulty**: ⭐⭐ Medium

---

## File Summary

### MainCore.cs (390 lines)
The orchestrator. Manages:
- ✓ Initialization of all services
- ✓ Event subscriptions
- ✓ State transitions (idle → running → idle)
- ✓ Error handling
- ✓ Cleanup on shutdown

**Key Methods**:
- `InitializeAsync()` - Set up everything
- `StartAsync()` - Begin data collection
- `StopAsync()` - Stop data collection
- `ShutdownAsync()` - Clean up
- Event handlers: `OnGameLaunched`, `OnGameExited`, `OnJournalLineRead`

### Service Interfaces (5 files)
Contracts that services must implement:
- `IGameProcessMonitor` - Detects game launch/exit
- `IJournalMonitor` - Reads game journal
- `ICapiAuth` - Handles authentication
- `ISquadronValidator` - Validates squadron membership
- `IOutputWriter` - Logging abstraction

### ConsoleOutputWriter.cs
Simple implementation of `IOutputWriter` for console logging.

---

## Design Patterns Used

| Pattern | Usage |
|---------|-------|
| **Orchestrator** | MainCore coordinates all services |
| **Dependency Injection** | Services passed to constructor |
| **Event-Driven** | Services communicate via events |
| **State Machine** | _isInitialized, _isRunning track state |
| **Async/Await** | Non-blocking I/O |
| **Null-Safe** | Handle nulls gracefully |
| **Fail-Fast** | Throw errors immediately |
| **IDisposable** | Clean resource cleanup |

---

## Compilation Status

✅ **BUILD SUCCEEDED**
- 0 Errors
- 14 Warnings (pre-existing, not from new code)

The code is ready to be extended with real implementations!

---

## Reading Order (Understanding the Code)

1. **ORCHESTRATOR_GUIDE.md** - High-level concepts (10 min read)
2. **MainCore.cs** - Read the comments, understand flow (15 min)
3. **Interface files** - Understand what each service does (10 min)
4. **DESIGN_DECISIONS.md** - Deep dive into "why" (25 min)

---

## Summary

You now have the **foundation** of the application:

✅ MainCore orchestrator that manages the lifecycle
✅ Service interfaces that define what each component does
✅ State machine that prevents invalid operations
✅ Event-driven architecture for loose coupling
✅ Async/await for responsive operations
✅ Comprehensive documentation explaining every decision

**The hardest part is done!** 🎯

Next steps are implementing the services to plug into this orchestrator.

---

**Questions to Ask Yourself Before Building Service Implementations:**

1. "What does this service need to do?"
   → Implement interface methods

2. "When should it start/stop?"
   → Based on Initialize/Start/Stop calls from MainCore

3. "What events should it raise?"
   → Check the event definition in interface

4. "Should this service throw or catch exceptions?"
   → Throw for errors, MainCore handles them

5. "Does it need to be super fast?"
   → Use async, avoid blocking calls

You're ready for the next phase! 🚀
