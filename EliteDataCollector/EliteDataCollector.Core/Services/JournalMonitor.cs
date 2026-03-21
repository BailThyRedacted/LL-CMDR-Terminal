using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Monitors the Elite Dangerous journal folder for new events and raises JournalLineRead events.
    ///
    /// Design Decision: Separate implementation allows us to:
    /// - Test MainCore without requiring Elite Dangerous to be installed
    /// - Swap different implementations (e.g., FileSystemWatcher vs. polling)
    /// - Reuse in multiple contexts (console app, GUI, service)
    ///
    /// How it works:
    /// 1. On StartAsync(), discovers the active journal file (Journal.YYYYMMDD_HHMMSS.log)
    /// 2. Uses FileSystemWatcher to detect when game writes new events to the journal
    /// 3. Reads new lines from the journal file, parsing each as JSON
    /// 4. Raises JournalLineRead event for important events (FSDJump, Location, Structure*, ColonisationBond, etc.)
    /// 5. Persists byte offsets to resume from the same position after app restart (no duplicate events)
    /// 6. Handles file locks gracefully with retry logic
    /// 7. Detects file rotation when game restarts and switches to new journal file
    /// </summary>
    public class JournalMonitor : IJournalMonitor
    {
        // ========== CONSTANTS ==========

        /// <summary>Relative path to journal folder from user's Documents</summary>
        private const string JOURNAL_FOLDER_RELATIVE = @"Saved Games\Frontier Developments\Elite Dangerous\Logs";

        /// <summary>Folder to store persistent state (byte offsets, etc.)</summary>
        private const string OFFSET_STORAGE_FOLDER = "EliteDangerousDataCollector";

        /// <summary>File name for storing journal offsets</summary>
        private const string OFFSET_STORAGE_FILE = "journal_offsets.json";

        /// <summary>Maximum retry attempts when file is locked by game</summary>
        private const int FILE_LOCK_RETRY_COUNT = 3;

        /// <summary>Milliseconds to wait between file lock retry attempts</summary>
        private const int FILE_LOCK_RETRY_DELAY_MS = 100;

        /// <summary>
        /// Important event types to raise as JournalLineRead events.
        /// All other events are silently skipped (e.g., Music, Dashboard, MusicalState).
        /// This reduces event volume by ~80% and keeps logs clean.
        /// </summary>
        private static readonly HashSet<string> IMPORTANT_EVENTS = new(StringComparer.Ordinal)
        {
            "FSDJump",           // System jump (affects faction influence)
            "Location",          // Station or surface arrival
            "StructureBuy",      // Construction project started
            "StructureSell",     // Construction project cancelled
            "StructureRepair",   // Construction project repaired
            "StructureTransfer", // Construction project transferred
            "ColonisationBond",  // Colonization data event
            "MissionAccepted",   // Squadron mission activity
            "MissionCompleted",
            "MissionFailed",
            "CargoDepot",        // Industrial commodities
            "TradingData"        // Trading activity
        };

        // ========== CONFIGURATION FIELDS (Injected) ==========

        /// <summary>Absolute path to the Elite Dangerous journal folder</summary>
        private readonly string _journalFolderPath;

        /// <summary>Optional output writer for logging operations (can be null for silent mode)</summary>
        private readonly IOutputWriter? _outputWriter;

        // ========== STATE FIELDS ==========

        /// <summary>FileSystemWatcher that detects when game writes to journal file</summary>
        private FileSystemWatcher? _fileWatcher;

        /// <summary>Token source for signaling monitoring loop to stop</summary>
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>Background task that processes journal file changes</summary>
        private Task? _monitoringTask;

        /// <summary>Full path to the currently monitored journal file</summary>
        private string? _currentJournalFile;

        /// <summary>Dictionary mapping journal filenames to their last-read byte offsets</summary>
        private Dictionary<string, (long offset, DateTime lastRead)> _offsets = new();

        /// <summary>Byte offset for the current file (how far we've read)</summary>
        private long _currentByteOffset = 0;

        /// <summary>Tracks whether we're currently monitoring (prevents duplicate StartAsync() calls)</summary>
        private bool _isMonitoring = false;

        // ========== EVENTS ==========

        /// <summary>
        /// Raised when a new journal line is read and parsed as an important event.
        /// Contains: raw line text, parsed JSON, and event type string.
        /// </summary>
        public event EventHandler<JournalLineEventArgs>? JournalLineRead;

        // ========== CONSTRUCTOR ==========

        /// <summary>
        /// Creates a new JournalMonitor instance.
        ///
        /// Teaching: This demonstrates Dependency Injection pattern:
        /// - journalFolderPath: Parameter allows testing with custom paths or defaults to real game folder
        /// - outputWriter: Optional, allows silent mode (null) or logging (ConsoleOutputWriter, file writer, etc.)
        ///
        /// Why optional parameters?
        /// - Flexibility: Users can provide custom values or use sensible defaults
        /// - Testing: Easy to inject mocks or custom implementations
        /// - Graceful degradation: Can work without output writer (silent mode)
        /// </summary>
        /// <param name="journalFolderPath">
        /// Optional path to journal folder. If null, uses standard Elite Dangerous location:
        /// %USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\Logs\
        /// </param>
        /// <param name="outputWriter">Optional output writer for logging (null = silent mode)</param>
        /// <exception cref="ArgumentException">Thrown if provided path doesn't exist</exception>
        public JournalMonitor(string? journalFolderPath = null, IOutputWriter? outputWriter = null)
        {
            // If no path provided, construct default path to Elite Dangerous journal folder
            if (string.IsNullOrWhiteSpace(journalFolderPath))
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _journalFolderPath = Path.Combine(userProfile, JOURNAL_FOLDER_RELATIVE);
            }
            else
            {
                _journalFolderPath = journalFolderPath;
            }

            // Validate that the journal folder exists
            // Why fail-fast? If journal folder doesn't exist, we can't proceed, so throw immediately
            // rather than silently failing later during StartAsync()
            if (!Directory.Exists(_journalFolderPath))
            {
                throw new ArgumentException(
                    $"Journal folder does not exist: {_journalFolderPath}\n" +
                    $"Are you sure Elite Dangerous has been launched at least once?",
                    nameof(journalFolderPath));
            }

            _outputWriter = outputWriter;

            // Load previously saved offsets from persistent storage
            // This allows us to resume from the same position after app restart
            LoadOffsets();

            _outputWriter?.WriteLine($"JournalMonitor initialized. Monitoring folder: {_journalFolderPath}");
        }

        // ========== LIFECYCLE METHODS ==========

        /// <summary>
        /// Begins monitoring the journal folder for new events.
        ///
        /// Teaching: Idempotent pattern - safe to call multiple times
        /// - First call: Sets up FileSystemWatcher and starts monitoring thread
        /// - Subsequent calls: Returns immediately without error (safe)
        ///
        /// This pattern is crucial for event handlers - allows calling StartAsync()
        /// even if already started, without causing duplicate monitoring.
        ///
        /// Why async? Because finding the journal file and starting the monitoring loop
        /// should not block the caller. The monitoring happens on a background thread.
        /// </summary>
        public async Task StartAsync()
        {
            // Idempotent: if already monitoring, return immediately
            if (_isMonitoring)
            {
                _outputWriter?.WriteLine("JournalMonitor.StartAsync() called but already monitoring. Ignoring.");
                return;
            }

            _outputWriter?.WriteLine("JournalMonitor.StartAsync() starting...");

            // Create cancellation token for cleanly stopping the monitoring loop
            _cancellationTokenSource = new CancellationTokenSource();

            // Find the active journal file (most recently modified Journal.*.log file)
            _currentJournalFile = FindLatestJournalFile();

            if (_currentJournalFile == null)
            {
                // No journal file found yet (Elite Dangerous not launched?)
                _outputWriter?.WriteLine("No journal file found. Waiting for game launch...");
                _currentJournalFile = null;
                _currentByteOffset = 0;
            }
            else
            {
                // Journal file found - restore previous byte offset if available
                string fileName = Path.GetFileName(_currentJournalFile);
                if (_offsets.TryGetValue(fileName, out var offsetData))
                {
                    _currentByteOffset = offsetData.Item1;
                    _outputWriter?.WriteLine($"Resuming from offset {offsetData.Item1} in {fileName}");
                }
                else
                {
                    _currentByteOffset = 0;
                    _outputWriter?.WriteLine($"Starting fresh from {fileName}");
                }
            }

            // Set up FileSystemWatcher to detect when game writes journal events
            // FileSystemWatcher is event-driven, so we get notified immediately when files change
            _fileWatcher = new FileSystemWatcher(_journalFolderPath)
            {
                Filter = "Journal*.log",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            // Subscribe to file change events
            _fileWatcher.Changed += OnJournalFileChanged;
            _fileWatcher.Created += OnJournalFileChanged;

            _outputWriter?.WriteLine("FileSystemWatcher set up. Watching for journal changes...");

            // Start the monitoring loop on a background thread
            // This thread will process journal events raised by FileSystemWatcher
            _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

            _isMonitoring = true;
            _outputWriter?.WriteLine("JournalMonitor started successfully.");
        }

        /// <summary>
        /// Stops monitoring the journal folder.
        ///
        /// Teaching: Graceful shutdown pattern
        /// - Never throws exceptions (always succeeds)
        /// - Cleans up file watchers
        /// - Saves current byte offset for next startup
        /// - Waits for monitoring thread to finish
        ///
        /// Why never throw? Because StopAsync() is often called during shutdown or error handling.
        /// If it throws, it can hide the original error or cause cascading failures.
        /// Better to log gracefully and continue shutdown.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isMonitoring)
            {
                _outputWriter?.WriteLine("JournalMonitor.StopAsync() called but not monitoring. Ignoring.");
                return;
            }

            _outputWriter?.WriteLine("JournalMonitor.StopAsync() shutting down...");

            try
            {
                // Signal the monitoring loop to stop
                _cancellationTokenSource?.Cancel();

                // Wait for the monitoring loop to finish (with timeout)
                if (_monitoringTask != null)
                {
                    await _monitoringTask.ConfigureAwait(false);
                }

                // Dispose FileSystemWatcher (releases OS resources)
                _fileWatcher?.Dispose();

                // Save current byte offset to persistent storage
                SaveOffsets();

                _isMonitoring = false;
                _outputWriter?.WriteLine("JournalMonitor stopped successfully.");
            }
            catch (Exception ex)
            {
                // Log but don't throw - we want shutdown to succeed even if cleanup fails
                _outputWriter?.WriteLine($"Error during JournalMonitor.StopAsync(): {ex.Message}");
            }
        }

        // ========== PRIVATE METHODS ==========

        /// <summary>
        /// Background thread that monitors for journal file changes and processes new events.
        ///
        /// Teaching: Event-driven file monitoring
        /// - Waits for FileSystemWatcher.Changed events
        /// - When file changes, reads new lines that have been added
        /// - Parses each line as JSON event
        /// - Raises JournalLineRead event for important events only
        /// - Detects file rotation (game restart creates new journal file)
        /// - Continues running until cancellation token is signaled
        ///
        /// Why try-catch? Because file I/O can fail (locked files, missing files, etc.)
        /// We catch exceptions and continue, so one error doesn't stop monitoring.
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            _outputWriter?.WriteLine("MonitoringLoop started.");

            try
            {
                // Main monitoring loop - continues until StopAsync() signals cancellation
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check if current journal file has rotated (game restart)
                        string? latestFile = FindLatestJournalFile();

                        if (latestFile == null)
                        {
                            // No journal file found yet - wait and retry
                            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        // If journal file changed, reset offset for new file
                        if (_currentJournalFile != latestFile)
                        {
                            _outputWriter?.WriteLine($"Journal file rotated from {Path.GetFileName(_currentJournalFile)} to {Path.GetFileName(latestFile)}");
                            _currentJournalFile = latestFile;

                            // Check if we have a saved offset for this file
                            string fileName = Path.GetFileName(_currentJournalFile);
                            if (_offsets.TryGetValue(fileName, out var offsetData))
                            {
                                _currentByteOffset = offsetData.Item1;
                                _outputWriter?.WriteLine($"Resuming new file from offset {offsetData.Item1}");
                            }
                            else
                            {
                                _currentByteOffset = 0;
                                _outputWriter?.WriteLine("Starting new file from beginning");
                            }
                        }

                        // Read new lines from journal file
                        await ReadNewLines().ConfigureAwait(false);

                        // Wait a bit before checking again (prevents busy-loop)
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested - exit loop gracefully
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue monitoring
                        _outputWriter?.WriteLine($"Error in MonitoringLoop: {ex.Message}");
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"Fatal error in MonitoringLoop: {ex.Message}");
            }

            _outputWriter?.WriteLine("MonitoringLoop exiting.");
        }

        /// <summary>
        /// Finds the most recent journal file in the journal folder.
        ///
        /// Journal files are named: Journal.YYYYMMDD_HHMMSS.log
        /// Example: Journal.20260321_141530.log
        ///
        /// Teaching: File discovery pattern
        /// - Scans folder for Journal*.log files
        /// - Parses timestamp from filename to find the most recent
        /// - Returns null if no files found (game hasn't been launched yet)
        ///
        /// Why parse timestamp? Because file creation time might not be reliable
        /// (files can be copied, timestamps modified), but the filename timestamp
        /// always reflects when Elite Dangerous created the file.
        /// </summary>
        private string? FindLatestJournalFile()
        {
            try
            {
                const string prefix = "Journal.";
                const string suffix = ".log";

                // Get all journal files in the folder
                var journalFiles = Directory.GetFiles(_journalFolderPath, "Journal*.log")
                    .Where(f =>
                    {
                        string fileName = Path.GetFileName(f);
                        // Validate format: Journal.YYYYMMDD_HHMMSS.log
                        return fileName.StartsWith(prefix, StringComparison.Ordinal) &&
                               fileName.EndsWith(suffix, StringComparison.Ordinal);
                    })
                    .OrderByDescending(f => f) // Sort descending (most recent first)
                    .FirstOrDefault();

                if (journalFiles != null)
                {
                    _outputWriter?.WriteLine($"Found journal file: {Path.GetFileName(journalFiles)}");
                }

                return journalFiles;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"Error finding journal file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads new lines from the current journal file starting from the saved byte offset.
        ///
        /// Teaching: Efficient file reading pattern with byte-offset tracking
        /// - Opens file in FileShare.ReadWrite mode (allows game to write while we read)
        /// - Seeks to the last known position (_currentByteOffset)
        /// - Reads all new lines added since last read
        /// - Updates _currentByteOffset after successful read
        /// - Handles file locks with retry logic (file might be locked by game)
        ///
        /// Why byte offsets? When the app restarts, we can resume from exactly where we left off.
        /// Without this, we'd either:
        /// a) Re-process all old events (duplicates)
        /// b) Miss events that happened while app was stopped
        /// With byte offsets, we get exactly the new events since last run.
        ///
        /// Why FileShare.ReadWrite? The game is constantly writing to the journal.
        /// We need to read while the game is writing. FileShare.ReadWrite allows this.
        /// </summary>
        private async Task ReadNewLines()
        {
            if (_currentJournalFile == null)
                return;

            try
            {
                // Try to open the file with retry logic (game might have it locked briefly)
                for (int attempt = 0; attempt < FILE_LOCK_RETRY_COUNT; attempt++)
                {
                    try
                    {
                        using var fileStream = File.Open(
                            _currentJournalFile,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite);

                        // Seek to the last known byte offset
                        fileStream.Seek(_currentByteOffset, SeekOrigin.Begin);

                        using var reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, false, 4096);

                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Try to parse this line as a JSON event
                            if (TryParseJsonLine(line, out string? eventType, out JsonDocument? parsedJson))
                            {
                                // Check if this is an important event type
                                if (eventType != null && IMPORTANT_EVENTS.Contains(eventType))
                                {
                                    // Raise event for this important event
                                    RaiseJournalLineRead(line, parsedJson, eventType);
                                }
                            }
                        }

                        // Update byte offset to current file position
                        _currentByteOffset = fileStream.Position;

                        // Success - no need to retry
                        break;
                    }
                    catch (IOException _) when (attempt < FILE_LOCK_RETRY_COUNT - 1)
                    {
                        // File is locked - wait and retry
                        _outputWriter?.WriteLine($"File locked (attempt {attempt + 1}/{FILE_LOCK_RETRY_COUNT}). Retrying...");
                        await Task.Delay(FILE_LOCK_RETRY_DELAY_MS).ConfigureAwait(false);
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // Journal file not found - probably game is not running
                _currentJournalFile = null;
                _currentByteOffset = 0;
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"Error reading journal file: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to parse a journal line as JSON and extract the event type.
        ///
        /// Teaching: JSON parsing pattern with error handling
        /// - Each journal line is a complete JSON object on a single line
        /// - Example: {"timestamp":"2026-03-21T14:15:30Z","event":"Location","SystemAddress":123,...}
        /// - We extract the "event" field to determine event type
        /// - If parsing fails, return false (line is not valid JSON)
        /// - Never throw - gracefully handle malformed lines
        ///
        /// Why try-parse instead of throwing? Because journal files can contain:
        /// - Empty lines (between events)
        /// - Partial lines (while game is writing)
        /// - Corrupted lines (rare but possible)
        /// We want to skip these and continue processing other valid lines.
        /// </summary>
        private bool TryParseJsonLine(string line, out string? eventType, out JsonDocument? parsedJson)
        {
            eventType = null;
            parsedJson = null;

            try
            {
                // Skip empty or whitespace-only lines
                if (string.IsNullOrWhiteSpace(line))
                    return false;

                // Parse the JSON line
                parsedJson = JsonDocument.Parse(line);
                var root = parsedJson.RootElement;

                // Extract event type from "event" field
                if (root.TryGetProperty("event", out var eventProperty))
                {
                    eventType = eventProperty.GetString();
                    return true;
                }

                return false;
            }
            catch (JsonException ex)
            {
                // Log parsing errors (rare, but good for debugging)
                _outputWriter?.WriteLine($"Failed to parse journal line: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads previously saved byte offsets from persistent storage.
        ///
        /// File format: JSON dictionary mapping journal filenames to their offsets + last read time
        /// Example:
        /// {
        ///   "Journal.20260320_141530.log": { "offset": 45678, "lastRead": "2026-03-20T14:15:30Z" },
        ///   "Journal.20260321_090000.log": { "offset": 12345, "lastRead": "2026-03-21T09:00:15Z" }
        /// }
        ///
        /// Teaching: Persistent application state pattern
        /// - Enables resume capability: restart app, continue from same position
        /// - Survives app crashes or user restarts
        /// - Handles file rotation: each journal file tracked separately
        /// - Auto-cleanup: doesn't restore offsets for files that no longer exist
        ///
        /// Why track multiple files? When the game restarts, it creates a new journal file.
        /// We track each file separately so we can resume both old and new files independently.
        /// Without this, restarting the game while the app is running would lose our position.
        /// </summary>
        private void LoadOffsets()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string offsetDir = Path.Combine(appDataPath, OFFSET_STORAGE_FOLDER);
                string offsetFile = Path.Combine(offsetDir, OFFSET_STORAGE_FILE);

                if (!File.Exists(offsetFile))
                {
                    _outputWriter?.WriteLine($"No saved offsets found at {offsetFile}");
                    return;
                }

                string json = File.ReadAllText(offsetFile);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, OffsetData>>(json);

                if (loaded == null)
                {
                    _outputWriter?.WriteLine("Failed to deserialize offsets");
                    return;
                }

                // Only restore offsets for files that still exist in the journal folder
                _offsets = new Dictionary<string, (long, DateTime)>();

                foreach (var kvp in loaded)
                {
                    string fileInJournal = Path.Combine(_journalFolderPath, kvp.Key);

                    if (File.Exists(fileInJournal))
                    {
                        _offsets[kvp.Key] = (kvp.Value.offset, DateTime.Parse(kvp.Value.lastRead));
                        _outputWriter?.WriteLine($"Loaded offset {kvp.Value.offset} for {kvp.Key}");
                    }
                }
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"Error loading offsets: {ex.Message}");
                _offsets = new Dictionary<string, (long, DateTime)>();
            }
        }

        /// <summary>
        /// Saves current byte offsets to persistent storage.
        ///
        /// This allows the app to resume from exactly where it left off after restart.
        /// Saves all tracked journal files (handles game restarts that create new files).
        ///
        /// Teaching: Persistence pattern - save state before shutdown
        /// - Called from StopAsync() before exiting
        /// - Ensures no loss of position information
        /// - Creates directory if it doesn't exist
        /// - Handles errors gracefully (logs but doesn't throw)
        ///
        /// Why save current file too? Even though we're stopping, the current file's
        /// offset might not have been saved yet. Saving it ensures next startup
        /// resumes from exactly the right position.
        /// </summary>
        private void SaveOffsets()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string offsetDir = Path.Combine(appDataPath, OFFSET_STORAGE_FOLDER);

                Directory.CreateDirectory(offsetDir);

                // Add current file's offset
                if (_currentJournalFile != null)
                {
                    string fileName = Path.GetFileName(_currentJournalFile);
                    _offsets[fileName] = (_currentByteOffset, DateTime.UtcNow);
                }

                // Convert to serializable format
                var toSerialize = new Dictionary<string, OffsetData>();
                foreach (var kvp in _offsets)
                {
                    toSerialize[kvp.Key] = new OffsetData
                    {
                        offset = kvp.Value.Item1,
                        lastRead = kvp.Value.Item2.ToIso8601String()
                    };
                }

                string json = JsonSerializer.Serialize(toSerialize, new JsonSerializerOptions { WriteIndented = true });
                string offsetFile = Path.Combine(offsetDir, OFFSET_STORAGE_FILE);

                File.WriteAllText(offsetFile, json);
                _outputWriter?.WriteLine($"Saved offsets to {offsetFile}");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"Error saving offsets: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the JournalLineRead event with the given event data.
        ///
        /// Teaching: Event-raising pattern
        /// - Called when we've parsed an important journal line
        /// - Passes the raw line, parsed JSON, and event type
        /// - MainCore subscribes to this event and processes/routes it
        /// - Uses null-conditional operator (?.) for safe invocation
        ///   (event might have no subscribers)
        ///
        /// Why pass both raw line and parsed JSON?
        /// - Raw line: useful for logging, debugging, audit trails
        /// - Parsed JSON: useful for extracting specific fields efficiently
        /// - Both together: maximum flexibility for event handlers
        /// </summary>
        private void RaiseJournalLineRead(string rawLine, JsonDocument parsedJson, string eventType)
        {
            try
            {
                var args = new JournalLineEventArgs
                {
                    RawLine = rawLine,
                    ParsedEvent = parsedJson,
                    EventType = eventType
                };
                JournalLineRead?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"Error raising JournalLineRead event: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles FileSystemWatcher.Changed event - called when journal file is modified.
        /// </summary>
        private void OnJournalFileChanged(object sender, FileSystemEventArgs e)
        {
            // FileSystemWatcher can fire multiple events for a single write
            // We let MonitoringLoop handle the actual reading (debounced by sleep)
            // This handler just ensures the monitoring loop gets a chance to run
        }

        // ========== HELPER CLASSES ==========

        /// <summary>
        /// Used for JSON serialization of offset data to persistent storage.
        /// Separates the runtime representation (tuple) from the serializable form.
        /// </summary>
        private class OffsetData
        {
            public long offset { get; set; }
            public string lastRead { get; set; } = "";
        }
    }

    /// <summary>
    /// Helper extensions for DateTime formatting.
    /// </summary>
    internal static class DateTimeExtensions
    {
        /// <summary>Converts DateTime to ISO 8601 string format.</summary>
        public static string ToIso8601String(this DateTime dt) => dt.ToString("O");
    }
}
