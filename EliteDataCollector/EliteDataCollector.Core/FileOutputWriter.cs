using System;
using System.IO;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Core
{
    /// <summary>
    /// File-based logging implementation of OutputWriter.
    ///
    /// DESIGN DECISION:
    /// Logs are written to %APPDATA%\EliteDangerousDataCollector\debug.log
    /// This allows logs to persist across app crashes and restarts,
    /// enabling asynchronous debugging without real-time console observation.
    ///
    /// FEATURES:
    /// - Appends to file (doesn't overwrite previous runs)
    /// - Handles file locks gracefully (retries with backoff)
    /// - Automatic rotation when log > 5 MB
    /// - Keeps max 10 rotated logs (oldest deleted)
    /// - Each line includes timestamp and severity level
    ///
    /// TEACHING NOTE - File I/O:
    /// Working with files in C# requires:
    /// 1. Path.Combine() - Build cross-platform file paths
    /// 2. Directory.CreateDirectory() - Ensure folder exists
    /// 3. File.AppendAllText() - Safe append to file
    /// 4. FileInfo - Get file metadata (size, etc.)
    /// 5. Lock handling - Other processes may hold the file
    ///
    /// EXAMPLE:
    /// var fileWriter = new FileOutputWriter();
    /// fileWriter.WriteLine(LogLevel.Error, "Critical issue!");
    ///
    /// You can combine this with ConsoleOutputWriter using CompositeOutputWriter.
    /// </summary>
    public class FileOutputWriter : OutputWriter
    {
        /// <summary>
        /// Path where logs are stored.
        /// %APPDATA% = C:\Users\YourName\AppData\Roaming on Windows
        /// </summary>
        private readonly string _logDirectory;

        /// <summary>
        /// Full path to the current log file.
        /// </summary>
        private readonly string _logFilePath;

        /// <summary>
        /// Maximum size of a log file before rotation (5 MB).
        /// This prevents log files from growing infinitely large.
        /// </summary>
        private const long MaxLogFileSizeBytes = 5 * 1024 * 1024;

        /// <summary>
        /// Maximum number of rotated log files to keep.
        /// When exceeded, the oldest log is deleted.
        /// </summary>
        private const int MaxRotatedLogs = 10;

        /// <summary>
        /// Number of times to retry writing if file is locked.
        /// Some processes (antivirus, indexers) can temporarily lock files.
        /// </summary>
        private const int WriteRetryCount = 3;

        /// <summary>
        /// Milliseconds to wait between retry attempts.
        /// Exponential backoff: 50ms, 100ms, 200ms
        /// </summary>
        private const int WriteRetryDelayMs = 50;

        /// <summary>
        /// Constructor: Sets up the log directory and file.
        ///
        /// TEACHING NOTE - Constructor Logic:
        /// The constructor runs once when you create a new FileOutputWriter().
        /// We use it for one-time setup that all methods will use.
        /// </summary>
        public FileOutputWriter()
        {
            // TEACHING NOTE - Environment.GetFolderPath():
            // Gets Windows special folder paths:
            // - ApplicationData = %APPDATA%
            // - LocalApplicationData = %LOCALAPPDATA%
            // - Desktop, MyDocuments, etc.
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // TEACHING NOTE - Path.Combine():
            // Safely joins path segments with the correct separator.
            // On Windows: uses \
            // On Linux: uses /
            // This makes code cross-platform!
            _logDirectory = Path.Combine(appDataPath, "EliteDangerousDataCollector");
            _logFilePath = Path.Combine(_logDirectory, "debug.log");

            // TEACHING NOTE - Directory.CreateDirectory():
            // Creates the folder if it doesn't exist.
            // Safe to call even if folder already exists (no error).
            Directory.CreateDirectory(_logDirectory);
        }

        /// <summary>
        /// Write a line to the log file.
        /// </summary>
        public void WriteLine(string message)
        {
            WriteLine(LogLevel.Info, message);
        }

        /// <summary>
        /// Write a formatted line to the log file.
        /// </summary>
        public void WriteLine(string format, params object[] args)
        {
            var message = string.Format(format, args);
            WriteLine(message);
        }

        /// <summary>
        /// Write a log line with timestamp and level indicator.
        ///
        /// TEACHING NOTE - Overloading:
        /// This demonstrates method overloading again.
        /// Different parameter combinations, same method name.
        /// </summary>
        public void WriteLine(LogLevel level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().ToUpper();
            var logLine = $"[{timestamp}] [{levelStr}] {message}";

            // Check if rotation is needed before writing
            CheckAndRotateIfNeeded();

            // Write to file with retry logic
            WriteToFileWithRetry(logLine);
        }

        /// <summary>
        /// Write a formatted log line with level.
        /// </summary>
        public void WriteLine(LogLevel level, string format, params object[] args)
        {
            var message = string.Format(format, args);
            WriteLine(level, message);
        }

        /// <summary>
        /// Check if the log file is too large, and rotate if needed.
        ///
        /// TEACHING NOTE - File Rotation:
        /// When a log file gets too big, we:
        /// 1. Rename current log to debug.log.1
        /// 2. Rename debug.log.1 to debug.log.2
        /// 3. Create new, empty debug.log
        /// 4. Delete any logs beyond .10
        ///
        /// This keeps disk usage bounded.
        /// </summary>
        private void CheckAndRotateIfNeeded()
        {
            // If log doesn't exist yet, nothing to rotate
            if (!File.Exists(_logFilePath))
                return;

            // Check its size
            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Length < MaxLogFileSizeBytes)
                return;

            // TEACHING NOTE - File Rotation Logic:
            // We rotate in reverse order to avoid overwriting.
            // If we went 1->2->3 forward, we'd lose .3 immediately.
            // By going backward, we don't lose anything.

            // Delete the oldest log if it exists
            var oldestLog = Path.Combine(_logDirectory, $"debug.log.{MaxRotatedLogs}");
            if (File.Exists(oldestLog))
                File.Delete(oldestLog);

            // Shift existing rotated logs: .9 -> .10, .8 -> .9, etc.
            for (int i = MaxRotatedLogs - 1; i >= 1; i--)
            {
                var from = Path.Combine(_logDirectory, $"debug.log.{i}");
                var to = Path.Combine(_logDirectory, $"debug.log.{i + 1}");

                // TEACHING NOTE - File.Move():
                // Renames a file (or moves it to a new location).
                // Third parameter: overwrite if destination exists.
                if (File.Exists(from))
                    File.Move(from, to, overwrite: true);
            }

            // Rename current log to .1
            var rotatedPath = Path.Combine(_logDirectory, "debug.log.1");
            File.Move(_logFilePath, rotatedPath, overwrite: true);
        }

        /// <summary>
        /// Write a line to the log file with retry logic for file locks.
        ///
        /// TEACHING NOTE - Defensive Programming:
        /// Sometimes files get locked by antivirus, indexing services, etc.
        /// Rather than crashing, we retry a few times with increasing delays.
        /// If all retries fail, we silently continue (logging shouldn't crash the app).
        /// </summary>
        private void WriteToFileWithRetry(string logLine)
        {
            for (int attempt = 0; attempt < WriteRetryCount; attempt++)
            {
                try
                {
                    // TEACHING NOTE - File.AppendAllText():
                    // Safely appends text to a file.
                    // Creates the file if it doesn't exist.
                    // Handles encoding automatically (UTF-8).
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine);

                    // Success! Return and don't retry
                    return;
                }
                catch (IOException) when (attempt < WriteRetryCount - 1)
                {
                    // TEACHING NOTE - When Clause:
                    // The "when" keyword adds a condition to exception handling.
                    // This catches IOException ONLY if it's not the last attempt.
                    // On the last attempt, the exception falls through to the catch-all.

                    // Wait before retrying, with exponential backoff
                    System.Threading.Thread.Sleep(WriteRetryDelayMs * (attempt + 1));
                }
                catch
                {
                    // TEACHING NOTE - Silent Failure:
                    // If all retries failed, we silently give up.
                    // Logging failures should NOT crash the application.
                    // The user will see the app continue running.
                    // We could log this to console, but that might cause loops.
                    return;
                }
            }
        }
    }
}