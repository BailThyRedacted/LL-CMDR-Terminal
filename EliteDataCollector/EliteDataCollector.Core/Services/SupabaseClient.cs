using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EliteDataCollector.Core.Models;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Service interface for interacting with Supabase PostgreSQL database.
    ///
    /// Design Decision: Separate as an interface allows us to:
    /// - Test modules with mocked Supabase client
    /// - Swap different implementations (REST API, gRPC, etc.)
    /// - Reuse across multiple modules (ColonizationModule, ExplorationModule, etc.)
    /// - Manage Supabase configuration centrally in one place
    ///
    /// Why separate from module? Because multiple modules may need database access.
    /// Better to have one shared service than duplicate code in each module.
    /// </summary>
    public interface SupabaseClient
    {
        /// <summary>
        /// Fetches the list of target systems to monitor for colonization tracking.
        ///
        /// Teaching: This is called once on app startup to load the systems we care about.
        /// We store the result in a HashSet for O(1) lookups when filtering events.
        ///
        /// Implementation should:
        /// - Query the "target_systems" table from Supabase
        /// - Return list of system names (e.g., "Sol", "Alpha Centauri", "Sirius")
        /// - Handle network errors gracefully (log, return empty list)
        /// - Never throw (graceful degradation)
        /// </summary>
        /// <returns>
        /// List of target system names. Returns empty list if:
        /// - Supabase is unreachable
        /// - No systems configured in database
        /// - Query fails for any reason
        /// </returns>
        Task<List<string>> GetTargetSystemsAsync();

        /// <summary>
        /// Uploads or updates system data in Supabase database (upsert operation).
        ///
        /// Teaching: This is called when the player enters a [target] system.
        /// We send the current BGS faction states, PowerPlay allegiance, and other metadata.
        ///
        /// Implementation should:
        /// - Query "systems" table
        /// - Use SystemData.Id (SystemAddress from game) as primary key
        /// - Upsert (insert if not exists, update if exists)
        /// - Update timestamp to current time
        /// - Serialize nested Factions and Structures lists to JSON if needed
        /// - Handle errors gracefully (log, continue)
        /// - Never throw (background processing should never crash)
        ///
        /// Timing: Called from module when:
        /// - Player enters a target system (Location or FSDJump event)
        /// - About once per system visit
        /// </summary>
        /// <param name="systemData">System information to upsert: name, factions, power, power state, etc.</param>
        Task UpsertSystemDataAsync(SystemData systemData);

        /// <summary>
        /// Uploads or updates structure/colonization project data for a system.
        ///
        /// Teaching: This is called when structures are built, repaired, or transferred.
        /// We track the progress and status of colonization projects.
        ///
        /// Implementation should:
        /// - Query "structures" table
        /// - Use (system_id: long, structure_name: string) as composite key
        /// - Upsert each structure in the list
        /// - Update progress_percent, state, timestamp
        /// - Handle errors gracefully
        /// - Never throw
        ///
        /// Timing: Called from module when:
        /// - StructureBuy event (new project started)
        /// - StructureRepair event (existing project updated)
        /// - StructureSell event (project cancelled)
        /// </summary>
        /// <param name="systemAddress">System address identifier (from game)</param>
        /// <param name="structures">List of structures in this system to upsert</param>
        Task UpsertStructuresAsync(long systemAddress, List<Structure> structures);

        /// <summary>
        /// Fetches systems from the ll_presence table (Lavigny's Legion presence).
        /// These are the specific systems where LL has active presence and data should be collected.
        ///
        /// Teaching: Similar to GetTargetSystemsAsync but queries a different table.
        /// This allows the colonization module to filter to only LL-relevant systems.
        ///
        /// Implementation should:
        /// - Query the "ll_presence" table from Supabase
        /// - Return list of system names (e.g., "Sol", "Alpha Centauri", "Sirius")
        /// - Handle network errors gracefully (log, return empty list)
        /// - Never throw (graceful degradation)
        /// </summary>
        /// <returns>
        /// List of LL presence system names. Returns empty list if:
        /// - Supabase is unreachable
        /// - No systems configured in ll_presence table
        /// - Query fails for any reason
        /// </returns>
        Task<List<string>> GetLlPresenceSystemsAsync();

        /// <summary>
        /// Inserts a new PowerPlay activity record into Supabase.
        /// Each journal activity event produces one row — records are not deduplicated.
        ///
        /// Called from PowerplayModule when the player triggers a PowerPlay event:
        /// PowerplayCollect, PowerplayDeliver, PowerplayVote, PowerplaySalary,
        /// PowerplayFastTrack, PowerplayJoin, PowerplayDefect, PowerplayLeave.
        ///
        /// Implementation should:
        /// - POST to the "powerplay_activities" table
        /// - Include all non-null fields from the activity record
        /// - Attach user_id for RLS filtering
        /// - Handle errors gracefully (log, continue)
        /// - Never throw
        /// </summary>
        /// <param name="activity">Populated activity record to insert.</param>
        Task InsertPowerplayActivityAsync(PowerplayActivity activity);

        /// <summary>
        /// Upserts the current PowerPlay state of a star system into Supabase.
        /// Only called for systems controlled by Arissa Lavigny-Duval.
        /// One row per system (keyed on SystemAddress); updated on each visit.
        ///
        /// Called from PowerplayModule on FSDJump/Location events where
        /// the Power field equals "Arissa Lavigny-Duval".
        ///
        /// Implementation should:
        /// - POST to "powerplay_systems" with on_conflict=id resolution
        /// - Update power_state and timestamp on revisit
        /// - Attach user_id for RLS filtering
        /// - Handle errors gracefully (log, continue)
        /// - Never throw
        /// </summary>
        /// <param name="system">System state to upsert.</param>
        Task UpsertPowerplaySystemAsync(PowerplaySystem system);
    }
}

