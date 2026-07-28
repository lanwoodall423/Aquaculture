using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    [DefOf]
    public static class AquacultureJobDefOf
    {
        public static JobDef AF_HarvestPondFish;
        public static DesignationDef AF_HarvestPondFishDesignation;
        public static JobDef AF_RemovePondEgg;
        public static DesignationDef AF_RemovePondEggDesignation;
        public static JobDef AF_SterilizePondFish;
        public static DesignationDef AF_SterilizePondFishDesignation;

        static AquacultureJobDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(AquacultureJobDefOf)); }
    }

    public static class FishHarvestUtility
    {
        public static void Designate(CompFishTraits fish, bool showMessage = true)
        {
            if (fish?.parent?.Spawned != true) return;
            Map map = fish.parent.Map;
            FishPondMapComponent ponds = map.GetComponent<FishPondMapComponent>();
            if (ponds?.CanHarvestFish(fish) != true)
            {
                Messages.Message("This fish no longer meets the pond's harvest rules.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) != null)
            {
                Messages.Message("This fish is already marked for harvest.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            map.designationManager.AddDesignation(new Designation(fish.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation));
            ponds.InvalidatePondMenus();
            if (showMessage) Messages.Message(fish.parent.LabelCap + " marked for pond harvest.", fish.parent, MessageTypeDefOf.TaskCompletion, false);
        }

        public static void DesignateSterilization(CompFishTraits fish)
        {
            if (fish?.parent?.Spawned != true || !fish.IsAlive || !fish.IsInPond || fish.sterilized) return;
            Map map = fish.parent.Map;
            if (map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_SterilizePondFishDesignation) != null)
            {
                Messages.Message("This fish is already marked for sterilization.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            map.designationManager.AddDesignation(new Designation(fish.parent, AquacultureJobDefOf.AF_SterilizePondFishDesignation));
            map.GetComponent<FishPondMapComponent>()?.InvalidatePondMenus();
            Messages.Message(fish.parent.LabelCap + " marked for sterilization.", fish.parent, MessageTypeDefOf.TaskCompletion, false);
        }
    }

    public sealed class WorkGiver_HarvestPondFish : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.GetComponent<FishPondMapComponent>()?.DesignatedHarvestFish() ?? Enumerable.Empty<Thing>();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompFishTraits fish = thing?.TryGetComp<CompFishTraits>();
            FishPondMapComponent ponds = pawn.Map.GetComponent<FishPondMapComponent>();
            return fish != null
                && fish.IsInPond
                && ponds?.CanHarvestFish(fish) == true
                && pawn.Map.designationManager.DesignationOn(thing, AquacultureJobDefOf.AF_HarvestPondFishDesignation) != null
                && pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced)
                && ponds.TryGetShoreCellForFish(fish, out _);
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompFishTraits fish = thing.TryGetComp<CompFishTraits>();
            FishPondMapComponent ponds = pawn.Map.GetComponent<FishPondMapComponent>();
            if (fish == null || ponds == null || !ponds.TryGetShoreCellForFish(fish, out IntVec3 shore)) return null;
            return JobMaker.MakeJob(AquacultureJobDefOf.AF_HarvestPondFish, thing, shore);
        }
    }

    public sealed class JobDriver_HarvestPondFish : JobDriver
    {
        private CompFishTraits Fish => job.targetA.Thing?.TryGetComp<CompFishTraits>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            AddFinishAction(_ =>
            {
                CompFishTraits fish = Fish;
                if (fish != null) fish.harvestReserved = false;
            });

            Toil secureFish = ToilMaker.MakeToil("SecurePondFish");
            secureFish.initAction = () =>
            {
                CompFishTraits fish = Fish;
                if (fish != null) fish.harvestReserved = true;
            };
            secureFish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return secureFish;

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil catchFish = Toils_General.Wait(300, TargetIndex.A);
            catchFish.WithProgressBarToilDelay(TargetIndex.A);
            catchFish.tickAction = () => pawn.rotationTracker.FaceTarget(job.targetA);
            yield return catchFish;

            Toil killFish = ToilMaker.MakeToil("HarvestPondFish");
            killFish.initAction = () =>
            {
                CompFishTraits fish = Fish;
                FishPondMapComponent ponds = pawn.Map.GetComponent<FishPondMapComponent>();
                Designation designation = pawn.Map.designationManager.DesignationOn(job.targetA.Thing, AquacultureJobDefOf.AF_HarvestPondFishDesignation);
                if (fish == null || ponds?.CanHarvestFish(fish) != true)
                {
                    designation?.Delete();
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                fish.MarkDead();
                designation?.Delete();
            };
            killFish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return killFish;

            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, null, false);
        }
    }

    public sealed class WorkGiver_RemovePondEgg : WorkGiver_Scanner
    {
        private static ThingDef EggDef => DefDatabase<ThingDef>.GetNamedSilentFail("AF_FishEgg");
        public override ThingRequest PotentialWorkThingRequest => EggDef == null ? ThingRequest.ForUndefined() : ThingRequest.ForDef(EggDef);
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            return thing is FishEggThing
                && thing.Map.terrainGrid.TerrainAt(thing.Position).defName == "AF_Pond"
                && pawn.Map.designationManager.DesignationOn(thing, AquacultureJobDefOf.AF_RemovePondEggDesignation) != null
                && pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            return JobMaker.MakeJob(AquacultureJobDefOf.AF_RemovePondEgg, thing);
        }
    }

    public sealed class JobDriver_RemovePondEgg : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil remove = Toils_General.Wait(120, TargetIndex.A);
            remove.WithProgressBarToilDelay(TargetIndex.A);
            yield return remove;
            Toil finish = ToilMaker.MakeToil("RemovePondEgg");
            finish.initAction = () =>
            {
                Thing egg = job.targetA.Thing;
                Designation designation = pawn.Map.designationManager.DesignationOn(egg, AquacultureJobDefOf.AF_RemovePondEggDesignation);
                designation?.Delete();
                if (egg?.Destroyed == false) egg.Destroy(DestroyMode.Vanish);
                pawn.Map.GetComponent<FishPondMapComponent>()?.InvalidatePondMenus();
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    public sealed class WorkGiver_SterilizePondFish : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.GetComponent<FishPondMapComponent>()?.DesignatedSterilizationFish() ?? Enumerable.Empty<Thing>();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompFishTraits fish = thing?.TryGetComp<CompFishTraits>();
            return fish?.IsAlive == true
                && fish.IsInPond
                && !fish.sterilized
                && pawn.Map.designationManager.DesignationOn(thing, AquacultureJobDefOf.AF_SterilizePondFishDesignation) != null
                && pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced)
                && FindMedicine(pawn) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Thing medicine = FindMedicine(pawn);
            if (medicine == null) return null;
            Job job = JobMaker.MakeJob(AquacultureJobDefOf.AF_SterilizePondFish, thing, medicine);
            job.count = 1;
            return job;
        }

        private static Thing FindMedicine(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Deadly), 9999f,
                thing => thing.def.IsMedicine && !thing.IsForbidden(pawn) && pawn.CanReserve(thing));
        }
    }

    public sealed class JobDriver_SterilizePondFish : JobDriver
    {
        private CompFishTraits Fish => job.targetA.Thing?.TryGetComp<CompFishTraits>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(job.targetB, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            AddFinishAction(_ =>
            {
                CompFishTraits fish = Fish;
                if (fish != null) fish.harvestReserved = false;
            });
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);

            Toil secureFish = ToilMaker.MakeToil("SecureFishForSterilization");
            secureFish.initAction = () =>
            {
                CompFishTraits fish = Fish;
                if (fish != null) fish.harvestReserved = true;
            };
            secureFish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return secureFish;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil procedure = Toils_General.Wait(600, TargetIndex.A);
            procedure.WithProgressBarToilDelay(TargetIndex.A);
            procedure.tickAction = () => pawn.rotationTracker.FaceTarget(job.targetA);
            yield return procedure;

            Toil finish = ToilMaker.MakeToil("SterilizePondFish");
            finish.initAction = () =>
            {
                CompFishTraits fish = Fish;
                Designation designation = pawn.Map.designationManager.DesignationOn(job.targetA.Thing, AquacultureJobDefOf.AF_SterilizePondFishDesignation);
                if (fish?.IsAlive != true || fish.sterilized)
                {
                    designation?.Delete();
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                Thing medicine = pawn.carryTracker.CarriedThing;
                if (medicine == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                medicine.SplitOff(1).Destroy(DestroyMode.Vanish);
                fish.sterilized = true;
                fish.parent.Map?.GetComponent<FishPondMapComponent>()?.NotifyFishChanged(fish);
                designation?.Delete();
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    public sealed partial class FishPondMapComponent
    {
        public IEnumerable<Thing> DesignatedHarvestFish()
        {
            foreach (CompFishTraits candidate in fish)
            {
                if (candidate?.parent?.Spawned != true) continue;
                if (map.designationManager.DesignationOn(candidate.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) != null)
                    yield return candidate.parent;
            }
        }

        public IEnumerable<Thing> DesignatedSterilizationFish()
        {
            foreach (CompFishTraits candidate in fish)
            {
                if (candidate?.parent?.Spawned != true) continue;
                if (map.designationManager.DesignationOn(candidate.parent, AquacultureJobDefOf.AF_SterilizePondFishDesignation) != null)
                    yield return candidate.parent;
            }
        }
    }
}
