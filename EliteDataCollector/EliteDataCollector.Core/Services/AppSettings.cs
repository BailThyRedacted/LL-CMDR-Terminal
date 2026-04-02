using System;
using System.Text.Json.Serialization;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// User application settings (per-user, stored in %APPDATA%).
    ///
    /// Teaching: Settings model for serialization
    /// - Holds all user-configurable options
    /// - Serializable to JSON via System.Text.Json
    /// - Stored encrypted at rest
    /// - Loaded on app startup
    /// </summary>
    public class AppSettings
    {
        /// <summary>INARA API key (stored encrypted)</summary>
        [JsonPropertyName("inara_api_key")]
        public string InaraApiKeyEncrypted { get; set; } = string.Empty;

        /// <summary>Commander ID from INARA (unencrypted, used for filtering)</summary>
        [JsonPropertyName("commander_id")]
        public int CommanderId { get; set; }

        /// <summary>Commander name from INARA (unencrypted, for display)</summary>
        [JsonPropertyName("commander_name")]
        public string CommanderName { get; set; } = string.Empty;

        /// <summary>When credentials were last verified</summary>
        [JsonPropertyName("last_verified")]
        public DateTime LastVerified { get; set; }

        /// <summary>Module toggles</summary>
        [JsonPropertyName("modules")]
        public ModuleSettings Modules { get; set; } = new();

        /// <summary>Whether first-time setup has completed</summary>
        [JsonPropertyName("setup_complete")]
        public bool SetupComplete { get; set; }

        /// <summary>Current installed version (e.g., "1.0.0")</summary>
        [JsonPropertyName("installed_version")]
        public string InstalledVersion { get; set; } = "1.0.0";

        /// <summary>Last time update check was performed</summary>
        [JsonPropertyName("last_update_check")]
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

        /// <summary>Whether auto-update checking is enabled</summary>
        [JsonPropertyName("auto_update_enabled")]
        public bool AutoUpdateEnabled { get; set; } = true;

        /// <summary>Number of backups to retain for rollback</summary>
        [JsonPropertyName("backup_retention_count")]
        public int BackupRetentionCount { get; set; } = 3;

        /// <summary>Pending update version awaiting installation</summary>
        [JsonPropertyName("pending_update_version")]
        public string? PendingUpdateVersion { get; set; } = null;

        /// <summary>User's choice for pending update (Yes/Later/No)</summary>
        [JsonPropertyName("pending_update_choice")]
        public string? PendingUpdateChoice { get; set; } = null;
    }

    /// <summary>
    /// Per-module configuration.
    /// </summary>
    public class ModuleSettings
    {
        /// <summary>Enable ColonizationModule (BGS/PowerPlay tracking)</summary>
        [JsonPropertyName("colonization_enabled")]
        public bool ColonizationEnabled { get; set; } = true;

        /// <summary>Enable ExplorationModule (exobiology alerts)</summary>
        [JsonPropertyName("exploration_enabled")]
        public bool ExplorationEnabled { get; set; } = false;
    }
}
