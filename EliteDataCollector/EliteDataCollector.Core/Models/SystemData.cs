using System;
using System.Collections.Generic;

namespace EliteDataCollector.Core.Models
{
    /// <summary>
    /// Represents a star system and its colonization‑relevant data.
    /// </summary>
    public class SystemData
    {
        // The unique identifier from the game (SystemAddress)
        public long Id { get; set; }

        // The system's name (e.g., "Sol")
        public string SystemName { get; set; }

        // When this data was recorded
        public DateTime Timestamp { get; set; }

        // The faction that currently controls the system (most influence)
        public string ControllingFaction { get; set; }

        // Powerplay power controlling the system (if any)
        public string Power { get; set; }

        // Powerplay state (e.g., "Exploited", "Contested")
        public string PowerState { get; set; }

        // List of all factions in the system with their influence
        public List<FactionInfluence> Factions { get; set; }

        // List of structures currently under construction
        public List<Structure> Structures { get; set; }

        // Lavigny's Legion influence % in this system (tracked separately for easy analytics)
        public double LavignyInfluence { get; set; }

        // Constructor to initialize the lists so they are never null
        public SystemData()
        {
            Factions = new List<FactionInfluence>();
            Structures = new List<Structure>();
        }
    }
}