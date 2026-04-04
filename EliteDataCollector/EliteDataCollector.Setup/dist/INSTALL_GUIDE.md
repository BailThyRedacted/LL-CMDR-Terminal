# Elite Data Collector - Installation Guide

## Quick Installation

### Option 1: Automated Installation (Recommended)
1. Extract all files to a folder
2. Double-click `install.bat`
3. Follow the on-screen prompts
4. The application will be installed to `Program Files` (or `Program Files (x86)`)

### Option 2: Manual Installation
1. Create a folder in `Program Files`: `Elite Data Collector`
2. Copy all files from the `bin\Release\net10.0-windows` folder to your installation folder
3. Create shortcuts manually pointing to `EliteDataCollector.Host.exe`

## System Requirements
- Windows 10 or newer
- .NET Runtime 10.0 or newer (included in the application folder)
- 100 MB free disk space

## First Run
When you run Elite Data Collector for the first time:
1. The application will create configuration files in `%LOCALAPPDATA%\Elite Data Collector`
2. You'll need to configure your settings in `appsettings.json`

## Uninstallation
To uninstall Elite Data Collector:
1. Run `uninstall.bat` from your installation folder
2. Or manually delete the installation folder and remove shortcuts

## Troubleshooting

### Application won't start
- Check that .NET 10.0 Runtime is installed
- Try running from Command Prompt to see error messages
- Check file permissions on the installation folder

### Missing dependencies
- All required DLLs are in the installation folder
- Do not move or delete any .dll files

## Support
For issues or questions, check the documentation in the main package.
