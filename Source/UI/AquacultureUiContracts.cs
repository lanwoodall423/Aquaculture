using System;
using System.Collections.Generic;

namespace AquacultureFishing
{
    /// <summary>Pure responsive-layout decisions shared by the settings document and tests.</summary>
    public enum AquacultureUiNavigationMode
    {
        Rail,
        Compact
    }

    /// <summary>Stable, human-readable IDs for document-local state and diagnostics.</summary>
    public static class AquacultureUiStableIds
    {
        public static string For(string scope, string entity)
        {
            string left = string.IsNullOrWhiteSpace(scope) ? "ui" : scope.Trim();
            string right = string.IsNullOrWhiteSpace(entity) ? "item" : entity.Trim();
            return left + "." + right;
        }

        public static IReadOnlyList<string> FindDuplicates(IEnumerable<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<string>();
            if (ids == null) return duplicates.AsReadOnly();
            foreach (string id in ids)
            {
                if (id == null) continue;
                if (!seen.Add(id) && !duplicates.Contains(id)) duplicates.Add(id);
            }
            return duplicates.AsReadOnly();
        }
    }

    /// <summary>Portable layout math used to keep narrow settings navigation predictable.</summary>
    public static class AquacultureUiResponsiveLayout
    {
        public static AquacultureUiNavigationMode NavigationMode(float width, float breakpoint = 720f)
        {
            return width < Math.Max(360f, breakpoint)
                ? AquacultureUiNavigationMode.Compact
                : AquacultureUiNavigationMode.Rail;
        }

        public static int CompactColumns(float width, float itemWidth = 96f, float gap = 5f, int itemCount = 6)
        {
            if (itemCount <= 0) return 1;
            float denominator = Math.Max(1f, itemWidth + gap);
            int columns = (int)Math.Floor((Math.Max(0f, width) + gap) / denominator);
            return Math.Max(1, Math.Min(itemCount, columns));
        }
    }

    /// <summary>Immutable display-only fish row snapshot. It contains no Thing or map ownership.</summary>
    public sealed class FishUiSnapshot
    {
        public FishUiSnapshot(string stableId, string defName, string label, string minimumExpertise)
        {
            StableId = stableId ?? string.Empty;
            DefName = defName ?? string.Empty;
            Label = label ?? string.Empty;
            MinimumExpertise = minimumExpertise ?? string.Empty;
        }

        public string StableId { get; private set; }
        public string DefName { get; private set; }
        public string Label { get; private set; }
        public string MinimumExpertise { get; private set; }
    }

