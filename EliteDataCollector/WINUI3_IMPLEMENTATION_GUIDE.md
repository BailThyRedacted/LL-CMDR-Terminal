# Elite Data Collector - WinUI 3 GUI Implementation Guide

## Current Status

The WinUI 3 GUI project structure has been created with:

### ✅ Completed

1. **Project Structure Created:**
   - `EliteDataCollector.UI/` - Main WinUI 3 application project
   - Proper csproj configuration with WinUI 3 packages
   - Integration with Core and Module projects

2. **Services Implemented:**
   - `DashboardSettingsService.cs` - Manages dashboard settings persistence in JSON
   - `JournalDataService.cs` - Real-time journal event monitoring and parsing
   - `ContuberniumService.cs` - Newsletter fetching on 14th/28th with caching

3. **ViewModels Created (MVVM):**
   - `MainWindowViewModel.cs` - Window management
   - `NavigationViewModel.cs` - Navigation state
   - `DashboardViewModel.cs` - Dashboard data binding and refresh logic
   - `SettingsViewModel.cs` - Settings persistence
   - `ColonizationViewModel.cs`, `BgsViewModel.cs`, `PowerplayViewModel.cs`, `ContuberniumViewModel.cs` - Module placeholders

4. **App Initialization:**
   - `App.xaml.cs` - Full DI setup, MainCore initialization, Module registration
   - `AppContext.cs` - Shared context for ViewModels
   - `Program.cs` - Entry point

5. **XAML Views (structure in place):**
   - MainWindow.xaml - Left navigation sidebar with 5 menu items
   - DashboardPage.xaml - Stats cards and activity list
   - SettingsPage.xaml - Customizable metrics toggles
   - Colonization/BGS/PowerPlay/Contubernium Pages - Placeholders

### ⚠️ Current Issue

**XAML Compilation Error:**
- XamlCompiler.exe exits with code 1 but provides no error details
- Issue appears to be environment/compatibility related with WindowsAppSDK 1.6.240923002
- Likely causes:
  - .NET 10 RC compatibility issue with XAML compiler
  - Missing platform SDK components
  - Namespace reference issues in complex project structure

## Solution Path Forward

### Option 1: Fix XAML Compilation (Recommended)
1. Upgrade or downgrade WindowsAppSDK version
2. Add diagnostic logging to XamlCompiler via MSBuild settings
3. Validate XAML namespace references
4. Check for .NET 10 RC specific issues

### Option 2: Code-First UI Approach
Skip XAML entirely and build UI programmatically in C#:
```csharp
var window = new MainWindow();
var rootGrid = new Grid();
var sidebar = CreateNavigation();
var contentArea = new Frame();
rootGrid.Children.Add(sidebar);
rootGrid.Children.Add(contentArea);
window.Content = rootGrid;
```

### Option 3: Incremental Build
1. Create minimal XAML (just App.xaml)
2. Verify it compiles
3. Add MainWindow.xaml
4. Incrementally add Pages

## File Structure Created

```
EliteDataCollector.UI/
├── App.xaml (.cs)
├── MainWindow.xaml (.cs)
├── Program.cs
├── AppContext.cs
├── appsettings.json
├── Services/
│   ├── DashboardSettingsService.cs
│   ├── JournalDataService.cs
│   └── ContuberniumService.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── NavigationViewModel.cs
│   ├── DashboardViewModel.cs
│   ├── SettingsViewModel.cs
│   └── ModuleViewModels.cs
└── Views/
    ├── DashboardPage.xaml (.cs)
    ├── SettingsPage.xaml (.cs)
    ├── ColonizationPage.xaml (.cs)
    ├── BgsPage.xaml (.cs)
    ├── PowerplayPage.xaml (.cs)
    └── ContuberniumPage.xaml (.cs)
```

## Architecture

