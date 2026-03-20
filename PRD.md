I've updated the PRD to **version 2.1** with the invisible operation and auto-launch requirements. All new additions are marked in bold.

---

# Product Requirements Document (PRD)  
## Elite Dangerous Colonization Data Collector  

**Version:** 2.1  
**Date:** March 20, 2026  
**Status:** Draft  

---

## 1. Introduction  

The **Elite Dangerous Colonization Data Collector** is a lightweight, modular application designed to capture real‑time colonization data from the player’s local journal files and the Frontier Companion API (CAPI). The application stores this data in a Supabase PostgreSQL database, focusing exclusively on colonization‑relevant information: system name, background simulation (BGS) and Powerplay state, structures present in the system, and the construction progress of those structures.  

**The application runs invisibly in the background with no visible console window, automatically detecting when Elite Dangerous launches and beginning data collection without user intervention. It remains resident between game sessions, ensuring seamless data capture for squadron members.**

The app is built with scalability in mind: a future graphical user interface (GUI) can be added without altering the core logic, and new gameplay‑specific modules (e.g., exploration, trading) can be introduced later as downloadable plug‑ins.  

To ensure the app is used only by authorized members of **Lavigny’s Legion**, the application verifies the commander’s squadron membership via the Frontier CAPI. Only verified members can upload data to the shared database.  

---

## 2. Purpose  

The primary purpose is to **automatically collect colonization data** as members of Lavigny’s Legion play Elite Dangerous and upload it to a central database. This data will be used by the squadron to track colonization progress, monitor system influence, and coordinate construction efforts.  

**By operating invisibly and launching automatically with the game, the app requires no user interaction beyond initial setup, ensuring consistent data collection without disrupting gameplay.**

By using only first‑party data (journal + CAPI), the app respects user privacy and avoids reliance on third‑party services that may have rate limits or data gaps.  

---

## 3. Scope  

### 3.1 In Scope  
- Real‑time monitoring of the Elite Dangerous journal folder on Windows.  
- Parsing of journal events relevant to **colonization** (e.g., system entry, structure placement, construction updates).  
- Authentication with Frontier’s CAPI using OAuth2 PKCE and secure token storage.  
- Verification that the authenticated commander is a member of the “Lavigny’s Legion” squadron.  
- Periodic fetching of the commander’s profile (every hour) to refresh squadron membership and other relevant data.  
- Uploading colonization data to a Supabase database, including:  
  - System name  
  - Timestamp of last visit/update  
  - BGS state (faction influences, controlling faction)  
  - Powerplay state (controlling power, state)  
  - List of structures (name, type, construction progress percentage)  
- **Invisible operation with no visible console window.**  
- **Automatic detection of Elite Dangerous process launch and exit.**  
- **Background operation that persists between game sessions.**  
- **Optional system tray icon for user interaction (status, logs, exit).**  
- A modular architecture allowing new gameplay loops to be added as plug‑ins (e.g., exploration, mining).  
- Support for future GUI (WPF, MAUI) without modifying core modules.  
- Automatic update checking on startup using the GitHub API, with user confirmation before downloading/installing updates.  

### 3.2 Out of Scope  
- Collection of data unrelated to colonization (e.g., trading transactions, exploration discoveries, mining yields, personal cargo).  
- Global data collection from third‑party APIs (EDSM, Inara, Spansh).  
- Historical data migration – only new data captured after app installation will be uploaded.  
- Mobile or web versions – the app is Windows‑only.  
- Offline caching of large datasets – only minimal local state (e.g., last processed system) is stored.  

---

## 4. Functional Requirements  

