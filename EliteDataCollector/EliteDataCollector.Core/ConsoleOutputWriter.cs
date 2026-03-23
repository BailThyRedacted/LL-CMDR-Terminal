using System;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Core
{
    /// <summary>
    /// Simple console-based implementation of OutputWriter.
    ///
    /// DESIGN DECISION:
    /// This is a concrete implementation of the OutputWriter interface.
    /// By having this separate from MainCore, we can:
    /// - Test MainCore with a mock OutputWriter
    /// - Swap to GUI output later without changing MainCore
    /// - Reuse in multiple contexts (console app, tests, etc.)
    ///
    /// EXAMPLE USAGE:
    /// var output = new ConsoleOutputWriter();
    /// var core = new MainCore(gameMonitor, journalMonitor, capiAuth, validator, output);
    /// </summary>
    public class ConsoleOutputWriter : OutputWriter
    {
        /// <summary>
        /// Write a line to the console with timestamp prefix.
        /// </summary>
        public void WriteLine(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] {message}");
        }

        /// <summary>
        /// Write a formatted line to the console (like string.Format).
        /// </summary>
        public void WriteLine(string format, params object[] args)
        {
            var message = string.Format(format, args);
            WriteLine(message);
        }
    }
}
