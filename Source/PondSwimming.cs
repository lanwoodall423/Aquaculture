using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    public sealed class FishTraitPondEffect
    {
        public string summary;
        public float maximumScale = 3f;
        public int durationTicks = 30000;
        public bool nightOnly;
        public float nightMultiplier = 1f;
        public bool thoughtNightOnly;
        public bool delicious;
        public float beauty;
        public float joyPerTick;
        public float childJoyFactor = 1f;
        public float animalsXpPerHour;
        public float healingFactor;
        public float immunityFactor;
        public float mentalBreakThreshold;
        public float moveSpeed;
        public float restFallRateFactor;
        public float cleansing;
        public float toxicReductionPerHour;
        public float socialImpact;
        public float comfyTemperatureMin;
        public float comfyTemperatureMax;
        public float heatInsulation;
        public float coldInsulation;
        public float psychicSensitivity;
        public float painOffset;
        public float comfort;
        public float algaeConsumptionPerHour;
        public float detritusReductionPerHour;
        public float cropGrowth;
        public float carryingCapacity;
        public ThoughtDef moodThought;

        public IEnumerable<string> ConfigErrors(string owner)
        {
            if (maximumScale <= 0f) yield return owner + " pondEffect must have a positive maximumScale.";
            if (durationTicks <= 0) yield return owner + " pondEffect must have a positive durationTicks.";
            if (nightMultiplier < 0f) yield return owner + " pondEffect has a negative nightMultiplier.";
            if (childJoyFactor < 0f) yield return owner + " pondEffect has a negative childJoyFactor.";
        }
    }

    public enum PondHediffEffect
    {
        HealingFactor,
        ImmunityFactor,
        MentalBreakThreshold,
        MoveSpeed,
        RestFallRateFactor,
        SocialImpact,
        ComfyTemperatureMin,
        ComfyTemperatureMax,
        HeatInsulation,
        ColdInsulation,
        PsychicSensitivity,
        PainOffset,
        Comfort
    }

    public sealed class PondTraitSnapshot
    {
        public readonly Dictionary<PondHediffEffect, float> effects = new Dictionary<PondHediffEffect, float>();
        public readonly Dictionary<PondHediffEffect, int> effectDurations = new Dictionary<PondHediffEffect, int>();
        public readonly HashSet<ThoughtDef> thoughts = new HashSet<ThoughtDef>();
        public float joyPerTick;
        public float animalsXpPerHour;
        public float cleansing;
        public float toxicReductionPerHour;
        public int durationTicks = 30000;

        public void Add(PondHediffEffect effect, float value, int durationTicks)
        {
            if (Mathf.Approximately(value, 0f)) return;
            effects[effect] = effects.TryGetValue(effect, out float current) ? current + value : value;
            effectDurations[effect] = Mathf.Max(effectDurations.TryGetValue(effect, out int currentDuration) ? currentDuration : 0, durationTicks);
        }

        public void Merge(FishTraitPondEffect effect, float scale, Pawn pawn, bool night)
        {
            if (effect == null || effect.nightOnly && !night) return;
            float joyScale = scale * (pawn?.DevelopmentalStage == DevelopmentalStage.Child ? effect.childJoyFactor : 1f);
            joyPerTick += effect.joyPerTick * joyScale;
            animalsXpPerHour += effect.animalsXpPerHour * scale;
            cleansing += effect.cleansing * scale;
            toxicReductionPerHour += effect.toxicReductionPerHour * scale;
            durationTicks = Mathf.Max(durationTicks, effect.durationTicks);
            if (effect.moodThought != null && (!effect.thoughtNightOnly || night)) thoughts.Add(effect.moodThought);
            Add(PondHediffEffect.HealingFactor, effect.healingFactor * scale, effect.durationTicks);
            Add(PondHediffEffect.ImmunityFactor, effect.immunityFactor * scale, effect.durationTicks);
            Add(PondHediffEffect.MentalBreakThreshold, effect.mentalBreakThreshold * scale, effect.durationTicks);
            Add(PondHediffEffect.MoveSpeed, effect.moveSpeed * scale, effect.durationTicks);
            Add(PondHediffEffect.RestFallRateFactor, effect.restFallRateFactor * scale, effect.durationTicks);
            Add(PondHediffEffect.SocialImpact, effect.socialImpact * scale, effect.durationTicks);
            Add(PondHediffEffect.ComfyTemperatureMin, effect.comfyTemperatureMin * scale, effect.durationTicks);
            Add(PondHediffEffect.ComfyTemperatureMax, effect.comfyTemperatureMax * scale, effect.durationTicks);
            Add(PondHediffEffect.HeatInsulation, effect.heatInsulation * scale, effect.durationTicks);
            Add(PondHediffEffect.ColdInsulation, effect.coldInsulation * scale, effect.durationTicks);
            Add(PondHediffEffect.PsychicSensitivity, effect.psychicSensitivity * scale, effect.durationTicks);
            Add(PondHediffEffect.PainOffset, effect.painOffset * scale, effect.durationTicks);
            Add(PondHediffEffect.Comfort, effect.comfort * scale, effect.durationTicks);
        }
    }

    public sealed class Hediff_SpentTimeInPond : HediffWithComps
    {
        private Dictionary<PondHediffEffect, float> effects = new Dictionary<PondHediffEffect, float>();
        private Dictionary<PondHediffEffect, int> effectExpiresAt = new Dictionary<PondHediffEffect, int>();
        public int expiresAt;
        private HediffStage stage;

        public override bool ShouldRemove => (Find.TickManager?.TicksGame ?? 0) >= expiresAt || base.ShouldRemove;
        public override HediffStage CurStage => stage ?? (stage = BuildStage());
        public override float PainOffset => Value(PondHediffEffect.PainOffset);

        public void Refresh(PondTraitSnapshot snapshot)
        {
            effects.Clear();
            effectExpiresAt.Clear();
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (KeyValuePair<PondHediffEffect, float> pair in snapshot.effects)
            {
                effects[pair.Key] = pair.Value;
                effectExpiresAt[pair.Key] = now + (snapshot.effectDurations.TryGetValue(pair.Key, out int duration) ? duration : snapshot.durationTicks);
            }
            expiresAt = effectExpiresAt.Count > 0 ? effectExpiresAt.Values.Max() : now + snapshot.durationTicks;
            Severity = 0.1f;
            stage = BuildStage();
            pawn?.health?.hediffSet?.DirtyCache();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref effects, "pondEffects", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref effectExpiresAt, "pondEffectExpiries", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref expiresAt, "expiresAt");
            if (effects == null) effects = new Dictionary<PondHediffEffect, float>();
            if (effectExpiresAt == null) effectExpiresAt = new Dictionary<PondHediffEffect, int>();
            stage = null;
        }

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            int now = Find.TickManager?.TicksGame ?? 0;
            List<PondHediffEffect> expired = effectExpiresAt.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList();
            if (expired.Count == 0) return;
            for (int i = 0; i < expired.Count; i++)
            {
                effects.Remove(expired[i]);
                effectExpiresAt.Remove(expired[i]);
            }
            expiresAt = effectExpiresAt.Count > 0 ? effectExpiresAt.Values.Max() : now;
            stage = BuildStage();
            pawn?.health?.hediffSet?.DirtyCache();
        }

        private float Value(PondHediffEffect effect)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return effectExpiresAt.TryGetValue(effect, out int expiry) && expiry > now && effects.TryGetValue(effect, out float value) ? value : 0f;
        }

        private HediffStage BuildStage()
        {
            var result = new HediffStage
            {
                naturalHealingFactor = 1f + Value(PondHediffEffect.HealingFactor),
                statOffsets = new List<StatModifier>(),
                statFactors = new List<StatModifier>()
            };
            AddOffset(result, StatDefOf.MentalBreakThreshold, PondHediffEffect.MentalBreakThreshold);
            AddOffset(result, StatDefOf.MoveSpeed, PondHediffEffect.MoveSpeed);
            AddOffset(result, StatDefOf.SocialImpact, PondHediffEffect.SocialImpact);
            AddOffset(result, StatDefOf.ComfyTemperatureMin, PondHediffEffect.ComfyTemperatureMin);
            AddOffset(result, StatDefOf.ComfyTemperatureMax, PondHediffEffect.ComfyTemperatureMax);
            AddOffset(result, StatDefOf.Insulation_Heat, PondHediffEffect.HeatInsulation);
            AddOffset(result, StatDefOf.Insulation_Cold, PondHediffEffect.ColdInsulation);
            AddOffset(result, StatDefOf.PsychicSensitivity, PondHediffEffect.PsychicSensitivity);
            AddOffset(result, StatDefOf.Comfort, PondHediffEffect.Comfort);
            AddFactor(result, StatDefOf.ImmunityGainSpeed, PondHediffEffect.ImmunityFactor);
            AddFactor(result, StatDefOf.RestFallRateFactor, PondHediffEffect.RestFallRateFactor);
            return result;
        }

        private void AddOffset(HediffStage target, StatDef stat, PondHediffEffect effect)
        {
            float value = Value(effect);
            if (!Mathf.Approximately(value, 0f)) target.statOffsets.Add(new StatModifier { stat = stat, value = value });
        }

        private void AddFactor(HediffStage target, StatDef stat, PondHediffEffect effect)
        {
            float value = Value(effect);
            if (!Mathf.Approximately(value, 0f)) target.statFactors.Add(new StatModifier { stat = stat, value = Mathf.Max(0.1f, 1f + value) });
        }
    }

    [DefOf]
    public static class PondSwimmingDefOf
    {
        public static JobDef AF_EnterPond;
        public static HediffDef AF_SpentTimeInPond;
        public static JoyKindDef Meditative;

        static PondSwimmingDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(PondSwimmingDefOf)); }
    }

    public static class PondSwimmingUtility
    {
        public static bool IsNight(Map map) => map != null && GenLocalDate.DayPercent(map) is float value && (value < 0.25f || value > 0.80f);

        public static AcceptanceReport CanEnter(Pawn pawn, IntVec3 requestedCell, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            if (pawn?.Spawned != true || !pawn.RaceProps.Humanlike) return "Only a spawned humanlike pawn can enter a pond.";
            if (pawn.Drafted) return "Drafted pawns cannot swim for recreation.";
            if (pawn.DeadOrDowned || pawn.InMentalState || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return "Pawn is not healthy enough to swim.";
            if (pawn.needs?.joy == null) return "Pawn cannot gain recreation.";
            if (JoyUtility.LordPreventsGettingJoy(pawn) || JoyUtility.TimetablePreventsGettingJoy(pawn)) return "Pawn is not available for recreation.";
            PondMovementUtility.PondInfo pond = PondMovementUtility.InfoAt(pawn.Map, requestedCell);
            if (pond == null) return "Select a constructed pond.";
            FishPondMapComponent component = pawn.Map.GetComponent<FishPondMapComponent>();
            if (component?.PondHasLivingFish(requestedCell) != true) return "This pond contains no living fish.";
            foreach (IntVec3 cell in pond.cells.OrderBy(cell => cell.DistanceToSquared(requestedCell)).ThenBy(cell => cell.DistanceToSquared(pawn.Position)))
            {
                if (!cell.Standable(pawn.Map) || PawnUtility.KnownDangerAt(cell, pawn.Map, pawn)) continue;
                if (!GenTemperature.SafeTemperatureAtCell(pawn, cell, pawn.Map)) continue;
                if (!pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some)) continue;
                destination = cell;
                return true;
            }
            return "No safe, reachable pond cell is available.";
        }

        public static void Apply(Pawn pawn, IntVec3 pondCell, PondTraitSnapshot snapshot)
        {
            if (pawn?.health == null || snapshot == null) return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(PondSwimmingDefOf.AF_SpentTimeInPond);
            Hediff_SpentTimeInPond hediff = existing as Hediff_SpentTimeInPond;
            if (hediff == null)
            {
                hediff = (Hediff_SpentTimeInPond)HediffMaker.MakeHediff(PondSwimmingDefOf.AF_SpentTimeInPond, pawn);
                pawn.health.AddHediff(hediff);
            }
            hediff.Refresh(snapshot);
            if (snapshot.cleansing > 0f) CleanPawn(pawn, snapshot.cleansing);
            if (snapshot.toxicReductionPerHour > 0f)
            {
                Hediff toxic = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
                if (toxic != null) toxic.Severity = Mathf.Max(0f, toxic.Severity - snapshot.toxicReductionPerHour / 10f);
            }
        }

        public static void GrantMemories(Pawn pawn, PondTraitSnapshot snapshot)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;
            foreach (ThoughtDef thought in snapshot.thoughts)
                if (thought != null) pawn.needs.mood.thoughts.memories.TryGainMemoryFast(thought);
        }

        private static void CleanPawn(Pawn pawn, float amount)
        {
            if (pawn?.filth == null || amount <= 0f) return;
            List<Filth> carried = pawn.filth.CarriedFilthListForReading.ToList();
            int layers = Mathf.Max(1, Mathf.CeilToInt(amount));
            for (int i = carried.Count - 1; i >= 0 && layers > 0; i--, layers--) carried[i].ThinFilth();
        }
    }

    public sealed partial class FishPondMapComponent
    {
        private bool pondTraitNightInitialized;
        private bool lastPondTraitNight;

        private void RefreshTimedPondTraitEffects()
        {
            bool night = PondSwimmingUtility.IsNight(map);
            if (pondTraitNightInitialized && night == lastPondTraitNight) return;
            pondTraitNightInitialized = true;
            lastPondTraitNight = night;
            for (int i = 0; i < pondStates.Count; i++) pondStates[i].beautyDirty = true;
        }

        public PondTraitSnapshot SwimmingEffectsAt(IntVec3 pondCell, Pawn pawn)
        {
            EnsurePondState();
            var snapshot = new PondTraitSnapshot();
            if (!pondByCell.TryGetValue(pondCell, out PondState pond)) return snapshot;
            bool night = PondSwimmingUtility.IsNight(map);
            foreach (IGrouping<FishTraitDef, CompFishTraits> group in LivingTraitGroups(pond))
            {
                FishTraitPondEffect effect = group.Key.pondEffect;
                if (effect == null) continue;
                snapshot.Merge(effect, Mathf.Min(effect.maximumScale, Mathf.Sqrt(group.Count())), pawn, night);
            }
            return snapshot;
        }

        private float PondTraitCapacityBonus(PondState pond) => AggregatePondValue(pond, effect => effect.carryingCapacity, false);
        private float PondTraitBeauty(PondState pond) => AggregatePondValue(pond, effect => effect.beauty, true);

        public float PondCropGrowthBonusAt(IntVec3 cell) => cropGrowthByCell.TryGetValue(cell, out float value) ? value : 0f;
        private float PondTraitCropGrowth(PondState pond) => Mathf.Min(0.15f, AggregatePondValue(pond, effect => effect.cropGrowth, false));

        private void ApplyPondTraitEcology(PondState pond, float hours)
        {
            float grazing = AggregatePondValue(pond, effect => effect.algaeConsumptionPerHour, false);
            if (grazing > 0f) pond.ecology.algae = Mathf.Max(0f, pond.ecology.algae - grazing * hours);
            float detritusReduction = AggregatePondValue(pond, effect => effect.detritusReductionPerHour, false);
            if (detritusReduction > 0f) pond.ecology.detritus = Mathf.Max(0f, pond.ecology.detritus - detritusReduction * hours);
        }

        private float AggregatePondValue(PondState pond, Func<FishTraitPondEffect, float> selector, bool applyNightMultiplier)
        {
            if (pond == null) return 0f;
            bool night = PondSwimmingUtility.IsNight(map);
            float result = 0f;
            foreach (IGrouping<FishTraitDef, CompFishTraits> group in LivingTraitGroups(pond))
            {
                FishTraitPondEffect effect = group.Key.pondEffect;
                if (effect == null || effect.nightOnly && !night) continue;
                float value = selector(effect);
                if (Mathf.Approximately(value, 0f)) continue;
                float scale = Mathf.Min(effect.maximumScale, Mathf.Sqrt(group.Count()));
                if (applyNightMultiplier && night) scale *= effect.nightMultiplier;
                result += value * scale;
            }
            return result;
        }

        private static IEnumerable<IGrouping<FishTraitDef, CompFishTraits>> LivingTraitGroups(PondState pond)
        {
            return pond.fish.Where(fish => fish?.parent?.Spawned == true && fish.IsAlive && fish.IsSwimmingInPond)
                .SelectMany(fish => fish.ActiveTraits.Where(trait => trait?.pondEffect != null).Select(trait => new { trait, fish }))
                .GroupBy(pair => pair.trait, pair => pair.fish);
        }
    }

    public sealed class FloatMenuOptionProvider_EnterPond : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => false;

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            IntVec3 clicked = context.ClickedCell;
            if (pawn?.Map == null || PondMovementUtility.InfoAt(pawn.Map, clicked) == null) return null;
            AcceptanceReport report = PondSwimmingUtility.CanEnter(pawn, clicked, out IntVec3 destination);
            if (!report.Accepted) return new FloatMenuOption("Enter Pond: " + report.Reason, null);
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Enter Pond", () =>
            {
                Job job = JobMaker.MakeJob(PondSwimmingDefOf.AF_EnterPond, destination);
                job.playerForced = true;
                pawn.jobs.TryTakeOrderedJob(job);
            }), pawn, destination);
        }
    }

    public sealed class JobDriver_EnterPond : JobDriver
    {
        private const int EffectInterval = 250;
        private PondTraitSnapshot current;
        private int swimTicks;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, ReservationLayerDefOf.Floor, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(() => pawn.Drafted || pawn.DeadOrDowned || pawn.InMentalState ||
                PondMovementUtility.InfoAt(pawn.Map, job.targetA.Cell) == null ||
                pawn.Map.GetComponent<FishPondMapComponent>()?.PondHasLivingFish(job.targetA.Cell) != true ||
                !GenTemperature.SafeTemperatureAtCell(pawn, job.targetA.Cell, pawn.Map));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            Toil swim = ToilMaker.MakeToil("SwimmingInPond");
            swim.defaultCompleteMode = ToilCompleteMode.Delay;
            swim.defaultDuration = job.def.joyDuration;
            swim.tickIntervalAction = delta => SwimTick(delta);
            swim.AddFinishAction(() =>
            {
                if (swimTicks >= 600) PondSwimmingUtility.GrantMemories(pawn, current);
            });
            swim.WithProgressBarToilDelay(TargetIndex.A);
            yield return swim;
        }

        private void SwimTick(int delta)
        {
            swimTicks += delta;
            FishPondMapComponent component = pawn.Map.GetComponent<FishPondMapComponent>();
            component?.NotifyPondWatcher(pawn, job.targetA.Cell);
            if (current == null || pawn.IsHashIntervalTick(EffectInterval))
            {
                current = component?.SwimmingEffectsAt(job.targetA.Cell, pawn);
                PondSwimmingUtility.Apply(pawn, job.targetA.Cell, current);
            }
            float joy = 0.000035f + (current?.joyPerTick ?? 0f);
            pawn.needs?.joy?.GainJoy(joy * delta, PondSwimmingDefOf.Meditative);
            if (current?.animalsXpPerHour > 0f && pawn.skills != null)
                pawn.skills.Learn(SkillDefOf.Animals, current.animalsXpPerHour * delta / 2500f);
            if (pawn.needs?.comfort != null && current?.effects.ContainsKey(PondHediffEffect.Comfort) == true)
                pawn.needs.comfort.ComfortUsed(current.effects[PondHediffEffect.Comfort]);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Plant), nameof(Plant.GrowthRate), HarmonyLib.MethodType.Getter)]
    public static class PlantGrowthRatePondTraitPatch
    {
        public static void Postfix(Plant __instance, ref float __result)
        {
            if (__instance?.Spawned != true || __instance.def.plant?.Sowable != true) return;
            __result *= 1f + (__instance.Map.GetComponent<FishPondMapComponent>()?.PondCropGrowthBonusAt(__instance.Position) ?? 0f);
        }
    }
}
