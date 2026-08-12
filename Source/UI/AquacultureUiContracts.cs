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
        {
            StableId = stableId ?? string.Empty;
            Label = label ?? string.Empty;
            WaterKind = waterKind ?? string.Empty;
            Population = population;
            Capacity = capacity;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string Label { get; private set; }
        public string WaterKind { get; private set; }
        public float Population { get; private set; }
        public float Capacity { get; private set; }
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
        {
            StableId = stableId ?? string.Empty;
            Label = label ?? string.Empty;
            Status = status ?? string.Empty;
            Revision = revision;
        }

        public string StableId { get; private set; }
        public string Label { get; private set; }
        public string Status { get; private set; }
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
}
