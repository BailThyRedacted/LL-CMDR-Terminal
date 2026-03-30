using System;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Defines logging severity levels.
    ///
    /// TEACHING NOTE - Enum Pattern:
    /// ============================
    /// An enum is a way to define a set of named constants.
    /// Instead of using magic numbers (0, 1, 2...) or strings ("debug", "error"),
    /// we use descriptive names that are type-safe and readable.
    ///
    /// Usage:
    /// if (logLevel >= LogLevel.Warning) { ... log it ... }
    ///
    /// Enums have integer values by default (0, 1, 2, 3, 4...) and can be compared.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Log nothing.</summary>
        None = 0,

        /// <summary>Verbose debugging information (most detailed, production should suppress).</summary>
        Debug = 1,

        /// <summary>General information about application flow.</summary>
        Info = 2,

        /// <summary>Warning conditions that don't stop execution.</summary>
        Warning = 3,

        /// <summary>Error conditions that need attention.</summary>
        Error = 4
    }
}
