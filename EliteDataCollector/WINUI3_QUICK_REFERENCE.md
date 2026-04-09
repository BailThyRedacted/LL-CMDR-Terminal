# WinUI 3 GUI - Quick Reference & Usage Guide

## Building the Project

```powershell
cd C:\Users\seva2\RiderProjects\LL-CMDR-Terminal\EliteDataCollector

# Build UI project
dotnet build EliteDataCollector.UI/EliteDataCollector.UI.csproj

# Build entire solution
dotnet build
```

## Project Structure at a Glance

```
EliteDataCollector.UI/
├── Services/
│   ├── DashboardSettingsService.cs    ← Settings JSON I/O
│   ├── JournalDataService.cs          ← Listens to journal events
│   └── ContuberniumService.cs         ← Newsletter fetcher
├── ViewModels/
│   ├── DashboardViewModel.cs          ← Dashboard logic
│   ├── SettingsViewModel.cs           ← Settings logic
│   └── ModuleViewModels.cs            ← Module placeholders
├── Views/
│   ├── DashboardPage.xaml             ← Dashboard UI
│   ├── SettingsPage.xaml              ← Settings UI
│   └── [Module Pages]                 ← Module UIs
└── App.xaml.cs                        ← Initialization & DI
```

## Component Purposes

| Component | Purpose | File |
|-----------|---------|------|
| **Dashboard** | Display credits, location, merits, activity feed | `DashboardPage.xaml` + `DashboardViewModel.cs` |
| **Settings** | Toggle metrics, configure refresh rate | `SettingsPage.xaml` + `SettingsViewModel.cs` |
| **Navigation** | Left sidebar menu switching | `MainWindow.xaml` |
| **Journal Monitor** | Real-time game event tracking | `JournalDataService.cs` |
| **Supabase Sync** | Periodic data refresh (5-10 min) | `DashboardViewModel.cs` timer |
| **Newsletter** | Fetch from GitHub on 14th/28th hourly | `ContuberniumService.cs` |

## Key Classes & Their Responsibilities

### `DashboardViewModel`
```csharp
// Exposes observable properties for UI binding
Credits                     // Current player credits
CurrentLocation             // Current star system
CurrentStarport             // Current station
PowerplayMerits             // Current merits
RecentActivity              // ObservableCollection<JournalEventDisplay>

// Starts timer for Supabase refresh
InitializeAsync()           // Called when page loads

// User triggered refresh
RefreshNowCommand           // RelayCommand for refresh button
```

### `DashboardSettingsService`
```csharp
// Saves/loads dashboard settings from JSON
GetSettings()               // Read current settings
SaveSettings(settings)      // Persist to JSON

// Settings file location
%APPDATA%\EliteDangerousDataCollector\dashboard-settings.json
```

### `JournalDataService`
```csharp
// Real-time journal event parsing
CurrentCredits              // Latest balance
CurrentSystemName           // Latest location
CurrentStarport             // Latest port

// Subscribe to changes
event DataChanged           // Fires on journal update

// Initialize with journal monitor
Initialize(journalMonitor)  // Start listening
```

### `ContuberniumService`
```csharp
// Newsletter management
GetCachedContent()          // Read cached newsletter

// Automatic fetch on schedule
InitializeAsync()           // Start hourly checker

// Manual control
ManualRefreshAsync()        // Force fetch now
SetRepositoryUrl(url)       // Update GitHub URL

// Cache file location
%APPDATA%\EliteDangerousDataCollector\contubernium-cache.md
```

## Data Binding Examples

### In Code-Behind (C#)
```csharp
// Dashboard properties auto-update when ViewModel changes
ViewModel.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(ViewModel.Credits))
        CreditsValue.Text = ViewModel.Credits.ToString("N0");
};
```

### In XAML (Data Template)
```xaml
<ListView ItemsSource="{x:Bind ViewModel.RecentActivity, Mode=OneWay}">
    <ListView.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding EventType}" />
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```

## Testing Checklist

- [ ] Build succeeds (once XAML compiler issue fixed)
- [ ] App launches without errors
- [ ] Dashboard loads with placeholder data
- [ ] Navigation buttons switch pages
- [ ] Settings page loads current settings
- [ ] Save Settings persists to JSON file
- [ ] Reset Defaults resets UI
- [ ] Refresh button shows loading state
- [ ] Journal events update Dashboard in real-time
- [ ] Supabase timer fires every 5 minutes
- [ ] Contubernium shows cached newsletter
- [ ] Manual refresh fetches new newsletter

