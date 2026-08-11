using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    public enum FishingRodSource
    {
        Equipped,
        Inventory,
        MapStorage
    }

    public sealed class FishingRodSession : IExposable
    {
        public Pawn pawn;
        public Thing rod;
        public ThingWithComps previousPrimary;
        public FishingRodSource source;
        public string jobDefName;
        public IntVec3 targetACell;
        public bool prepared;
        public bool targetCReplaced;
        public bool originalTargetCValid;
        public bool originalTargetCHasThing;
        public IntVec3 originalTargetCCell;
        public Thing originalTargetThing;

        [NonSerialized]
        internal Job runtimeJob;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref rod, "rod");
            Scribe_References.Look(ref previousPrimary, "previousPrimary");
            Scribe_Values.Look(ref source, "source", FishingRodSource.Equipped);
            Scribe_Values.Look(ref jobDefName, "jobDefName");
            Scribe_Values.Look(ref targetACell, "targetACell");
            Scribe_Values.Look(ref prepared, "prepared");
            Scribe_Values.Look(ref targetCReplaced, "targetCReplaced");
            Scribe_Values.Look(ref originalTargetCValid, "originalTargetCValid");
            Scribe_Values.Look(ref originalTargetCHasThing, "originalTargetCHasThing");
            Scribe_Values.Look(ref originalTargetCCell, "originalTargetCCell");
            Scribe_References.Look(ref originalTargetThing, "originalTargetThing");
        }

        public bool Matches(Pawn candidatePawn, Job job)
        {
            if (candidatePawn != pawn || job == null) return false;
            if (runtimeJob != null) return runtimeJob == job;
            if (candidatePawn.CurJob != job) return false;
            return jobDefName == job.def?.defName && targetACell == job.targetA.Cell;
        }
    }

    public sealed class FishingRodWorkflowComponent : GameComponent
    {
        private List<FishingRodSession> sessions = new List<FishingRodSession>();

        public FishingRodWorkflowComponent(Game game) { }

        public static FishingRodWorkflowComponent Current => Verse.Current.Game?.GetComponent<FishingRodWorkflowComponent>();

        public FishingRodSession Find(Pawn pawn, Job job)
        {
            return sessions.FirstOrDefault(session => session != null && session.Matches(pawn, job));
        }

        public FishingRodSession FindForPawn(Pawn pawn)
        {
            return sessions.FirstOrDefault(session => session?.pawn == pawn);
        }

        public bool Add(FishingRodSession session)
        {
            if (session == null) return false;
            FishingRodSession existing = FindForPawn(session.pawn);
            if (existing != null && existing != session)
            {
                if (!FishingRodWorkflow.RestoreStaleSession(existing, existing.pawn)) return false;
            }
            sessions.RemoveAll(item => item?.pawn == session.pawn);
            sessions.Add(session);
            return true;
        }

        public override void GameComponentTick()
        {
            AquacultureInGameTestTickPatch.PollFromExistingComponent();
            int ticks = Verse.Find.TickManager?.TicksGame ?? -1;
            if (ticks < 0 || ticks % 60 != 0) return;
            foreach (FishingRodSession session in sessions.ToList())
            {
                if (session?.pawn == null)
                {
                    sessions.Remove(session);
                    continue;
                }
                Job currentJob = session.pawn.CurJob;
                if (currentJob != null && session.Matches(session.pawn, currentJob)) continue;
                if (!session.prepared && !FishingRodWorkflow.IsHeldByPawn(session.pawn, session.rod))
                {
                    sessions.Remove(session);
                    continue;
                }
                if (FishingRodWorkflow.RestoreStaleSession(session, session.pawn)) sessions.Remove(session);
            }
        }

        public void Remove(FishingRodSession session)
        {
            if (session != null) sessions.Remove(session);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref sessions, "fishingRodSessions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                sessions = sessions?.Where(session => session?.pawn != null).ToList() ?? new List<FishingRodSession>();
        }
    }

    public sealed class FishingRodMapComponent : MapComponent
    {
        private readonly HashSet<Thing> rods = new HashSet<Thing>();
        private int nextCleanupTick;

        public FishingRodMapComponent(Map map) : base(map) { }

        public void Register(Thing rod)
        {
            if (rod?.TryGetComp<CompFishingRodTackle>() != null) rods.Add(rod);
        }

        public void Deregister(Thing rod)
        {
            if (rod != null) rods.Remove(rod);
        }

        public IEnumerable<ThingWithComps> RodsFor(Pawn pawn)
        {
            CleanupIfNeeded();
            return rods.OfType<ThingWithComps>().Where(rod =>
                rod.Spawned && rod.Map == map && rod.TryGetComp<CompFishingRodTackle>() != null).ToList();
        }

        private void CleanupIfNeeded()
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (tick < nextCleanupTick) return;
            nextCleanupTick = tick + 600;
            rods.RemoveWhere(rod => rod == null || !rod.Spawned || rod.Map != map || rod.TryGetComp<CompFishingRodTackle>() == null);
        }
    }

    public static class FishingRodWorkflow
    {
        private const string NoRodKey = "AquacultureFishing.NoUsableFishingRod";
        private const string RestrictedRodKey = "AquacultureFishing.FishingRodRestricted";
        private const string NoReachableRodKey = "AquacultureFishing.NoReachableFishingRod";
        private const string NoRodFallback = "No usable fishing rod is available.";
        private const string RestrictedFallback = "This pawn cannot automatically acquire a fishing rod.";
        private const string NoReachableFallback = "No usable fishing rod is reachable in approved storage.";

        public static bool TryEnsureSelection(Pawn pawn, Job job, out string reason)
        {
            reason = null;
            if (pawn == null || job == null)
            {
                reason = Text(NoRodKey, NoRodFallback);
                return false;
            }

            FishingRodWorkflowComponent component = FishingRodWorkflowComponent.Current;
            if (component == null)
            {
                ThingWithComps equippedFallback = FishingRodUtility.EquippedRodThing(pawn);
                if (equippedFallback != null) return true;
                reason = Text(NoRodKey, NoRodFallback);
                return false;
            }

            FishingRodSession existing = component.Find(pawn, job);
            if (existing != null)
            {
                if (existing.prepared || RodStillAvailable(pawn, existing)) return true;
                component.Remove(existing);
            }

            FishingRodSession stale = component.FindForPawn(pawn);
            if (stale != null)
            {
                if (!RestoreStaleSession(stale, pawn))
                {
                    reason = Text(NoRodKey, NoRodFallback);
                    return false;
                }
                component.Remove(stale);
            }

            ThingWithComps equipped = FishingRodUtility.EquippedRodThing(pawn);
            if (equipped != null)
            {
                if (component.Add(NewSession(pawn, job, equipped, FishingRodSource.Equipped, true))) return true;
            }

            if (pawn.IsPrisoner || pawn.IsSlave)
            {
                reason = Text(RestrictedRodKey, RestrictedFallback);
                return false;
            }

            ThingWithComps rod = BestInventoryRod(pawn);
            if (rod != null)
            {
                if (component.Add(NewSession(pawn, job, rod, FishingRodSource.Inventory, false))) return true;
            }

            rod = BestStoredRod(pawn);
            if (rod != null)
            {
                if (component.Add(NewSession(pawn, job, rod, FishingRodSource.MapStorage, false))) return true;
            }

            reason = Text(NoReachableRodKey, NoReachableFallback);
            if (pawn.Map == null) reason = Text(NoRodKey, NoRodFallback);
            return false;
        }

        public static bool TryReserve(object driver, bool errorOnFailed, out string reason)
        {
            reason = null;
            JobDriver jobDriver = driver as JobDriver;
            Pawn pawn = jobDriver?.GetActor();
            Job job = JobFor(jobDriver);
            if (!TryEnsureSelection(pawn, job, out reason))
            {
                ReportFailure(reason);
                return false;
            }

            FishingRodSession session = FishingRodWorkflowComponent.Current?.Find(pawn, job);
            if (session == null || session.prepared || session.source != FishingRodSource.MapStorage) return true;
            if (!RodStillAvailable(pawn, session) || !pawn.Reserve(session.rod, job, 1, -1, null, errorOnFailed))
            {
                reason = Text(NoReachableRodKey, NoReachableFallback);
                ReportFailure(reason);
                return false;
            }
            return true;
        }

        public static bool FishingPreToilReservationsPrefix(object __instance, bool errorOnFailed)
        {
            return TryReserve(__instance, errorOnFailed, out _);
        }

        public static IEnumerable<Toil> PreparationToils(object driver)
        {
            JobDriver jobDriver = driver as JobDriver;
            Pawn pawn = jobDriver?.GetActor();
            Job job = JobFor(jobDriver);
            if (jobDriver == null || pawn == null || job == null) yield break;

            jobDriver.AddFinishAction(_ => Cleanup(pawn, job));
            if (!TryEnsureSelection(pawn, job, out string reason))
            {
                yield return FailureToil(jobDriver, reason);
                yield break;
            }

            FishingRodWorkflowComponent component = FishingRodWorkflowComponent.Current;
            if (component == null) yield break;
            FishingRodSession session = component.Find(pawn, job);
            if (session == null || session.prepared || session.source == FishingRodSource.Equipped) yield break;

            RestoreTargetC(job, session);
            if (session.source == FishingRodSource.MapStorage && !IsHeldByPawn(pawn, session.rod))
            {
                yield return GotoStoredRodToil(jobDriver, pawn, session);
                yield return CarryStoredRodToil(jobDriver, pawn, session);
            }

            yield return PrepareToil(jobDriver, pawn, job);
        }

        public static void CleanupPostfix(object __instance)
        {
            JobDriver jobDriver = __instance as JobDriver;
            if (jobDriver == null) return;
            Cleanup(jobDriver.GetActor(), JobFor(jobDriver));
        }

        public static void Cleanup(Pawn pawn, Job job)
        {
            FishingRodWorkflowComponent component = FishingRodWorkflowComponent.Current;
            FishingRodSession session = component?.Find(pawn, job);
            if (session == null) return;

            RestoreTargetC(job, session);
            bool holdingTemporaryRod = session.source != FishingRodSource.Equipped && IsHeldByPawn(pawn, session.rod);
            if ((session.prepared || holdingTemporaryRod) && session.source != FishingRodSource.Equipped)
            {
                if (!RestoreTemporaryEquipment(pawn, session)) return;
            }
            component.Remove(session);
        }

        public static string ScoreDescription(ThingWithComps rod)
        {
            CompFishingRodTackle tackle = rod?.TryGetComp<CompFishingRodTackle>();
            return tackle == null ? string.Empty :
                tackle.MaxFishMass.ToString("0.##") + ":" + tackle.CatchChanceFactor.ToString("0.###") + ":" +
                (tackle.WaitTimeFactor * tackle.ReelTimeFactor).ToString("0.###");
        }

        private static Toil PrepareToil(JobDriver driver, Pawn pawn, Job job)
        {
            Toil toil = ToilMaker.MakeToil("AcquireFishingRod");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                FishingRodSession session = FishingRodWorkflowComponent.Current?.Find(pawn, job);
                if (session == null || !TryPrepareTemporaryEquipment(pawn, session))
                {
                    string reason = Text(NoRodKey, NoRodFallback);
                    ReportFailure(reason);
                    RestoreTargetC(job, session);
                    driver.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                RestoreTargetC(job, session);
            };
            return toil;
        }

        private static Toil GotoStoredRodToil(JobDriver driver, Pawn pawn, FishingRodSession session)
        {
            ThingWithComps rod = session.rod as ThingWithComps;
            Toil toil = ToilMaker.MakeToil("GotoFishingRod");
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            toil.initAction = () =>
            {
                if (!StoredRodStillAvailable(pawn, rod))
                {
                    ReportFailure(Text(NoReachableRodKey, NoReachableFallback));
                    driver.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                pawn.pather.StartPath(rod, PathEndMode.ClosestTouch);
            };
            toil.AddEndCondition(() => rod != null && rod.Spawned && rod.Map == pawn.Map
                ? JobCondition.Ongoing : JobCondition.Incompletable);
            return toil;
        }

        private static Toil CarryStoredRodToil(JobDriver driver, Pawn pawn, FishingRodSession session)
        {
            ThingWithComps rod = session.rod as ThingWithComps;
            Toil toil = ToilMaker.MakeToil("CarryFishingRod");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                if (pawn.carryTracker?.CarriedThing == rod) return;
                if (!StoredRodStillAvailable(pawn, rod) || pawn.carryTracker == null || !pawn.carryTracker.TryStartCarry(rod))
                {
                    ReportFailure(Text(NoReachableRodKey, NoReachableFallback));
                    driver.EndJobWith(JobCondition.Incompletable);
                }
            };
            return toil;
        }

        private static bool StoredRodStillAvailable(Pawn pawn, ThingWithComps rod)
        {
            return rod != null && !rod.Destroyed && !rod.Discarded && rod.Spawned && rod.Map == pawn?.Map &&
                ApprovedStoredRod(rod, pawn);
        }

        private static Toil FailureToil(JobDriver driver, string reason)
        {
            Toil toil = ToilMaker.MakeToil("AcquireFishingRod");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                ReportFailure(reason);
                driver.EndJobWith(JobCondition.Incompletable);
            };
            return toil;
        }

        private static bool TryPrepareTemporaryEquipment(Pawn pawn, FishingRodSession session)
        {
            ThingWithComps rod = session?.rod as ThingWithComps;
            if (pawn?.equipment == null || rod == null || rod.Destroyed || rod.Discarded) return false;
            if (pawn.equipment.Contains(rod))
            {
                session.prepared = true;
                return true;
            }

            ThingOwner inventory = pawn.inventory?.GetDirectlyHeldThings();
            ThingOwner carried = pawn.carryTracker?.GetDirectlyHeldThings();
            bool inInventory = inventory?.Contains(rod) == true;
            bool carriedRod = carried?.Contains(rod) == true;
            if (!inInventory && !carriedRod) return false;

            ThingWithComps previous = pawn.equipment.Primary;
            if (previous != null && previous != rod)
            {
                if (inventory == null || !pawn.equipment.TryTransferEquipmentToContainer(previous, inventory)) return false;
                session.previousPrimary = previous;
            }

            bool removed = inInventory ? inventory.Remove(rod) : carried.Remove(rod);
            if (!removed)
            {
                RestorePreviousWeapon(pawn, session.previousPrimary);
                session.previousPrimary = null;
                return false;
            }

            try
            {
                pawn.equipment.AddEquipment(rod);
            }
            catch
            {
                AbortPreparation(pawn, session, rod);
                return false;
            }

            if (!pawn.equipment.Contains(rod))
            {
                AbortPreparation(pawn, session, rod);
                return false;
            }

            session.prepared = true;
            return true;
        }

        private static void AbortPreparation(Pawn pawn, FishingRodSession session, ThingWithComps rod)
        {
            bool previousRestored = RestorePreviousWeapon(pawn, session.previousPrimary);
            if (previousRestored) session.previousPrimary = null;
            bool rodRestored = PutInInventoryOrDrop(pawn, rod);
            if (!previousRestored || !rodRestored) session.prepared = true;
        }

        private static bool RestoreTemporaryEquipment(Pawn pawn, FishingRodSession session)
        {
            if (pawn == null || session == null) return false;
            ThingWithComps rod = session.rod as ThingWithComps;
            bool rodRestored = true;
            if (rod != null && !rod.Destroyed && !rod.Discarded)
            {
                ThingOwner inventory = pawn.inventory?.GetDirectlyHeldThings();
                if (pawn.equipment?.Contains(rod) == true)
                {
                    if (inventory == null || !pawn.equipment.TryTransferEquipmentToContainer(rod, inventory))
                    {
                        rodRestored = pawn.Map != null && pawn.Position.IsValid &&
                            pawn.equipment.TryDropEquipment(rod, out _, pawn.Position, false);
                    }
                }
                else if (pawn.carryTracker?.GetDirectlyHeldThings().Contains(rod) == true)
                {
                    if (inventory == null || !pawn.carryTracker.GetDirectlyHeldThings().TryTransferToContainer(rod, inventory, false))
                    {
                        rodRestored = pawn.Map != null && pawn.Position.IsValid &&
                            pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _, null);
                    }
                }
                else if (inventory?.Contains(rod) == true || rod.Spawned)
                {
                    // The rod was already returned or an equipment mod moved it externally.
                }
                else if (rod.ParentHolder == null)
                {
                    rodRestored = PutInInventoryOrDrop(pawn, rod);
                }
            }
            bool previousRestored = RestorePreviousWeapon(pawn, session.previousPrimary);
            return rodRestored && previousRestored;
        }

        internal static bool RestoreStaleSession(FishingRodSession session, Pawn pawn)
        {
            if (session == null) return true;
            RestoreTargetC(session.runtimeJob ?? pawn?.CurJob, session);
            bool holdingTemporaryRod = session.source != FishingRodSource.Equipped && IsHeldByPawn(pawn, session.rod);
            if ((!session.prepared && !holdingTemporaryRod) || session.source == FishingRodSource.Equipped) return true;
            return RestoreTemporaryEquipment(pawn, session);
        }

        private static bool RestorePreviousWeapon(Pawn pawn, ThingWithComps previous)
        {
            if (previous == null || previous.Destroyed || previous.Discarded) return true;
            if (pawn?.equipment == null) return false;
            if (pawn.equipment.Contains(previous)) return true;
            if (pawn.equipment.Primary != null) return false;
            ThingOwner inventory = pawn.inventory?.GetDirectlyHeldThings();
            if (inventory?.Contains(previous) != true)
                return previous.ParentHolder != null || previous.Spawned;
            if (!inventory.Remove(previous)) return false;
            try
            {
                pawn.equipment.AddEquipment(previous);
                if (pawn.equipment.Contains(previous)) return true;
            }
            catch
            {
            }
            inventory.TryAddOrTransfer(previous, false);
            return false;
        }

        private static bool PutInInventoryOrDrop(Pawn pawn, ThingWithComps rod)
        {
            if (pawn == null || rod == null || rod.Destroyed || rod.Discarded) return false;
            ThingOwner inventory = pawn.inventory?.GetDirectlyHeldThings();
            if (inventory != null && inventory.TryAddOrTransfer(rod, false)) return true;
            if (pawn.Map != null && pawn.Position.IsValid)
            {
                if (GenPlace.TryPlaceThing(rod, pawn.Position, pawn.Map, ThingPlaceMode.Near)) return true;
            }
            if (pawn.equipment?.Primary == null)
            {
                try
                {
                    pawn.equipment.AddEquipment(rod);
                    if (pawn.equipment.Contains(rod)) return true;
                }
                catch { }
            }
            if (pawn.carryTracker != null && pawn.carryTracker.TryStartCarry(rod)) return true;
            return false;
        }

        private static bool RodStillAvailable(Pawn pawn, FishingRodSession session)
        {
            if (session?.rod is not ThingWithComps rod || rod.Destroyed || rod.Discarded) return false;
            if (session.prepared) return true;
            if (session.source == FishingRodSource.Inventory) return pawn.inventory?.GetDirectlyHeldThings().Contains(rod) == true;
            if (session.source == FishingRodSource.MapStorage)
                return IsHeldByPawn(pawn, rod) ||
                    (rod.Spawned && rod.Map == pawn.Map && ApprovedStoredRod(rod, pawn) && pawn.CanReserveAndReach(rod, PathEndMode.ClosestTouch, Danger.Some));
            return pawn.equipment?.Contains(rod) == true;
        }

        private static ThingWithComps BestInventoryRod(Pawn pawn)
        {
            return pawn?.inventory?.GetDirectlyHeldThings().OfType<ThingWithComps>()
                .Where(rod => rod.TryGetComp<CompFishingRodTackle>() != null && !rod.IsForbidden(pawn))
                .OrderByDescending(RodMass)
                .ThenByDescending(RodCatch)
                .ThenBy(RodTiming)
                .ThenBy(rod => rod.ThingID)
                .FirstOrDefault();
        }

        private static ThingWithComps BestStoredRod(Pawn pawn)
        {
            return pawn?.Map?.GetComponent<FishingRodMapComponent>()?.RodsFor(pawn)
                .Where(rod => ApprovedStoredRod(rod, pawn))
                .Where(rod => pawn.CanReserveAndReach(rod, PathEndMode.ClosestTouch, Danger.Some))
                .OrderByDescending(RodMass)
                .ThenByDescending(RodCatch)
                .ThenBy(RodTiming)
                .ThenBy(rod => rod.ThingID)
                .FirstOrDefault();
        }

        private static bool ApprovedStoredRod(ThingWithComps rod, Pawn pawn)
        {
            if (rod == null || pawn == null || !rod.Spawned || rod.Map != pawn.Map || rod.IsForbidden(pawn)) return false;
            try { return StoreUtility.IsInValidStorage(rod); }
            catch { return false; }
        }

        private static float RodMass(ThingWithComps rod) => rod.TryGetComp<CompFishingRodTackle>()?.MaxFishMass ?? 0f;
        private static float RodCatch(ThingWithComps rod) => rod.TryGetComp<CompFishingRodTackle>()?.CatchChanceFactor ?? 0f;
        private static float RodTiming(ThingWithComps rod)
        {
            CompFishingRodTackle tackle = rod.TryGetComp<CompFishingRodTackle>();
            return tackle == null ? float.MaxValue : tackle.WaitTimeFactor * tackle.ReelTimeFactor;
        }

        private static FishingRodSession NewSession(Pawn pawn, Job job, ThingWithComps rod, FishingRodSource source, bool prepared)
        {
            FishingRodSession session = new FishingRodSession
            {
                pawn = pawn,
                rod = rod,
                source = source,
                prepared = prepared,
                runtimeJob = job,
                jobDefName = job.def?.defName,
                targetACell = job.targetA.Cell,
                originalTargetCValid = job.targetC.HasThing || job.targetC.Cell.IsValid,
                originalTargetCHasThing = job.targetC.HasThing,
                originalTargetThing = job.targetC.Thing,
                originalTargetCCell = job.targetC.Cell
            };
            return session;
        }

        internal static bool IsHeldByPawn(Pawn pawn, Thing rod)
        {
            return pawn?.inventory?.GetDirectlyHeldThings().Contains(rod) == true ||
                pawn?.carryTracker?.GetDirectlyHeldThings().Contains(rod) == true ||
                pawn?.equipment?.Contains(rod) == true;
        }

        private static Job JobFor(JobDriver driver)
        {
            return driver == null ? null : AccessTools.Field(typeof(JobDriver), "job")?.GetValue(driver) as Job;
        }

        private static void RestoreTargetC(Job job, FishingRodSession session)
        {
            if (job == null || session == null || !session.targetCReplaced) return;
            if (!session.originalTargetCValid) job.targetC = LocalTargetInfo.Invalid;
            else if (session.originalTargetCHasThing && session.originalTargetThing != null) job.targetC = session.originalTargetThing;
            else job.targetC = session.originalTargetCCell;
            session.targetCReplaced = false;
        }

        private static string Text(string key, string fallback)
        {
            string translated = key.Translate().ToString();
            return translated == key ? fallback : translated;
        }

        public static void ReportFailure(string reason)
        {
            if (!reason.NullOrEmpty()) JobFailReason.Is(reason);
        }
    }
}
