using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Custom EventArgs for journal line events.
    /// We pass the raw line and parsed JSON so listeners can decide what to do with it.
    ///
    /// Design Decision: Pass both raw and parsed data
    /// - Raw: for debugging/logging
    /// - Parsed: for efficiency (don't force every listener to parse JSON)
    /// </summary>
    public class JournalLineEventArgs : EventArgs
    {
        public string RawLine { get; set; }
        public JsonDocument ParsedEvent { get; set; }
        public string EventType { get; set; }
    }

    /// <summary>
    /// Monitors the Elite Dangerous journal folder for new or modified files.
    /// Reads only new lines appended since last read (using byte-offset tracking).
    ///
    /// Design Decision: Separate from game monitor because
    /// - Journal might exist before game even runs
    /// - Easier to test independently
    /// - Can be reused for other purposes
    /// </summary>
    public interface JournalMonitor
    {
        /// <summary>
        /// Starts monitoring the journal folder for new entries.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops monitoring the journal folder.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Raised when a new journal line has been read and parsed.
        /// The EventArgs contains the event type, raw line, and parsed JSON.
        /// </summary>
        event EventHandler<JournalLineEventArgs>? JournalLineRead;
    }
}
