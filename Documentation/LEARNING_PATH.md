# 📚 Complete Learning Path - MainCore Implementation

## What We've Created

### Code Files (Ready to Use)
✅ **MainCore.cs** (390 lines)
- The orchestrator that coordinates everything
- Fully commented, explains every decision
- Implements state machine: Initialize → Start → Stop → Shutdown

✅ **Service Interfaces** (5 files)
- IGameProcessMonitor.cs - Detects game launch/exit
- IJournalMonitor.cs - Reads game journal
- ICapiAuth.cs - Authentication management
- ISquadronValidator.cs - Squadron membership check
- IOutputWriter.cs - Logging abstraction

✅ **Implementation Example**
- ConsoleOutputWriter.cs - Shows how to implement an interface

### Documentation Files (Reference)
📖 **ORCHESTRATOR_GUIDE.md**
- High-level overview of orchestrator concept
- Explains each key design decision
- Great for understanding "why" before reading code

📖 **DESIGN_DECISIONS.md**
- 10 major design decisions explained in depth
- Why each decision was made
- How to recognize these patterns elsewhere

📖 **ARCHITECTURE_DIAGRAMS.md**
- Visual diagrams in ASCII format
- State machine, call sequences, event flows
- Threading model explanation

📖 **COMPLETION_SUMMARY.md**
- Quick reference for what was built
- Next steps prioritized
- Testing strategy

📖 **This file: LEARNING_PATH.md**
- Your reading and learning roadmap
- Where to go next

---

## Your Learning Journey

### Phase 1: Understand the Big Picture (30 minutes)

**Read in this order:**

1. **ORCHESTRATOR_GUIDE.md** (top section)
   - Learn what an orchestrator is
   - See the state transitions
   - Understand why we need each service

2. **ARCHITECTURE_DIAGRAMS.md** (first 3 diagrams)
   - Dependency graph - how services connect
   - State transitions - idle → running → idle
   - Event flow - what happens when

3. **Skip the code for now!**

**After this phase:** You understand WHAT the system does and HOW it transitions states.

---

### Phase 2: Learn the Design Patterns (45 minutes)

**Read these sections from DESIGN_DECISIONS.md:**

1. Decision 1: Dependency Injection
   - Why interfaces? Why not `new GameProcessMonitor()`?
   - How does this help testing?

2. Decision 2: Event-Driven
   - How do services communicate?
   - Why not direct method calls?

3. Decision 3: State Management
   - What are _isInitialized and _isRunning?
   - Why are they important?

4. Decision 4: Async/Await
   - What does `async Task` mean?
   - Why non-blocking operations?

5. Decision 5: Null Safety
   - What's `?? throw`?
   - What's `?.`?

**After this phase:** You understand the PATTERNS and PRINCIPLES behind the code.

---

### Phase 3: Read the Source Code (60 minutes)

**Read MainCore.cs:**

1. **Read the class comments** (top section)
   - Understand philosophy
   - See LIFECYCLE diagram

2. **Read Constructor**
   - See how services are injected
   - Understand null checks

3. **Read InitializeAsync()**
   - Understand initialization order
   - See event subscription happens FIRST

4. **Read StartAsync()**
   - See idempotency check
   - Understand validation flow
   - Notice squadron check happens here

5. **Read StopAsync()**
   - Simple and graceful
   - Idempotent by design

6. **Read ShutdownAsync()**
   - Event unsubscription
   - Final cleanup

7. **Read Event Handlers** (OnGameLaunched, etc.)
   - These are called BY EVENTS
   - They call the async methods

**Pro Tip:** Read one method at a time. Run the code through your head:
```
"What state are we in?"
"What can happen next?"
"What if something fails?"
```

**After this phase:** You understand HOW the code works.

---

### Phase 4: Deep Dive Questions (60 minutes)

**Test your understanding:**

1. **State Questions:**
   - Can you call `StartAsync()` before `InitializeAsync()`? What happens?
   - Can you call `StartAsync()` twice? What happens?
   - What if the game launches while we're initializing?

2. **Event Questions:**
   - When does `OnGameLaunched()` get called?
   - What thread is it called on?
   - What would happen if we didn't subscribe to events before starting GameProcessMonitor?

