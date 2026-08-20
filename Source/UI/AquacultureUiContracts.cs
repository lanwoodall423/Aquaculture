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

    /// <summary>Portable master/detail modes used by workspace documents at different widths.</summary>
    public enum AquacultureUiMasterDetailMode
    {
        SideBySide,
        Stacked
    }

    /// <summary>Persisted Aquaculture presentation-density values. The numeric order is a save contract.</summary>
    public enum AquaculturePresentationDensity
    {
        Comfortable = 0,
        Normal = 1,
        Compact = 2
    }

    /// <summary>
    /// Immutable, engine-independent presentation preferences. AquacultureSettings owns the serialized
    /// fields; this contract owns their safe defaults and normalization so portable tests can exercise
    /// reload behavior without loading RimWorld or Insight Canvas.
    /// </summary>
    public struct AquaculturePresentationPreferences : IEquatable<AquaculturePresentationPreferences>
    {
        public const int MinimumDensityIndex = (int)AquaculturePresentationDensity.Comfortable;
        public const int MaximumDensityIndex = (int)AquaculturePresentationDensity.Compact;
        public const int DefaultDensityIndex = (int)AquaculturePresentationDensity.Normal;

        public AquaculturePresentationPreferences(int densityIndex, bool highContrast, bool reducedMotion)
        {
            DensityIndex = ClampDensityIndex(densityIndex);
            HighContrast = highContrast;
            ReducedMotion = reducedMotion;
        }

        public int DensityIndex { get; private set; }
        public bool HighContrast { get; private set; }
        public bool ReducedMotion { get; private set; }

        public static AquaculturePresentationPreferences Default =>
            new AquaculturePresentationPreferences(DefaultDensityIndex, false, false);

        public static AquaculturePresentationPreferences FromSerializedValues(int densityIndex,
            bool highContrast, bool reducedMotion)
        {
            return new AquaculturePresentationPreferences(densityIndex, highContrast, reducedMotion);
        }

        public static int ClampDensityIndex(int value)
        {
            return value < MinimumDensityIndex ? MinimumDensityIndex :
                value > MaximumDensityIndex ? MaximumDensityIndex : value;
        }

        public bool Equals(AquaculturePresentationPreferences other)
        {
            return DensityIndex == other.DensityIndex && HighContrast == other.HighContrast &&
                ReducedMotion == other.ReducedMotion;
        }

        public override bool Equals(object obj)
        {
            return obj is AquaculturePresentationPreferences &&
                Equals((AquaculturePresentationPreferences)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DensityIndex;
                hash = hash * 31 + (HighContrast ? 1 : 0);
                return hash * 31 + (ReducedMotion ? 1 : 0);
            }
        }

        public static bool operator ==(AquaculturePresentationPreferences left,
            AquaculturePresentationPreferences right) => left.Equals(right);

        public static bool operator !=(AquaculturePresentationPreferences left,
            AquaculturePresentationPreferences right) => !left.Equals(right);
    }

    /// <summary>Semantic status categories used by shared Aquaculture compositions.</summary>
    public enum AquacultureUiStatus
    {
        Healthy,
        Attention,
        Critical,
        Unknown
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

        public static AquacultureUiMasterDetailMode MasterDetailMode(float width, float breakpoint = 960f)
        {
            return width < Math.Max(360f, breakpoint)
                ? AquacultureUiMasterDetailMode.Stacked
                : AquacultureUiMasterDetailMode.SideBySide;
        }

        public static int CompactColumns(float width, float itemWidth = 96f, float gap = 5f, int itemCount = 6)
        {
            if (itemCount <= 0) return 1;
            float denominator = Math.Max(1f, itemWidth + gap);
            int columns = (int)Math.Floor((Math.Max(0f, width) + gap) / denominator);
            return Math.Max(1, Math.Min(itemCount, columns));
        }
    }

    /// <summary>
    /// Pure viewport math for Insight Canvas virtual lists. Empty lists collapse, short lists
    /// measure to their content, and long lists receive a bounded virtualized viewport.
    /// </summary>
    public static class AquacultureUiListSizing
    {
        public static float HeightForCount(int itemCount, float rowHeight, float minimumHeight,
            float maximumHeight)
        {
            if (itemCount <= 0) return 0f;

            float safeRowHeight = IsFinite(rowHeight) ? Math.Max(0f, rowHeight) : 0f;
            float safeMinimum = IsFinite(minimumHeight) ? Math.Max(0f, minimumHeight) : 0f;
            float safeMaximum = IsFinite(maximumHeight) && maximumHeight > 0f
                ? maximumHeight : float.PositiveInfinity;
            safeMinimum = Math.Min(safeMinimum, safeMaximum);

            float desired = itemCount * safeRowHeight;
            if (float.IsInfinity(desired) || float.IsNaN(desired)) desired = safeMaximum;
            return Math.Min(safeMaximum, Math.Max(safeMinimum, desired));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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

    /// <summary>Player-facing pond filters. The values are a UI contract, not simulation state.</summary>
    public enum AquaculturePondFilter
    {
        All,
        NeedsAttention,
        Critical,
        Healthy
    }

    /// <summary>Pure, deterministic ordering and filtering for the Journal pond workspace.</summary>
    public static class AquacultureWorkspaceOrdering
    {
        public static int Compare(AquacultureWorkspaceRowSnapshot left, AquacultureWorkspaceRowSnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int priority = AquacultureHealthPriorityRules.Rank(right.Priority)
                .CompareTo(AquacultureHealthPriorityRules.Rank(left.Priority));
            if (priority != 0) return priority;
            int label = StringComparer.OrdinalIgnoreCase.Compare(left.Label, right.Label);
            return label != 0 ? label : StringComparer.Ordinal.Compare(left.StableId, right.StableId);
        }

        public static bool Matches(AquacultureWorkspaceRowSnapshot row, string query, AquaculturePondFilter filter)
        {
            if (row == null) return false;
            bool categoryMatch;
            switch (filter)
            {
                case AquaculturePondFilter.NeedsAttention:
                    categoryMatch = row.Priority != AquacultureHealthPriority.Healthy;
                    break;
                case AquaculturePondFilter.Critical:
                    categoryMatch = AquacultureHealthPriorityRules.Rank(row.Priority) >=
                        AquacultureHealthPriorityRules.Rank(AquacultureHealthPriority.SevereStress);
                    break;
                case AquaculturePondFilter.Healthy:
                    categoryMatch = row.Priority == AquacultureHealthPriority.Healthy;
                    break;
                default:
                    categoryMatch = true;
                    break;
            }
            if (!categoryMatch) return false;
            string normalized = query == null ? string.Empty : query.Trim();
            if (normalized.Length == 0) return true;
            if (Contains(row.Label, normalized) || Contains(row.Status, normalized)) return true;
            for (int i = 0; i < row.Details.Count; i++)
                if (Contains(row.Details[i], normalized)) return true;
            return false;
        }

        public static string PreserveSelection(string selectedId, IEnumerable<string> availableIds,
            string fallbackId = null)
        {
            string first = string.Empty;
            bool selectedAvailable = false;
            bool fallbackAvailable = false;
            foreach (string id in availableIds ?? new string[0])
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (first.Length == 0) first = id;
                if (id == selectedId) selectedAvailable = true;
                if (id == fallbackId) fallbackAvailable = true;
            }
            if (selectedAvailable) return selectedId;
            if (fallbackAvailable) return fallbackId;
            return first;
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>Semantic health state for the individual fish dossier.</summary>
    public enum AquacultureDossierHealthState
    {
        Unknown,
        Healthy,
        Attention,
        Critical,
        Dead
    }

    /// <summary>Pure thresholds for dossier presentation; gameplay systems remain authoritative.</summary>
    public static class AquacultureDossierHealthRules
    {
        public static AquacultureDossierHealthState Classify(bool alive, float foodReserve,
            float habitatFit, float starvationProgress)
        {
            if (!alive) return AquacultureDossierHealthState.Dead;
            if (Unknown(foodReserve) || Unknown(habitatFit) || Unknown(starvationProgress))
                return AquacultureDossierHealthState.Unknown;
            if (starvationProgress > 0f || foodReserve <= 0.20f || habitatFit <= 0.25f)
                return AquacultureDossierHealthState.Critical;
            if (foodReserve < 0.50f || habitatFit < 0.75f)
                return AquacultureDossierHealthState.Attention;
            return AquacultureDossierHealthState.Healthy;
        }

        private static bool Unknown(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f;
        }
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
            : this(stableId, species, sex, lifeStage, breed, generation, condition, production, traits,
                knowledge, knowledgeLink, -1f, -1f, -1f, -1, false,
                AquacultureDossierHealthState.Unknown, revision)
        {
        }

        public AquacultureFishDossierSnapshot(string stableId, string species, string sex, string lifeStage,
            string breed, string generation, string condition, string production, IEnumerable<string> traits,
            string knowledge, string knowledgeLink, float foodReserve, float habitatFit,
            float starvationProgress, int expectedMeatYield, bool sterilized,
            AquacultureDossierHealthState healthState, int revision)
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
            FoodReserve = foodReserve;
            HabitatFit = habitatFit;
            StarvationProgress = starvationProgress;
            ExpectedMeatYield = expectedMeatYield;
            Sterilized = sterilized;
            HealthState = healthState;
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
        public float FoodReserve { get; private set; }
        public float HabitatFit { get; private set; }
        public float StarvationProgress { get; private set; }
        public int ExpectedMeatYield { get; private set; }
        public bool Sterilized { get; private set; }
        public AquacultureDossierHealthState HealthState { get; private set; }
        public int Revision { get; private set; }
    }

    /// <summary>
    /// Immutable presentation permissions for the Journal. Research fields describe capabilities;
    /// knowledge/gameplay fields describe meaningful colony state. No simulation or knowledge data is
    /// stored here.
    /// </summary>
    public sealed class AquacultureUiDisclosureSnapshot : IEquatable<AquacultureUiDisclosureSnapshot>
    {
        public AquacultureUiDisclosureSnapshot(bool pondkeepingAvailable, bool managedAquacultureAvailable,
            bool industrialAquacultureAvailable, bool selectiveBreedingAvailable, bool hasPonds,
            bool hasKnownSpecies, bool hasEstablishedSpecies, bool hasKnownHealth, bool hasKnownPopulation,
            bool hasConservationKnowledge, bool hasBreeds, bool hasActiveCommission)
        {
            PondkeepingAvailable = pondkeepingAvailable;
            ManagedAquacultureAvailable = managedAquacultureAvailable;
            IndustrialAquacultureAvailable = industrialAquacultureAvailable;
            SelectiveBreedingAvailable = selectiveBreedingAvailable;
            HasPonds = hasPonds;
            HasKnownSpecies = hasKnownSpecies;
            HasEstablishedSpecies = hasEstablishedSpecies;
            HasKnownHealth = hasKnownHealth;
            HasKnownPopulation = hasKnownPopulation;
            HasConservationKnowledge = hasConservationKnowledge;
            HasBreeds = hasBreeds;
            HasActiveCommission = hasActiveCommission;
        }

        public static AquacultureUiDisclosureSnapshot None => new AquacultureUiDisclosureSnapshot(
            false, false, false, false, false, false, false, false, false, false, false, false);

        public bool PondkeepingAvailable { get; private set; }
        public bool ManagedAquacultureAvailable { get; private set; }
        public bool IndustrialAquacultureAvailable { get; private set; }
        public bool SelectiveBreedingAvailable { get; private set; }
        public bool HasPonds { get; private set; }
        public bool HasKnownSpecies { get; private set; }
        public bool HasEstablishedSpecies { get; private set; }
        public bool HasKnownHealth { get; private set; }
        public bool HasKnownPopulation { get; private set; }
        public bool HasConservationKnowledge { get; private set; }
        public bool HasBreeds { get; private set; }
        public bool HasActiveCommission { get; private set; }

        public bool SpeciesPageVisible => true;
        public bool PondsPageVisible => PondkeepingAvailable || HasPonds;
        public bool BreedsPageVisible => SelectiveBreedingAvailable || HasBreeds;
        public bool ConservationPageVisible => HasConservationKnowledge;
        public bool CommissionsPageVisible => HasActiveCommission || HasBreeds;

        public bool BasicPondFactsVisible => PondsPageVisible;
        public bool ManagedFeedingVisible => ManagedAquacultureAvailable;
        public bool ManagedHarvestingVisible => ManagedAquacultureAvailable;
        public bool IndustrialDiagnosticsVisible => IndustrialAquacultureAvailable;
        public bool PopulationManagementVisible => IndustrialAquacultureAvailable;
        public bool SelectiveBreedingControlsVisible => SelectiveBreedingAvailable;

        /// <summary>Industrial diagnostics still require a relevant learned health observation.</summary>
        public bool PondDiagnosisVisible => IndustrialAquacultureAvailable && HasKnownHealth;
        public bool ExactPondMetricsVisible => PondDiagnosisVisible;
        public bool CriticalPondFilterVisible => PondDiagnosisVisible;

        public IReadOnlyList<string> VisibleJournalPageIds
        {
            get
            {
                List<string> pages = new List<string> { "overview", "species" };
                if (PondsPageVisible) pages.Add("ponds");
                if (BreedsPageVisible) pages.Add("breeds");
                if (ConservationPageVisible) pages.Add("conservation");
                if (CommissionsPageVisible) pages.Add("commissions");
                return pages.AsReadOnly();
            }
        }

        public string ResolveActiveJournalPage(string requestedPage)
        {
            if (string.IsNullOrEmpty(requestedPage)) return "overview";
            IReadOnlyList<string> pages = VisibleJournalPageIds;
            for (int i = 0; i < pages.Count; i++)
                if (pages[i] == requestedPage) return requestedPage;
            return "overview";
        }

        public bool Equals(AquacultureUiDisclosureSnapshot other)
        {
            return other != null &&
                PondkeepingAvailable == other.PondkeepingAvailable &&
                ManagedAquacultureAvailable == other.ManagedAquacultureAvailable &&
                IndustrialAquacultureAvailable == other.IndustrialAquacultureAvailable &&
                SelectiveBreedingAvailable == other.SelectiveBreedingAvailable &&
                HasPonds == other.HasPonds && HasKnownSpecies == other.HasKnownSpecies &&
                HasEstablishedSpecies == other.HasEstablishedSpecies && HasKnownHealth == other.HasKnownHealth &&
                HasKnownPopulation == other.HasKnownPopulation &&
                HasConservationKnowledge == other.HasConservationKnowledge && HasBreeds == other.HasBreeds &&
                HasActiveCommission == other.HasActiveCommission;
        }

        public override bool Equals(object obj) => Equals(obj as AquacultureUiDisclosureSnapshot);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PondkeepingAvailable ? 1 : 0;
                hash = hash * 31 + (ManagedAquacultureAvailable ? 1 : 0);
                hash = hash * 31 + (IndustrialAquacultureAvailable ? 1 : 0);
                hash = hash * 31 + (SelectiveBreedingAvailable ? 1 : 0);
                hash = hash * 31 + (HasPonds ? 1 : 0);
                hash = hash * 31 + (HasKnownSpecies ? 1 : 0);
                hash = hash * 31 + (HasEstablishedSpecies ? 1 : 0);
                hash = hash * 31 + (HasKnownHealth ? 1 : 0);
                hash = hash * 31 + (HasKnownPopulation ? 1 : 0);
                hash = hash * 31 + (HasConservationKnowledge ? 1 : 0);
                hash = hash * 31 + (HasBreeds ? 1 : 0);
                return hash * 31 + (HasActiveCommission ? 1 : 0);
            }
        }
    }

    /// <summary>
    /// Immutable, per-fish field permissions. Research grants measuring/acting capability while
    /// Knowledge Framework facets grant learned interpretation of identity and biology.
    /// </summary>
    public sealed class AquacultureFishDossierDisclosure : IEquatable<AquacultureFishDossierDisclosure>
    {
        public AquacultureFishDossierDisclosure(bool initialized, bool identityKnown, bool traitsKnown,
            bool sizeKnown, bool healthKnown, bool breedingKnown, bool hasRegisteredBreed, bool sterilized,
            bool managedAquacultureAvailable, bool industrialAquacultureAvailable,
            bool selectiveBreedingAvailable)
        {
            Initialized = initialized;
            IdentityKnown = identityKnown;
            TraitsKnown = traitsKnown;
            SizeKnown = sizeKnown;
            HealthKnown = healthKnown;
            BreedingKnown = breedingKnown;
            HasRegisteredBreed = hasRegisteredBreed;
            Sterilized = sterilized;
            ManagedAquacultureAvailable = managedAquacultureAvailable;
            IndustrialAquacultureAvailable = industrialAquacultureAvailable;
            SelectiveBreedingAvailable = selectiveBreedingAvailable;
        }

        public bool Initialized { get; private set; }
        public bool IdentityKnown { get; private set; }
        public bool TraitsKnown { get; private set; }
        public bool SizeKnown { get; private set; }
        public bool HealthKnown { get; private set; }
        public bool BreedingKnown { get; private set; }
        public bool HasRegisteredBreed { get; private set; }
        public bool Sterilized { get; private set; }
        public bool ManagedAquacultureAvailable { get; private set; }
        public bool IndustrialAquacultureAvailable { get; private set; }
        public bool SelectiveBreedingAvailable { get; private set; }

        public bool SpeciesIdentityVisible => IdentityKnown;
        public bool SexVisible => Initialized;
        public bool LifeStageVisible => Initialized;
        public bool TraitsVisible => Initialized && TraitsKnown;
        public bool BreedVisible => Initialized && HasRegisteredBreed;
        public bool GenerationVisible => Initialized && HasRegisteredBreed && BreedingKnown;
        public bool ExactHealthMetricsVisible => Initialized && IndustrialAquacultureAvailable && HealthKnown;
        public bool FoodReserveVisible => ExactHealthMetricsVisible;
        public bool HabitatFitVisible => ExactHealthMetricsVisible;
        public bool StarvationStateVisible => ExactHealthMetricsVisible;
        public bool HealthClassificationVisible => Initialized;
        public bool ExpectedMeatYieldVisible => Initialized && ManagedAquacultureAvailable && IdentityKnown && SizeKnown;
        public bool SterilizationVisible => Initialized &&
            (Sterilized || (SelectiveBreedingAvailable && BreedingKnown));
        public bool KnowledgeVisible => IdentityKnown;
        public bool ProductionGroupVisible => ExpectedMeatYieldVisible || SterilizationVisible;

        public bool Equals(AquacultureFishDossierDisclosure other)
        {
            return other != null && Initialized == other.Initialized && IdentityKnown == other.IdentityKnown &&
                TraitsKnown == other.TraitsKnown && SizeKnown == other.SizeKnown && HealthKnown == other.HealthKnown &&
                BreedingKnown == other.BreedingKnown && HasRegisteredBreed == other.HasRegisteredBreed &&
                Sterilized == other.Sterilized && ManagedAquacultureAvailable == other.ManagedAquacultureAvailable &&
                IndustrialAquacultureAvailable == other.IndustrialAquacultureAvailable &&
                SelectiveBreedingAvailable == other.SelectiveBreedingAvailable;
        }

        public override bool Equals(object obj) => Equals(obj as AquacultureFishDossierDisclosure);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Initialized ? 1 : 0;
                hash = hash * 31 + (IdentityKnown ? 1 : 0);
                hash = hash * 31 + (TraitsKnown ? 1 : 0);
                hash = hash * 31 + (SizeKnown ? 1 : 0);
                hash = hash * 31 + (HealthKnown ? 1 : 0);
                hash = hash * 31 + (BreedingKnown ? 1 : 0);
                hash = hash * 31 + (HasRegisteredBreed ? 1 : 0);
                hash = hash * 31 + (Sterilized ? 1 : 0);
                hash = hash * 31 + (ManagedAquacultureAvailable ? 1 : 0);
                hash = hash * 31 + (IndustrialAquacultureAvailable ? 1 : 0);
                return hash * 31 + (SelectiveBreedingAvailable ? 1 : 0);
            }
        }
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
