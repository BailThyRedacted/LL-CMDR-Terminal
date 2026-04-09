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
    }
}

