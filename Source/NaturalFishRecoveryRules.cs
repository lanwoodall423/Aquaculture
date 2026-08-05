using System.Collections.Generic;
using System.Linq;

namespace AquacultureFishing
{
    public enum NaturalWaterHabitat
    {
        Pond,
        Lake,
        River,
        Marsh,
        Coastal,
        Ocean
    }

    public readonly struct NaturalFishMigrationSource
    {
        public readonly NaturalWaterHabitat habitat;
        public readonly string fishDefName;
        public readonly float population;

        public NaturalFishMigrationSource(NaturalWaterHabitat habitat, string fishDefName, float population)
        {
            this.habitat = habitat;
            this.fishDefName = fishDefName;
            this.population = population;
        }
    }

    public static class NaturalFishMigrationRules
    {
        public const float MinimumRecordedSourcePopulation = 0f;

        // Ponds, lakes, and marshes are closed. Open-water pressure is same-category only.
        public static bool IsOpen(NaturalWaterHabitat habitat)
        {
            return habitat == NaturalWaterHabitat.River || habitat == NaturalWaterHabitat.Coastal ||
                habitat == NaturalWaterHabitat.Ocean;
        }

        public static bool AreCompatible(NaturalWaterHabitat first, NaturalWaterHabitat second)
        {
            return IsOpen(first) && first == second;
        }

        public static bool HasCompatibleRecordedSource(IEnumerable<NaturalFishMigrationSource> sources,
            NaturalWaterHabitat destination, string fishDefName)
        {
            if (!IsOpen(destination) || string.IsNullOrEmpty(fishDefName)) return false;
            return (sources ?? Enumerable.Empty<NaturalFishMigrationSource>()).Any(source =>
                AreCompatible(destination, source.habitat) && source.fishDefName == fishDefName &&
                source.population > MinimumRecordedSourcePopulation);
        }

        public static bool NeedsRegionalRecovery(float population, float breedingFloor)
        {
            return population <= System.Math.Max(0f, breedingFloor);
        }
    }
}