## Common Issues & Solutions

### Issue: Dashboard shows no data
**Solution**: 
- Check if game is running and journal exists
- Verify `JournalDataService` subscribed to `JournalMonitor`
- Check AppData logs at `%APPDATA%\EliteDangerousDataCollector\debug.log`

### Issue: Settings don't persist
**Solution**:
- Verify JSON file created at `%APPDATA%\EliteDangerousDataCollector\dashboard-settings.json`
- Check file permissions
- Ensure SaveSettings() is called after modifying properties

### Issue: Supabase data not refreshing
**Solution**:
- Verify Supabase URL/key in `appsettings.json`
- Check network connectivity
- Monitor timer in DashboardViewModel (default 5 min)

### Issue: Newsletter not fetching
**Solution**:
- Verify current date is 14th or 28th of month
- Check GitHub repo URL in `appsettings.json`
- Check network connectivity
- Verify cache file permissions

## Environment Setup

### Required
- Windows 10/11
- .NET 10 SDK
- Visual Studio 2022 or Rider

### Configuration Files

**appsettings.json** (in UI project root):
```json
{
  "Supabase": {
    "Url": "https://rrabnukibililqrckojh.supabase.co",
    "PublishableKey": "..."
  },
  "Contubernium": {
    "RepositoryUrl": "https://github.com/placeholder/contubernium"
  }
}
```

**dashboard-settings.json** (created automatically in AppData):
```json
{
  "version": 1,
  "lastModified": "2026-04-09T12:00:00Z",
  "displayMetrics": ["Credits", "CurrentLocation", "..."],
  "supabaseRefreshIntervalMinutes": 5,
  "contuberniumCheckEnabled": true
}
```

## Debugging Tips

### Enable Debug Output
```csharp
// In DashboardViewModel
_outputWriter?.WriteLine($"Dashboard initialized with {Credits} credits");
```

### View Logs
```powershell
# Real-time log location
cat %APPDATA%\EliteDangerousDataCollector\debug.log

# Watch for changes
Get-Content -Path <path> -Wait -Tail 20
```

### Check Event Flow
```csharp
// Subscribe to journal monitor in code-behind for debugging
_journalMonitor.JournalLineRead += (s, e) =>
{
    Debug.WriteLine($"Journal: {e.EventType}");
};
```

### Inspect Settings File
```powershell
$settingsPath = "$env:APPDATA\EliteDangerousDataCollector\dashboard-settings.json"
Get-Content $settingsPath | ConvertFrom-Json | Format-List
```

## MVVM Pattern Used

This project uses the **MVVM Community Toolkit** pattern:

```
View (XAML)
    ↓ (DataBinding)
ViewModel (ObservableObject)
    ↓ (Commands/Properties)
Model (Services)
    ↓ (Data)
```

**Key Classes**:
- `ObservableObject` - Base class for ViewModels with INotifyPropertyChanged
- `ObservableProperty` - Auto-generates property change notifications
- `RelayCommand` - Simplified command implementation
- `RelayCommand<T>` - Generic version for parameterized commands

## Performance Considerations

- **Journal Events**: Event-driven (no polling)
- **Supabase Sync**: Timer-based (default 5 min, adjustable)
- **UI Updates**: Batched via MainThread dispatcher
- **Newsletter Fetch**: Only on 14th/28th (hourly check)
- **Settings I/O**: Async to prevent UI blocking

## Security Notes

- Supabase credentials stored in `appsettings.json` (publishable key only)
- Settings saved locally in AppData (user's profile)
- No credentials cached or persisted
- All network traffic uses HTTPS

## Extending the GUI

### Add New Dashboard Metric
1. Add property to `DashboardViewModel`
2. Add UI element to `DashboardPage.xaml`
3. Parse from journal in `JournalDataService`
4. Add toggle to `SettingsPage` if user-configurable

### Add New Module Page
1. Create `XyzPage.xaml` (.cs) in Views/
2. Create `XyzViewModel.cs` in ViewModels/
3. Add navigation button to `MainWindow.xaml`
4. Register in `MainWindowViewModel`

### Change Refresh Interval
Edit `appsettings.json` or let user configure via Settings:
```csharp
SupabaseRefreshIntervalMinutes = 10;  // Default 5, range 1-60
```

### Change Newsletter URL
```csharp
// In App.xaml.cs or Settings
contuberniumService.SetRepositoryUrl("https://github.com/new/repo");
```

---

**For detailed architectural information, see `WINUI3_STATUS.md`**

