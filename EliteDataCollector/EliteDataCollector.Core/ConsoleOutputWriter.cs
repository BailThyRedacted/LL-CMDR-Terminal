using System;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Core
{
    /// <summary>
    /// Simple console-based implementation of IOutputWriter.
    ///
    /// DESIGN DECISION:
    /// This is a concrete implementation of the IOutputWriter interface.
    /// By having this separate from MainCore, we can:
    /// - Test MainCore with a mock IOutputWriter
    /// - Swap to GUI output later without changing MainCore
    /// - Reuse in multiple contexts (console app, tests, etc.)
    ///
    /// EXAMPLE USAGE:
    /// var output = new ConsoleOutputWriter();
    /// var core = new MainCore(gameMonitor, journalMonitor, capiAuth, validator, output);
    /// </summary>
    public class ConsoleOutputWriter : IOutputWriter
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
