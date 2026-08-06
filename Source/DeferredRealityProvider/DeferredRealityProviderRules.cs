using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace DeferredReality.Aquaculture
{
    /// <summary>Dependency-free identity and state rules shared by the provider and executable tests.</summary>
    public static class DeferredRealityProviderRules
    {
        public const string ProviderId = "lan.aquaculture.natural-water";
        public const int SemanticApiVersion = 2;
        public const int SchemaVersion = 2;
        public const int MinimumProviderSemanticApiVersion = 2;
        public const int MinimumDeferredRealitySchemaVersion = 5;
        public const int MaximumDynamicRecords = 4096;
        public const string NaturalWaterKind = "natural-water";
        public const string SequenceDomain = "natural-water-population";
        public const string MigrationPrefix = "aquaculture.natural-water.migration";

        public static string RegionId(int worldTile, string stableInstance)
        {
            return Stable("region", worldTile.ToString(CultureInfo.InvariantCulture), stableInstance);
        }

        public static string MapAlias(string providerId, int mapUniqueId, int worldTile)
        {
            return Stable("map-alias", providerId, mapUniqueId.ToString(CultureInfo.InvariantCulture),
                worldTile.ToString(CultureInfo.InvariantCulture));
        }

        public static string WaterBodyId(string regionId, string habitat, int anchorX, int anchorZ, int cellCount)
        {
            return Stable("water", regionId, habitat, anchorX.ToString(CultureInfo.InvariantCulture),
                anchorZ.ToString(CultureInfo.InvariantCulture), cellCount.ToString(CultureInfo.InvariantCulture));
        }

        public static string PopulationId(string regionId, string waterBodyId, string fishDefName)
        {
            return ProviderId + ":" + NaturalWaterKind + ":" + (regionId ?? string.Empty) + ":" +
                PopulationSubject(waterBodyId, fishDefName);
        }

        public static string PopulationSubject(string waterBodyId, string fishDefName)
        {
            return (waterBodyId ?? string.Empty) + ":fish:" + (fishDefName ?? string.Empty);
        }

        public static string ProcessId(string populationId)
        {
            // Preserve the historical provider process key so old DRF saves are upgraded in place.
            return "aquaculture:population:" + (populationId ?? string.Empty);
        }

        public static string TopologyId(string regionId, string waterBodyId)
        {
            return Stable("topology", ProviderId, regionId, waterBodyId);
        }

        public static string ConstraintId(string regionId, string populationId, string kind)
        {
            return Stable("constraint", ProviderId, regionId, populationId, kind);
        }

        public static string AnchorId(string regionId, string waterBodyId)
        {
            return Stable("anchor", ProviderId, regionId, waterBodyId);
        }

        public static string MigrationId(string regionId)
        {
            return Stable(MigrationPrefix, ProviderId, regionId);
        }

        public static string OperationId(string kind, string regionId, string waterBodyId, string fishDefName,
            string logicalEventId)
        {
            return Stable("operation", ProviderId, kind, regionId, waterBodyId, fishDefName, logicalEventId);
        }

        public static string Stable(string prefix, params string[] parts)
        {
            var builder = new StringBuilder(prefix ?? string.Empty);
            foreach (string part in parts ?? Array.Empty<string>())
            {
                string value = part ?? string.Empty;
                builder.Append(':').Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
            }
            return builder.ToString();
        }

        public static bool IsOpenHabitat(string habitat)
        {
            return string.Equals(habitat, "River", StringComparison.Ordinal) ||
                string.Equals(habitat, "Coastal", StringComparison.Ordinal) ||
                string.Equals(habitat, "Ocean", StringComparison.Ordinal);
        }

        public static bool AreCompatibleHabitats(string source, string destination)
        {
            return IsOpenHabitat(source) && string.Equals(source, destination, StringComparison.Ordinal);
        }

        public static bool CanRecover(string sourceSpecies, string destinationSpecies, string sourceHabitat,
            string destinationHabitat, float sourcePopulation, float destinationPopulation, float breedingFloor)
        {
            return !string.IsNullOrEmpty(sourceSpecies) && string.Equals(sourceSpecies, destinationSpecies, StringComparison.Ordinal) &&
                AreCompatibleHabitats(sourceHabitat, destinationHabitat) && IsFinitePositive(sourcePopulation) &&
                IsFinite(destinationPopulation) && destinationPopulation <= Math.Max(0f, breedingFloor);
        }

        public static float NormalizeNonNegative(float value, float fallback = 0f, float maximum = float.MaxValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Math.Max(0f, Math.Min(maximum, value));
        }

        public static float AdvancePopulation(float amount, float capacity, float elapsedTicks, float suitability)
        {
            amount = NormalizeNonNegative(amount);
            capacity = Math.Max(0f, NormalizeNonNegative(capacity));
            elapsedTicks = NormalizeNonNegative(elapsedTicks);
            suitability = Math.Max(0f, Math.Min(1f, NormalizeNonNegative(suitability, 1f)));
            if (capacity <= 0f) return 0f;
            float days = elapsedTicks / 60000f;
            float next = amount + (amount >= 2f ? amount * 0.035f * days * suitability : 0f) - amount * 0.01f * days;
            return Math.Max(0f, Math.Min(capacity, NormalizeNonNegative(next)));
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        public static bool IsValidPopulation(string populationId, string providerId, string regionId, string subjectId,
            float amount, float capacity)
        {
            return !string.IsNullOrEmpty(populationId) && providerId == ProviderId && !string.IsNullOrEmpty(regionId) &&
                !string.IsNullOrEmpty(subjectId) && IsFinite(amount) && amount >= 0f && IsFinite(capacity) && capacity >= 0f;
        }

        public static bool IsWithinRecordLimit(int count)
        {
            return count >= 0 && count <= MaximumDynamicRecords;
        }

        /// <summary>Models the provider's durable-marker rule for executable tests.</summary>
        public static bool ApplyExactlyOnce(ISet<string> appliedOperations, string operationId, Func<bool> mutation)
        {
            if (appliedOperations == null || string.IsNullOrEmpty(operationId) || mutation == null) return false;
            if (appliedOperations.Contains(operationId)) return true;
            if (!mutation()) return false;
            appliedOperations.Add(operationId);
            return true;
        }
    }
}
