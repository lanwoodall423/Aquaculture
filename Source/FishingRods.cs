using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    public enum FishingTackleSlot
    {
        Handle,
        Rod,
        Reel,
        Line,
        Lure
    }

    public sealed class FishingRodExtension : DefModExtension
    {
        public float baseMaximumFishMass = 1f;
    }

    public sealed class FishFishingExtension : DefModExtension
    {
        public float freshwaterAttraction = 1f;
        public float saltwaterAttraction = 1f;
    }

    public sealed class FishingTacklePartDef : Def
    {
        public FishingTackleSlot slot;
        public bool isDefault;
        public float waitTimeFactor = 1f;
        public float reelTimeFactor = 1f;
        public float recreationLossFactor = 1f;
        public float catchChanceFactor = 1f;
        public float maximumFishMassOffset;
        public float freshwaterAttraction = 1f;
        public float saltwaterAttraction = 1f;
        public float workAmount;
        public int minimumCraftingSkill;
        public ResearchProjectDef researchPrerequisite;
        public List<ThingDefCountClass> costs = new List<ThingDefCountClass>();

        public bool Available => researchPrerequisite == null || researchPrerequisite.IsFinished;

        public string EffectsSummary
        {
            get
            {
                var effects = new List<string>();
                FormatFactor(effects, "Wait Time", waitTimeFactor);
                FormatFactor(effects, "Reel Time", reelTimeFactor);
                FormatFactor(effects, "Recreation Loss", recreationLossFactor);
                FormatFactor(effects, "Catch Chance", catchChanceFactor);
                if (Mathf.Abs(maximumFishMassOffset) > 0.001f)
                    effects.Add(maximumFishMassOffset.ToString("+#0.##;-#0.##") + " kg Max Fish Mass");
                FormatFactor(effects, "Freshwater Attraction", freshwaterAttraction);
                FormatFactor(effects, "Saltwater Attraction", saltwaterAttraction);
                return effects.Count == 0 ? "No stat change" : string.Join(", ", effects);
            }
        }

        public string CostSummary
        {
            get
            {
                string materials = costs == null || costs.Count == 0 ? "No materials" :
                    string.Join(", ", costs.Select(cost => cost.count + " " + cost.thingDef.LabelCap));
                return materials + " | Work " + Mathf.RoundToInt(workAmount);
            }
        }

        private static void FormatFactor(List<string> effects, string label, float factor)
        {
            if (Mathf.Abs(factor - 1f) <= 0.001f) return;
            float percent = (factor - 1f) * 100f;
            string sign = percent >= 0f ? "+" : string.Empty;
            effects.Add(sign + Mathf.RoundToInt(percent) + "% " + label);
        }
    }

    public sealed class CompProperties_FishingRodTackle : CompProperties
    {
        public CompProperties_FishingRodTackle() { compClass = typeof(CompFishingRodTackle); }
    }

    public sealed class CompFishingRodTackle : ThingComp
    {
        private string handlePart;
        private string rodPart;
        private string reelPart;
        private string linePart;
        private string lurePart;
        private FishingTackleSlot pendingSlot;
        private string pendingPart;
        private int nextMaterialCheckTick;

        public bool HasPendingUpgrade => !pendingPart.NullOrEmpty();
        public FishingTackleSlot PendingSlot => pendingSlot;
        public FishingTacklePartDef PendingPart => pendingPart.NullOrEmpty()
            ? null
            : DefDatabase<FishingTacklePartDef>.GetNamedSilentFail(pendingPart);
        public float WaitTimeFactor => Parts.Aggregate(1f, (value, part) => value * part.waitTimeFactor);
        public float ReelTimeFactor => Parts.Where(part => part.slot != FishingTackleSlot.Rod)
            .Aggregate(1f, (value, part) => value * part.reelTimeFactor);
        public float RecreationLossFactor => Parts.Aggregate(1f, (value, part) => value * part.recreationLossFactor);
        public float CatchChanceFactor => Parts.Aggregate(1f, (value, part) => value * part.catchChanceFactor);
        public float MaxFishMass => Mathf.Max(0.01f, (parent.def.GetModExtension<FishingRodExtension>()?.baseMaximumFishMass ?? 1f) +
            Parts.Where(part => part.slot == FishingTackleSlot.Rod || part.slot == FishingTackleSlot.Line)
                .Sum(part => part.maximumFishMassOffset));
        public bool CanCheckMaterials => (Find.TickManager?.TicksGame ?? 0) >= nextMaterialCheckTick;
        public FishingTacklePartDef Lure => PartFor(FishingTackleSlot.Lure);
        public IEnumerable<FishingTacklePartDef> Parts => Enum.GetValues(typeof(FishingTackleSlot)).Cast<FishingTackleSlot>().Select(PartFor);

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref handlePart, "handlePart");
            Scribe_Values.Look(ref rodPart, "rodPart");
            Scribe_Values.Look(ref reelPart, "reelPart");
            Scribe_Values.Look(ref linePart, "linePart");
            Scribe_Values.Look(ref lurePart, "lurePart");
            Scribe_Values.Look(ref pendingSlot, "pendingSlot", FishingTackleSlot.Handle);
            Scribe_Values.Look(ref pendingPart, "pendingPart");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && PendingPart == null) ClearPendingUpgrade();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            PendingFishingRodRegistry.Notify(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            PendingFishingRodRegistry.Remove(parent, map);
            base.PostDeSpawn(map, mode);
        }

        public FishingTacklePartDef PartFor(FishingTackleSlot slot)
        {
            string key = KeyFor(slot);
            FishingTacklePartDef part = key.NullOrEmpty()
                ? null
                : DefDatabase<FishingTacklePartDef>.GetNamedSilentFail(key);
            return part ?? DefDatabase<FishingTacklePartDef>.AllDefsListForReading.First(def => def.slot == slot && def.isDefault);
        }

        public bool RequestUpgrade(FishingTacklePartDef part, out string reason)
        {
            reason = null;
            if (part == null || !part.Available) { reason = "That tackle part is not available."; return false; }
            if (HasPendingUpgrade) { reason = "This rod already has a pending tackle upgrade."; return false; }
            if (PartFor(part.slot) == part) { reason = "That part is already installed."; return false; }
            if (parent.ParentHolder is Pawn_EquipmentTracker equipment && equipment.Primary == parent)
            {
                if (!equipment.TryDropEquipment((ThingWithComps)parent, out ThingWithComps dropped, equipment.ParentHolder as Pawn != null
                        ? ((Pawn)equipment.ParentHolder).Position : IntVec3.Invalid, false))
                {
                    reason = "This fishing rod could not be unequipped for upgrading.";
                    return false;
                }
            }
            pendingSlot = part.slot;
            pendingPart = part.defName;
            nextMaterialCheckTick = 0;
            PendingFishingRodRegistry.Notify(this);
            return true;
        }

        public void CancelUpgrade()
        {
            ClearPendingUpgrade();
        }

        public void InstallPendingUpgrade()
        {
            FishingTacklePartDef part = PendingPart;
            if (part == null) return;
            SetKey(part.slot, part.isDefault ? null : part.defName);
            ClearPendingUpgrade();
        }

        public void DeferMaterialCheck()
        {
            const int MaterialRetryTicks = 600;
            nextMaterialCheckTick = (Find.TickManager?.TicksGame ?? 0) + MaterialRetryTicks;
        }

        private void ClearPendingUpgrade()
        {
            pendingPart = null;
            nextMaterialCheckTick = 0;
            PendingFishingRodRegistry.Notify(this);
        }

        public IEnumerable<Gizmo> EquippedGizmos()
        {
            yield return new Command_Action
            {
                defaultLabel = "Tackle",
                defaultDesc = "Customize this fishing rod's handle, rod, reel, line, and lure.",
                icon = parent.def.uiIcon,
                action = () => Find.WindowStack.Add(new Dialog_FishingTackle(this))
            };
        }

        public override string CompInspectStringExtra()
        {
            string installed = string.Join("\n", Enum.GetValues(typeof(FishingTackleSlot)).Cast<FishingTackleSlot>()
                .Select(slot => slot + ": " + PartFor(slot).LabelCap));
            if (HasPendingUpgrade) installed += "\nPending: " + PendingSlot + " -> " + PendingPart.LabelCap;
            return installed;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            yield return new StatDrawEntry(StatCategoryDefOf.Weapon, "Max Fish Mass", MaxFishMass.ToString("0.##") + " kg",
                "A hooked fish heavier than this mass cannot be landed.", 4100);
            yield return new StatDrawEntry(StatCategoryDefOf.Weapon, "Wait time factor", WaitTimeFactor.ToStringPercent(),
                "The combined tackle multiplier applied to the Wait phase.", 4099);
            yield return new StatDrawEntry(StatCategoryDefOf.Weapon, "Reel time factor", ReelTimeFactor.ToStringPercent(),
                "The combined tackle multiplier applied to the Reel phase.", 4098);
            yield return new StatDrawEntry(StatCategoryDefOf.Weapon, "Catch chance factor", CatchChanceFactor.ToStringPercent(),
                "The combined tackle multiplier applied to catch success.", 4097);
        }

        private string KeyFor(FishingTackleSlot slot)
        {
            switch (slot)
            {
                case FishingTackleSlot.Handle: return handlePart;
                case FishingTackleSlot.Rod: return rodPart;
                case FishingTackleSlot.Reel: return reelPart;
                case FishingTackleSlot.Line: return linePart;
                default: return lurePart;
            }
        }

        private void SetKey(FishingTackleSlot slot, string key)
        {
            if (slot == FishingTackleSlot.Handle) handlePart = key;
            else if (slot == FishingTackleSlot.Rod) rodPart = key;
            else if (slot == FishingTackleSlot.Reel) reelPart = key;
            else if (slot == FishingTackleSlot.Line) linePart = key;
            else lurePart = key;
        }
    }

    public sealed class Dialog_FishingTackle : Window
    {
        private readonly CompFishingRodTackle tackle;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(920f, 620f);

        public Dialog_FishingTackle(CompFishingRodTackle tackle)
        {
            this.tackle = tackle;
            doCloseX = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 38f), "Tackle");
            Text.Font = GameFont.Small;
            Rect outRect = new Rect(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 72f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, 5f * 100f + 44f);
            DrawColumnHeader(new Rect(inRect.x + 122f, inRect.y + 42f, 210f, 24f), "Name");
            DrawColumnHeader(new Rect(inRect.x + 340f, inRect.y + 42f, 250f, 24f), "Effects");
            DrawColumnHeader(new Rect(inRect.x + 598f, inRect.y + 42f, 156f, 24f), "Cost");
            Widgets.BeginScrollView(outRect, ref scroll, view);
            int index = 0;
            foreach (FishingTackleSlot slot in Enum.GetValues(typeof(FishingTackleSlot)))
            {
                FishingTacklePartDef current = tackle.PartFor(slot);
                Rect row = new Rect(0f, index++ * 100f, view.width, 92f);
                Widgets.DrawMenuSection(row);
                Widgets.Label(new Rect(row.x + 12f, row.y + 10f, 110f, 28f), slot.ToString());
                Rect nameRect = new Rect(row.x + 122f, row.y + 8f, 210f, 76f);
                Rect effectsRect = new Rect(row.x + 340f, row.y + 8f, 250f, 76f);
                Rect costRect = new Rect(row.x + 598f, row.y + 8f, 156f, 76f);
                Text.Font = GameFont.Medium;
                Widgets.Label(nameRect, current.LabelCap);
                Text.Font = GameFont.Small;
                Widgets.Label(effectsRect, current.EffectsSummary);
                Widgets.Label(costRect, current.CostSummary.Replace(" | ", "\n"));
                if (Widgets.ButtonText(new Rect(row.xMax - 112f, row.y + 10f, 100f, 34f), current.isDefault ? "Add" : "Replace"))
                    OpenPartMenu(slot);
                bool oldEnabled = GUI.enabled;
                GUI.enabled = !current.isDefault && !tackle.HasPendingUpgrade;
                if (Widgets.ButtonText(new Rect(row.xMax - 112f, row.y + 50f, 100f, 34f), "Remove"))
                    Request(DefDatabase<FishingTacklePartDef>.AllDefsListForReading.First(def => def.slot == slot && def.isDefault));
                GUI.enabled = oldEnabled;
            }
            if (tackle.HasPendingUpgrade)
            {
                Widgets.Label(new Rect(0f, 506f, view.width - 140f, 32f), "Pending: " + tackle.PendingSlot + " -> " + tackle.PendingPart.LabelCap);
                if (Widgets.ButtonText(new Rect(view.width - 130f, 504f, 120f, 34f), "Cancel bill")) tackle.CancelUpgrade();
            }
            Widgets.EndScrollView();
        }

        private void OpenPartMenu(FishingTackleSlot slot)
        {
            List<FloatMenuOption> options = DefDatabase<FishingTacklePartDef>.AllDefsListForReading
                .Where(part => part.slot == slot && !part.isDefault)
                .Select(part => new FloatMenuOption(part.LabelCap + "\n" + part.EffectsSummary + "\n" + part.CostSummary,
                    part.Available ? (Action)(() => Request(part)) : null)).ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void DrawColumnHeader(Rect rect, string label)
        {
            GUI.color = Color.gray;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
        }

        private void Request(FishingTacklePartDef part)
        {
            if (tackle.RequestUpgrade(part, out string reason))
                Messages.Message("Tackle upgrade bill created for " + tackle.parent.LabelCap + ".", MessageTypeDefOf.TaskCompletion, false);
            else Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
        }
    }

    public static class FishingRodUtility
    {
        public const int CastTicks = 180;
        public const int BaseWaitTicks = 4500;
        public const int BaseReelTicks = 2400;

        public static CompFishingRodTackle EquippedRod(Pawn pawn)
        {
            return pawn?.equipment?.AllEquipmentListForReading.Select(thing => thing.TryGetComp<CompFishingRodTackle>()).FirstOrDefault(comp => comp != null);
        }

        public static bool HasEquippedRod(Pawn pawn) => EquippedRod(pawn) != null;

        public static void FishingJobPostfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && !HasEquippedRod(pawn)) __result = null;
        }

        public static IEnumerable<Gizmo> EquipmentGizmosPostfix(IEnumerable<Gizmo> __result, Pawn_EquipmentTracker __instance)
        {
            foreach (Gizmo gizmo in __result) yield return gizmo;
            if (__instance?.Primary?.TryGetComp<CompFishingRodTackle>() is CompFishingRodTackle tackle)
                foreach (Gizmo gizmo in tackle.EquippedGizmos()) yield return gizmo;
        }

        public static float ExpertiseFactor(Pawn pawn)
        {
            KnowledgeRank level = FishingProgressionComponent.Current?.ProgressFor(pawn, false)?.ExpertiseLevel ?? KnowledgeRank.Novice;
            return 1f - FishingProgressionUtility.TimeReduction(level);
        }

        public static int WaitTicks(Pawn pawn)
        {
            CompFishingRodTackle rod = EquippedRod(pawn);
            return Mathf.Max(300, Mathf.RoundToInt(BaseWaitTicks * (rod?.WaitTimeFactor ?? 1f) * ExpertiseFactor(pawn) *
                (AquacultureMod.Settings?.fishingDurationFactor ?? 0.30f)));
        }

        public static int ReelTicks(Pawn pawn)
        {
            CompFishingRodTackle rod = EquippedRod(pawn);
            return Mathf.Max(240, Mathf.RoundToInt(BaseReelTicks * (rod?.ReelTimeFactor ?? 1f) * ExpertiseFactor(pawn) *
                (AquacultureMod.Settings?.fishingDurationFactor ?? 0.30f)));
        }

        public static float AttractionFor(Pawn pawn, ThingDef fishDef)
        {
            FishingTacklePartDef lure = EquippedRod(pawn)?.Lure;
            FishFishingExtension fish = fishDef?.GetModExtension<FishFishingExtension>();
            PondWaterKind water = AquaticSpeciesProfile.For(fishDef).waterKind;
            float lureFactor = water == PondWaterKind.Saltwater ? lure?.saltwaterAttraction ?? 1f : lure?.freshwaterAttraction ?? 1f;
            float fishFactor = water == PondWaterKind.Saltwater ? fish?.saltwaterAttraction ?? 1f : fish?.freshwaterAttraction ?? 1f;
            float knowledge = FishingProgressionComponent.Current?.ProgressFor(pawn, false)?.KnowledgeFor(fishDef) ?? 0f;
            return Mathf.Max(0.01f, lureFactor * fishFactor * (1f + knowledge * 0.15f));
        }

        public static float BaseFishMass(ThingDef fishDef)
        {
            float mass = 0.1f;
            try { mass = Mathf.Max(0.01f, fishDef.GetStatValueAbstract(StatDefOf.Mass)); } catch { }
            return mass;
        }

        public static IEnumerable<Toil> FishingPhases(object driver, string framework)
        {
            Pawn pawn = FishingAttemptIntegration.PawnFor(driver);
            IntVec3 cell = FishingAttemptIntegration.WaterCellFor(driver);

            Toil cast = Toils_General.Wait(CastTicks, TargetIndex.A);
            cast.debugName = "Fishing - Cast";
            cast.WithProgressBarToilDelay(TargetIndex.A);
            cast.initAction = () =>
            {
                if (pawn?.CurJob != null) pawn.CurJob.reportStringOverride = "fishing - cast.";
                FishingFeedback.Cast(pawn, cell);
            };
            cast.tickAction = () => pawn?.rotationTracker?.FaceCell(cell);
            yield return cast;

            int waitDuration = WaitTicks(pawn);
            int waitElapsed = 0;
            Toil wait = Toils_General.Wait(waitDuration, TargetIndex.A);
            wait.debugName = "Fishing - Wait";
            wait.WithProgressBarToilDelay(TargetIndex.A);
            wait.initAction = () =>
            {
                if (pawn?.CurJob != null) pawn.CurJob.reportStringOverride = "fishing - wait.";
                FishingAttemptIntegration.BeginWaitPhase(driver, framework, waitDuration);
                FishingFeedback.WaitStarted(pawn, cell);
            };
            wait.tickAction = () =>
            {
                waitElapsed++;
                FishingAttemptRecord attempt = FishingAttemptIntegration.ProgressWaitPhase(driver, framework, waitElapsed, waitDuration);
                FishingFeedback.WaitTick(pawn, cell, attempt, waitElapsed, waitDuration);
                MaintainFishing(pawn, cell);
            };
            yield return wait;

            int reelDuration = ReelTicks(pawn);
            int reelElapsed = 0;
            Toil reel = Toils_General.Wait(reelDuration, TargetIndex.A);
            reel.debugName = "Fishing - Reel";
            reel.WithProgressBarToilDelay(TargetIndex.A);
            reel.initAction = () =>
            {
                if (pawn?.CurJob != null) pawn.CurJob.reportStringOverride = "fishing - reel.";
                FishingFeedback.ReelStarted(pawn, cell);
            };
            reel.tickAction = () =>
            {
                reelElapsed++;
                FishingFeedback.ReelTick(pawn, cell, FishingProgressionComponent.Current?.AttemptFor(pawn), reelElapsed, reelDuration);
                MaintainFishing(pawn, cell);
            };
            reel.AddFinishAction(() => { if (pawn?.CurJob != null) pawn.CurJob.reportStringOverride = "fishing."; });
            yield return reel;
        }

        private static void MaintainFishing(Pawn pawn, IntVec3 cell)
        {
            pawn?.rotationTracker?.FaceCell(cell);
            CompFishingRodTackle rod = EquippedRod(pawn);
            if (pawn?.needs?.joy != null && rod != null && rod.RecreationLossFactor < 1f)
                pawn.needs.joy.CurLevel += (1f - rod.RecreationLossFactor) * 0.000004f;
        }
    }

    /// <summary>Small, pawn-driven feedback cues. It deliberately reuses the fishing job cadence.</summary>
    public static class FishingFeedback
    {
        public static void Cast(Pawn pawn, IntVec3 cell)
        {
            if (pawn?.Map == null || !cell.IsValid) return;
            MoteMaker.ThrowText(cell.ToVector3Shifted(), pawn.Map, "Cast");
        }

        public static void WaitStarted(Pawn pawn, IntVec3 cell)
        {
            if (pawn?.Map == null || !cell.IsValid) return;
            MoteMaker.ThrowText(cell.ToVector3Shifted(), pawn.Map, "Waiting");
        }

        public static void WaitTick(Pawn pawn, IntVec3 cell, FishingAttemptRecord attempt, int elapsed, int duration)
        {
            if (attempt?.biteDecided != true || attempt.feedbackBiteShown || elapsed % 45 != 0) return;
            if (attempt.fishBit)
            {
                attempt.feedbackBiteShown = true;
                MoteMaker.ThrowText(cell.ToVector3Shifted(), pawn.Map, "Bite!");
                if (pawn?.CurJob != null) pawn.CurJob.reportStringOverride = "fishing - bite!";
            }
            else if (elapsed >= Mathf.Max(45, Mathf.RoundToInt(duration * 0.35f)))
            {
                attempt.feedbackBiteShown = true;
                MoteMaker.ThrowText(cell.ToVector3Shifted(), pawn.Map, "No bite");
            }
        }

        public static void ReelStarted(Pawn pawn, IntVec3 cell)
        {
            if (pawn?.Map == null || !cell.IsValid) return;
            MoteMaker.ThrowText(cell.ToVector3Shifted(), pawn.Map, "Reel");
        }

        public static void ReelTick(Pawn pawn, IntVec3 cell, FishingAttemptRecord attempt, int elapsed, int duration)
        {
            if (pawn == null || attempt?.fishBit != true || elapsed % 90 != 0) return;
            float tension = TensionFor(pawn, attempt);
            if (pawn.CurJob != null) pawn.CurJob.reportStringOverride = "fishing - reel (tension " + tension.ToStringPercent() + ")";
            if (tension > 0.90f) MoteMaker.ThrowText(cell.ToVector3Shifted(), pawn.Map, "Line strain");
            else if (tension < 0.35f) MoteMaker.ThrowText(cell.ToVector3Shifted(), pawn.Map, "Fish tiring");
        }

        public static float TensionFor(Pawn pawn, FishingAttemptRecord attempt)
        {
            if (attempt?.FishDef == null) return 0f;
            CompFishingRodTackle rod = FishingRodUtility.EquippedRod(pawn);
            float capacity = rod?.MaxFishMass ?? 0.1f;
            float massPressure = Mathf.Clamp01(attempt.HookedFishMass / Mathf.Max(0.1f, capacity));
            float animals = pawn?.skills?.GetSkill(SkillDefOf.Animals).Level / 20f ?? 0f;
            float knowledge = AquacultureKnowledgeAdapter.SpeciesKnowledgeFor(pawn, attempt.FishDef);
            return Mathf.Clamp01(0.18f + massPressure * 0.70f - animals * 0.18f - knowledge * 0.12f);
        }
    }

    [DefOf]
    public static class FishingRodDefOf
    {
        public static JobDef AF_UpgradeFishingRod;
        static FishingRodDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(FishingRodDefOf)); }
    }

    public static class PendingFishingRodRegistry
    {
        private static readonly Dictionary<Map, HashSet<Thing>> ByMap = new Dictionary<Map, HashSet<Thing>>();

        public static IEnumerable<Thing> For(Map map)
        {
            if (map == null || !ByMap.TryGetValue(map, out HashSet<Thing> rods)) return Enumerable.Empty<Thing>();
            rods.RemoveWhere(rod => rod?.Spawned != true || rod.Map != map || rod.TryGetComp<CompFishingRodTackle>()?.HasPendingUpgrade != true);
            if (rods.Count == 0)
            {
                ByMap.Remove(map);
                return Enumerable.Empty<Thing>();
            }
            return rods.ToList();
        }

        public static void Notify(CompFishingRodTackle tackle)
        {
            Thing rod = tackle?.parent;
            Map map = rod?.Map;
            if (map == null) return;
            if (!ByMap.TryGetValue(map, out HashSet<Thing> rods))
            {
                rods = new HashSet<Thing>();
                ByMap[map] = rods;
            }
            if (tackle.HasPendingUpgrade) rods.Add(rod);
            else rods.Remove(rod);
            if (rods.Count == 0) ByMap.Remove(map);
        }

        public static void Remove(Thing rod, Map map)
        {
            if (map == null || !ByMap.TryGetValue(map, out HashSet<Thing> rods)) return;
            rods.Remove(rod);
            if (rods.Count == 0) ByMap.Remove(map);
        }
    }

    public sealed class WorkGiver_UpgradeFishingRod : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForUndefined();
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => PendingFishingRodRegistry.For(pawn?.Map);

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompFishingRodTackle tackle = thing?.TryGetComp<CompFishingRodTackle>();
            FishingTacklePartDef part = tackle?.PendingPart;
            return part != null && part.Available && pawn.skills.GetSkill(SkillDefOf.Crafting).Level >= part.minimumCraftingSkill &&
                tackle.CanCheckMaterials && !thing.IsForbidden(pawn) && pawn.CanReserve(thing);
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompFishingRodTackle tackle = thing.TryGetComp<CompFishingRodTackle>();
            List<ThingCount> ingredients = FindIngredients(pawn, tackle.PendingPart);
            if (ingredients == null)
            {
                tackle.DeferMaterialCheck();
                return null;
            }
            Job job = JobMaker.MakeJob(FishingRodDefOf.AF_UpgradeFishingRod, thing);
            job.targetQueueB = ingredients.Select(pair => (LocalTargetInfo)pair.Thing).ToList();
            job.countQueue = ingredients.Select(pair => pair.Count).ToList();
            return job;
        }

        private static List<ThingCount> FindIngredients(Pawn pawn, FishingTacklePartDef part)
        {
            var result = new List<ThingCount>();
            if (part?.costs == null) return result;
            foreach (ThingDefCountClass cost in part.costs)
            {
                int remaining = cost.count;
                foreach (Thing thing in pawn.Map.listerThings.ThingsOfDef(cost.thingDef)
                    .Where(candidate => !candidate.IsForbidden(pawn) && pawn.CanReserveAndReach(candidate, PathEndMode.ClosestTouch, Danger.Some)))
                {
                    int take = Mathf.Min(remaining, thing.stackCount);
                    result.Add(new ThingCount(thing, take));
                    remaining -= take;
                    if (remaining <= 0) break;
                }
                if (remaining > 0) return null;
            }
            return result.Count == 0 ? new List<ThingCount>() : result;
        }
    }

    public sealed class JobDriver_UpgradeFishingRod : JobDriver
    {
        private Thing Rod => job.targetA.Thing;
        private CompFishingRodTackle Tackle => Rod?.TryGetComp<CompFishingRodTackle>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Rod, job, 1, -1, null, errorOnFailed)) return false;
            pawn.ReserveAsManyAsPossible(job.targetQueueB, job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            AddFailCondition(() => Tackle?.PendingPart == null);
            Toil extract = ToilMaker.MakeToil("SelectTackleMaterial");
            extract.initAction = () =>
            {
                if (job.targetQueueB.NullOrEmpty()) return;
                job.targetB = job.targetQueueB[0];
                job.targetQueueB.RemoveAt(0);
                job.count = job.countQueue.NullOrEmpty() ? 1 : job.countQueue[0];
                if (!job.countQueue.NullOrEmpty()) job.countQueue.RemoveAt(0);
            };
            extract.defaultCompleteMode = ToilCompleteMode.Instant;
            if (!job.targetQueueB.NullOrEmpty())
            {
                yield return extract;
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                Toil consume = ToilMaker.MakeToil("ConsumeTackleMaterial");
                consume.initAction = ConsumeIngredients;
                consume.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return consume;
                yield return Toils_Jump.JumpIf(extract, () => !job.targetQueueB.NullOrEmpty());
            }
            else yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil work = Toils_General.Wait(Mathf.Max(60, Mathf.RoundToInt(Tackle?.PendingPart?.workAmount ?? 60f)), TargetIndex.A);
            work.debugName = "Upgrade fishing rod tackle";
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.activeSkill = () => SkillDefOf.Crafting;
            work.tickAction = () => pawn.skills.Learn(SkillDefOf.Crafting, 0.08f);
            yield return work;

            Toil install = ToilMaker.MakeToil("InstallFishingTackle");
            install.initAction = () => Tackle?.InstallPendingUpgrade();
            install.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return install;
        }

        private void ConsumeIngredients()
        {
            if (pawn.carryTracker.CarriedThing != null) pawn.carryTracker.DestroyCarriedThing();
        }
    }
}
