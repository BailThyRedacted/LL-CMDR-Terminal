# Elite Data Collector

A production-ready Windows application that monitors Elite Dangerous gameplay and automatically collects valuable data about exobiology, colonization, and BGS activity.

**Built as a teaching project demonstrating professional C# architecture, async/await patterns, dependency injection, and modular design.**

## Features

### ExplorationModule
Identify high-value exobiology planets with real-time alerts:
- Scores planets 0-100 based on atmosphere, temperature, gravity, type, and landability
- Console alerts for high-value exploration targets (estimated 100K - 20M+ credits per planet)
- Persistent local storage of all scans in JSON format

### ColonizationModule
Track BGS faction influence and colonization progress:
- Monitors system control, faction influence, and PowerPlay allegiance
- Uploads data to Supabase for analytics and tracking
- Targets configurable systems for focused colonization efforts
- Tracks multiple factions and faction states (Boom, War, Election, etc.)

## Architecture

**Orchestrator Pattern:**
```
Elite Dangerous Journal (game writes events)
           ↓
   JournalMonitor (file watcher + parser)
           ↓
       MainCore (event router)
           ↓
    GameLoopModules (ExplorationModule, ColonizationModule)
           ↓
   Data Processing & Storage (local JSON, Supabase)
```

**Key Design Principles:**
- **Async-first**: Non-blocking I/O, never freezes the app
- **Dependency Injection**: Easy testing and swapping implementations
- **Event-driven**: Services communicate via events, not direct calls
- **Graceful Degradation**: Errors never crash the app, always log and continue
- **Modular**: New modules can be added without modifying existing code

## Technology Stack

- **Language**: C# 12 (.NET 10.0)
- **Architecture**: Windows Console App (Service ready)
- **Database**: Supabase (PostgreSQL via REST API)
- **File Monitoring**: FileSystemWatcher (event-driven, ~0% CPU idle)
- **JSON**: System.Text.Json (modern, performant, no external deps)
- **Configuration**: Microsoft.Extensions.Configuration (appsettings.json)
- **Installer**: WiX 4 + PowerShell/Batch scripts
- **Packaging**: Pre-built MSI-ready distributions

## Quick Start

### Option 1: Use Pre-Built Installer (Easiest)

**Prerequisites:**
- Windows 10/11 (64-bit)
- Elite Dangerous (installed and run once)

**Installation (1 minute):**
1. Download the installer package from Releases
2. Extract to any folder
3. Double-click `install.bat`
4. Follow prompts - app installs to Program Files

**That's it!** Find shortcuts on your Desktop and Start Menu.

### Option 2: Portable (No Installation)

1. Download the installer package
2. Extract anywhere (USB drive, custom folder, etc.)
3. Run `EliteDataCollector.Host.exe` directly
4. No installation needed - portable format

### Option 3: Build from Source (Development)

**Prerequisites:**
- Windows 10/11
- .NET 10 SDK
- Elite Dangerous

**Setup (2 minutes):**
```bash
git clone https://github.com/BailThyRedacted/LL-CMDR-Terminal.git
cd LL-CMDR-Terminal
```

**Run:**
```bash
cd EliteDataCollector\EliteDataCollector.Host
dotnet run
```

**Full setup guide:** See [QUICKSTART.md](QUICKSTART.md)

## File Structure

```
LL-CMDR-Terminal/
├── EliteDataCollector.Core/
│   ├── Services/
│   │   ├── GameProcessMonitor.cs      (detects game launch/exit)
│   │   ├── JournalMonitor.cs          (reads journal files in real-time)
│   │   ├── ExobiologyScoringEngine.cs (scores planets for exobiology)
│   │   ├── GameLoopModule.cs          (interface for all modules)
│   │   └── OutputWriter.cs            (logging abstraction)
│   ├── Models/
│   │   ├── SystemData.cs              (colonization data structure)
│   │   ├── PlanetScan.cs              (exobiology planet data)
│   │   ├── FactionInfluence.cs        (BGS faction data)
│   │   └── Structure.cs               (colonization project data)
│   ├── MainCore.cs                    (orchestrator/event router)
│   └── ConsoleOutputWriter.cs         (console logging)
│
├── Modules/
│   ├── ExplorationModule/
│   │   └── ExplorationModule.cs       (high-value planet detector)
│   └── ColonizationModule/
│       └── ColonizationModule.cs      (BGS faction tracker)
│
├── EliteDataCollector.Host/
│   ├── Program.cs                     (entry point, DI setup)
│   ├── Form1.cs                       (GUI stub, future expansion)
│   └── appsettings.json               (configuration)
│
├── QUICKSTART.md                      (user setup guide)
└── README.md                          (this file)
```

## Code Statistics

