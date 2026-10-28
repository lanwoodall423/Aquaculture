#if AQUACULTURE_NATIVE_KNOWLEDGE
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
#endif

namespace AquacultureFishing
{
    /// <summary>Compatibility entry point retained for older startup code; V3 owns the integration.</summary>
    public static class AquacultureSharedKnowledgeIntegration
    {
        public static void Register() => AquacultureKnowledgeAdapter.EnsureRegistration();
    }

#if AQUACULTURE_NATIVE_KNOWLEDGE
    /// <summary>
    /// Developer-only no-KnowledgeFramework variant. Read-only legacy
    /// progression; no V3 registrations or parallel saved knowledge store.
    /// </summary>
    public static class AquacultureKnowledgeAdapter
    {
        public const string DomainId = "aquaculture";
        public const string GlobalSubjectId = "aquaculture.global";
        public const string SpeciesArchetype = "fish_species";
        public const string IndividualArchetype = "exceptional_fish";
        public const string BreedArchetype = "registered_breed";
        public const string WaterArchetype = "water_population";
        public const string PondArchetype = "managed_pond";
        public const string AnglerArchetype = "angler";

        public static bool IsRegistered => false;
        public static bool IsFrameworkReady => false;

        // The Journal selects NativeExpertiseMenu when the framework is absent.
        // Retain this type only for the existing window field/contract.
        public sealed class ExpertiseMenu
        {
            public void SelectPawn(Pawn pawn) { }
            public void ClearSelection() { }
            public void Draw(Rect bounds)
            {
                Widgets.Label(bounds, "Native fishing expertise is available in the Aquaculture Journal.");
            }
        }

        public static void EnsureRegistration() { }
        public static void Register() { }

        // DiscoveryDirector still owns journal events. Legacy fishing progress
        // is written by FishingProgressionComponent.Complete, not by this shim.
        public static void Handle(AquacultureEvent value) { }
        public static void Invalidate(Pawn pawn) { }
        public static void TrackIndividual(CompFishTraits fish) { }
        public static void ForgetIndividual(CompFishTraits fish) { }

        private static PawnFishingProgress LegacyFor(Pawn pawn)
        {
            return FishingProgressionComponent.Current?.ProgressFor(pawn, false);
        }

        public static float SpeciesKnowledgeFor(Pawn pawn, ThingDef fishDef)
        {
            if (fishDef == null) return 0f;
            return NativeFishingKnowledgeRules.SpeciesKnowledge(
                LegacyFor(pawn)?.speciesKnowledge, fishDef.defName);
        }

        public static float ColonySpeciesKnowledgeFor(ThingDef fishDef)
        {
            return NativeFishingKnowledgeRules.ColonySpeciesKnowledge<PawnFishingProgress>(
                FishingProgressionComponent.Current?.PawnProgress, fishDef?.defName,
                record => record?.pawn?.Faction?.def?.isPlayer == true,
                record => record?.speciesKnowledge);
        }

        public static IEnumerable<ThingDef> KnownSpeciesFor(Pawn pawn)
        {
            if (pawn == null) return Enumerable.Empty<ThingDef>();
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish)
                .Where(fish => SpeciesKnowledgeFor(pawn, fish) >= 0.02f).ToList();
        }

        public static float ExpertiseFor(Pawn pawn)
        {
            return NativeFishingKnowledgeRules.Experience(LegacyFor(pawn)?.expertiseExperience ?? 0f);
        }

        public static FishingRank RankForExperience(float experience, float adeptMin,
            float expertMin, float masterMin)
        {
            return NativeFishingKnowledgeRules.RankForExperience(experience, adeptMin, expertMin, masterMin);
        }

        public static float TimeReductionForRank(FishingRank rank)
        {
            // Native per-rank progression bonus; installed framework stays authoritative.
            return Math.Max(0, Math.Min(3, (int)rank)) * 0.10f;
        }

        public static FishingRank ExpertiseRankFor(Pawn pawn)
        {
            return NativeFishingKnowledgeRules.RankForExperience(ExpertiseFor(pawn));
        }

        public static float ExpertiseProgressFor(Pawn pawn)
        {
            return NativeFishingKnowledgeRules.ProgressForExperience(ExpertiseFor(pawn));
        }

        public static AquacultureKnowledgeView SpeciesView(ThingDef fishDef, Pawn pawn, bool colony)
        {
            if (fishDef == null)
                return new AquacultureKnowledgeView(null, null, 0f, 0f, false, null);
            float amount = colony ? ColonySpeciesKnowledgeFor(fishDef) : SpeciesKnowledgeFor(pawn, fishDef);
            bool identified = amount > 0f;
            return new AquacultureKnowledgeView("species:" + fishDef.defName,
                identified ? "Identified" : "Unknown", amount, amount, identified,
                identified ? new[] { "identity" } : Array.Empty<string>());
        }

        public static bool IsPlayerReadableTrait(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                (name.StartsWith("AF_Color_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Pattern_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Scale_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Size_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Body_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Finish_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Effect_", StringComparison.Ordinal));
        }
    }
#endif
}
