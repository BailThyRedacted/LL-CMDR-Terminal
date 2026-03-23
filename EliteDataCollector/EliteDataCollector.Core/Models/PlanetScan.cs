using System;

namespace EliteDataCollector.Core.Models
{
    /// <summary>
    /// Represents a scanned planet with exobiology data.
    ///
    /// Teaching: Model classes hold data related to a specific domain object.
    /// - PlanetScan captures everything we know about a planet from scanning.
    /// - Used to pass data from parsing (JSON) to scoring to file storage.
    /// - Serializable to JSON for persistent storage in scans.json
    /// </summary>
    public class PlanetScan
    {
        /// <summary>
        /// Name of the system containing this planet.
        /// Example: "Achenar", "Sol", "Trypolyamides"
        /// </summary>
        public string SystemName { get; set; } = "Unknown";

        /// <summary>
        /// Name of the body/planet.
        /// Example: "Sol A" (star), "Sol 1" (planet), "Achenar AB 1 a" (exoplanet)
        /// </summary>
        public string BodyName { get; set; } = "Unknown";

        /// <summary>
        /// Type of planet/body.
        /// Examples: "Terrestrial", "Water World", "High Metal Content", "Rocky Body", "Icy Body", "Gas Giant"
        /// </summary>
        public string PlanetType { get; set; } = "Unknown";

        /// <summary>
        /// Atmospheric composition, if present.
        /// Examples: "Ammonia atmosphere", "Water atmosphere", "Methane atmosphere", "Nitrogen atmosphere"
        /// Null if planet has no atmosphere (vacuum).
        /// </summary>
        public string? Atmosphere { get; set; }

        /// <summary>
        /// Surface temperature in Kelvin.
        /// Example: 288.15 for Earth-like (~15°C), 1000 for hot lava planets, 50 for icy worlds
        /// </summary>
        public double SurfaceTemperature { get; set; }

        /// <summary>
        /// Surface gravity in G (Earth gravity units).
        /// Example: 1.0 = Earth gravity, 0.1 = 10% of Earth, 5.0 = 5x Earth gravity
        /// Lower gravity can support exotic organisms.
        /// </summary>
        public double Gravity { get; set; }

        /// <summary>
        /// Whether the planet is safe to land on (can collect samples).
        /// Critical value multiplier for exobiology potential: landable planets are worth ~5-10x more.
        /// </summary>
        public bool Landable { get; set; }

        /// <summary>
        /// When this scan was recorded.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Exobiology potential score: 0 to 100.
        /// - 0-20: Very low potential (barren, no atmosphere, etc.)
        /// - 20-40: Low potential (weak atmospheres, harsh conditions)
        /// - 40-60: Moderate potential (suitable conditions)
        /// - 60-80: High potential (excellent exobiology conditions)
        /// - 80-100: Exceptional (ideal temperature, atmosphere, gravity, landable)
        ///
        /// Score is calculated by ExobiologyScoringEngine based on:
        /// - Atmosphere type (40% weight)
        /// - Planet type (20% weight)
        /// - Temperature range (20% weight)
        /// - Gravity (10% weight)
        /// - Landable status (10% weight)
        /// </summary>
        public int ExobiologyScore { get; set; }

        /// <summary>
        /// Estimated profit value in credits from Vista Genomics if all organisms collected.
        /// Examples:
        /// - Exceptional planets (score > 80): 10M - 20M credits
        /// - High-value planets (score 60-80): 2M - 10M credits
        /// - Moderate planets (score 40-60): 500K - 2M credits
        /// - Low-value (score < 40): 100K - 500K credits
        /// Non-landable planets worth ~10-20% of landable equivalent.
        /// </summary>
        public long EstimatedValue { get; set; }

        /// <summary>
        /// True if only bacterium species were found on this planet (via ScanOrganic events).
        /// Bacterium is very low value (~20K per species), so planets with ONLY bacterium
        /// are not alerted and considered "low priority" for exploration.
        /// Mixed planets (flora/fauna + bacterium) are still high value.
        /// </summary>
        public bool BacteriumOnly { get; set; }
    }
}
