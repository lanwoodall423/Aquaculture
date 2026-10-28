using System;
using System.Collections.Generic;

namespace AquacultureFishing
{
    /// <summary>
    /// Positive runtime-fish identity markers. Category-free fish can opt in
    /// explicitly without broadening recognition of ordinary resource Defs.
    /// </summary>
    public static class FishIdentityRules
    {
        public static bool Recognizes(bool hasRecognizedFishCategory,
            bool hasAquariumFishComp, bool hasAquacultureSpeciesExtension)
        {
            return hasRecognizedFishCategory || hasAquariumFishComp ||
                hasAquacultureSpeciesExtension;
        }
    }

    /// <summary>
    /// Four stable fishing ranks owned by Aquaculture. This type has no framework
    /// assembly dependency and is not itself a serialized value.
    /// </summary>
    public enum FishingRank
    {
        Novice = 0,
        Adept = 1,
        Expert = 2,
        Master = 3
    }

    public static class FishingRankRules
    {
        // Legacy player settings persist 0/2/3/4, not the rank's ordinal.
        public static FishingRank FromLegacySetting(int value)
        {
            if (value >= 4) return FishingRank.Master;
            if (value == 3) return FishingRank.Expert;
            if (value == 2) return FishingRank.Adept;
            return FishingRank.Novice;
        }

        public static int LegacySettingForRank(FishingRank rank)
        {
            switch (rank)
            {
                case FishingRank.Master: return 4;
                case FishingRank.Expert: return 3;
                case FishingRank.Adept: return 2;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// Read-only, framework-free projections of the saved Aquaculture fishing
    /// progress records. This is not a second Knowledge Framework state store.
    /// </summary>
    public static class NativeFishingKnowledgeRules
    {
        public const float AdeptExperience = 100f;
        public const float ExpertExperience = 300f;
        public const float MasterExperience = 700f;

        public static float SpeciesAmount(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount)) return 0f;
            return Math.Min(1f, Math.Max(0f, amount));
        }

        public static float Experience(float amount)
        {
            return float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f ? 0f : amount;
        }

        public static float SpeciesKnowledge(IReadOnlyDictionary<string, float> amounts, string speciesDefName)
        {
            return amounts != null && !string.IsNullOrEmpty(speciesDefName) &&
                amounts.TryGetValue(speciesDefName, out float amount)
                ? SpeciesAmount(amount) : 0f;
        }

        /// <summary>
        /// Aggregate legacy personal knowledge for the player's faction only.
        /// The supplied eligibility predicate is evaluated before accessing the
        /// record's knowledge, so unowned pawns cannot contribute to colony UI.
        /// This reads existing records and never changes them.
        /// </summary>
        public static float ColonySpeciesKnowledge<T>(IEnumerable<T> records, string speciesDefName,
            Func<T, bool> belongsToPlayerFaction,
            Func<T, IReadOnlyDictionary<string, float>> readSpeciesKnowledge) where T : class
        {
            if (records == null || string.IsNullOrEmpty(speciesDefName) ||
                belongsToPlayerFaction == null || readSpeciesKnowledge == null) return 0f;
            float total = 0f;
            foreach (T record in records)
            {
                if (record == null || !belongsToPlayerFaction(record)) continue;
                total += SpeciesKnowledge(readSpeciesKnowledge(record), speciesDefName);
                // Equivalent to clamp01(sum) without risking large or nonfinite totals.
                if (total >= 1f) return 1f;
            }
            return total;
        }

        public static FishingRank RankForExperience(float amount, float adeptMin = AdeptExperience,
            float expertMin = ExpertExperience, float masterMin = MasterExperience)
        {
            if (!ValidThresholds(adeptMin, expertMin, masterMin)) return FishingRank.Novice;
            float experience = Experience(amount);
            if (experience >= masterMin) return FishingRank.Master;
            if (experience >= expertMin) return FishingRank.Expert;
            if (experience >= adeptMin) return FishingRank.Adept;
            return FishingRank.Novice;
        }

        public static float ProgressForExperience(float amount, float adeptMin = AdeptExperience,
            float expertMin = ExpertExperience, float masterMin = MasterExperience)
        {
            if (!ValidThresholds(adeptMin, expertMin, masterMin)) return 0f;
            float experience = Experience(amount);
            FishingRank rank = RankForExperience(experience, adeptMin, expertMin, masterMin);
            if (rank == FishingRank.Master) return 1f;
            float low = rank == FishingRank.Expert ? expertMin :
                rank == FishingRank.Adept ? adeptMin : 0f;
            float high = rank == FishingRank.Expert ? masterMin :
                rank == FishingRank.Adept ? expertMin : adeptMin;
            return Math.Min(1f, Math.Max(0f, (experience - low) / (high - low)));
        }

        private static bool ValidThresholds(float adeptMin, float expertMin, float masterMin)
        {
            return !float.IsNaN(adeptMin) && !float.IsInfinity(adeptMin) &&
                !float.IsNaN(expertMin) && !float.IsInfinity(expertMin) &&
                !float.IsNaN(masterMin) && !float.IsInfinity(masterMin) &&
                adeptMin > 0f && expertMin > adeptMin && masterMin > expertMin;
        }
    }

