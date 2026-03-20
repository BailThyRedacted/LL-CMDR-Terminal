namespace EliteDataCollector.Core.Models
{
    /// <summary>
    /// Represents a faction's influence and state within a system.
    /// </summary>
    public class FactionInfluence
    {
        // The faction's name (e.g., "Lavigny's Legion")
        public string Name { get; set; }

        // Influence percentage (0.0 to 100.0)
        public double Influence { get; set; }

        // The faction's current state (e.g., "None", "Boom", "War")
        public string State { get; set; }

        // Allegiance (e.g., "Empire", "Federation", "Independent")
        public string Allegiance { get; set; }
    }
}