# MainCore Orchestrator - Step-by-Step Guide

## What is an Orchestrator?

Think of an **orchestrator** like a **conductor of an orchestra**:
- Multiple musicians (services) play instruments (do specific jobs)
- The conductor tells them when to start, stop, and coordinates their timing
- Without a conductor, everyone plays alone and nothing sounds good together

In code: `MainCore` will be that conductor. It will:
1. **Initialize** all services (start the musicians)
2. **Manage their lifecycle** (tell them when to play)
3. **Listen for events** (when something happens, call the right service)
4. **Coordinate between them** (if journal monitor detects a system entry, tell the database service to upload)

---

## Architecture Decisions & Why

### Decision 1: Use Dependency Injection (DI)
**What it means:** Services are "injected" (given to) MainCore, not created inside it.

**Why?**
- Makes testing easier (we can swap real services for fake ones)
- Services can be reused elsewhere in the app
- Follows SOLID principles (the "D" = Dependency Inversion)

```csharp
// BAD: MainCore creates its own services
public class MainCore
{
    private JournalMonitor journalMonitor = new(); // Tightly coupled
}

// GOOD: Services are passed in
public class MainCore
{
    private readonly JournalMonitor _journalMonitor;

    public MainCore(JournalMonitor journalMonitor)
    {
        _journalMonitor = journalMonitor;
    }
}
```

### Decision 2: Use Interfaces for All Services
**What it means:** Services implement contracts (interfaces) that define what they can do.

**Why?**
- We can swap implementations later without changing MainCore
- Easy to mock for testing
- Clear API - anyone can see what services are available

Example:
```csharp
// Contract (interface)
public interface IGameProcessMonitor
{
    Task StartAsync();
    Task StopAsync();
    event EventHandler GameLaunched;
}

// Implementation
public class GameProcessMonitor : IGameProcessMonitor
{
    // ... actual code ...
}

// Usage
public class MainCore
{
    private readonly IGameProcessMonitor _gameMonitor;

    public MainCore(IGameProcessMonitor gameMonitor)
    {
        _gameMonitor = gameMonitor; // Can be any implementation
    }
}
```

### Decision 3: Use Events for Decoupling
**What it means:** Services don't call each other directly. Instead, they publish events that others listen for.

**Why?**
- Services are independent - they don't need to know about each other
- Easy to add new listeners without modifying existing code
- Clean separation of concerns

```
Example flow:
GameProcessMonitor detects game launched
    ↓ (raises event)
MainCore listens and hears: "Game launched!"
    ↓
MainCore tells JournalMonitor: "Start watching journals"
    ↓
MainCore tells CapiAuth: "Check for stored credentials"
```

### Decision 4: Async/Await for Non-Blocking Operations
**What it means:** Operations that take time (reading files, network calls) don't freeze the app.

**Why?**
- App stays responsive
- Can do multiple things at once
- In C#, `async` methods return a `Task` that represents work in progress

```csharp
// Sync (blocks everything)
public void ReadFile()
{
    // If file is slow, app freezes here
    var content = File.ReadAllText("big_file.txt");
}

// Async (doesn't block)
public async Task ReadFileAsync()
{
    // If file is slow, app continues; this method will complete later
    var content = await File.ReadAllTextAsync("big_file.txt");
}
```

### Decision 5: State Management
**What it means:** MainCore tracks whether the app is:
- Initializing
- Running
- Waiting for game
- Collecting data
- Shutting down

