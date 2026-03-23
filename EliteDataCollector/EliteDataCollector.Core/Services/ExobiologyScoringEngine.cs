namespace EliteDataCollector.Core.Services
{
    using EliteDataCollector.Core.Models;

    /// <summary>
    /// Utility class for scoring planets' exobiology potential and estimating their value.
    ///
    /// Teaching: Utility classes (static only, no instances) hold pure logic.
    /// - Keeps scoring algorithm separate from module (testable, reusable)
    /// - Encodes domain knowledge about exobiology organisms
    /// - Converts planetary characteristics into actionable scores (0-100)
    /// - Estimates credits based on Vista Genomics price list
    ///
    /// Data Source: https://canonn.science/codex/vista-genomics-price-list/
    /// - Highest value organism: Fonticulua Fluctus = 20,000,000 credits
    /// - Mid-tier organisms: Tussock, Stratum, Fonticulua variants = 1M - 5M credits
    /// - Lowest non-bacterium: Radicoida Unica = 119,037 credits
    /// - Bacterium (IGNORED): ~20,000 credits per species (very low value)
    /// </summary>
    public static class ExobiologyScoringEngine
    {
        // ========== SCORING WEIGHTS ==========

        /// <summary>
        /// Atmosphere is the most important factor for exobiology.
        /// Rich atmospheres (Ammonia, Methane, Nitrogen, Water) support diverse organisms.
        /// No atmosphere or thin atmospheres = barren, no life.
        /// Weight: 40% of total score
        /// </summary>
        private const double ATMOSPHERE_WEIGHT = 0.40;

        /// <summary>
        /// Planet type indicates habitability and environmental diversity.
        /// Water World, High Metal Content, Icy, Rocky = good for exobiology
        /// Gas Giants = no surface to colonize, poor exobiology
        /// Weight: 20% of total score
        /// </summary>
        private const double PLANET_TYPE_WEIGHT = 0.20;

        /// <summary>
        /// Temperature determines which organisms can survive.
        /// Extreme temps (very hot, very cold) support specialized organisms
        /// Earth-like temps (~200-300K) are also good
        /// Moderate temps are best for diversity
        /// Weight: 20% of total score
        /// </summary>
        private const double TEMPERATURE_WEIGHT = 0.20;

        /// <summary>
        /// Gravity affects organism development and specialization.
        /// Lower gravity (~0.2-0.8G) supports exotic organisms
        /// Earth-like gravity (0.8-1.2G) is also good
        /// Very high gravity (>2G) limits habitability
        /// Weight: 10% of total score
        /// </summary>
        private const double GRAVITY_WEIGHT = 0.10;

        /// <summary>
        /// Landable status is critical: players can ONLY collect samples on landable planets.
        /// Non-landable planets have signals but can't be harvested = worthless.
        /// Landable planets get massive multiplier.
        /// Weight: 10% of total score (but acts as multiplier on final value)
        /// </summary>
        private const double LANDABLE_WEIGHT = 0.10;

        // ========== VALUE RANGES ==========

        /// <summary>Maximum exobiology organism value (Vista Genomics)</summary>
        private const long MAX_ORGANISM_VALUE = 20_000_000;  // Fonticulua Fluctus

        /// <summary>Mid-tier organism value (average high-value organisms)</summary>
        private const long MID_ORGANISM_VALUE = 3_000_000;   // Typical valuable species

        /// <summary>Minimum non-bacterium organism value</summary>
        private const long MIN_ORGANISM_VALUE = 119_037;     // Radicoida Unica

        /// <summary>Bacterium value (very low, ignored)</summary>
        private const long BACTERIUM_VALUE = 20_000;

        // ========== ATMOSPHERE SCORING ==========

        /// <summary>
        /// Scores an atmosphere on 0-100 scale for exobiology potential.
        /// Teaching: Switchonup high-value atmospheres support many exotic organisms.
        /// </summary>
        private static int ScoreAtmosphere(string? atmosphere)
        {
            if (string.IsNullOrWhiteSpace(atmosphere))
                return 0;  // No atmosphere = no exobiology

            atmosphere = atmosphere.ToLower();

            // High-value atmospheres: Ammonia, Methane, Nitrogen, Water
            // These support diverse exotic organisms
            if (atmosphere.Contains("ammonia"))
                return 100;
            if (atmosphere.Contains("methane"))
                return 95;
            if (atmosphere.Contains("nitrogen"))
                return 90;
            if (atmosphere.Contains("water"))
                return 85;

            // Carbon dioxide atmosphere: moderate value
            if (atmosphere.Contains("carbon dioxide") || atmosphere.Contains("co2"))
                return 50;

            // Oxygen atmospheres: some organisms
            if (atmosphere.Contains("oxygen") || atmosphere.Contains("argon"))
                return 40;

            // Sulphur and other exotic: moderate
            if (atmosphere.Contains("sulphur") || atmosphere.Contains("sulfur"))
                return 45;

            // Unknown/rare atmosphere: assume moderate value
            return 30;
        }

        // ========== PLANET TYPE SCORING ==========

        /// <summary>
        /// Scores a planet type on 0-100 scale for exobiology diversity.
        /// </summary>
        private static int ScorePlanetType(string? planetType)
        {
            if (string.IsNullOrWhiteSpace(planetType))
                return 25;  // Unknown = moderate

            planetType = planetType.ToLower();

            // High-value types: support exotic exobiology
            if (planetType.Contains("water world"))
                return 95;
            if (planetType.Contains("high metal content"))
                return 90;
            if (planetType.Contains("icy"))
                return 85;
            if (planetType.Contains("terrestrial"))
                return 80;
            if (planetType.Contains("rocky"))
                return 75;

            // Gas giants: no surface exobiology (but moons might exist)
            if (planetType.Contains("gas giant"))
                return 10;

            // Unknown or rare types
            return 50;
        }

        // ========== TEMPERATURE SCORING ==========

        /// <summary>
        /// Scores a surface temperature on 0-100 scale for organism support.
        /// Extreme temperatures (both very hot and very cold) support specialized exotic organisms.
        /// Moderate Earth-like temperatures also support good diversity.
        /// </summary>
        private static int ScoreTemperature(double temperatureKelvin)
        {
            if (temperatureKelvin <= 0)
                return 0;  // Invalid

            // Extreme cold: below 50K (specialized cryogenic organisms)
            if (temperatureKelvin < 50)
                return 90;

            // Very cold: 50-150K (ice worlds, exotic cold-adapted)
            if (temperatureKelvin < 150)
                return 95;

            // Cold: 150-273K (Earth polar/winter-like)
            if (temperatureKelvin < 273)
                return 85;

            // Cool: 273-288K (Earth autumn-like to winter)
            if (temperatureKelvin < 288)
                return 75;

            // Temperate: 288-310K (Earth-like, 15-37°C)
            // Best temperature range for diverse organisms
            if (temperatureKelvin <= 310)
                return 100;

            // Warm: 310-400K (hot but not extreme)
            if (temperatureKelvin < 400)
                return 80;

            // Hot: 400-600K (desert/lava-like)
            if (temperatureKelvin < 600)
                return 85;

            // Very hot: 600-1000K (volcanic/extreme heat)
            if (temperatureKelvin < 1000)
                return 90;

            // Extreme hot: 1000K+ (lava planets, extreme thermophiles)
            return 95;
        }

        // ========== GRAVITY SCORING ==========

        /// <summary>
        /// Scores surface gravity on 0-100 scale for organism specialization.
        /// </summary>
        private static int ScoreGravity(double gravityG)
        {
            if (gravityG <= 0)
                return 0;

            // Very low gravity: 0.1-0.3G (exotic organisms thrive)
            if (gravityG < 0.3)
                return 95;

            // Low gravity: 0.3-0.7G (many exotic organisms)
            if (gravityG < 0.7)
                return 90;

            // Low-moderate: 0.7-0.9G
            if (gravityG < 0.9)
                return 85;

            // Earth-like: 0.9-1.1G (good diversity)
            if (gravityG <= 1.1)
                return 100;

            // High: 1.1-1.5G
            if (gravityG < 1.5)
                return 85;

            // Very high: 1.5-3G (harsh, fewer organisms)
            if (gravityG < 3)
                return 60;

            // Extreme: 3G+ (extreme pressure, some specialized organisms)
            return 50;
        }

        // ========== PUBLIC SCORING METHODS ==========

        /// <summary>
        /// Calculates exobiology potential score for a planet (0-100).
        ///
        /// Teaching: Multi-factor scoring algorithm
        /// - Each factor (atmosphere, type, temp, gravity, landable) scored independently
        /// - Weighted sum produces final score
        /// - Landable status adds bonus (10% boost if landable, 90% penalty if not)
        ///
        /// Example scores:
        /// - Barren rock, no atmosphere, non-landable: 5-15
        /// - Earth-like but not landable: 40-50
        /// - Exceptional landable with rich atmosphere: 85-95
        /// </summary>
        public static int ScorePlanet(PlanetScan planet)
        {
            // Score each factor independently
            var atmosphereScore = ScoreAtmosphere(planet.Atmosphere);
            var planetTypeScore = ScorePlanetType(planet.PlanetType);
            var temperatureScore = ScoreTemperature(planet.SurfaceTemperature);
            var gravityScore = ScoreGravity(planet.Gravity);

            // Calculate weighted sum
            double baseScore = (atmosphereScore * ATMOSPHERE_WEIGHT) +
                              (planetTypeScore * PLANET_TYPE_WEIGHT) +
                              (temperatureScore * TEMPERATURE_WEIGHT) +
                              (gravityScore * GRAVITY_WEIGHT);

            // Landable status: huge bonus if you can actually land
            if (planet.Landable)
            {
                baseScore = baseScore * 1.10;  // 10% bonus for being landable
            }
            else
            {
                baseScore = baseScore * 0.40;  // 60% penalty if not landable (can't collect!)
            }

            // Clamp to 0-100
            return Math.Min(100, Math.Max(0, (int)Math.Round(baseScore)));
        }

        /// <summary>
        /// Estimates profit value in credits for a planet.
        ///
        /// Teaching: Value estimation based on organism rarity
        /// - Highest value: exceptional planets with all premium organisms
        /// - Lower value: moderate potential planets
        /// - No value: bacterium-only (can't collect more than 20K profit)
        ///
        /// Formula:
        /// 1. Base value from score: score * 200K (1-20M range)
        /// 2. Landable multiplier: 5x if landable, 0.2x if not
        /// 3. Bacterium penalty: if bacterium-only, cap at 100K
        /// </summary>
        public static long EstimateValue(int score, PlanetScan planet)
        {
            if (score <= 0)
                return 0;

            // Base value: higher score = higher value
            // Formula: score * 200,000 gives us:
            // - Score 50 = 10M (reasonable mid-tier)
            // - Score 80 = 16M (high tier)
            // - Score 100 = 20M (exceptional)
            long baseValue = score * 200_000L;

            // Landable multiplier: can only harvest samples if landable
            if (planet.Landable)
            {
                baseValue = (long)(baseValue * 5.0);  // Huge multiplier for harvestable planets
            }
            else
            {
                baseValue = (long)(baseValue * 0.2);  // Signals visible but can't harvest much
            }

            // Bacterium-only: very low value
            if (planet.BacteriumOnly)
            {
                baseValue = Math.Min(baseValue, 100_000);  // Cap at 100K for bacterium
            }

            return baseValue;
        }
    }
}