| ID   | Requirement                                                                                     | Priority |
|------|-------------------------------------------------------------------------------------------------|----------|
| FR‑1 | The app shall monitor the Elite Dangerous journal folder for new or modified `.log` files.      | High     |
| FR‑2 | The app shall read only new lines appended to the active journal file, using byte‑offset tracking. | High     |
| FR‑3 | The app shall parse each journal line as JSON and route it to interested modules based on the event type. | High     |
| FR‑4 | The app shall support OAuth2 PKCE authentication with Frontier’s CAPI, including token refresh. | High     |
| FR‑5 | The app shall store access and refresh tokens securely using Windows Data Protection API (DPAPI). | High     |
| FR‑6 | The app shall fetch the commander’s profile from CAPI at startup and then every hour thereafter. | High     |
| FR‑7 | The app shall verify that the commander’s squadron matches “Lavigny’s Legion” (case‑insensitive) on first authentication and periodically every 24 hours. | High     |
| FR‑8 | If squadron verification fails, the app shall prevent any data uploads and display a clear error message. | High     |
| **FR‑9** | **The Colonization Module (built‑in) shall, on each `FSDJump` or `Location` event, capture:**<br> - System name<br> - Timestamp<br> - BGS data (factions, influences, states, controlling faction)<br> - Powerplay data (controlling power, state, if present)<br> **and upload to Supabase.** | **High** |
| **FR‑10** | **The Colonization Module shall detect colonization‑related journal events (e.g., `ColonisationBond`, `StructureConstruction`, `StructureDeployed`) as they become available, and update the system’s structures and construction progress in Supabase.** | **High** |
| **FR‑11** | **The app shall provide an optional Inara upload service that, if enabled by the user, sends colonization data to Inara via its public API (respecting rate limits).** | **Medium** |
| **FR‑12** | **The app shall check for new releases on GitHub at startup, compare the local version with the latest release, and if a newer version exists, prompt the user with an option to download and install it. The update process must be non‑destructive and preserve user configuration.** | **High** |
| FR‑13 | The app shall respect the CAPI rate limit (max 1 request per second average). Profile polling shall be every hour. | High |
| **FR‑14** | **The application shall run with no visible console window. It shall operate as a background process, with an optional system tray icon for user interaction (view status, access logs, exit).** | **High** |
| **FR‑15** | **The application shall automatically detect when Elite Dangerous is launched (via process monitoring of `EliteDangerous64.exe`) and begin data collection without user intervention.** | **High** |
| **FR‑16** | **The application shall continue running in the background after Elite Dangerous exits, waiting for the next game session, unless explicitly terminated by the user.** | **High** |
| **FR‑17** | **On first run (or if authentication is required), the application shall display a Windows notification or system tray balloon prompt guiding the user to complete CAPI authentication via their browser. After authentication, a confirmation notification shall be shown.** | **Medium** |

---

## 5. Non‑Functional Requirements  

| ID   | Requirement                                                                                     | Priority |
|------|-------------------------------------------------------------------------------------------------|----------|
| NFR‑1 | The app shall have minimal CPU and memory footprint (idle < 50 MB RAM, < 0.5% CPU).             | High     |
| **NFR‑2** | **When idle (Elite Dangerous not running), the application shall consume < 10 MB RAM and < 0.1% CPU.** | **High** |
| NFR‑3 | The app shall be self‑contained – no installation required; runs from a single executable.       | High     |
| NFR‑4 | The app shall handle journal file rotation and temporary file locks gracefully.                 | High     |
| NFR‑5 | The app shall support asynchronous I/O to avoid blocking the main thread.                       | High     |
| NFR‑6 | All external HTTP calls shall have timeouts (10 seconds) and retry logic (3 attempts) with exponential backoff. | Medium   |
| NFR‑7 | The app shall be compatible with Windows 10/11 (64‑bit).                                        | High     |
| NFR‑8 | The codebase shall be well‑documented and follow SOLID principles to facilitate future GUI integration. | Medium   |
| NFR‑9 | Module interface shall be stable; changes to core should not break existing modules.            | High     |
| NFR‑10 | The app shall respect Frontier’s CAPI rate limits (max 1 request per second average).           | High     |
| NFR‑11 | Inara API calls shall respect Inara’s rate limits (one request per 10 seconds per API key) and be queued if necessary. | Medium   |
| **NFR‑12** | **The application shall monitor for Elite Dangerous process every 5 seconds to balance responsiveness with CPU usage.** | **High** |

---

## 6. System Architecture  

The application is divided into three layers:  

