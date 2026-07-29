using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    public enum FishingExpertiseLevel
    {
        Untrained,
        Novice,
        Adept,
        Expert,
        Master
    }

    public sealed class FishExpertiseSetting : IExposable
    {
        public string fishDefName;
        public FishingExpertiseLevel minimumFishingExpertise;

        public FishExpertiseSetting() { }
        public FishExpertiseSetting(ThingDef fishDef)
        {
            fishDefName = fishDef?.defName;
        }

        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);

        public void ExposeData()
        {
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref minimumFishingExpertise, "minimumFishingExpertise", FishingExpertiseLevel.Untrained);
            minimumFishingExpertise = (FishingExpertiseLevel)Mathf.Clamp((int)minimumFishingExpertise, 0, 4);
        }
    }

    public sealed class PawnFishingProgress : IExposable
    {
        public Pawn pawn;
        public float expertiseExperience;
        public Dictionary<string, float> speciesKnowledge = new Dictionary<string, float>();

        public FishingExpertiseLevel ExpertiseLevel => FishingProgressionUtility.LevelFor(expertiseExperience);
        public float ExpertiseProgress => FishingProgressionUtility.ProgressFor(expertiseExperience);

        public float KnowledgeFor(ThingDef fishDef)
        {
            return fishDef != null && speciesKnowledge.TryGetValue(fishDef.defName, out float value)
                ? Mathf.Clamp01(value)
                : 0f;
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

        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref waterCell, "waterCell");
            Scribe_Values.Look(ref framework, "framework");
            Scribe_Values.Look(ref startedTick, "startedTick");
        }
    }

    public static class FishingProgressionUtility
    {
        private static readonly float[] Thresholds = { 0f, 1f, 100f, 300f, 700f };

        public static FishingExpertiseLevel LevelFor(float experience)
        {
            if (experience >= Thresholds[4]) return FishingExpertiseLevel.Master;
            if (experience >= Thresholds[3]) return FishingExpertiseLevel.Expert;
            if (experience >= Thresholds[2]) return FishingExpertiseLevel.Adept;
            if (experience >= Thresholds[1]) return FishingExpertiseLevel.Novice;
            return FishingExpertiseLevel.Untrained;
        }

        public static float ProgressFor(float experience)
        {
            FishingExpertiseLevel level = LevelFor(experience);
            if (level == FishingExpertiseLevel.Master) return 1f;
            int index = (int)level;
            return Mathf.InverseLerp(Thresholds[index], Thresholds[index + 1], experience);
        }

        public static int AnimalsSkill(Pawn pawn)
        {
            return pawn?.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
        }

        public static float DurationFactor(Pawn pawn, ThingDef fishDef)
        {
            PawnFishingProgress progress = FishingProgressionComponent.Current?.ProgressFor(pawn, false);
            float animals = AnimalsSkill(pawn) / 20f;
            float knowledge = progress?.KnowledgeFor(fishDef) ?? 0f;
            float expertise = (int)(progress?.ExpertiseLevel ?? FishingExpertiseLevel.Untrained) / 4f;
            return Mathf.Clamp(1.25f - animals * 0.35f - knowledge * 0.30f - expertise * 0.25f, 0.45f, 1.25f);
        }

        public static float EscapeChance(Pawn pawn, ThingDef fishDef)
        {
            PawnFishingProgress progress = FishingProgressionComponent.Current?.ProgressFor(pawn, false);
            float animals = AnimalsSkill(pawn) / 20f;
            float knowledge = progress?.KnowledgeFor(fishDef) ?? 0f;
            float expertise = (int)(progress?.ExpertiseLevel ?? FishingExpertiseLevel.Untrained) / 4f;
            return Mathf.Clamp(0.55f - animals * 0.25f - knowledge * 0.20f - expertise * 0.15f, 0.05f, 0.80f);
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
                attempts.RemoveAll(record => record?.pawn == null || record.FishDef == null || record.framework.NullOrEmpty());
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

        public FishingAttemptRecord Pair(Pawn pawn, IntVec3 cell, string framework, IEnumerable<ThingDef> presentSpecies)
        {
            if (pawn == null || pawn.Map == null) return null;
            attempts.RemoveAll(item => item.pawn == pawn);
            FishingExpertiseLevel level = ProgressFor(pawn, false)?.ExpertiseLevel ?? FishingExpertiseLevel.Untrained;
            List<ThingDef> eligible = presentSpecies?.Where(FishUtility.IsFish).Distinct()
                .Where(fish => (AquacultureMod.Settings?.MinimumExpertiseFor(fish) ?? FishingExpertiseLevel.Untrained) <= level)
                .ToList() ?? new List<ThingDef>();
            if (eligible.Count == 0) return null;
            ThingDef selected = eligible.RandomElement();
            var attempt = new FishingAttemptRecord
            {
                pawn = pawn,
                fishDefName = selected.defName,
                waterCell = cell,
                framework = framework,
                startedTick = Find.TickManager?.TicksGame ?? 0
            };
            attempts.Add(attempt);
            return attempt;
        }

        public bool Resolve(Pawn pawn, string framework, Func<FishingAttemptRecord, bool> stillAvailable, out FishingAttemptRecord attempt)
        {
            attempt = AttemptFor(pawn, framework);
            if (attempt == null) return false;
            bool allowed = stillAvailable?.Invoke(attempt) == true &&
                (AquacultureMod.Settings?.MinimumExpertiseFor(attempt.FishDef) ?? FishingExpertiseLevel.Untrained) <=
                (ProgressFor(pawn, false)?.ExpertiseLevel ?? FishingExpertiseLevel.Untrained);
            bool caught = allowed && !Rand.Chance(FishingProgressionUtility.EscapeChance(pawn, attempt.FishDef));
            if (!caught) attempts.Remove(attempt);
            return caught;
        }

        public void Complete(FishingAttemptRecord attempt)
        {
            if (attempt?.pawn == null || attempt.FishDef == null) return;
            PawnFishingProgress progress = ProgressFor(attempt.pawn);
            float knowledge = progress.KnowledgeFor(attempt.FishDef);
            progress.speciesKnowledge[attempt.fishDefName] = Mathf.Clamp01(knowledge + 0.08f + (1f - knowledge) * 0.04f);
            FishingExpertiseLevel required = AquacultureMod.Settings?.MinimumExpertiseFor(attempt.FishDef) ?? FishingExpertiseLevel.Untrained;
            progress.expertiseExperience += 8f + (int)required * 2f + (1f - knowledge) * 4f;
            attempts.Remove(attempt);
            AquacultureJournalComponent.Current?.NotifyFishingCatch(attempt.FishDef, attempt.pawn);
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
            FishingAttemptRecord attempt = FishingProgressionComponent.Current?.Pair(pawn, WaterCellFor(driver), VfeFramework, VfeSpecies(zone));
            AccessTools.Field(driver?.GetType(), "fishCaught")?.SetValue(driver, attempt?.FishDef);
            AccessTools.Field(driver?.GetType(), "fishAmount")?.SetValue(driver, 1);
            AccessTools.Field(driver?.GetType(), "fishAmountWithSkill")?.SetValue(driver, 1);
            return attempt;
        }

        public static FishingAttemptRecord PairOdyssey(object driver)
        {
            Pawn pawn = PawnFor(driver);
            IntVec3 cell = WaterCellFor(driver);
            return FishingProgressionComponent.Current?.Pair(pawn, cell, OdysseyFramework, OdysseySpecies(pawn, cell));
        }

        public static IEnumerable<ThingDef> OdysseySpecies(Pawn pawn, IntVec3 cell)
        {
            WaterBody body = pawn?.Map == null ? null : FishingUtility.GetWaterBody(cell, pawn.Map);
            if (body?.HasFish != true || body.Population <= 0f) return Enumerable.Empty<ThingDef>();
            return body.CommonFishIncludingExtras.Concat(body.UncommonFish).Where(FishUtility.IsFish).Distinct();
        }

        public static bool VfeStillAvailable(object driver, FishingAttemptRecord attempt)
        {
            Pawn pawn = PawnFor(driver);
            Zone zone = VfeZoneFor(driver) as Zone;
            return pawn?.Map != null && zone?.Map == pawn.Map && attempt.waterCell.GetZone(pawn.Map) == zone &&
                VfeSpecies(zone).Contains(attempt.FishDef);
        }

        public static bool OdysseyStillAvailable(Pawn pawn, FishingAttemptRecord attempt)
        {
            return pawn?.Map != null && OdysseySpecies(pawn, attempt.waterCell).Contains(attempt.FishDef);
        }

        public static float CurrentDurationFactor(object iterator)
        {
            object driver = AccessTools.Field(iterator?.GetType(), "<>4__this")?.GetValue(iterator);
            Pawn pawn = PawnFor(driver);
            FishingAttemptRecord attempt = FishingProgressionComponent.Current?.AttemptFor(pawn);
            return FishingProgressionUtility.DurationFactor(pawn, attempt?.FishDef);
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
            return true;
        }

        public static void VceCatchPostfix(FishingAttemptRecord __state)
        {
            if (__state != null) FishingProgressionComponent.Current?.Complete(__state);
        }

        public static bool OdysseyCatchesPrefix(Pawn __0, IntVec3 __1, bool __2, ref bool __3, ref List<Thing> __result)
        {
            Pawn pawn = __0;
            FishingProgressionComponent component = FishingProgressionComponent.Current;
            FishingAttemptRecord existing = component?.AttemptFor(pawn, OdysseyFramework);
            if (existing == null) return true;
            bool caught = component.Resolve(pawn, OdysseyFramework, attempt => OdysseyStillAvailable(pawn, attempt), out FishingAttemptRecord attempt);
            __3 = false;
            __result = new List<Thing>();
            if (!caught) return false;
            Thing fish = ThingMaker.MakeThing(attempt.FishDef);
            fish.stackCount = 1;
            __result.Add(fish);
            component.Complete(attempt);
            return false;
        }
    }
}
