# Elite Data Collector - Installation Guide

## Quick Installation

### Option 1: Automated (Recommended)
1. Ensure the [.NET SDK](https://dot.net/download) is installed on your machine
2. Double-click `install.bat`
3. Accept the UAC elevation prompt when Windows asks
4. The installer will build the app and install it to `Program Files\Elite Data Collector` automatically

### Option 2: PowerShell
```powershell
# Run from an elevated PowerShell prompt
.\install.ps1
```

### Option 3: WiX MSI
Build the MSI with the WiX toolset, then run `EliteDataCollector.Host.msi`. Requires `wix` on PATH and `WixToolset.UI.wixext`.

## System Requirements
- Windows 10 x64 or newer
- [.NET SDK](https://dot.net/download) (build-time only — the installed app is self-contained and requires no runtime on the user's machine)
- ~200 MB free disk space (self-contained publish includes the .NET runtime)

## What the installer does
1. Runs `dotnet publish --self-contained --runtime win-x64` to produce a standalone build
2. Copies all files to `%ProgramFiles%\Elite Data Collector`
3. Creates shortcuts on your Desktop and in the Start Menu
4. Registers the app in Add/Remove Programs (via HKLM)

## First Run
On first launch, Elite Data Collector will create its configuration directory at `%LOCALAPPDATA%\Elite Data Collector`. Edit `appsettings.json` in the install folder to configure your Supabase credentials and preferences before running.

## Uninstallation
- **Add/Remove Programs**: find "Elite Data Collector" and click Uninstall
- **Manual**: run `uninstall.bat` from `%ProgramFiles%\Elite Data Collector`
- **PowerShell**: run `uninstall.ps1` from `%ProgramFiles%\Elite Data Collector` (elevated)

> The uninstaller will refuse to run while the application is open. Close it first.

## Troubleshooting

### install.bat says "dotnet SDK not found"
Download and install the .NET SDK from [https://dot.net/download](https://dot.net/download), then re-run the installer.

### UAC prompt does not appear
Right-click `install.bat` and choose **Run as administrator**.

### Application won't start after install
Run `EliteDataCollector.Host.exe` from a Command Prompt to see the error output directly.

### Missing or corrupted files
Re-run `install.bat` — it will overwrite the existing installation cleanly.

## Support
For issues or questions, refer to the main project documentation.
