# Changelog

All notable changes to Elite Data Collector are documented here.

---

## [Unreleased]

---

## [1.0.0] — 2026-04-10

### Added
- **WinUI 3 GUI** (`EliteDataCollector.UI`) — full desktop application with sidebar navigation, Dashboard, Settings, and module pages
  - MVVM architecture using CommunityToolkit.Mvvm 8.2.2
  - Real-time journal event display via `JournalDataService`
  - Periodic Supabase sync (configurable 1–60 min) via `DashboardViewModel`
  - Dashboard settings persistence to `%APPDATA%\EliteDangerousDataCollector\dashboard-settings.json`
  - Contubernium newsletter fetching on 14th/28th of each month with local cache
- **PowerplayModule** — tracks PowerPlay merits and power allegiance from journal events
- **Auto-update checking** — compares local version against latest GitHub release on startup
- **Idle detection** — defers update installation until the player is idle
- **WiX MSI installer** (`EliteDataCollector.Setup`) — `publish.bat` builds a self-contained `EliteDataCollector.UI.exe` and packages it into an MSI
- **KeyGen utility** — admin tool for generating `KEY-CMDR*` authentication keys

### Changed
- **Migrated from .NET 10 → .NET 8** across all projects (`net8.0-windows` / `net8.0-windows10.0.19041.0`)
- **WindowsAppSDK upgraded** from 1.6.240923002 → **1.8.260317003** (resolves XAML compilation issue)
- **Primary entry point** changed from `EliteDataCollector.Host` (console) to `EliteDataCollector.UI` (GUI)
- `CompositeOutputWriter` added to `Core` — writes to both console and file simultaneously
- Solution file (`EliteDataCollector.slnx`) updated to include all projects: UI, Setup, KeyGen, all three Modules
- Version metadata (`1.0.0.0`) added to Core, ColonizationModule, ExplorationModule, and PowerplayModule

### Fixed
- XAML compilation (XamlCompiler exit code 1) — resolved by upgrading to WindowsAppSDK 1.8 on .NET 8

---

## [v2.2] — 2026-03-xx

### Fixed
- Installer fixes

## [v2.1] — 2026-03-xx

### Added
- WiX MSI installer created

## [v2] — 2026-03-xx

### Added
- Supabase integration with `ll_presence` filtering
- Update checking and downloading
- Idle detection

## [v1.1.1] — 2026-03-xx

### Changed
- Docs updated for v1.1; auth service references removed

## [v1.1] — 2026-03-xx

### Fixed
- CAPI and Inara auth
- Added log levels
- Added `CompositeOutputWriter`

## [v1] — 2026-03-xx

### Added
- PowerplayModule (enabled by default)

---

[Unreleased]: https://github.com/BailThyRedacted/LL-CMDR-Terminal/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/BailThyRedacted/LL-CMDR-Terminal/releases/tag/v1.0.0

