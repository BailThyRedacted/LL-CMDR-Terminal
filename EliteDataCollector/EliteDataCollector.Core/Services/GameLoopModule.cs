using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Interface for game loop modules that process journal events and game data.
    ///
    /// Design Decision: Separate interface for modules allows:
    /// - Dynamic loading of different module implementations
    /// - Each module processes events independently
    /// - Modules don't know about each other (loose coupling)
    /// - Easy to add new modules without changing MainCore
    ///
    /// Teaching: This is the module contract. All modules must implement:
    /// - Initialization (load config, connect to services)
    /// - Event processing (react to journal events)
    /// - Shutdown (cleanup resources)
    ///
    /// Module Lifecycle:
    /// 1. MainCore creates module instance
    /// 2. Calls InitializeAsync() - module loads config and hooks up to services
    /// 3. Module receives events via OnJournalLineAsync() and OnCapiProfileAsync()
    /// 4. When app shuts down, ShutdownAsync() is called for cleanup
    /// </summary>
    public interface GameLoopModule
    {
        /// <summary>
        /// Name of this module (e.g., "Colonization", "Exploration")
        /// Used for logging and identification
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Human-readable description of what this module does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Initializes the module on application startup.
        ///
        /// Teaching: Initialize is called once when the app starts
        /// - Extract services from the service provider
        /// - Load module configuration
        /// - Connect to database/external services
        /// - Set up any internal state
        /// - Should never throw - log errors and continue gracefully
        ///
        /// Parameters:
        /// - IServiceProvider: Access to DI container to get services
        ///   Example: services.GetService(typeof(SupabaseClient))
        /// </summary>
        /// <param name="services">Service provider for dependency injection</param>
        Task InitializeAsync(IServiceProvider services);

        /// <summary>
        /// Processes a single journal line event.
        ///
        /// Teaching: Called for each important journal line detected
        /// - Raw line (as written by game)
        /// - Parsed JSON (already parsed by JournalMonitor)
        /// - Should process asynchronously without blocking
        /// - Should never throw - catch all exceptions and log
        ///
        /// This is the core event processing method. Module should:
        /// - Check event type (location, structure, faction, etc.)
        /// - Extract relevant data from JSON
        /// - Update internal state if needed
        /// - Call external services asynchronously
        /// - Handle errors gracefully
        ///
        /// Timing: Called frequently (10x per second in busy gameplay)
        /// Must be fast! Expensive operations should be async and non-blocking.
        /// </summary>
        /// <param name="line">Raw journal line as written by game</param>
        /// <param name="parsedEvent">Already-parsed JSON document of the line</param>
        Task OnJournalLineAsync(string line, JsonDocument parsedEvent);

        /// <summary>
        /// Processes commander profile data from CAPI (optional).
        ///
        /// Teaching: Called periodically with fresh commander profile
        /// - Contains: location, rank, credits, ships, etc.
        /// - Can be called less frequently than OnJournalLineAsync
        /// - Module can ignore if not needed
        ///
        /// Design: Kept for future use. Implementation can be empty Task.CompletedTask.
        /// </summary>
        /// <param name="profile">Commander profile JSON from CAPI</param>
        Task OnCapiProfileAsync(JsonDocument profile);

        /// <summary>
        /// Shuts down the module when application exits.
        ///
        /// Teaching: Cleanup method called during shutdown
        /// - Save any pending data
        /// - Close database connections
        /// - Cancel background operations
        /// - Release file handles
        /// - Should never throw - catch all exceptions
        ///
        /// Graceful shutdown is important for data integrity.
        /// </summary>
        Task ShutdownAsync();
    }
}
