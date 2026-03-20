namespace EliteDataCollector.Core.Models
{
    /// <summary>
    /// Represents a structure being built in a colonization system.
    /// </summary>
    public class Structure
    {
        // The structure's name (e.g., "Orbital Outpost")
        public string Name { get; set; }

        // The type of structure (e.g., "Settlement", "Surface Port")
        public string Type { get; set; }

        // How much construction is complete (0 to 100)
        public double ProgressPercent { get; set; }
    }
}