    /// <summary>Immutable species display snapshot for later Insight Canvas screens.</summary>
    public sealed class SpeciesUiSnapshot
    {
        public SpeciesUiSnapshot(string stableId, string defName, string label, int revision)
        {
            StableId = stableId ?? string.Empty;
            DefName = defName ?? string.Empty;
            Label = label ?? string.Empty;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string DefName { get; private set; }
        public string Label { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Immutable pond status snapshot for future pond-owned views.</summary>
    public sealed class PondUiSnapshot
    {
        public PondUiSnapshot(string stableId, string label, string waterKind, float population,
            float capacity, int revision)
            : this(stableId, label, waterKind, population, capacity, 0f, 0f, revision)
        {
        }

        public PondUiSnapshot(string stableId, string label, string waterKind, float population,
            float capacity, float feedHours, float temperature, int revision)
        {
            StableId = stableId ?? string.Empty;
            Label = label ?? string.Empty;
            WaterKind = waterKind ?? string.Empty;
            Population = population;
            Capacity = capacity;
            FeedHours = feedHours;
            Temperature = temperature;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string Label { get; private set; }
        public string WaterKind { get; private set; }
        public float Population { get; private set; }
        public float Capacity { get; private set; }
        public float FeedHours { get; private set; }
        public float Temperature { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Immutable breed display snapshot for later Insight Canvas screens.</summary>
    public sealed class BreedUiSnapshot
    {
        public BreedUiSnapshot(string stableId, string speciesId, string label, int revision)
        {
            StableId = stableId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            Label = label ?? string.Empty;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string SpeciesId { get; private set; }
        public string Label { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Immutable population status snapshot; it never owns or mutates simulation state.</summary>
    public sealed class PopulationUiSnapshot
    {
        public PopulationUiSnapshot(string stableId, string speciesId, float current, float capacity, int revision)
        {
            StableId = stableId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            Current = current;
            Capacity = capacity;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string SpeciesId { get; private set; }
        public float Current { get; private set; }
        public float Capacity { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Immutable commission status snapshot for future substantial dialogs.</summary>
    public sealed class CommissionUiSnapshot
    {
        public CommissionUiSnapshot(string stableId, string label, string status, int revision)
            : this(stableId, label, status, string.Empty, 0, revision)
        {
        }

        public CommissionUiSnapshot(string stableId, string label, string status,
            string requirements, int eligibleSpecimens, int revision)
        {
            StableId = stableId ?? string.Empty;
            Label = label ?? string.Empty;
            Status = status ?? string.Empty;
            Requirements = requirements ?? string.Empty;
            EligibleSpecimens = eligibleSpecimens;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string Label { get; private set; }
        public string Status { get; private set; }
        public string Requirements { get; private set; }
        public int EligibleSpecimens { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Immutable trait catalog row used by the searchable Advanced page.</summary>
    public sealed class TraitUiSnapshot
    {
        public TraitUiSnapshot(string stableId, string defName, string label, string effectLine,
            string description, float defaultWeight)
        {
            StableId = stableId ?? string.Empty;
            DefName = defName ?? string.Empty;
            Label = label ?? string.Empty;
            EffectLine = effectLine ?? string.Empty;
            Description = description ?? string.Empty;
            DefaultWeight = defaultWeight;
        }

        public string StableId { get; private set; }
        public string DefName { get; private set; }
        public string Label { get; private set; }
        public string EffectLine { get; private set; }
        public string Description { get; private set; }
        public float DefaultWeight { get; private set; }
    }

    /// <summary>Immutable settings-catalog snapshot. Lists are copied at construction and never exposed mutably.</summary>
    public sealed class SettingsUiSnapshot
    {
        public SettingsUiSnapshot(int revision, IEnumerable<string> pageIds,
            IEnumerable<FishUiSnapshot> fish, IEnumerable<TraitUiSnapshot> traits)
        {
            Revision = revision;
            PageIds = Copy(pageIds);
            Fish = Copy(fish);
            Traits = Copy(traits);
        }

        public int Revision { get; private set; }
        public IReadOnlyList<string> PageIds { get; private set; }
        public IReadOnlyList<FishUiSnapshot> Fish { get; private set; }
        public IReadOnlyList<TraitUiSnapshot> Traits { get; private set; }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            return new List<T>(values ?? new T[0]).AsReadOnly();
        }
    }

    /// <summary>Explicit catalog revision boundary; invalidation never performs a world or map query.</summary>
    public sealed class AquacultureUiSnapshotCache
    {
        public int Revision { get; private set; }

        public void Invalidate()
        {
            Revision++;
        }
    }

    /// <summary>Player-facing ordering for pond health. Higher severity wins; healthy is the fallback.</summary>
    public enum AquacultureHealthPriority
    {
        Healthy,
        Advice,
        Reproduction,
        SevereStress,
        Starvation,
        Lethal
    }

    /// <summary>Pure health-priority rules shared by runtime presentation and portable tests.</summary>
    public static class AquacultureHealthPriorityRules
    {
        public static AquacultureHealthPriority Highest(bool lethal, bool starvation, bool severeStress,
            bool reproductionBlocked, bool advice)
        {
            if (lethal) return AquacultureHealthPriority.Lethal;
            if (starvation) return AquacultureHealthPriority.Starvation;
            if (severeStress) return AquacultureHealthPriority.SevereStress;
            if (reproductionBlocked) return AquacultureHealthPriority.Reproduction;
            return advice ? AquacultureHealthPriority.Advice : AquacultureHealthPriority.Healthy;
        }

        public static int Rank(AquacultureHealthPriority priority) => (int)priority;
    }

    /// <summary>Immutable, map-free workspace row. Runtime adapters populate it before Paint.</summary>
    public sealed class AquacultureWorkspaceRowSnapshot
    {
        public AquacultureWorkspaceRowSnapshot(string stableId, string label, string status,
            AquacultureHealthPriority priority, IEnumerable<string> details, int revision)
        {
            StableId = stableId ?? string.Empty;
            Label = label ?? string.Empty;
            Status = status ?? string.Empty;
            Priority = priority;
            Details = new List<string>(details ?? new string[0]).AsReadOnly();
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string Label { get; private set; }
        public string Status { get; private set; }
        public AquacultureHealthPriority Priority { get; private set; }
        public IReadOnlyList<string> Details { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Immutable fish dossier data. It deliberately contains display strings, not live Thing references.</summary>
    public sealed class AquacultureFishDossierSnapshot
    {
        public AquacultureFishDossierSnapshot(string stableId, string species, string sex, string lifeStage,
            string breed, string generation, string condition, string production, IEnumerable<string> traits,
            string knowledge, string knowledgeLink, int revision)
        {
            StableId = stableId ?? string.Empty;
            Species = species ?? string.Empty;
            Sex = sex ?? string.Empty;
            LifeStage = lifeStage ?? string.Empty;
            Breed = breed ?? string.Empty;
            Generation = generation ?? string.Empty;
            Condition = condition ?? string.Empty;
            Production = production ?? string.Empty;
            Traits = new List<string>(traits ?? new string[0]).AsReadOnly();
            Knowledge = knowledge ?? string.Empty;
            KnowledgeLink = knowledgeLink ?? string.Empty;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string Species { get; private set; }
        public string Sex { get; private set; }
        public string LifeStage { get; private set; }
        public string Breed { get; private set; }
        public string Generation { get; private set; }
        public string Condition { get; private set; }
        public string Production { get; private set; }
        public IReadOnlyList<string> Traits { get; private set; }
        public string Knowledge { get; private set; }
        public string KnowledgeLink { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Pure planner output copied from the authoritative forecast calculation.</summary>
    public sealed class AquaculturePlannerForecastSnapshot
    {
        public AquaculturePlannerForecastSnapshot(int totalFish, int physicalCapacity, int sustainableCapacity,
            int industrialCapacity, float dailyDemand, float feedNeeded, bool danger, bool predationRisk,
            IEnumerable<string> warnings, string cacheKey, int revision)
        {
            TotalFish = totalFish;
            PhysicalCapacity = physicalCapacity;
            SustainableCapacity = sustainableCapacity;
            IndustrialCapacity = industrialCapacity;
            DailyDemand = dailyDemand;
            FeedNeeded = feedNeeded;
            Danger = danger;
            PredationRisk = predationRisk;
            Warnings = new List<string>(warnings ?? new string[0]).AsReadOnly();
            CacheKey = cacheKey ?? string.Empty;
            Revision = revision;
        }

        public int TotalFish { get; private set; }
        public int PhysicalCapacity { get; private set; }
        public int SustainableCapacity { get; private set; }
        public int IndustrialCapacity { get; private set; }
        public float DailyDemand { get; private set; }
        public float FeedNeeded { get; private set; }
        public bool Danger { get; private set; }
        public bool PredationRisk { get; private set; }
        public IReadOnlyList<string> Warnings { get; private set; }
        public string CacheKey { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>Deterministic planner cache key contract; no game or map access is performed here.</summary>
    public static class AquaculturePlannerCacheContract
    {
        public static string Key(IEnumerable<string> orderedPlan, string water, int revision)
        {
            return (water ?? string.Empty) + "|" + revision + "|" +
                string.Join(";", orderedPlan ?? new string[0]);
        }
    }
}
