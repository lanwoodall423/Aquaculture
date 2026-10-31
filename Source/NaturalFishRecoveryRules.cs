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

        public static bool IsValidRecordedSourcePopulation(float population)
        {
            // Corrupt infinite source counts must never authorize migration pressure.
            return !float.IsNaN(population) && !float.IsInfinity(population) &&
                population > MinimumRecordedSourcePopulation;
        }

        public static bool HasCompatibleRecordedSource(IEnumerable<NaturalFishMigrationSource> sources,
            NaturalWaterHabitat destination, string fishDefName)
        {
            if (!IsOpen(destination) || string.IsNullOrEmpty(fishDefName)) return false;
            return (sources ?? Enumerable.Empty<NaturalFishMigrationSource>()).Any(source =>
                AreCompatible(destination, source.habitat) && source.fishDefName == fishDefName &&
                IsValidRecordedSourcePopulation(source.population));
        }

        public static bool NeedsRegionalRecovery(float population, float breedingFloor)
        {
            // Corrupt saved populations or breeding thresholds must not authorize migration.
            if (float.IsNaN(population) || float.IsInfinity(population) ||
                float.IsNaN(breedingFloor) || float.IsInfinity(breedingFloor)) return false;
            return population <= System.Math.Max(0f, breedingFloor);
        }
    }

    /// <summary>
    /// Portable AQ-EXP-01 food coverage drill. These are aggregate estimates, not fish mortality
    /// predictions; no colony, species, game map, knowledge or saved state is inspected.
    /// </summary>
    public enum PondFeederOutageRisk
    {
        InsufficientData,
        NoDemand,
        Covered,
        PossibleShortfall,
        CertainShortfall
    }

    /// <summary>
    /// A deliberately small immutable snapshot. Natural food rates are lower/upper estimates,
    /// not measurements obtained by bypassing knowledge or research restrictions.
    /// </summary>
    public readonly struct PondFeederOutageInput
    {
        public readonly int HorizonHours;
        public readonly double FishDemandPerHour;
        public readonly double PreparedFeed;
        public readonly double NaturalFoodLowPerHour;
        public readonly double NaturalFoodHighPerHour;

        public PondFeederOutageInput(int horizonHours, double fishDemandPerHour, double preparedFeed,
            double naturalFoodLowPerHour, double naturalFoodHighPerHour)
        {
            HorizonHours = horizonHours;
            FishDemandPerHour = fishDemandPerHour;
            PreparedFeed = preparedFeed;
            NaturalFoodLowPerHour = naturalFoodLowPerHour;
            NaturalFoodHighPerHour = naturalFoodHighPerHour;
        }
    }

    public readonly struct PondFeederOutagePrediction
    {
        public readonly PondFeederOutageRisk Risk;
        public readonly double? MinimumFoodShortfall;
        public readonly double? MaximumFoodShortfall;
        public readonly double? ConservativeCoverageHours;
        public readonly double? OptimisticCoverageHours;

        internal PondFeederOutagePrediction(PondFeederOutageRisk risk, double? minimumFoodShortfall,
            double? maximumFoodShortfall, double? conservativeCoverageHours, double? optimisticCoverageHours)
        {
            Risk = risk;
            MinimumFoodShortfall = minimumFoodShortfall;
            MaximumFoodShortfall = maximumFoodShortfall;
            ConservativeCoverageHours = conservativeCoverageHours;
            OptimisticCoverageHours = optimisticCoverageHours;
        }
    }

    public static class PondFeederOutageRiskRules
    {
        /// <summary>
        /// Considers only food supply at fixed hourly demand over 24 or 48 hours.
        /// No algae/organism regrowth, consumption competition, temperature, predation,
        /// starvation timing or changes to demand are simulated. Invalid or unknown data
        /// never produces a reassuring result.
        /// </summary>
        public static PondFeederOutagePrediction Evaluate(PondFeederOutageInput input)
        {
            if ((input.HorizonHours != 24 && input.HorizonHours != 48) ||
                !FiniteNonNegative(input.FishDemandPerHour) ||
                !FiniteNonNegative(input.PreparedFeed) ||
                !FiniteNonNegative(input.NaturalFoodLowPerHour) ||
                !FiniteNonNegative(input.NaturalFoodHighPerHour) ||
                input.NaturalFoodLowPerHour > input.NaturalFoodHighPerHour)
                return InsufficientData();

            double required = input.FishDemandPerHour * input.HorizonHours;
            double minimumAvailable = input.PreparedFeed +
                input.NaturalFoodLowPerHour * input.HorizonHours;
            double maximumAvailable = input.PreparedFeed +
                input.NaturalFoodHighPerHour * input.HorizonHours;
            if (!FiniteNonNegative(required) || !FiniteNonNegative(minimumAvailable) ||
                !FiniteNonNegative(maximumAvailable))
                return InsufficientData();

            if (input.FishDemandPerHour == 0d)
                return new PondFeederOutagePrediction(PondFeederOutageRisk.NoDemand,
                    0d, 0d, input.HorizonHours, input.HorizonHours);

            double minimumShortfall = System.Math.Max(0d, required - maximumAvailable);
            double maximumShortfall = System.Math.Max(0d, required - minimumAvailable);
            PondFeederOutageRisk risk = maximumShortfall == 0d
                ? PondFeederOutageRisk.Covered
                : minimumShortfall > 0d
                    ? PondFeederOutageRisk.CertainShortfall
                    : PondFeederOutageRisk.PossibleShortfall;

            return new PondFeederOutagePrediction(risk, minimumShortfall, maximumShortfall,
                CoverageHours(input.HorizonHours, input.FishDemandPerHour,
                    input.PreparedFeed, input.NaturalFoodLowPerHour),
                CoverageHours(input.HorizonHours, input.FishDemandPerHour,
                    input.PreparedFeed, input.NaturalFoodHighPerHour));
        }

        private static bool FiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static double CoverageHours(int horizon, double hourlyDemand, double reserve,
            double naturalFoodPerHour)
        {
            double netDemand = hourlyDemand - naturalFoodPerHour;
            if (netDemand <= 0d || reserve >= netDemand * horizon) return horizon;
            return reserve / netDemand;
        }

        private static PondFeederOutagePrediction InsufficientData()
        {
            return new PondFeederOutagePrediction(PondFeederOutageRisk.InsufficientData,
                null, null, null, null);
        }
    }
}
