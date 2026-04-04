using System;
using System.Text.Json.Serialization;

namespace EliteDataCollector.Core.Models
{
    /// <summary>
    /// Represents the current PowerPlay state of a star system controlled by
    /// Arissa Lavigny-Duval, as observed on FSDJump or Location events.
    ///
    /// One row per system — upserted on each visit.
    /// Only systems where Power == "Arissa Lavigny-Duval" are uploaded.
    /// </summary>
    public class PowerplaySystem
    {
        /// <summary>
        /// Elite Dangerous SystemAddress — unique 64-bit system identifier.
        /// Used as the primary key for upsert operations.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>Star system name (e.g., "Kamadhenu").</summary>
        [JsonPropertyName("system_name")]
        public string SystemName { get; set; } = string.Empty;

        /// <summary>
        /// Controlling PowerPlay power (always "Arissa Lavigny-Duval" for rows
        /// stored by this module, but stored for completeness).
        /// </summary>
        [JsonPropertyName("power")]
        public string Power { get; set; } = string.Empty;

        /// <summary>
        /// PowerPlay state of the system (e.g., "Stronghold", "Fortified",
        /// "Exploited", "Turmoil", "Contested", "Unoccupied").
        /// </summary>
        [JsonPropertyName("power_state")]
        public string PowerState { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the most recent observation.</summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Commander identifier derived from CMDR name or Windows username.
        /// Used for Supabase Row Level Security (RLS).
        /// </summary>
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
    }
}
