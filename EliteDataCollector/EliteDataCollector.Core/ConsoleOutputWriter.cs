using System;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Core
{
    /// <summary>
    /// Console-based implementation of OutputWriter with log level support.
    ///
    /// DESIGN DECISION:
    /// This is a concrete implementation of the OutputWriter interface.
    /// By having this separate from MainCore, we can:
    /// - Test MainCore with a mock OutputWriter
    /// - Swap to GUI output later without changing MainCore
    /// - Reuse in multiple contexts (console app, tests, etc.)
    ///
    /// LOG LEVELS:
    /// The console automatically color-codes messages based on severity:
    /// - Debug (Gray):   Verbose details, not shown by default in production
    /// - Info (White):   General application flow information
    /// - Warning (Yellow): Issues that don't stop execution
    /// - Error (Red):    Serious problems requiring attention
    ///
    /// TEACHING NOTE - Colors:
    /// Console.ForegroundColor changes ALL subsequent output until reset.
    /// We save the original color, change it for our message, then restore it.
    /// This prevents color bleeding into other output.
    ///
    /// EXAMPLE USAGE:
    /// var output = new ConsoleOutputWriter();
    /// output.SetMinimumLogLevel(LogLevel.Info);  // Hide Debug messages
    /// output.WriteLine(LogLevel.Warning, "This is yellow!");
    /// output.WriteLine("This is info level (default)");
    /// </summary>
    public class ConsoleOutputWriter : OutputWriter
    {
        /// <summary>
        /// Minimum log level to display. Logs below this level are silently ignored.
        /// Default: Info (so Debug messages are suppressed unless explicitly enabled).
        /// </summary>
        private LogLevel _minimumLogLevel = LogLevel.Info;

        /// <summary>
        /// Set the minimum log level. Messages below this level will be ignored.
        ///
        /// EXAMPLE:
        /// SetMinimumLogLevel(LogLevel.Debug);    // Show everything
        /// SetMinimumLogLevel(LogLevel.Warning);  // Only warnings and errors
        /// </summary>
        public void SetMinimumLogLevel(LogLevel level)
        {
            _minimumLogLevel = level;
        }

        /// <summary>
        /// Write a line to the console with timestamp and color-coding.
        /// Uses Info level by default for backward compatibility.
        /// </summary>
        public void WriteLine(string message)
        {
            // Call the Log method with Info level (default for backward compatibility)
            Log(LogLevel.Info, message);
        }

        /// <summary>
        /// Write a formatted line to the console (like string.Format).
        /// Uses Info level by default for backward compatibility.
        /// </summary>
        public void WriteLine(string format, params object[] args)
        {
            var message = string.Format(format, args);
            WriteLine(message);
        }

        /// <summary>
        /// Write a log message with a specific level and color-coding.
        ///
        /// TEACHING NOTE - Method Overloading:
        /// This is called "method overloading" - same method name, different parameters.
        /// C# chooses which version to call based on what parameters you pass.
        ///
        /// EXAMPLE:
        /// output.WriteLine("Hello");                         // Uses default WriteLine
        /// output.WriteLine(LogLevel.Error, "Database down"); // Uses this method
        /// </summary>
        public void WriteLine(LogLevel level, string message)
        {
            // TEACHING NOTE - Guard Clause:
            // Check if we should skip this log level.
            // If level < minimum, silently ignore it.
            if (level < _minimumLogLevel)
            {
                return;
            }

            Log(level, message);
        }

        /// <summary>
        /// Write a formatted log message with a specific level.
        /// </summary>
        public void WriteLine(LogLevel level, string format, params object[] args)
        {
            var message = string.Format(format, args);
            WriteLine(level, message);
        }

        /// <summary>
        /// Internal method that actually writes the colored output.
        ///
        /// TEACHING NOTE - Private vs Public:
        /// This is private (only callable from inside this class)
        /// because users should call WriteLine() instead.
        /// This is an implementation detail they don't need to know about.
        /// </summary>
        private void Log(LogLevel level, string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = $"[{timestamp}] ";

            // Save the original console color so we can restore it
            var originalColor = Console.ForegroundColor;

            try
            {
                // Set color based on log level
                Console.ForegroundColor = GetColorForLevel(level);

                // Include level indicator in the message for Error and Warning
                if (level == LogLevel.Error)
                    prefix += "[ERROR] ";
                else if (level == LogLevel.Warning)
                    prefix += "[WARN] ";
                else if (level == LogLevel.Debug)
                    prefix += "[DEBUG] ";

                Console.WriteLine($"{prefix}{message}");
            }
            finally
            {
                // Always restore the original color, even if an exception occurs
                // TEACHING NOTE - Finally:
                // The "finally" block runs regardless of success or failure.
                // This ensures cleanup always happens.
                Console.ForegroundColor = originalColor;
            }
        }

        /// <summary>
        /// Returns the console color for a log level.
        ///
        /// TEACHING NOTE - Switch Expression:
        /// This is a modern C# feature (C# 8+) that's cleaner than if-else.
        /// Pattern syntax: `value => result,` means "when value, return result"
        /// </summary>
        private ConsoleColor GetColorForLevel(LogLevel level) => level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,       // Subtle
            LogLevel.Info => ConsoleColor.White,       // Normal
            LogLevel.Warning => ConsoleColor.Yellow,   // Attention
            LogLevel.Error => ConsoleColor.Red,        // Urgent
            _ => ConsoleColor.White                    // Fallback
        };
    }
}
