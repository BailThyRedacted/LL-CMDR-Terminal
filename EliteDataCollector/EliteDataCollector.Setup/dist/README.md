# Elite Data Collector - Installer Package

## Package Contents

This folder contains the complete Elite Data Collector application with multiple installation options.

### Files Included
- `install.bat` - Simple batch installer (recommended for basic installation)
- `install.ps1` - Advanced PowerShell installer with full system integration
- `uninstall.bat` - Batch uninstaller
- `uninstall.ps1` - PowerShell uninstaller
- `EliteDataCollector.Host.exe` - Main application executable
- `appsettings.json` - Application configuration file
- All required .NET and Supabase DLLs and runtime files

## Installation Instructions

### Method 1: Batch Installer (Easiest)
```
1. Double-click install.bat
2. Follow the prompts
3. Application will be installed to Program Files
```

### Method 2: PowerShell Installer (Recommended for advanced users)
```
1. Right-click install.ps1
2. Select "Run with PowerShell"
3. Or run from PowerShell with: powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1
```

### Method 3: Manual Installation
```
1. Create folder: C:\Program Files\Elite Data Collector
2. Copy all files to that folder
3. Create shortcuts manually to EliteDataCollector.Host.exe
```

### Method 4: Portable Installation
```
1. Copy all files to any location (USB drive, custom folder, etc.)
2. Run EliteDataCollector.Host.exe directly from that location
3. No installation required - portable format
```

## System Requirements
- **OS**: Windows 10 or newer (64-bit)
- **Disk Space**: ~200 MB
- **.NET Runtime**: 10.0 (all required files included)
- **Admin Rights**: Required for system-wide installation (Methods 1-2)

## First Run
1. Launch Elite Data Collector
2. Application will create configuration files:
   - `%LOCALAPPDATA%\Elite Data Collector\` - Config directory
3. Edit `appsettings.json` to configure settings

## Uninstallation

### Using Batch Uninstaller
```
1. Run uninstall.bat from the installation folder or original package
2. Confirm when prompted
3. All files and shortcuts will be removed
```

### Using PowerShell Uninstaller
```
1. Run uninstall.ps1 with: powershell -NoProfile -ExecutionPolicy Bypass -File uninstall.ps1
```

### Manual Uninstallation
```
1. Delete the installation folder
2. Remove shortcuts from:
   - Desktop
   - Start Menu > Programs
3. Optional: Delete C:\Users\[username]\AppData\Local\Elite Data Collector
```

## Troubleshooting

### Application won't start
- Ensure all DLL files are in the same directory as the .exe
- Check that you have access to the installation directory
- Try running from Command Prompt to see error messages

### Shortcut creation failed
- Run installer as Administrator
- Check User Account Control (UAC) settings
- Try using PowerShell installer instead

### Permission denied errors
- Right-click installer and select "Run as administrator"
- Check folder permissions on installation target
- Temporarily disable antivirus during installation

## System Integration

When using Method 1 or 2, the installer will:
- Create Start Menu shortcuts
- Create Desktop shortcut
- Add application to Add/Remove Programs (Control Panel)
- Create registry entries for uninstall
- Set working directory for proper file access

## Features

### Exobiology Module
- Real-time planet value assessment
- Atmosphere and composition analysis  
- Automatic high-value alerts

### Colonization Module
- BGS faction tracking
- System control monitoring
- Influence level tracking

## Support & Documentation
See the main project documentation for:
- Architecture overview
- Configuration guide
- Feature details
- Troubleshooting FAQs

## Version
Elite Data Collector v1.0.0.0

---

**Note**: This application requires an active Elite Dangerous game session to monitor gameplay events.