    /// <summary>Pure compatibility and identity rules shared by the runtime adapter and executable tests.</summary>
    public static class AquacultureKnowledgeContract
    {
        public const int MinimumApiVersion = 3;
        public const int MinimumCapabilityVersion = 3;
        public const int MaximumDynamicSubjects = 2048;
        public const string MinimumSemanticRelease = "3.0.0-beta.1";

        // Keep a private canonical list for validation: readonly does not make a public
        // array's elements immutable, and external adapters may accidentally modify it.
        private static readonly string[] requiredV3Capabilities =
        {
            "domains", "colony-knowledge", "pawn-knowledge", "expertise", "domain-ui",
            "evidence-transactions", "facets", "confidence", "discovery-stages", "relationships",
            "claims", "typed-measurements", "subject-archetypes", "requirement-stages",
            "observation-recipes", "contextual-knowledge", "milestones", "structural-relations",
            "subject-lifecycle", "accrual-policies", "claim-staleness", "transmission"
        };

        // Preserve the existing public field signature for compatibility, but never
        // use its mutable array when deciding whether Knowledge V3 is supported.
        public static readonly string[] RequiredV3Capabilities =
            (string[])requiredV3Capabilities.Clone();

        public static bool SupportsV3(int apiVersion, Func<int, string, bool> supports,
            Func<string, int> capabilityVersion)
        {
            if (supports == null || capabilityVersion == null || apiVersion < MinimumApiVersion) return false;
            for (int i = 0; i < requiredV3Capabilities.Length; i++)
            {
                string capability = requiredV3Capabilities[i];
                if (!supports(MinimumApiVersion, capability) || capabilityVersion(capability) < MinimumCapabilityVersion)
                    return false;
            }
            return true;
        }

        public static string StableEventId(string kind, string primaryId, int tick, string contextId, string detail = null)
        {
            return Join(kind, primaryId, tick.ToString(), contextId, detail);
        }

        public static string StableMigrationConsumerId(string migrationId, string scope)
        {
            return Join(migrationId, scope);
        }

        public static string StableContextId(string contextType, int mapId, string anchorId, string topologyId,
            string waterKind)
        {
            string id = mapId + ":" + (anchorId ?? string.Empty) + ":" + (topologyId ?? string.Empty);
            return string.IsNullOrEmpty(waterKind) ? id : id + ":" + waterKind;
        }

        public static bool RegistrationComplete(params bool[] phases)
        {
            if (phases == null || phases.Length == 0) return false;
            for (int i = 0; i < phases.Length; i++) if (!phases[i]) return false;
            return true;
        }

        public static bool DynamicSubjectCountAllowed(int count)
        {
            return count >= 0 && count <= MaximumDynamicSubjects;
        }

        public static bool CanFinalizeMigration(IList<bool> imports, bool committed)
        {
            if (!committed || imports == null || imports.Count == 0) return false;
            for (int i = 0; i < imports.Count; i++) if (!imports[i]) return false;
            return true;
        }

        public static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static string Join(params string[] parts)
        {
            List<string> normalized = new List<string>();
            for (int i = 0; i < parts.Length; i++)
                normalized.Add((parts[i] ?? string.Empty).Replace("|", "/"));
            return string.Join("|", normalized.ToArray());
        }
    }
}