### Data Flow
```
Journal Events → JournalMonitor (Core)
                    ↓
                JournalDataService (UI)
                    ↓
                DashboardViewModel
                    ↓
                DashboardPage.xaml

Supabase → RefreshTimer (5-10 min)
              ↓
         DashboardViewModel
              ↓
         DashboardPage.xaml

Settings → DashboardSettingsService (JSON)
              ↓
         SettingsViewModel
              ↓
         SettingsPage.xaml
         
GitHub Repo → ContuberniumService (14th/28th hourly)
                     ↓
                ContuberniumViewModel
                     ↓
                ContuberniumPage.xaml
```

### MVVM Pattern
- **View**: XAML files (UI markup)
- **ViewModel**: `*ViewModel.cs` classes with `ObservableObject` from MVVM Toolkit
- **Model**: Core services (JournalDataService, DashboardSettingsService, etc.)
- **Services**: DI-managed infrastructure (Supabase, Journal monitoring, etc.)

## Key Features Implemented

1. **Dashboard**
   - Real-time credits/location/merits display
   - Recent activity feed
   - Refresh button with manual trigger
   - Periodic Supabase sync (configurable 5-10 min)

2. **Settings**
   - Toggle metrics visibility (Credits, Location, Influence, Merits, Activity)
   - Configure Supabase refresh interval
   - Enable/disable Contubernium checks
   - Save/Reset to defaults
   - Settings stored in `%APPDATA%\EliteDangerousDataCollector\dashboard-settings.json`

3. **Contubernium**
   - Fetch from public GitHub repo on 14th & 28th of month
   - Hourly checks on those dates
   - Cache locally in AppData
   - Manual refresh button
   - Last-updated timestamp

4. **Navigation**
   - Left sidebar with 5 main sections
   - Frame-based page switching
   - Consistent styling (Discord-like dark theme)

## Next Steps

1. **Resolve XAML compilation issue**
   - Check build.binlog for details
   - Test with simpler XAML if needed
   - Consider updating SDK versions

2. **Wire Up Real Data**
   - Connect JournalDataService to actual journal monitoring
   - Test Supabase periodic refresh
   - Validate Contubernium fetch

3. **Testing**
   - Test navigation between pages
   - Test settings persistence
   - Test real-time journal updates
   - Test module data integration

4. **Polish**
   - Add loading indicators
   - Error handling/retry logic
   - Animations and transitions
   - Accessibility features

## Dashboard Settings JSON Format

```json
{
  "version": 1,
  "lastModified": "2026-04-09T12:00:00Z",
  "displayMetrics": ["Credits", "CurrentLocation", "FactionInfluence", "PowerplayMerits", "RecentActivity"],
  "supabaseRefreshIntervalMinutes": 5,
  "contuberniumCheckEnabled": true
}
```

## Important Notes

- All services use async/await for non-blocking operations
- Journal monitoring is event-driven (subscribed to JournalMonitor)
- Supabase refresh uses Timer to run periodically
- ContuberniumService uses hourly checks but only fetches on 14th/28th
- Settings are version-controlled for future schema migrations
- All ViewModels inherit from `ObservableObject` for property change notifications

## Debug Commands

```powershell
# Build verbose output
dotnet build -v diag

# View binary log (requires MsBuildStructuredLog viewer)
# Available at: https://msbuildlog.com/

# Clean rebuild
dotnet clean
dotnet build

# Run project (once XAML issue fixed)
dotnet run
```

## Known Limitations

- XAML compilation currently failing (see "Current Issue" above)
- Module data panels are placeholder UI only
- Newsletter body display is plain text (could be Markdown rendered)
- No image/media support on dashboard cards yet
- Settings only local (not synced to Supabase)

## Configuration

Edit `appsettings.json` in the UI project:
```json
{
  "Supabase": {
    "Url": "...",
    "PublishableKey": "..."
  },
  "Contubernium": {
    "RepositoryUrl": "https://github.com/user/contubernium"
  }
}
```

Replace placeholder GitHub URL when you have the actual Contubernium repo link.