### 6.1 Core Library (`EliteDataCollector.Core`)  
- **JournalMonitor** – watches the journal folder, reads new lines, and raises events.  
- **GameProcessMonitor** – monitors for `EliteDangerous64.exe` process and raises start/exit events.  
- **CapiAuth** – handles OAuth2 PKCE flow and token storage.  
- **CapiClient** – fetches profile data using stored tokens.  
- **SquadronValidator** – checks the profile’s squadron field against “Lavigny’s Legion” and maintains verification state.  
- **SupabaseClientWrapper** – provides an authenticated Supabase client for colonization data.  
- **InaraClient** – (optional) handles Inara API calls, with rate‑limit queuing.  
- **UpdateChecker** – checks GitHub releases, compares versions, and orchestrates download/installation.  
- **ModuleLoader** – discovers and loads modules from the `Modules` folder.  
- **IOutputWriter** – abstract output interface.  
- **Shared Models** – data classes used by core and modules.  

### 6.2 Windows Background Host (`EliteDataCollector.Host`)  
- **Windows Forms application with hidden main window.**  
- Implements `IOutputWriter` to write to internal log (and optionally display via system tray).  
- **System tray icon** with context menu for user interaction.  
- **Notification support** for Windows toast or balloon tips.  
- **Manages the lifecycle of core services** – starts monitoring when game launches, stops when game exits.  

### 6.3 Modules (Pluggable DLLs)  
Each module:  
- Implements `IGameLoopModule`.  
- Registers for journal events it cares about.  
- Uses injected services (`IOutputWriter`, `ISupabaseClient`, `IInaraClient`, etc.) to display info and upload data.  

### 6.4 Future GUI Host (`EliteDataCollector.GUI`)  
- WPF, MAUI, or WinForms application with visible window.  
- Implements `IOutputWriter` to write to a text control.  
- Shares same core library as background host.  

---

## 7. Module Specifications  

### 7.1 IGameLoopModule Interface  

```csharp
public interface IGameLoopModule
{
    string Name { get; }
    string Description { get; }

    Task InitializeAsync(IServiceProvider services);
    Task OnJournalLineAsync(string line, JsonDocument parsedEvent);
    Task OnCapiProfileAsync(JsonDocument profile);
    Task ShutdownAsync();
}
```

### 7.2 Colonization Module  

- **Purpose:** Collect and upload colonization data to Supabase.  
- **Journal events handled:**  
  - `FSDJump` – captures system name, BGS, Powerplay state when entering a system.  
  - `Location` – similar to `FSDJump`, used at login or when respawning.  
  - `ColonisationBond` (expected) – records a colonization bond earned (indicates construction progress).  
  - `StructureConstruction` (expected) – updates the progress of a specific structure.  
  - `StructureDeployed` (expected) – records that a structure has been placed.  
- **Logic:**  
  - Maintain a local cache of system data to avoid duplicate uploads within a short period (e.g., 5 minutes).  
  - On each system entry, extract BGS and Powerplay information from the journal event and upload to Supabase.  
  - For colonization‑specific events, update the corresponding system’s structure list and construction progress.  
- **Data to upload:**  
  - System name  
  - Timestamp of event  
  - BGS snapshot (factions, influences, states, controlling faction)  
  - Powerplay snapshot (controlling power, state)  
  - List of structures (name, type, current construction progress)  
- **Configuration:** `ColonizationModule.json` – user can enable/disable Inara upload, set local cache duration.  

---

## 8. Data Flow  

1. **Application starts** (at Windows login via Startup folder or manually).  
   - Hidden window loads, system tray icon appears.  
   - Core services initialize, but data collection is paused.  
   - GameProcessMonitor begins checking for Elite Dangerous every 5 seconds.  

2. **User launches Elite Dangerous.**  
   - GameProcessMonitor detects `EliteDangerous64.exe` process.  
   - Raises `GameStarted` event.  
   - JournalMonitor starts watching journal folder.  
   - CapiClient refreshes profile (and squadron verification runs).  

3. **Data Collection (while game runs):**  
   - JournalMonitor raises events for new journal lines.  
   - Colonization Module processes relevant events and uploads to Supabase.  
   - Profile refresh occurs every hour.  

