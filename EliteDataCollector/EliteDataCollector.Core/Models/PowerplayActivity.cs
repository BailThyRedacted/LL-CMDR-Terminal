using System;
using System.Text.Json.Serialization;

namespace EliteDataCollector.Core.Models
{
    /// <summary>
    /// Represents a single PowerPlay player activity event from the Elite Dangerous journal.
    ///
    /// One record is inserted per event — activities are not deduplicated.
    /// Covers all PP2.0 activity events:
    ///   PowerplayCollect, PowerplayDeliver, PowerplayVote,
    ///   PowerplaySalary, PowerplayFastTrack,
    ///   PowerplayJoin, PowerplayDefect, PowerplayLeave
    /// </summary>
    public class PowerplayActivity
    {
        /// <summary>Unique record ID (UUID generated client-side).</summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Journal event name (e.g., "PowerplayDeliver").</summary>
        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        /// <summary>PowerPlay power the commander is pledged to at the time of the event.</summary>
        [JsonPropertyName("power")]
        public string? Power { get; set; }

        /// <summary>
        /// Star system name where the event occurred.
        /// Populated from the most recent FSDJump/Location event.
        /// Null if current system is not yet known.
        /// </summary>
        [JsonPropertyName("system_name")]
        public string? SystemName { get; set; }

        /// <summary>
        /// Commodity or module type for Collect/Deliver events.
        /// Maps to the "Type" field in the journal event.
        /// Null for non-commodity events.
        /// </summary>
        [JsonPropertyName("item_type")]
        public string? ItemType { get; set; }

        /// <summary>
        /// Quantity of items collected or delivered.
        /// Null for non-commodity events.
        /// </summary>
        [JsonPropertyName("count")]
        public int? Count { get; set; }

        /// <summary>
        /// Merits earned or spent during the activity (PP2.0).
        /// Null for events that do not involve merits.
        /// </summary>
        [JsonPropertyName("merits")]
        public int? Merits { get; set; }

        /// <summary>
        /// Credit amount for Salary or FastTrack events.
        /// Null for non-credit events.
        /// </summary>
        [JsonPropertyName("amount")]
        public long? Amount { get; set; }

        /// <summary>
        /// Number of votes cast for Vote events.
        /// Null for non-vote events.
        /// </summary>
        [JsonPropertyName("votes")]
        public int? Votes { get; set; }

        /// <summary>UTC timestamp of the journal event.</summary>
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
