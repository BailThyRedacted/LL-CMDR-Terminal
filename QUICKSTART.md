# Elite Data Collector - Quick Start Guide

Welcome! This guide will get you up and running with the Elite Data Collector in 5 minutes.

## What This App Does

**Elite Data Collector** is a Windows background service that monitors your Elite Dangerous gameplay and tracks valuable data:

- **ColonizationModule**: Tracks BGS (Background Simulation) faction influence and PowerPlay state in target systems
- **ExplorationModule**: Identifies high-value exobiology planets and alerts you to valuable scanning targets

## Prerequisites

### 1. Download .NET 10 Runtime
The app requires .NET 10. Download the Windows runtime:
- Go to: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- Click "**Runtime**" (not SDK)
- Download: **Desktop Runtime** (Windows x64)
- Run the installer and follow prompts

Verify installation:
```bash
dotnet --version
```
Should show version 10.0.x or higher.

### 2. Elite Dangerous
- Install [Elite Dangerous](https://www.elitedangerous.com/)
- Launch the game once to create the journal folder
- Journal files are stored in: `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\Logs\`

### 3. Supabase Account (Optional, for ColonizationModule)
Skip this if you only want to use ExplorationModule.

If using ColonizationModule:
- Create free account at: https://supabase.com
- Create a new project
- Get your project URL and API key from Settings → API

## Setup (5 minutes)

### Step 1: Download the App
```bash
git clone https://github.com/BailThyRedacted/LL-CMDR-Terminal.git
cd "LL-CMDR-Terminal"
```

Or download as ZIP from GitHub and extract.

### Step 2: Configure Supabase (If Using ColonizationModule)

Edit this file:
```
EliteDataCollector\EliteDataCollector.Host\appsettings.json
```

Replace with your Supabase credentials:
```json
{
  "Supabase": {
    "Url": "https://YOUR_PROJECT_ID.supabase.co",
    "Key": "YOUR_PUBLIC_ANON_KEY"
  }
}
```

Find these in Supabase Dashboard:
- Settings → API → Project URL (= your Url)
- Settings → API → anon public (= your Key)

**Save the file.**

### Step 3: Build the App
```bash
cd EliteDataCollector\EliteDataCollector.Core
dotnet build

cd ..\Modules\ExplorationModule
dotnet build

cd ..\..
```

You should see: `Build succeeded`

### Step 4: Create a Shortcut to Run

Create a batch file `run.bat` in the `LL-CMDR-Terminal` folder:

```batch
@echo off
cd EliteDataCollector\EliteDataCollector.Host
dotnet run
```

Or run manually from PowerShell:
```powershell
cd EliteDataCollector\EliteDataCollector.Host
dotnet run
```

## Running the App

### 1. Start the Application
```bash
dotnet run
```

You should see:
```
[GameProcessMonitor] Starting game process monitoring...
[JournalMonitor] Journal monitor initialized.
[Colonization] Initializing...
[Exploration] Initializing...
```

### 2. Launch Elite Dangerous
Start the game in the launcher. The app will detect it automatically:
```
[GameProcessMonitor] Game launched detected!
[JournalMonitor] Journal monitor started...
```

### 3. Start Playing
- **Scanning planets?** ExplorationModule will alert you to valuable exobiology targets
- **Flying to target systems?** ColonizationModule will track BGS faction data

Watch the console for alerts:
```
[Exploration] 🎯 HIGH VALUE: Sol - Sol 1
- Atmosphere: Water atmosphere - Temp: 288.1K
- Gravity: 1.00G - Landable: YES - Score: 89/100
- Est. Value: ~12.5M credits
```

### 4. Exit the App
Press `Ctrl+C` to stop. All data is saved automatically.

## Where Your Data Is Saved

**Local Files** (ExplorationModule):
```
%APPDATA%\EliteDangerousDataCollector\scans.json
```
- JSON array of all planets you scanned
- Includes: atmosphere, temperature, gravity, score, estimated value
- Opens in any text editor

**Supabase** (ColonizationModule):
- BGS faction data uploaded automatically
- Accessible from your Supabase dashboard
- View at: https://app.supabase.com/

**Offset Storage** (Resume capability):
```
%APPDATA%\EliteDangerousDataCollector\journal_offsets.json
```
- Stores position in Elite journal files
- Allows app to resume without re-reading old events

## Features Explained

### ExplorationModule (Always On)

**What it does:**
- Listens to Scan and ScanOrganic events from your journal
- Scores planets based on exobiology potential (0-100)
- Alerts you to high-value planets (score > 60)
- Saves all scans to local JSON file

**Scoring factors:**
- Atmosphere type (40%) - Ammonia, Methane, Nitrogen, Water = best
- Planet type (20%) - Water World, High Metal Content = good
- Temperature (20%) - Extreme or Earth-like = best
- Gravity (10%) - Lower gravity = more exotic organisms
- Landable (10%) - Must be landable to harvest samples

**Example alert:**
```
[Exploration] 🎯 HIGH VALUE: Achenar - Achenar AB 1 a
- Atmosphere: Ammonia atmosphere - Temp: 150.0K - Gravity: 0.45G
- Landable: YES - Score: 92/100 - Est. Value: ~18.2M credits
```

**Value ranges:**
- Score > 80: 10M - 20M+ credits
- Score 60-80: 2M - 10M credits
- Score 40-60: 500K - 2M credits
- Score < 40: Bacterium-only (< 100K)

### ColonizationModule (Tracks BGS)

**What it does:**
- Runs when you're in target systems
- Tracks faction influence and BGS state
- Monitors PowerPlay control and allegiance
- Uploads data to your Supabase database
- Helps squadrons track colonization progress

**Requires:**
- Supabase credentials in appsettings.json
- Target systems configured in Supabase database
- Table: `target_systems` with system names

**Data collected:**
- System name and address
- Controlling faction
- PowerPlay power and state
- All faction influence values
- All faction states (Boom, War, Election, etc.)

## Troubleshooting

### "dotnet: command not found"
**Solution:** .NET 10 is not installed. Download and install from: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

### "Journal files not found"
**Solution:** Elite Dangerous journal hasn't been created yet. Launch Elite Dangerous once and fly for a few seconds to trigger journal creation.

### "Cannot find journal folder"
**Solution:** App looks for journals in default location:
```
%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\Logs\
```
If your Elite Dangerous is installed elsewhere, check this folder exists.

### "ExplorationModule not alerting"
**Possible reasons:**
1. You haven't scanned any planets yet
2. Planets you scanned have low exobiology potential (score < 60)
3. Planets are bacterium-only (ignored)

**Test it:**
- Jump to known exobiology system (e.g., bubble nebula systems)
- Scan several planets
- Look for console alerts

### "ColonizationModule shows 'Loaded 0 target systems'"
**Possible reasons:**
1. Supabase credentials wrong in appsettings.json
2. `target_systems` table doesn't exist in Supabase
3. No systems configured in the table

**Fix:**
1. Verify Supabase URL and Key are correct
2. Create table in Supabase with required schema
3. Add system names to the table

### "Nothing in console, app seems stuck"
**Check:**
1. Is Elite Dangerous running? App waits for game launch
2. Is there a journal file being written? Check size of latest Journal.*.log in Logs folder
3. Try: `Ctrl+C` to exit, and restart

## Configuration Files

### appsettings.json
Located at: `EliteDataCollector\EliteDataCollector.Host\appsettings.json`

Default:
```json
{
  "Supabase": {
    "Url": "",
    "Key": ""
  }
}
```

Set these to your Supabase credentials if using ColonizationModule.

### scans.json
Created automatically at: `%APPDATA%\EliteDangerousDataCollector\scans.json`

Format (JSON array):
```json
[
  {
    "systemName": "Sol",
    "bodyName": "Sol 1",
    "planetType": "Water world",
    "atmosphere": "Water atmosphere",
    "surfaceTemperature": 288.15,
    "gravity": 1.0,
    "landable": true,
    "timestamp": "2026-03-23T14:30:00Z",
    "exobiologyScore": 85,
    "estimatedValue": 12500000,
    "bacteriumOnly": false
  }
]
```

## First Run Checklist

- [ ] .NET 10 installed (`dotnet --version` works)
- [ ] Elite Dangerous installed and launched once
- [ ] Supabase credentials in appsettings.json (if using ColonizationModule)
- [ ] App builds successfully (`dotnet build` shows 0 errors)
- [ ] App starts without errors
- [ ] Elite Dangerous is running
- [ ] Console shows "[GameProcessMonitor] Game launched detected!"

## Tips & Tricks

### Monitoring High-Value Exobiology
1. Use exploration guides to find systems with exobiology
2. Run the app
3. Scan planets in those systems
4. Watch console for high-value alerts
5. Jump to alerted planets and collect samples
6. Check `%APPDATA%\EliteDangerousDataCollector\scans.json` to see recorded data

### Tracking Colonization
1. Set up your target systems in Supabase
2. Fly to those systems
3. App automatically uploads faction data
4. Check your Supabase dashboard to see data flow

### Resuming After Restart
- All scan data persists in scans.json
- Journal offsets saved so no duplicate events
- Just restart the app and it picks up where it left off

## Next Steps

- **Customize**: Edit scoring algorithm in `ExobiologyScoringEngine.cs` to fit your preferences
- **Extend**: Add new modules for missions, combat, trading, etc.
- **Share**: Contribute improvements back to the project!

## Support & Issues

**Common questions:**
- "Can I use this on Mac/Linux?" - Currently Windows-only (uses Elite Dangerous journal files)
- "Does this affect game performance?" - No, runs separately with minimal CPU usage
- "Will I get banned for using this?" - No, it only reads local files, doesn't modify anything

**Report bugs:**
- GitHub Issues: https://github.com/BailThyRedacted/LL-CMDR-Terminal/issues

---

**Fly safe, Commander!** 🚀

Made with ❤️ for Elite Dangerous explorers and colonizers.
