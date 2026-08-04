using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    public sealed class FishExpertiseSetting : IExposable
    {
        public string fishDefName;
        private int minimumFishingExpertiseValue;

        public KnowledgeRank minimumFishingExpertise
        {
            get { return FishingProgressionUtility.RankFromLegacySetting(minimumFishingExpertiseValue); }
            set { minimumFishingExpertiseValue = FishingProgressionUtility.LegacySettingForRank(value); }
        }

        public FishExpertiseSetting() { }
        public FishExpertiseSetting(ThingDef fishDef)
        {
            fishDefName = fishDef?.defName;
        }

        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);

        public void ExposeData()
        {
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref minimumFishingExpertiseValue, "minimumFishingExpertise", 0);
            minimumFishingExpertiseValue = Mathf.Clamp(minimumFishingExpertiseValue, 0, 4);
        }
    }

    public sealed class PawnFishingProgress : IExposable
    {
        public Pawn pawn;
        public float expertiseExperience;
        public Dictionary<string, float> speciesKnowledge = new Dictionary<string, float>();

        public KnowledgeRank ExpertiseLevel => AquacultureKnowledgeAdapter.ExpertiseRankFor(pawn);
        public float ExpertiseProgress => AquacultureKnowledgeAdapter.ExpertiseProgressFor(pawn);

        public float KnowledgeFor(ThingDef fishDef)
        {
            return AquacultureKnowledgeAdapter.SpeciesKnowledgeFor(pawn, fishDef);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref expertiseExperience, "expertiseExperience");
            Scribe_Collections.Look(ref speciesKnowledge, "speciesKnowledge", LookMode.Value, LookMode.Value);
            if (speciesKnowledge == null) speciesKnowledge = new Dictionary<string, float>();
            expertiseExperience = Mathf.Max(0f, expertiseExperience);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                List<string> invalid = speciesKnowledge.Where(pair => pair.Key.NullOrEmpty()).Select(pair => pair.Key).ToList();
                for (int i = 0; i < invalid.Count; i++) speciesKnowledge.Remove(invalid[i]);
                foreach (string key in speciesKnowledge.Keys.ToList()) speciesKnowledge[key] = Mathf.Clamp01(speciesKnowledge[key]);
            }
        }
    }

    public sealed class FishingAttemptRecord : IExposable
    {
        public Pawn pawn;
        public string fishDefName;
        public IntVec3 waterCell;
        public string framework;
        public int startedTick;
        public bool biteDecided;
        public bool fishBit;
        public bool feedbackBiteShown;
        public int biteTick = -1;
        public int waitDuration;
        public float hookedFishMass;
        public List<string> hookedTraitNames = new List<string>();
        public Dictionary<string, float> hookedTraitValues = new Dictionary<string, float>();

        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);
        public float HookedFishMass => hookedFishMass > 0f ? hookedFishMass : FishingRodUtility.BaseFishMass(FishDef);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref waterCell, "waterCell");
            Scribe_Values.Look(ref framework, "framework");
            Scribe_Values.Look(ref startedTick, "startedTick");
            Scribe_Values.Look(ref biteDecided, "biteDecided");
            Scribe_Values.Look(ref fishBit, "fishBit");
            Scribe_Values.Look(ref feedbackBiteShown, "feedbackBiteShown");
            Scribe_Values.Look(ref biteTick, "biteTick", -1);
            Scribe_Values.Look(ref waitDuration, "waitDuration");
            Scribe_Values.Look(ref hookedFishMass, "hookedFishMass");
            Scribe_Collections.Look(ref hookedTraitNames, "hookedTraitNames", LookMode.Value);
            Scribe_Collections.Look(ref hookedTraitValues, "hookedTraitValues", LookMode.Value, LookMode.Value);
            if (hookedTraitNames == null) hookedTraitNames = new List<string>();
            if (hookedTraitValues == null) hookedTraitValues = new Dictionary<string, float>();
        }

        public void CaptureHookedFish()
        {
            if (!fishBit || FishDef == null) return;
            Thing fish = ThingMaker.MakeThing(FishDef);
            CompFishTraits traits = fish.TryGetComp<CompFishTraits>();
            if (traits != null)
            {
                hookedTraitNames = traits.traitDefNames.ToList();
                hookedTraitValues = new Dictionary<string, float>(traits.traitValues);
                hookedFishMass = FishingRodUtility.BaseFishMass(FishDef) * traits.MassFactor;
            }
            else hookedFishMass = FishingRodUtility.BaseFishMass(FishDef);
            fish.Destroy();
        }

        public void ApplyTo(Thing fish)
        {
            fish?.TryGetComp<CompFishTraits>()?.ApplyCaughtTraits(hookedTraitNames, hookedTraitValues);
        }
    }

    public static class CaughtFishTraitTransfer
    {
        private static FishingAttemptRecord pending;

        public static void Begin(FishingAttemptRecord attempt) => pending = attempt;
        public static void Clear() => pending = null;

        public static void ApplyIfPending(CompFishTraits traits)
        {
            if (pending?.FishDef != traits?.parent?.def) return;
            FishingAttemptRecord attempt = pending;
            pending = null;
            attempt.ApplyTo(traits.parent);
        }
    }

    public static class FishingProgressionUtility
    {
        private static readonly float[] Thresholds = { 0f, 100f, 300f, 700f };

        public static KnowledgeRank LevelFor(float experience)
        {
            return KnowledgeRanks.ForExperience(experience, Thresholds[1], Thresholds[2], Thresholds[3]);
        }

        public static float ProgressFor(float experience)
        {
            KnowledgeRank level = LevelFor(experience);
            if (level == KnowledgeRank.Master) return 1f;
            int index = (int)level;
            return Mathf.InverseLerp(Thresholds[index], Thresholds[index + 1], experience);
        }

        public static float TimeReduction(KnowledgeRank rank) => KnowledgeRanks.Bonus(rank, 0.10f);

        public static KnowledgeRank RankFromLegacySetting(int value)
        {
            if (value >= 4) return KnowledgeRank.Master;
            if (value == 3) return KnowledgeRank.Expert;
            if (value == 2) return KnowledgeRank.Adept;
            return KnowledgeRank.Novice;
        }

        public static int LegacySettingForRank(KnowledgeRank rank)
        {
            if (rank == KnowledgeRank.Master) return 4;
            if (rank == KnowledgeRank.Expert) return 3;
            if (rank == KnowledgeRank.Adept) return 2;
            return 0;
        }

        public static int AnimalsSkill(Pawn pawn)
        {
            return pawn?.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
        }

        public static float EscapeChance(Pawn pawn, ThingDef fishDef)
        {
            PawnFishingProgress progress = FishingProgressionComponent.Current?.ProgressFor(pawn, false);
            float animals = AnimalsSkill(pawn) / 20f;
            float knowledge = progress?.KnowledgeFor(fishDef) ?? 0f;
            float expertise = (int)(progress?.ExpertiseLevel ?? KnowledgeRank.Novice) / 3f;
            float baseEscape = Mathf.Clamp(0.55f - animals * 0.25f - knowledge * 0.20f - expertise * 0.15f, 0.05f, 0.80f);
            float catchFactor = FishingRodUtility.ActiveRod(pawn)?.CatchChanceFactor ?? 1f;
            return Mathf.Clamp01(1f - (1f - baseEscape) * catchFactor);
        }

        public static float BiteChance(Pawn pawn, ThingDef fishDef)
        {
            PawnFishingProgress progress = FishingProgressionComponent.Current?.ProgressFor(pawn, false);
            float knowledge = progress?.KnowledgeFor(fishDef) ?? 0f;
            float expertise = (int)(progress?.ExpertiseLevel ?? KnowledgeRank.Novice) / 3f;
            return Mathf.Clamp(0.82f + knowledge * 0.08f + expertise * 0.10f, 0.05f, 1f);
        }
    }

    public sealed class FishingProgressionComponent : GameComponent
    {
        private List<PawnFishingProgress> pawnProgress = new List<PawnFishingProgress>();
        private List<FishingAttemptRecord> attempts = new List<FishingAttemptRecord>();

        public FishingProgressionComponent(Game game) { }

        public static FishingProgressionComponent Current => Verse.Current.Game?.GetComponent<FishingProgressionComponent>();
        public IReadOnlyList<PawnFishingProgress> PawnProgress => pawnProgress;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref pawnProgress, "aquacultureFishingProgression", LookMode.Deep);
            Scribe_Collections.Look(ref attempts, "aquacultureFishingAttempts", LookMode.Deep);
            if (pawnProgress == null) pawnProgress = new List<PawnFishingProgress>();
            if (attempts == null) attempts = new List<FishingAttemptRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawnProgress.RemoveAll(record => record?.pawn == null);
                attempts.RemoveAll(record => record?.pawn == null || record.framework.NullOrEmpty() ||
                    (record.FishDef == null && (!record.biteDecided || record.fishBit)));
            }
        }

        public PawnFishingProgress ProgressFor(Pawn pawn, bool create = true)
        {
            if (pawn == null) return null;
            PawnFishingProgress record = pawnProgress.FirstOrDefault(item => item.pawn == pawn);
            if (record == null && create)
            {
                record = new PawnFishingProgress { pawn = pawn };
                pawnProgress.Add(record);
            }
            return record;
        }

        public FishingAttemptRecord AttemptFor(Pawn pawn, string framework = null)
        {
            if (pawn == null) return null;
            return attempts.LastOrDefault(item => item.pawn == pawn && (framework == null || item.framework == framework));
        }

        public FishingAttemptRecord BeginAttempt(Pawn pawn, IntVec3 cell, string framework, int waitDuration)
        {
            if (pawn == null || pawn.Map == null) return null;
            attempts.RemoveAll(item => item.pawn == pawn);
            var attempt = new FishingAttemptRecord
            {
                pawn = pawn,
                waterCell = cell,
                framework = framework,
                startedTick = Find.TickManager?.TicksGame ?? 0,
                biteDecided = false,
                fishBit = false,
                waitDuration = Mathf.Max(1, waitDuration),
                biteTick = -1
            };
            attempts.Add(attempt);
            AquacultureEventRouter.FishingStarted(pawn, pawn.Map, cell, framework);
            return attempt;
        }

        public FishingAttemptRecord ProgressWait(Pawn pawn, string framework, IEnumerable<ThingDef> presentSpecies,
            Func<ThingDef, float> attraction, int elapsedTicks, int waitDuration)
        {
            FishingAttemptRecord attempt = AttemptFor(pawn, framework);
            if (attempt == null) return null;
            attempt.waitDuration = Mathf.Max(1, waitDuration);
            if (!attempt.biteDecided && elapsedTicks >= Mathf.Max(30, Mathf.RoundToInt(attempt.waitDuration * 0.35f)))
                DecideBite(attempt, presentSpecies, attraction);
            return attempt;
        }

        public FishingAttemptRecord Pair(Pawn pawn, IntVec3 cell, string framework, IEnumerable<ThingDef> presentSpecies,
            Func<ThingDef, float> attraction = null)
        {
            if (pawn == null || pawn.Map == null) return null;
            FishingAttemptRecord attempt = BeginAttempt(pawn, cell, framework, BaseWaitTicksForLegacyPair());
            DecideBite(attempt, presentSpecies, attraction);
            return attempt;
        }

        private void DecideBite(FishingAttemptRecord attempt, IEnumerable<ThingDef> presentSpecies, Func<ThingDef, float> attraction)
        {
            if (attempt == null || attempt.biteDecided) return;
            KnowledgeRank level = ProgressFor(attempt.pawn, false)?.ExpertiseLevel ?? KnowledgeRank.Novice;
            NaturalFishPopulationMapComponent populations = attempt.pawn.Map.GetComponent<NaturalFishPopulationMapComponent>();
            List<ThingDef> eligible = presentSpecies?.Where(FishUtility.IsFish).Distinct()
                .Where(fish => (AquacultureMod.Settings?.MinimumExpertiseFor(fish) ?? KnowledgeRank.Novice) <= level)
                .Where(fish => populations?.Contains(attempt.waterCell, fish) == true)
                .ToList() ?? new List<ThingDef>();
            ThingDef selected = eligible.Count == 0 ? null : eligible.RandomElementByWeight(fish => Mathf.Max(0.01f,
                (attraction?.Invoke(fish) ?? 1f) * (populations?.LocalWeight(attempt.waterCell, fish) ?? 1f)));
            bool fishBit = selected != null && Rand.Chance(FishingProgressionUtility.BiteChance(attempt.pawn, selected));
            attempt.fishDefName = selected?.defName;
            attempt.biteDecided = true;
            attempt.fishBit = fishBit;
            attempt.biteTick = Find.TickManager?.TicksGame ?? 0;
            attempt.CaptureHookedFish();
            if (fishBit)
                attempt.pawn.Map?.GetComponent<NaturalFishPopulationMapComponent>()?
                    .WarnIfCatchLikelyCrossesBreedingFloor(attempt.waterCell, selected);
            AquacultureEventRouter.FishHooked(attempt);
        }

        private static int BaseWaitTicksForLegacyPair() => Mathf.Max(300, FishingRodUtility.BaseWaitTicks);

        public bool Resolve(Pawn pawn, string framework, Func<FishingAttemptRecord, bool> stillAvailable, out FishingAttemptRecord attempt)
        {
            attempt = AttemptFor(pawn, framework);
            if (attempt == null) return false;
            CompFishingRodTackle rod = FishingRodUtility.ActiveRod(pawn);
            bool allowed = rod != null && attempt.biteDecided && attempt.fishBit && stillAvailable?.Invoke(attempt) == true &&
                (AquacultureMod.Settings?.MinimumExpertiseFor(attempt.FishDef) ?? KnowledgeRank.Novice) <=
                (ProgressFor(pawn, false)?.ExpertiseLevel ?? KnowledgeRank.Novice);
            bool capacityPassed = allowed && attempt.HookedFishMass <= rod.MaxFishMass;
            bool caught = capacityPassed && !Rand.Chance(FishingProgressionUtility.EscapeChance(pawn, attempt.FishDef));
            if (!caught)
            {
                attempts.Remove(attempt);
                AquacultureEventRouter.FishEscaped(attempt, rod == null ? "No suitable rod was equipped." :
                    capacityPassed ? "The fish slipped the hook." : "The fish was too heavy for the line.");
            }
            return caught;
        }

        public void Complete(FishingAttemptRecord attempt)
        {
            if (attempt?.pawn == null || attempt.FishDef == null) return;
            PawnFishingProgress progress = ProgressFor(attempt.pawn);
            float knowledge = progress.speciesKnowledge.TryGetValue(attempt.fishDefName, out float legacyKnowledge)
                ? Mathf.Clamp01(legacyKnowledge) : 0f;
            progress.speciesKnowledge[attempt.fishDefName] = Mathf.Clamp01(knowledge + 0.08f + (1f - knowledge) * 0.04f);
            KnowledgeRank required = AquacultureMod.Settings?.MinimumExpertiseFor(attempt.FishDef) ?? KnowledgeRank.Novice;
            progress.expertiseExperience += 8f + (int)required * 2f + (1f - knowledge) * 4f;
            attempt.pawn.Map?.GetComponent<NaturalFishPopulationMapComponent>()?.ConsumeCatch(attempt.waterCell, attempt.FishDef);
            attempts.Remove(attempt);
            AquacultureEventRouter.FishCaught(attempt);
            AquacultureKnowledgeAdapter.Invalidate(attempt.pawn);
        }
    }

    public static class FishingAttemptIntegration
    {
        private const string VfeFramework = "VFE";
        private const string OdysseyFramework = "Odyssey";
        private static readonly FieldInfo DriverPawn = AccessTools.Field(typeof(JobDriver), "pawn");
        private static readonly FieldInfo DriverJob = AccessTools.Field(typeof(JobDriver), "job");

        public static Pawn PawnFor(object driver) => DriverPawn?.GetValue(driver) as Pawn;

        public static IntVec3 WaterCellFor(object driver)
        {
            Job job = DriverJob?.GetValue(driver) as Job;
            if (job == null) return IntVec3.Invalid;
            return driver?.GetType().FullName == "RimWorld.JobDriver_Fish" ? job.GetTarget(TargetIndex.B).Cell : job.targetA.Cell;
        }

        public static IEnumerable<ThingDef> VfeSpecies(object zone)
        {
            return AccessTools.Field(zone?.GetType(), "fishInThisZone")?.GetValue(zone) as IEnumerable<ThingDef>
                ?? Enumerable.Empty<ThingDef>();
        }

        public static object VfeZoneFor(object driver)
        {
            return AccessTools.Field(driver?.GetType(), "fishingZone")?.GetValue(driver);
        }

        public static FishingAttemptRecord PairVfe(object driver)
        {
            Pawn pawn = PawnFor(driver);
            object zone = VfeZoneFor(driver);
            IntVec3 cell = WaterCellFor(driver);
            IEnumerable<ThingDef> frameworkSpecies = VfeSpecies(zone);
            IEnumerable<ThingDef> species = pawn?.Map?.GetComponent<NaturalFishPopulationMapComponent>()?
                .SpeciesAt(cell, frameworkSpecies) ?? Enumerable.Empty<ThingDef>();
            FishingAttemptRecord attempt = FishingProgressionComponent.Current?.Pair(pawn, WaterCellFor(driver), VfeFramework,
                species, fish => FishingRodUtility.AttractionFor(pawn, fish));
            AccessTools.Field(driver?.GetType(), "fishCaught")?.SetValue(driver, attempt?.FishDef);
            AccessTools.Field(driver?.GetType(), "fishAmount")?.SetValue(driver, 1);
            AccessTools.Field(driver?.GetType(), "fishAmountWithSkill")?.SetValue(driver, 1);
            return attempt;
        }

        public static FishingAttemptRecord ProgressVfe(object driver, int elapsedTicks, int waitDuration)
        {
            Pawn pawn = PawnFor(driver);
            object zone = VfeZoneFor(driver);
            IntVec3 cell = WaterCellFor(driver);
            IEnumerable<ThingDef> frameworkSpecies = VfeSpecies(zone);
            IEnumerable<ThingDef> species = pawn?.Map?.GetComponent<NaturalFishPopulationMapComponent>()?
                .SpeciesAt(cell, frameworkSpecies) ?? Enumerable.Empty<ThingDef>();
            FishingAttemptRecord attempt = FishingProgressionComponent.Current?.ProgressWait(pawn, VfeFramework, species,
                fish => FishingRodUtility.AttractionFor(pawn, fish), elapsedTicks, waitDuration);
            if (attempt?.biteDecided == true)
            {
                AccessTools.Field(driver?.GetType(), "fishCaught")?.SetValue(driver, attempt.FishDef);
                AccessTools.Field(driver?.GetType(), "fishAmount")?.SetValue(driver, 1);
                AccessTools.Field(driver?.GetType(), "fishAmountWithSkill")?.SetValue(driver, 1);
            }
            return attempt;
        }

        public static FishingAttemptRecord PairOdyssey(object driver)
        {
            Pawn pawn = PawnFor(driver);
            IntVec3 cell = WaterCellFor(driver);
            return FishingProgressionComponent.Current?.Pair(pawn, cell, OdysseyFramework,
                OdysseySpecies(pawn, cell), fish => FishingRodUtility.AttractionFor(pawn, fish));
        }

        public static FishingAttemptRecord ProgressOdyssey(object driver, int elapsedTicks, int waitDuration)
        {
            Pawn pawn = PawnFor(driver);
            IntVec3 cell = WaterCellFor(driver);
            return FishingProgressionComponent.Current?.ProgressWait(pawn, OdysseyFramework,
                OdysseySpecies(pawn, cell), fish => FishingRodUtility.AttractionFor(pawn, fish), elapsedTicks, waitDuration);
        }

        public static IEnumerable<ThingDef> OdysseySpecies(Pawn pawn, IntVec3 cell)
        {
            WaterBody body = pawn?.Map == null ? null : FishingUtility.GetWaterBody(cell, pawn.Map);
            NaturalFishPopulationMapComponent populations = pawn?.Map?.GetComponent<NaturalFishPopulationMapComponent>();
            return populations?.SpeciesAtOdyssey(cell, body) ?? Enumerable.Empty<ThingDef>();
        }

        public static bool VfeStillAvailable(object driver, FishingAttemptRecord attempt)
        {
            Pawn pawn = PawnFor(driver);
            Zone zone = VfeZoneFor(driver) as Zone;
            NaturalFishPopulationMapComponent populations = pawn?.Map?.GetComponent<NaturalFishPopulationMapComponent>();
            return pawn?.Map != null && zone?.Map == pawn.Map && attempt.waterCell.GetZone(pawn.Map) == zone &&
                populations?.Contains(attempt.waterCell, attempt.FishDef) == true;
        }

        public static bool OdysseyStillAvailable(Pawn pawn, FishingAttemptRecord attempt)
        {
            return pawn?.Map != null && OdysseySpecies(pawn, attempt.waterCell).Contains(attempt.FishDef);
        }

        public static bool HasEquippedRod(object driver)
        {
            return FishingRodUtility.HasEquippedRod(PawnFor(driver));
        }

        public static void BeginWaitPhase(object driver, string framework, int waitDuration)
        {
            Pawn pawn = PawnFor(driver);
            FishingProgressionComponent.Current?.BeginAttempt(pawn, WaterCellFor(driver), framework, waitDuration);
        }

        public static FishingAttemptRecord ProgressWaitPhase(object driver, string framework, int elapsedTicks, int waitDuration)
        {
            return framework == VfeFramework ? ProgressVfe(driver, elapsedTicks, waitDuration) :
                ProgressOdyssey(driver, elapsedTicks, waitDuration);
        }

        public static IEnumerable<Toil> VfeMakeNewToilsPostfix(IEnumerable<Toil> __result, object __instance)
        {
            foreach (Toil toil in FishingRodWorkflow.PreparationToils(__instance)) yield return toil;
            foreach (Toil toil in ReplaceFishingDelay(__result, __instance, VfeFramework)) yield return toil;
        }

        public static IEnumerable<Toil> OdysseyMakeNewToilsPostfix(IEnumerable<Toil> __result, object __instance)
        {
            foreach (Toil toil in FishingRodWorkflow.PreparationToils(__instance)) yield return toil;
            foreach (Toil toil in ReplaceFishingDelay(__result, __instance, OdysseyFramework)) yield return toil;
        }

        private static IEnumerable<Toil> ReplaceFishingDelay(IEnumerable<Toil> toils, object driver, string framework)
        {
            bool replaced = false;
            foreach (Toil toil in toils)
            {
                if (!replaced && toil.defaultCompleteMode == ToilCompleteMode.Delay && toil.defaultDuration > 60)
                {
                    replaced = true;
                    foreach (Toil phase in FishingRodUtility.FishingPhases(driver, framework)) yield return phase;
                }
                else yield return toil;
            }
        }

        public static bool VceCatchPrefix(object __instance, out FishingAttemptRecord __state)
        {
            object driver = AccessTools.Field(__instance?.GetType(), "<>4__this")?.GetValue(__instance);
            Pawn pawn = PawnFor(driver);
            __state = null;
            FishingProgressionComponent component = FishingProgressionComponent.Current;
            bool caught = component != null && component.Resolve(pawn, VfeFramework,
                attempt => VfeStillAvailable(driver, attempt), out __state);
            if (!caught)
            {
                (driver as JobDriver)?.EndJobWith(JobCondition.Succeeded);
                return false;
            }
            CaughtFishTraitTransfer.Begin(__state);
            return true;
        }

        public static void VceCatchPostfix(FishingAttemptRecord __state)
        {
            CaughtFishTraitTransfer.Clear();
            if (__state != null) FishingProgressionComponent.Current?.Complete(__state);
        }

        public static Exception VceCatchFinalizer(Exception __exception)
        {
            CaughtFishTraitTransfer.Clear();
            return __exception;
        }

        public static bool OdysseyCatchesPrefix(Pawn __0, IntVec3 __1, bool __2, ref bool __3, ref List<Thing> __result)
        {
            Pawn pawn = __0;
            FishingProgressionComponent component = FishingProgressionComponent.Current;
            FishingAttemptRecord existing = component?.AttemptFor(pawn, OdysseyFramework);
            if (existing == null)
            {
                if (pawn?.CurJobDef?.defName == "Fish" && FishingRodUtility.HasEquippedRod(pawn))
                {
                    __3 = false;
                    __result = new List<Thing>();
                    return false;
                }
                return true;
            }
            bool caught = component.Resolve(pawn, OdysseyFramework, attempt => OdysseyStillAvailable(pawn, attempt), out FishingAttemptRecord attempt);
            __3 = false;
            __result = new List<Thing>();
            if (!caught) return false;
            Thing fish = ThingMaker.MakeThing(attempt.FishDef);
            attempt.ApplyTo(fish);
            fish.stackCount = 1;
            __result.Add(fish);
            component.Complete(attempt);
            return false;
        }
    }
}
