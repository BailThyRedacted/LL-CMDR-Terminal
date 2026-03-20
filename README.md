# Elite Dangerous Colonization Data Collector

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%2B-0078D6.svg)](https://www.microsoft.com/windows)

A lightweight, invisible background application that automatically collects colonization data from Elite Dangerous and uploads it to a central Supabase database for squadron members of **Lavigny's Legion**.

---

## 📋 Overview

This application runs silently in your system tray, automatically detecting when Elite Dangerous launches and begins collecting real-time colonization data from your journal files and the Frontier Companion API (CAPI). Data is uploaded to a shared Supabase database, enabling the squadron to track colonization progress, monitor system influence, and coordinate construction efforts.

### Key Features

- 🕵️ **Invisible Operation** – Runs in background with no visible console window
- 🎮 **Auto-Launch Detection** – Automatically starts/stops data collection when Elite Dangerous launches/exits
- 🔐 **Squadron Verification** – Only members of Lavigny's Legion can upload data
- 📊 **Colonization Data** – Captures system BGS/Powerplay state, structures, and construction progress
- ☁️ **Supabase Integration** – Uploads data to shared PostgreSQL database
- 🔄 **Auto-Updates** – Checks GitHub for new versions and prompts for installation
- 🔌 **Modular Architecture** – Future modules can be added as plug-ins

---

## 🚀 Quick Start

### Prerequisites

- Windows 10 or 11 (64-bit)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- Elite Dangerous (installed and played at least once)
- Membership in **Lavigny's Legion** squadron

### Installation

1. **Download** the latest release from the [Releases](https://github.com/lavignyslegion/elite-colonization-collector/releases) page
2. **Extract** the contents to a permanent folder (e.g., `C:\Program Files\EliteDataCollector\`)
3. **Run** `EliteDataCollector.exe` once to complete authentication

### First-Time Setup

1. **Authenticate with Frontier** – Your browser will open. Log in to your Frontier account and authorize the application.
2. **Squadron Verification** – The app will verify you're a member of Lavigny's Legion.
3. **Add to Startup** – You'll be prompted to add the app to Windows Startup for automatic operation.

The app will now run silently in your system tray. It will automatically start collecting data when you launch Elite Dangerous.

---

## 📊 Data Collected

The application collects only colonization-relevant data:

| Data Type | Description |
|-----------|-------------|
| **System Information** | System name, timestamp of last visit |
| **BGS State** | Factions, influence percentages, states, controlling faction |
| **Powerplay State** | Controlling power, power state (if applicable) |
| **Structures** | Structure names, types, construction progress percentages |

All data is uploaded to a shared Supabase database accessible to squadron leadership for analysis and coordination.

---

## 🛠️ Configuration

### `appsettings.json`

```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "Key": "your-supabase-anon-key"
  },
  "Capi": {
    "ClientId": "your-frontier-client-id",
    "ClientSecret": "your-frontier-client-secret"
  }
}
```

### Module Configuration

Modules store their configuration in the `Modules` folder:

- `ColonizationModule.json` – Configure local cache duration and Inara upload preferences

### Inara Integration (Optional)

To enable Inara upload:
1. Obtain an API key from [Inara](https://inara.cz/inara-api/)
2. Enter the key when prompted by the app (first run or via tray menu)
3. The key is encrypted and stored securely using Windows DPAPI

---

## 🔧 Development

### Building from Source

```bash
# Clone the repository
git clone https://github.com/lavignyslegion/elite-colonization-collector.git
cd elite-colonization-collector

# Restore dependencies
dotnet restore

# Build the solution
dotnet build -c Release

# Publish self-contained executable
dotnet publish EliteDataCollector.Host -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Project Structure

```
EliteDataCollector/
├── EliteDataCollector.Core/           # Core library
│   ├── Interfaces/                    # Module interfaces
│   ├── Services/                      # Journal monitor, CAPI auth, etc.
│   ├── Models/                        # Shared data models
│   └── ModuleLoader.cs                # Module discovery and loading
├── EliteDataCollector.Host/           # Windows background host
│   ├── Program.cs                     # Hidden form, tray icon
│   └── Notifications.cs               # Toast/balloon notifications
├── Modules/
│   └── ColonizationModule/            # Colonization data collection
│       ├── ColonizationModule.cs      # Module implementation
│       └── ColonizationModule.json    # Configuration
└── EliteDataCollector.Tests/          # Unit tests
```

### Creating a New Module

1. Create a new Class Library project targeting .NET 8.0
2. Reference `EliteDataCollector.Core.dll`
3. Implement `IGameLoopModule` interface:

```csharp
public class MyModule : IGameLoopModule
{
    public string Name => "My Module";
    public string Description => "Does something useful";

    public async Task InitializeAsync(IServiceProvider services)
    {
        // Setup module (load config, subscribe to events)
    }

    public async Task OnJournalLineAsync(string line, JsonDocument parsedEvent)
    {
        // Process journal events
        string eventName = parsedEvent.RootElement.GetProperty("event").GetString();
        switch (eventName)
        {
            case "FSDJump":
                // Handle jump
                break;
        }
    }

    public async Task OnCapiProfileAsync(JsonDocument profile)
    {
        // Handle profile updates
    }

    public async Task ShutdownAsync()
    {
        // Cleanup
    }
}
```

4. Build and place the DLL in the `Modules` folder

---

## 🔒 Security

- **CAPI Tokens** – Stored using Windows DPAPI (encrypted per user)
- **Inara API Key** – Also encrypted via DPAPI
- **Supabase Connection** – Uses anon key with restricted permissions
- **Squadron Verification** – Prevents unauthorized data uploads
- **No Passwords** – OAuth2 PKCE flow, no credentials stored

---

## 📋 Requirements Traceability

| Requirement | Implementation |
|-------------|----------------|
| Invisible operation | Windows Forms hidden window, system tray |
| Auto-launch detection | Process monitor checks for `EliteDangerous64.exe` every 5 seconds |
| Journal monitoring | `FileSystemWatcher` with byte-offset tracking |
| CAPI authentication | OAuth2 PKCE with secure token storage |
| Squadron verification | Profile parsing, 24-hour re-check |
| Supabase upload | Batch upserts with retry logic |
| Module system | MEF/assembly scanning for `IGameLoopModule` |
| Auto-updates | GitHub API check, user prompt, safe replacement |

---

## 🐛 Troubleshooting

### App doesn't start collecting data

- Ensure Elite Dangerous is running
- Check that the journal folder exists: `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous`
- Verify the app has read access to that folder

### Authentication fails

- Ensure you're using the correct Frontier account
- Verify the application is approved in your Frontier developer account
- Check that you're a member of Lavigny's Legion

### Data not uploading

- Check network connectivity
- Verify Supabase URL and key are correct
- Check squadron verification status (tray icon tooltip shows status)

### High memory/CPU usage

- Default idle: <10 MB RAM, <0.1% CPU
- If higher, restart the app
- Check for excessive log file sizes

---

## 📄 License

This project is licensed under the MIT License – see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgements

- [Frontier Developments](https://www.frontier.co.uk/) for Elite Dangerous and the Companion API
- [Supabase](https://supabase.com/) for the open-source Firebase alternative
- [Inara](https://inara.cz/) for community data aggregation
- Lavigny's Legion squadron members for testing and feedback

---

## 📞 Support

For issues, questions, or contributions:

- Open an [Issue](https://github.com/lavignyslegion/elite-colonization-collector/issues)
- Join our [Discord](https://discord.gg/lavignyslegion) (squadron members only)
- Contact squadron leadership directly

---

*This application is not endorsed by or affiliated with Frontier Developments plc.*