4. **User exits Elite Dangerous.**  
   - GameProcessMonitor detects process exit.  
   - Raises `GameExited` event.  
   - JournalMonitor stops watching.  
   - Data collection halts; app returns to idle state.  

5. **App remains running** – waiting for next game session.  

---

## 9. Security  

- **CAPI tokens** are stored using `ProtectedData` (DPAPI) with scope `CurrentUser`.  
- **Inara API key** is stored similarly, encrypted.  
- **Supabase connection** uses an anon key limited to the project.  
- **No sensitive data** (e.g., commander password) is stored or transmitted.  
- **OAuth2 PKCE** prevents authorization code interception.  
- **GitHub API** uses public endpoints; update download is verified via SHA‑256.  
- **Squadron membership check** acts as an access control layer, ensuring only Lavigny’s Legion members can contribute data.  

---

## 10. Deployment & Configuration  

- The application is distributed as a single `.exe` (self‑contained) with a `Modules` folder.  
- Users place the Colonization Module DLL (and any future modules) into `Modules`.  
- **First‑run experience:**  
  - User runs the .exe manually (or via installer).  
  - App launches hidden, shows system tray icon, and displays a notification prompting authentication.  
  - Browser opens for CAPI OAuth2 approval.  
  - After authentication, squadron membership is verified; if not a member, the app shows error notification and exits.  
- **Startup integration:**  
  - User is prompted to add the app to Windows Startup (or does so manually via `shell:startup`).  
  - App will then run silently at boot.  
- Configuration: `appsettings.json` contains Supabase URL and key, and CAPI client ID.  
- Inara configuration: encrypted file or Windows Credential Manager.  
- Update behavior: on startup, checks GitHub and prompts user (via notification or tray balloon).  
- The app is intended to run continuously; users can exit via tray icon context menu.  

---

## 11. Testing  

| Test Area            | Approach                                                                 |
|----------------------|--------------------------------------------------------------------------|
| **Invisible Operation** | **Launch app; verify no console window appears; system tray icon is present.** |
| **Process Monitoring** | **Launch Elite Dangerous; verify data collection starts within 5 seconds. Exit game; verify collection stops.** |
| **Startup Integration** | **Add to Startup folder; reboot; verify app runs and detects game.** |
| Journal Monitoring   | Simulate journal writes, test file rotation, seek position recovery.     |
| OAuth2 Flow          | Manual testing with real Frontier credentials; mock HTTP for unit tests. |
| Squadron Validation  | Mock CAPI profile responses with different squadron names; verify access control. |
| Module Loading       | Load sample modules with known behavior; verify service injection.       |
| Supabase Integration | Use a test Supabase project; verify upserts and schema compatibility.    |
| Colonization Module  | Simulate `FSDJump` and colonization events; verify uploads.              |
| Inara Integration    | Mock Inara API to test request queuing and rate limiting.                |
| Update Checker       | Simulate different version scenarios; test download/installation.        |
| **Notifications**    | **Verify toast/balloon appears on first run and when updates available.** |
| Performance          | Run for extended periods (8+ hours) with and without game; monitor memory/CPU. |

---

## 12. Milestones  

| Milestone               | Description                                                                   | Estimated Date |
|-------------------------|-------------------------------------------------------------------------------|----------------|
| **M1: Core MVP**        | Journal monitoring, basic authentication.                                      | Week 1         |
| **M2: CAPI Integration**| OAuth2, token storage, profile fetch.                                          | Week 2         |
| **M3: Squadron Check**  | Profile parsing, squadron validation, access control.                          | Week 3         |
| **M4: Module Framework**| Interface defined, ModuleLoader, IOutputWriter.                                | Week 4         |
| **M5: Colonization Module** | FSDJump/Location handling, Supabase upload.                                | Week 5         |
| **M6: Colonization Events** | Support for structure‑related journal events, progress tracking.            | Week 6         |
| **M7: Invisible Operation** | Windows Forms hidden host, system tray, notifications.                       | Week 7         |
| **M8: Process Monitoring** | Game process detection, auto start/stop of collection.                       | Week 8         |
| **M9: Inara Integration** | Inara client, rate‑limited queuing.                                          | Week 9         |
| **M10: Updates & Polish** | Update checker, error handling, documentation.                               | Week 10        |
| **M11: Release v1.0**   | Binary distribution, user guide, startup integration.                         | Week 11        |

