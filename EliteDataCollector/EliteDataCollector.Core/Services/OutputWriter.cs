using System;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Abstract output interface for logging and messages.
    ///
    /// Design Decision: Interface for output allows multiple implementations
    /// - Console app: writes to console
    /// - GUI: writes to text box
    /// - Windows Service: writes to event log
    /// - Without this, MainCore would be tightly coupled to a specific output method
    /// </summary>
    public interface OutputWriter
    {
        /// <summary>
        /// Writes a line of text to the output destination.
        /// </summary>
        void WriteLine(string message);

        /// <summary>
        /// Writes a formatted line (like Console.WriteLine with format args).
        /// </summary>
        void WriteLine(string format, params object[] args);
    }
}