- **Total Lines**: ~1,900 of production code
- **MainCore.cs**: ~390 lines (orchestrator with extensive teaching comments)
- **GameProcessMonitor.cs**: ~480 lines (game detection with dual-mode support)
- **JournalMonitor.cs**: ~734 lines (file monitoring + offset persistence)
- **ColonizationModule.cs**: ~493 lines (BGS data extraction + Supabase upload)
- **ExplorationModule.cs**: ~430 lines (exobiology scoring + alerts)
- **ExobiologyScoringEngine.cs**: ~340 lines (4-factor scoring algorithm)
- **Models & Services**: ~200 lines (clean, well-documented)

## Learning Value

This project demonstrates professional patterns:

1. **Async/Await Throughout**: Non-blocking I/O, Tasks, proper cancellation
2. **Dependency Injection**: Service locator pattern, interface-based design
3. **Event-Driven Architecture**: Loose coupling via events, not direct calls
4. **Error Resilience**: Try-catch in critical paths, graceful degradation
5. **State Management**: Tracking current system, planet cache, offset persistence
6. **JSON Parsing**: Safe property access patterns, no exceptions on missing fields
7. **File I/O**: FileSystemWatcher, byte offsets, FileShare.ReadWrite for concurrent access
8. **Configuration Management**: Machine.Extensions.Configuration, appsettings patterns
9. **Null Safety**: Proper use of nullable types, null-coalescing operators
10. **Modular Design**: Modules implement interface, receive events, can be extended

Every class has extensive XML comments explaining the "why" not just the "what".

## How It Works

### Game Launch Detection
1. **GameProcessMonitor** polls for EliteDangerous64.exe (configurable: WMI or polling mode)
2. When found, raises `GameLaunched` event
3. **MainCore** starts **JournalMonitor**

### Journal Event Processing
1. **JournalMonitor** watches Elite Dangerous Logs folder via FileSystemWatcher
2. Latest Journal.*.log file is tailed (reading only new lines)
3. Lines are parsed as JSON, event type extracted
4. Only important events forwarded (Scan, ScanOrganic, FSDJump, Location, Structure*)
5. Byte offsets saved for resume capability (survives app crash/restart)

### Module Processing
1. **MainCore** routes events to each module
2. **ExplorationModule** scores planets and alerts
3. **ColonizationModule** uploads BGS data
4. Both modules handle errors independently (one failure doesn't affect the other)

### Data Persistence
1. **ExplorationModule**: Scans saved to `%APPDATA%\EliteDangerousDataCollector\scans.json`
2. **ColonizationModule**: Data uploaded to Supabase (configurable)
3. **JournalMonitor**: Offsets saved to `journal_offsets.json` for resume

## Build & Test

### Build
```bash
cd EliteDataCollector\EliteDataCollector.Core
dotnet build

cd ..\Modules\ExplorationModule
dotnet build

cd ..\Modules\ColonizationModule
dotnet build
```

Expected: `Build succeeded` with 0 errors.

### Run
```bash
cd EliteDataCollector\EliteDataCollector.Host
dotnet run
```

### Expected Console Output
```
[GameProcessMonitor] Starting game process monitoring...
[JournalMonitor] Monitoring journal folder: C:\Users\...\Saved Games\Frontier Developments\Elite Dangerous\Logs
[MainCore] Initializing services...
[Colonization] Initializing...
[Exploration] Initializing...
[MainCore] Waiting for game launch...

[GameProcessMonitor] Game launched detected! (EliteDangerous64.exe)
[JournalMonitor] Started monitoring journal
[Colonization] Initialized
[Exploration] Started ready to scan planets.

[Exploration] Scanned: Sol 1 - Score: 65/100
[Exploration] 🎯 HIGH VALUE: Sol - Sol 1 - Atmosphere: Water atmosphere - Score: 85/100 - Est. Value: ~15.3M credits
```

## Performance

- **Memory**: ~50-80 MB at idle (small .NET runtime footprint)
- **CPU**: ~0% idle, <1% while processing events
- **Disk I/O**: ~100 Bytesper event read, negligible write frequency
- **Network**: Supabase uploads only on relevant events (~1/minute during gameplay)

All async/await, no blocking, efficient file monitoring.

## Future Roadmap

- [ ] Mission tracking module
- [ ] Trade profit calculator module
- [ ] Combat stats module
- [ ] GUI interface 
- [ ] Multiple squad support
- [ ] Custom module plugin system
- [ ] Web dashboard for data visualization
- [ ] Discord bot integration for alerts

## Contributing

Contributions welcome! See [GitHub Issues](https://github.com/BailThyRedacted/LL-CMDR-Terminal/issues) for ideas.

### Adding a New Module
1. Create folder in `Modules/YourModule/`
2. Implement `GameLoopModule` interface
3. Add project reference to Core
4. Register in `Program.cs` DI container
5. Module receives events automatically from `MainCore`

---

**Questions?** See [QUICKSTART.md](QUICKSTART.md) for setup help.

**Fly safe, Commander!** 🚀