---

## 13. Risks & Mitigations  

| Risk                                                      | Mitigation                                                                                       |
|-----------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| Frontier changes CAPI or journal format unexpectedly.     | Monitor official forums; design parser to be robust (ignore unknown fields).                     |
| Supabase rate limits exceeded.                            | Batch uploads; implement retries with exponential backoff.                                       |
| Token refresh failure leads to authentication loss.       | Store refresh token; retry with backoff; prompt user to re‑authenticate after repeated failures. |
| File lock on journal prevents reading.                    | Use `FileShare.ReadWrite` when opening; retry after short delay.                                 |
| Modules cause performance degradation.                    | Isolate modules in separate tasks; enforce timeouts for module methods.                          |
| Inara API key exposure.                                   | Encrypt key using DPAPI; never store in plain text.                                              |
| Inara rate limits cause data loss.                        | Implement a queue with 10‑second spacing between requests; log failures.                         |
| Update process corrupts user configuration.               | Download new version to a temporary folder, then copy over existing files after backup.          |
| User declines update but later encounters a bug.          | Provide an “ignore this version” option; allow manual check from tray menu.                      |
| Squadron membership check false positive due to API error.| Implement retries with backoff; after three consecutive failures, treat as “unauthorised”.        |
| User leaves squadron after authorisation.                 | Periodic re‑checks (every 24 hours) will detect the change and disable uploads.                  |
| Colonization journal events are not yet documented.       | Keep module flexible; log unknown events and allow manual mapping as Frontier releases details.   |
| **Process monitoring fails to detect game launch.**       | **Fallback to journal folder monitoring as secondary trigger.**                                  |
| **User accidentally exits app via tray menu.**            | **Provide option to restart on next boot; clearly label exit option.**                           |

---

## 14. Glossary  

| Term          | Definition                                                                                       |
|---------------|--------------------------------------------------------------------------------------------------|
| CAPI          | Frontier’s Companion API – provides profile and fleet carrier data.                              |
| Journal       | Text files in `Saved Games\Frontier Developments\Elite Dangerous` containing gameplay events.    |
| PKCE          | Proof Key for Code Exchange – OAuth2 extension for public clients.                               |
| Supabase      | Open‑source Firebase alternative; provides PostgreSQL database and REST API.                     |
| BGS           | Background Simulation – faction influence and system state.                                      |
| Inara         | Community‑run website (inara.cz) that aggregates Elite Dangerous data.                           |
| GitHub        | Platform hosting the source code; used for release distribution.                                 |
| Squadron      | A group of players (e.g., Lavigny’s Legion) in Elite Dangerous.                                  |
| Colonization  | The gameplay feature introduced in 2024 allowing players to build structures and expand the Bubble. |
| **System Tray** | **Area of the Windows taskbar where background applications can place icons.**                  |

---

## 15. Appendices  

- **Appendix A:** Supabase Table Schemas (Colonization)  
  - `systems` – id, name, last_updated, controlling_faction, power, power_state  
  - `faction_influences` – system_id, faction_name, influence, state, timestamp  
  - `structures` – system_id, structure_name, structure_type, progress_percent, last_updated  
- **Appendix B:** Journal Event Documentation Reference  
- **Appendix C:** Module Development Guide  
- **Appendix D:** Inara API Integration Guide  
- **Appendix E:** Update Mechanism Technical Specification  
- **Appendix F:** Squadron Verification Flow  
- **Appendix G:** Process Monitoring & Invisible Operation Implementation Guide  

---

**Approvals**  

| Role            | Name          | Signature | Date       |
|-----------------|---------------|-----------|------------|
| Product Owner   |               |           |            |
| Technical Lead  |               |           |            |

---

*This document is subject to change as the project evolves.*