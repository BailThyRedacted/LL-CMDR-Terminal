using System;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Monitors the Elite Dangerous game process and raises events when it starts or exits.
    ///
    /// Design Decision: Separate abstraction for process monitoring allows us to:
    /// - Swap different implementations (e.g., polling vs. WMI)
    /// - Test MainCore without actually launching the game
    /// - Reuse in multiple contexts (console app, GUI, service)
    /// </summary>
    public interface IGameProcessMonitor
    {
        /// <summary>
        /// Starts monitoring for the game process (e.g., EliteDangerous64.exe).
        /// After this is called, GameLaunched and GameExited events will fire as appropriate.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops monitoring for the game process.
        /// No further events will be raised after this completes.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Raised when the game process is detected as launched.
        /// </summary>
        event EventHandler? GameLaunched;

        /// <summary>
        /// Raised when the game process is detected as exited.
        /// </summary>
        event EventHandler? GameExited;
    }
}