3. **Error Handling Questions:**
   - What happens if `CapiAuth.RefreshTokenAsync()` throws an exception?
   - What happens if `SquadronValidator.ValidateAsync()` returns false?
   - What if `JournalMonitor.StopAsync()` throws during shutdown?

4. **Design Questions:**
   - Why is `IOutputWriter` optional?
   - Why do we unsubscribe from events during shutdown?
   - Why is `StartAsync()` idempotent?

**Answers in DESIGN_DECISIONS.md!**

---

### Phase 5: Try Examples (30 minutes)

**Write pseudocode for these scenarios:**

1. **Scenario: Game launches**
   ```
   What happens?
   - GameProcessMonitor detects EliteDangerous64.exe
   - [What next?]
   - [What next?]
   - Journal monitor should be running
   - Start should have validated squadron

   Write it out step by step
   ```

2. **Scenario: User calls StartAsync() twice**
   ```
   First call: _isRunning is false
   - [What happens?]

   Second call: _isRunning is true
   - [What happens?]
   - [Is this good or bad?]
   ```

3. **Scenario: Squadron validation fails**
   ```
   In StartAsync():
   - Journal monitor started
   - Auth refreshed
   - [Validate squadron]
   - [Validation returns false]
   - [What should we do?]
   - [Is the app still in a good state?]
   ```

---

### Phase 6: Interface Implementation (Done!)

You already have examples:
- **IOutputWriter** → implemented by **ConsoleOutputWriter**

Study ConsoleOutputWriter to see:
- Clean implementation
- Simple, focused code
- How to satisfy interface contract

---

## C# Concepts To Master

### 1. Interfaces
```csharp
public interface IGameProcessMonitor
{
    Task StartAsync();
    event EventHandler? GameLaunched;
}

// Multiple implementations possible
public class GameProcessMonitor : IGameProcessMonitor { ... }
public class MockGameProcessMonitor : IGameProcessMonitor { ... }
```

### 2. Async/Await
```csharp
public async Task InitializeAsync()
{
    await _service.StartAsync(); // Wait for this to finish
    // Continue only after it's done
}
```

### 3. Events
```csharp
public event EventHandler? GameLaunched;

// Raise it
GameLaunched?.Invoke(this, EventArgs.Empty);

// Subscribe to it
_service.GameLaunched += MyHandler;

// Handler is called when event fires
private void MyHandler(object? sender, EventArgs e) { ... }
```

### 4. Null Operators
```csharp
// Null coalescing - throw if null
_service = param ?? throw new ArgumentNullException(...);

// Null conditional - safe if null
_outputWriter?.WriteLine(...); // Does nothing if null
```

### 5. Try-Catch-Finally
```csharp
try
{
    // Something that might fail
}
catch (Exception ex)
{
    // Handle error
}
finally
{
    // Always runs, even if exception
}
```

---

## Questions to Answer (Self-Test)

### Easy (Should know after Phase 1)
- [ ] What does MainCore do?
- [ ] What are the 3 states of MainCore?
- [ ] What triggers the state changes?

### Medium (Should know after Phase 3)
- [ ] Why is `InitializeAsync()` called first?
- [ ] Why do we subscribe to events BEFORE starting services?
- [ ] What happens if squad validation fails in `StartAsync()`?

### Hard (Should know after Phase 5)
- [ ] Design a new service and show how it would integrate
- [ ] Explain why events are better than direct method calls
- [ ] Draw the call sequence when game launches

### Expert (Bonus)
- [ ] How would you make MainCore thread-safe?
- [ ] How would you add timeout handling?
- [ ] How would you add module loading to the existing pattern?

---

## Next Implementation

### You're Ready For:

1. **Writing Tests**
   - Create MockGameProcessMonitor
   - Mock raising events
   - Verify MainCore responds correctly

2. **Implementing GameProcessMonitor**
   - Use `Process.GetProcessesByName()`
   - Periodically check and raise events
   - See how to implement real service

3. **Reading Real Frontier Code**
   - Journal event documentation
   - CAPI OAuth2 flow
   - Supabase REST API

### Build Order:

```
Week 1: Mocks (learn testing)
  ↓
Week 2: GameProcessMonitor (simplest real service)
  ↓
Week 3: JournalMonitor (more complex)
  ↓
Week 4: CapiAuth (requires documentation)
  ↓
Week 5: SquadronValidator (API integration)
  ↓
Week 6+: ModuleLoader, system tray, etc.
```

