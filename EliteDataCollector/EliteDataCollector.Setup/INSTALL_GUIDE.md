# Elite Data Collector - Installation Guide

## Quick Installation

### Option 1: MSI Installer (Recommended)
1. Run `publish.bat` from the solution root to build the self-contained app and MSI
2. Double-click `EliteDataCollector.Setup\output\EliteDataCollector-Setup.msi`
3. Follow the installer wizard to choose an install directory
4. Launch from your Desktop or Start Menu shortcut

### Option 2: Automated Script (Requires .NET 8 SDK)
1. Ensure the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) is installed
2. Double-click `install.bat`
3. Accept the UAC elevation prompt when Windows asks
4. The installer will build and install the app to `Program Files\Elite Data Collector`

### Option 3: PowerShell (Requires .NET 8 SDK)
```powershell
# Run from an elevated PowerShell prompt
.\install.ps1
```

### Option 4: Portable ZIP
Run `create-portable-package.bat` after building in Release mode. Extract the ZIP anywhere and run `EliteDataCollector.UI.exe`.

## System Requirements
- Windows 10 x64 (build 19041) or newer
- ~200 MB free disk space (self-contained, no .NET runtime needed on user machine)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) is required only for building from source (Options 2 & 3)

## What the installer does
1. Publishes a self-contained WinUI 3 application (no .NET runtime required on user machine)
2. Copies all files to `%ProgramFiles%\Elite Data Collector`
3. Creates shortcuts on your Desktop and in the Start Menu
4. Registers the app in Add/Remove Programs (via HKLM)

## First Run
On first launch, Elite Data Collector will create its configuration directory at `%LOCALAPPDATA%\Elite Data Collector`. Edit `appsettings.json` in the install folder to configure your Supabase credentials and preferences before running.

## Uninstallation
- **Add/Remove Programs**: find "Elite Data Collector" and click Uninstall
- **Manual**: run `uninstall.bat` from `%ProgramFiles%\Elite Data Collector` (as Administrator)
- **PowerShell**: run `uninstall.ps1` from `%ProgramFiles%\Elite Data Collector` (elevated)

> The uninstaller will refuse to run while the application is open. Close it first.

## Troubleshooting

### install.bat says "dotnet SDK not found"
Download and install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then re-run the installer.

### UAC prompt does not appear
Right-click `install.bat` and choose **Run as administrator**.

### Application won't start after install
Run `EliteDataCollector.UI.exe` from a Command Prompt to see the error output directly.

### Missing or corrupted files
Re-run `install.bat` — it will overwrite the existing installation cleanly.

## Support
For issues or questions, refer to the main project documentation.
