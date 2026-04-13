using EliteDataCollector.Core;

namespace EliteDataCollector.UI
{
    /// <summary>
    /// Application-level context shared across the UI.
    /// Provides access to MainCore and configuration for all ViewModels.
    /// </summary>
    public static class AppContext
    {
        public static MainCore? MainCore { get; set; }

        /// <summary>
        /// True if this is a first-run (setup not complete).
        /// When true, the main window navigates to Settings instead of Dashboard.
        /// </summary>
        public static bool IsFirstRun { get; set; }
    }
}