---

## How to Read The Code

### Strategy 1: Top to Bottom
Start at class declaration, read down:
- Comments
- Fields
- Constructor
- Methods in order

### Strategy 2: By Feature
Find what you want to understand:
- "What happens during Initialize?" → Read InitializeAsync()
- "What are the states?" → Read _isInitialized, _isRunning comments
- "How do services start?" → Read StartAsync()

### Strategy 3: By Event
Trace an event through the system:
- Start with a service raising an event
- Find the event handler in MainCore
- Follow the method it calls
- See the result

**Pro Tip:** Use VS Code's "Go to Definition" (Ctrl+Click or F12) to jump between files.

---

## Common Beginner Mistakes (Avoid These!)

❌ **"I'll create my own MainCore instead of understanding this one"**
- This one is battle-tested DESIGN
- Learn the pattern, then extend it

❌ **"I don't need tests"**
- Tests PROVE your code works
- Tests catch bugs you miss

❌ **"I'll make everything static"**
- Static makes testing impossible
- Dependency injection is better

❌ **"I'll throw exceptions everywhere"**
- Catch what you can handle
- Only throw errors that should stop execution

❌ **"I'll write all code first, then add comments"**
- Write comments AS you go
- Good comments save debugging time

❌ **"Events are complex, I'll use direct calls instead"**
- Events teach you loose coupling
- Learn them, use them

---

## Success Metrics

### After Phase 1:
- [ ] Can you draw a state machine of Initialize → Start → Stop → Shutdown?
- [ ] Can you explain why events are better than direct calls?

### After Phase 3:
- [ ] Can you explain what each method does?
- [ ] Can you trace the flow when game launches?
- [ ] Can you predict what happens on error?

### After Phase 5:
- [ ] Can you write a new service and integrate it?
- [ ] Can you design tests for MainCore?
- [ ] Can you explain every design decision?

---

## Helpful Queries to Explore

**In VS Code:**

1. "Find all event subscriptions"
   - Search: `+=`
   - Shows: Where events are subscribed

2. "Find all event unsubscriptions"
   - Search: `-=`
   - Shows: Where events are cleaned up

3. "Find all state checks"
   - Search: `_is`
   - Shows: All state transitions

4. "Find all async methods"
   - Search: `async Task`
   - Shows: All async operations

5. "Find all null checks"
   - Search: `??`
   - Shows: All fail-fast validations

---

## Cheat Sheet

```csharp
// PATTERN: Fail-fast with clear error
parameter ?? throw new ArgumentNullException(nameof(parameter))

// PATTERN: State validation
if (!_isInitialized) throw new InvalidOperationException(...)

// PATTERN: Idempotency
if (_isRunning) return;

// PATTERN: Null-safe logging
_outputWriter?.WriteLine(...)

// PATTERN: Subscribe to events
_service.EventName += OnEventName;

// PATTERN: Async method
public async Task DoSomethingAsync()
{
    await _service.StartAsync();
}

// PATTERN: Event handler
private async void OnServiceEvent(object? sender, EventArgs e)
{
    await SomeMethodAsync();
}

// PATTERN: Safe cleanup
try { await _service.StopAsync(); }
catch (Exception ex) { _outputWriter?.WriteLine(...); }
```

---

## Your Next Checkpoint

✅ Phase 1-3 Complete: Understanding (2-3 hours)
⏭️ Phase 4-5 Next: Mastery (2-3 hours)
🎯 Then: Build GameProcessMonitor (3-4 hours)

---

## Resources Inside This Folder

- **ORCHESTRATOR_GUIDE.md** - Tutorials
- **DESIGN_DECISIONS.md** - In-depth explanations
- **ARCHITECTURE_DIAGRAMS.md** - Visual reference
- **COMPLETION_SUMMARY.md** - Quick facts
- **PRD.md** - Product requirements

---

## Remember

You're learning a **solid architectural pattern** that professional developers use daily:
- ✅ Dependency Injection
- ✅ Event-Driven Architecture
- ✅ State Machines
- ✅ Async/Await
- ✅ SOLID Principles

This knowledge transfers to ANY C# application, not just this one.

**Start with Phase 1. Come back to this document as you progress.** 🚀
