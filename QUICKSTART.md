# Elite Data Collector - Setup Guide

Welcome! Choose your setup method below.

## What This App Does

**Elite Data Collector** is a Windows application that monitors your Elite Dangerous gameplay and tracks valuable data:

- **ExplorationModule**: Identifies high-value exobiology planets and alerts you to valuable scanning targets
- **ColonizationModule**: Tracks BGS (Background Simulation) faction influence and PowerPlay state in target systems

---

## QUICK SETUP: Use Pre-Built Installer (Recommended)

### Prerequisites
- Windows 10/11 (64-bit)
- Elite Dangerous (installed and run at least once)
- ~200 MB free disk space

### Installation (1 minute)

1. **Download installer package** from the Releases section
2. **Extract the folder** to any location
3. **Double-click `install.bat`**
4. **Follow prompts** - app installs to Program Files
5. **Done!** Check Desktop and Start Menu for shortcuts

### First Run
1. Click the Elite Data Collector shortcut
2. Launch Elite Dangerous
3. Start playing - the app monitors in the background
4. Watch console for alerts when high-value planets detected

### Using Portable (No Installation)
- Instead of running install.bat, just run `EliteDataCollector.Host.exe` directly
- No installation needed - works from any folder (USB drive, custom location, etc.)

---

## DEVELOPER SETUP: Build from Source

Skip this section unless you want to modify the code or build from source.

### Prerequisites

**Download and install:**
- .NET 8 SDK: https://dotnet.microsoft.com/en-us/download
- Visual Studio Code or Visual Studio (optional, for development)

**Verify installation:**
```bash
dotnet --version
```
Should show version 8.0.x or higher.

### Step 1: Clone Repository
```bash
git clone https://github.com/BailThyRedacted/LL-CMDR-Terminal.git
cd "LL-CMDR-Terminal"
```

### Step 2: Build the App
```bash
cd EliteDataCollector\EliteDataCollector.Core
dotnet build

cd ..\Modules\ExplorationModule
dotnet build

cd ..\..
```

You should see: `Build succeeded`

### Step 3: Run the App

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

### If Using Installer
1. Look for **Elite Data Collector** on your Desktop or Start Menu
2. Click the shortcut - the app launches
3. Launch Elite Dangerous
4. Play normally - app monitors in background
5. Watch console window for alerts

### If Using Source Build
```bash
dotnet run
```

You should see:
```
[GameProcessMonitor] Starting game process monitoring...
[JournalMonitor] Monitoring journal folder: C:\Users\...\Saved Games\Frontier Developments\Elite Dangerous\Logs
[Colonization] Initializing...
[Exploration] Initializing...
[MainCore] Waiting for game launch...
```

### Step 2: Launch Elite Dangerous
Start the game in the launcher. The app will detect it automatically:
```
[GameProcessMonitor] Game launched detected! (EliteDangerous64.exe)
[JournalMonitor] Started monitoring journal
[Colonization] Initialized
[Exploration] Started ready to scan planets.
```

### Step 3: Start Playing
- **Scanning planets?** ExplorationModule will alert you to valuable exobiology targets
- **Flying to target systems?** ColonizationModule will track BGS faction data

Watch the console for alerts:
```
[Exploration] 🎯 HIGH VALUE: Sol - Sol 1
- Atmosphere: Water atmosphere - Temp: 288.1K
- Gravity: 1.00G - Landable: YES - Score: 89/100
- Est. Value: ~12.5M credits
```

### Step 4: Exit the App
Press `Ctrl+C` to stop (or close the console window). All data is saved automatically.

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

## Uninstallation

### If Using Installer
1. Locate the installer folder (where you extracted it)
2. Run `uninstall.bat`
3. Confirm when prompted
4. All files, shortcuts, and registry entries are removed

**Or manually:**
1. Delete the installation folder from Program Files
2. Delete shortcuts from Desktop and Start Menu
3. Done!

### If Using Source Build
Simply delete the cloned repository folder. No registry entries created.

## Re-installing

To upgrade or reinstall:
1. **Uninstall** using the method above
2. **Download** the latest installer package
3. **Run install.bat** from the new package
4. All configuration files are preserved in `%LOCALAPPDATA%\Elite Data Collector`

## Troubleshooting

### "dotnet: command not found"
**Solution:** .NET 8 SDK is not installed. Download and install from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

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

### Auto-Update Notifications

**When does the app check for updates?**
The app checks for new releases on startup. If a new version is available on GitHub, you'll see:
```
========================================
  UPDATE AVAILABLE
========================================
Current Version: 1.0.0
Latest Version:  1.1.0
Release Date:    2026-04-10

Release Notes:
---------
- Fixed exobiology scoring
- Added new planet alerts
---------

Update will be installed when you stop playing (app becomes idle).
[Y]es - Install now / [L]ater - Ask again tomorrow / [D]isable auto-update
```

**What happens after you say "Yes"?**
1. Download starts when you stop playing (to avoid interrupting gameplay)
2. A backup of the current version is created in: `%APPDATA%\EliteDangerousDataCollector\backups\`
3. The new version is installed
4. App restarts automatically

**Can I disable auto-update?**
Yes. When prompted for an update, press `D` to disable auto-update checks. You can re-enable it later by editing:
```
%APPDATA%\EliteDangerousDataCollector\settings.json
```
Change `"auto_update_enabled": false` to `"auto_update_enabled": true`

**How do I rollback to the previous version?**
If an update causes problems, backups are stored in:
```
%APPDATA%\EliteDangerousDataCollector\backups\
```
The app keeps the last 3 versions. You can manually restore from a backup or contact support for help.

**"Update check failed" or "No internet connection"?**
The app gracefully handles network issues. It will try again on the next startup. You can continue using the app offline normally.

### Nothing in console, app seems stuck
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

### If Using Installer
- [ ] Installer package downloaded and extracted
- [ ] Ran `install.bat` successfully
- [ ] Desktop or Start Menu shortcut created
- [ ] Elite Dangerous installed and launched once
- [ ] Shortcut launches app without errors
- [ ] Console window opens when launching

### If Building from Source
- [ ] .NET 8 SDK installed (`dotnet --version` works)
- [ ] Repository cloned successfully
- [ ] `dotnet build` shows 0 errors
- [ ] App starts without errors (`dotnet run`)
- [ ] Elite Dangerous installed and launched once

### For Both Setups
- [ ] Console shows "[GameProcessMonitor] Game launched detected!"
- [ ] Supabase credentials in appsettings.json (if using ColonizationModule)
- [ ] No permission errors in console

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