**Why?**
- Prevents errors (can't start if already started)
- Clear state transitions
- Easy to debug ("what state were we in?")

---

## Class Diagram (Text Format)

```
┌─────────────────────────────────────┐
│           MainCore                  │
│  (The Orchestrator)                 │
├─────────────────────────────────────┤
│ - _gameMonitor                      │
│ - _journalMonitor                   │
│ - _capiAuth                         │
│ - _squadronValidator                │
│ - _outputWriter                     │
│ - _isRunning                        │
├─────────────────────────────────────┤
│ + InitializeAsync()                 │
│ + StartAsync()                      │
│ + StopAsync()                       │
│ + ShutdownAsync()                   │
│ - OnGameLaunched()                  │
│ - OnGameExited()                    │
│ - OnJournalLine()                   │
└─────────────────────────────────────┘
         ↑
    Listens to events from:
    - IGameProcessMonitor
    - IJournalMonitor
    - ICapiAuth (if events exist)
```

---

## Step-by-Step Implementation

### Step 1: Create Interface Definitions
We need to define what services SHOULD do (contracts), even if they don't exist yet.

### Step 2: Create MainCore Class Structure
Define fields, constructor, and basic lifecycle methods.

### Step 3: Implement Service Lifecycle
- Initialize: Set up services in the right order
- Start: Turn on monitoring when game launches
- Stop: Turn off monitoring when game exits

### Step 4: Implement Event Handlers
Wire up listeners for game/journal events.

### Step 5: Add State Validation
Ensure operations happen in correct order.

---

## Key C# Concepts You'll See

### 1. `readonly` Keyword
```csharp
private readonly IGameProcessMonitor _gameMonitor;
```
Means: This field can only be set once (in constructor), never changed again. Prevents accidental bugs.

### 2. `async/await`
```csharp
public async Task InitializeAsync()
{
    await _gameMonitor.StartAsync(); // Wait for this to finish
    await _journalMonitor.StartAsync(); // Then this
}
```
Means: Wait for async operations to complete before moving on.

### 3. Event Handlers
```csharp
_gameMonitor.GameLaunched += OnGameLaunched; // Subscribe to event
```
Means: When GameLaunched event fires, call `OnGameLaunched` method.

### 4. Null Coalescing Operator (`??`)
```csharp
var writer = _outputWriter ?? Console.Out; // Use _outputWriter if not null, else Console
```

### 5. `throw new Exception()`
```csharp
if (_isRunning)
    throw new InvalidOperationException("Already running");
```
Means: Throw error if precondition not met (fail fast).

---

## What We're Building

```csharp
public class MainCore : IDisposable
{
    // Services
    private readonly IGameProcessMonitor _gameMonitor;
    private readonly IJournalMonitor _journalMonitor;
    private readonly ICapiAuth _capiAuth;
    private readonly ISquadronValidator _squadronValidator;
    private readonly IOutputWriter _outputWriter;

    // State
    private bool _isRunning = false;
    private bool _isInitialized = false;

    // Constructor (dependency injection)
    public MainCore(
        IGameProcessMonitor gameMonitor,
        IJournalMonitor journalMonitor,
        ICapiAuth capiAuth,
        ISquadronValidator squadronValidator,
        IOutputWriter outputWriter)
    {
        _gameMonitor = gameMonitor ?? throw new ArgumentNullException(nameof(gameMonitor));
        _journalMonitor = journalMonitor ?? throw new ArgumentNullException(nameof(journalMonitor));
        _capiAuth = capiAuth ?? throw new ArgumentNullException(nameof(capiAuth));
        _squadronValidator = squadronValidator ?? throw new ArgumentNullException(nameof(squadronValidator));
        _outputWriter = outputWriter;
    }

    // Initialize: Set up all services
    public async Task InitializeAsync()
    {
        // Check state
        if (_isInitialized)
            throw new InvalidOperationException("Already initialized");

        try
        {
            _outputWriter?.WriteLine("MainCore: Initializing...");

            // Wire up event handlers BEFORE starting services
            _gameMonitor.GameLaunched += OnGameLaunched;
            _gameMonitor.GameExited += OnGameExited;
            _journalMonitor.JournalLineRead += OnJournalLineRead;

            // Start the game process monitor (always running)
            await _gameMonitor.StartAsync();

            // Initialize other services (but don't start them yet)
            await _capiAuth.InitializeAsync();
            await _squadronValidator.InitializeAsync();

            _isInitialized = true;
            _outputWriter?.WriteLine("MainCore: Initialized successfully");
        }
        catch (Exception ex)
        {
            _outputWriter?.WriteLine($"MainCore: Initialization failed: {ex.Message}");
            throw;
        }
    }

    // Start: Begin data collection (called when game launches)
    public async Task StartAsync()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Not initialized. Call InitializeAsync first.");

        if (_isRunning)
            return; // Already running

        try
        {
            _outputWriter?.WriteLine("MainCore: Starting data collection...");

            // Start journal monitoring
            await _journalMonitor.StartAsync();

            // Refresh auth in case token expired
            await _capiAuth.RefreshTokenAsync();

            // Re-validate squadron membership
            var isValidMember = await _squadronValidator.ValidateAsync();
            if (!isValidMember)
            {
                _outputWriter?.WriteLine("MainCore: Squad validation failed. Stopping.");
                await StopAsync();
                return;
            }

            _isRunning = true;
            _outputWriter?.WriteLine("MainCore: Data collection started");
        }
        catch (Exception ex)
        {
            _outputWriter?.WriteLine($"MainCore: Start failed: {ex.Message}");
            throw;
        }
    }

    // Stop: Stop data collection (called when game exits)
    public async Task StopAsync()
    {
        if (!_isRunning)
            return; // Already stopped

        try
        {
            _outputWriter?.WriteLine("MainCore: Stopping data collection...");

            await _journalMonitor.StopAsync();

            _isRunning = false;
            _outputWriter?.WriteLine("MainCore: Data collection stopped");
        }
        catch (Exception ex)
        {
            _outputWriter?.WriteLine($"MainCore: Stop failed: {ex.Message}");
        }
    }

    // Shutdown: Clean up everything
    public async Task ShutdownAsync()
    {
        try
        {
            _outputWriter?.WriteLine("MainCore: Shutting down...");

            if (_isRunning)
                await StopAsync();

            // Unsubscribe from events
            _gameMonitor.GameLaunched -= OnGameLaunched;
            _gameMonitor.GameExited -= OnGameExited;
            _journalMonitor.JournalLineRead -= OnJournalLineRead;

            // Stop game monitor
            await _gameMonitor.StopAsync();

            _isInitialized = false;
            _outputWriter?.WriteLine("MainCore: Shutdown complete");
        }
        catch (Exception ex)
        {
            _outputWriter?.WriteLine($"MainCore: Shutdown failed: {ex.Message}");
        }
    }

    // Event Handlers
    private async void OnGameLaunched(object sender, EventArgs e)
    {
        _outputWriter?.WriteLine("MainCore: Game launched detected!");
        await StartAsync();
    }

    private async void OnGameExited(object sender, EventArgs e)
    {
        _outputWriter?.WriteLine("MainCore: Game exit detected!");
        await StopAsync();
    }

    private async void OnJournalLineRead(object sender, JournalLineEventArgs e)
    {
        _outputWriter?.WriteLine($"MainCore: New journal event: {e.EventType}");
        // In future, route to modules here
    }

    // IDisposable pattern (cleanup resources)
    public void Dispose()
    {
        ShutdownAsync().Wait(); // Sync wrapper for async shutdown
    }
}
```

---

## Next Steps After Creating MainCore

1. **Create the interface definitions** (IGameProcessMonitor, etc.)
2. **Stub out simple implementations** to test the structure
3. **Build GameProcessMonitor** (easiest first)
4. **Build JournalMonitor** (more complex)
5. **Build CAPI services**
6. **Integrate modules**
