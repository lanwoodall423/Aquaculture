using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    public sealed class CompProperties_FishContainer : CompProperties
    {
        public int capacity = 6;
        public float fishingAttractionRadius = 12f;

        public CompProperties_FishContainer()
        {
            compClass = typeof(CompFishContainer);
        }
    }

    public sealed class CompFishContainer : ThingComp, IThingHolder, ISearchableContents
    {
        private ThingOwner<Thing> innerContainer;
        private bool acceptFish = true;

        public CompProperties_FishContainer Props => (CompProperties_FishContainer)props;
        public int FishCount => innerContainer?.Count ?? 0;
        public int SpaceRemaining => Math.Max(0, Props.capacity - FishCount);
        public bool CanAcceptFish => acceptFish && SpaceRemaining > 0 && parent.Spawned;
        public ThingOwner SearchableContents => innerContainer;

        public override void Initialize(CompProperties properties)
        {
            base.Initialize(properties);
            innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            parent.Map.GetComponent<FishRoutingMapComponent>().Register(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            map?.GetComponent<FishRoutingMapComponent>()?.Deregister(this);
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (previousMap != null && innerContainer?.Count > 0)
                innerContainer.TryDropAll(parent.Position, previousMap, ThingPlaceMode.Near);
            base.PostDestroy(mode, previousMap);
        }

        public bool TryAccept(Thing fish)
        {
            if (!CanAcceptFish || fish == null || !FishUtility.IsFish(fish.def)) return false;
            CompFishTraits traits = fish.TryGetComp<CompFishTraits>();
            if (traits?.IsAlive != true || traits.IsInPond) return false;
            if (fish.Spawned) fish.DeSpawn();
            bool accepted = innerContainer.TryAdd(fish, false);
            if (accepted) traits.ResetAirExposure();
            return accepted;
        }

        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref acceptFish, "acceptFish", true);
            Scribe_Deep.Look(ref innerContainer, "fishContainer", this);
            if (innerContainer == null) innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public override string CompInspectStringExtra()
        {
            return $"Fish: {FishCount} / {Props.capacity}\nAccept fish: {(acceptFish ? "Yes" : "No")}";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            yield return new Command_Toggle
            {
                defaultLabel = "Accept fish",
                defaultDesc = "Automatically accept fish caught at nearby fishing zones. Fish kept inside remain alive.",
                icon = TexCommand.ForbidOff,
                isActive = () => acceptFish,
                toggleAction = () => acceptFish = !acceptFish
            };
            if (FishCount > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Empty fish",
                    defaultDesc = "Remove all stored fish and place them beside the container.",
                    icon = TexCommand.Install,
                    action = () => innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near)
                };
            }
        }
    }

    public sealed class FishRoutingMapComponent : MapComponent
    {
        private struct PendingCatch
        {
            public Thing fish;
            public CompFishContainer container;
        }

        private readonly HashSet<CompFishContainer> containers = new HashSet<CompFishContainer>();
        private readonly List<PendingCatch> pending = new List<PendingCatch>();

        public FishRoutingMapComponent(Map map) : base(map) { }

        public void Register(CompFishContainer container) => containers.Add(container);
        public void Deregister(CompFishContainer container) => containers.Remove(container);

        public CompFishContainer BestContainerFor(IEnumerable<IntVec3> fishingCells)
        {
            if (fishingCells == null) return null;
            List<IntVec3> cells = fishingCells as List<IntVec3> ?? fishingCells.ToList();
            if (cells.Count == 0) return null;
            CompFishContainer best = null;
            float bestDistance = float.MaxValue;
            foreach (CompFishContainer container in containers)
            {
                if (container?.CanAcceptFish != true) continue;
                float distance = cells.Min(cell => cell.DistanceToSquared(container.parent.Position));
                float radius = container.Props.fishingAttractionRadius;
                if (distance <= radius * radius && distance < bestDistance)
                {
                    best = container;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public void QueueCatch(Thing fish, CompFishContainer container)
        {
            if (fish != null && container != null && !pending.Any(entry => entry.fish == fish))
                pending.Add(new PendingCatch { fish = fish, container = container });
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (pending.Count == 0) return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingCatch entry = pending[i];
                pending.RemoveAt(i);
                if (entry.fish?.Spawned != true || entry.fish.Map != map || entry.container?.CanAcceptFish != true) continue;
                IntVec3 originalCell = entry.fish.Position;
                entry.fish.DeSpawn();
                if (!entry.container.TryAccept(entry.fish))
                    GenPlace.TryPlaceThing(entry.fish, originalCell, map, ThingPlaceMode.Near);
            }
        }
    }

    public static class FishContainerUtility
    {
        private static readonly string[] FishingJobNames = { "Fish", "VCEF_FishJob" };

        public static bool IsFishing(Pawn pawn)
        {
            string defName = pawn?.CurJobDef?.defName;
            return defName != null && FishingJobNames.Contains(defName);
        }

        public static CompFishContainer BestForZone(Map map, IEnumerable<IntVec3> cells)
        {
            return map?.GetComponent<FishRoutingMapComponent>()?.BestContainerFor(cells);
        }

        public static void PreferContainerCell(Pawn pawn, Zone zone, Job job, bool odyssey)
        {
            if (pawn == null || zone == null || job == null) return;
            CompFishContainer container = BestForZone(pawn.Map, zone.Cells);
            if (container == null) return;

            IntVec3 bestCell = IntVec3.Invalid;
            IntVec3 bestStand = IntVec3.Invalid;
            float bestDistance = float.MaxValue;
            foreach (IntVec3 cell in zone.Cells)
            {
                if (!cell.InBounds(pawn.Map) || cell.IsForbidden(pawn)) continue;
                bool canReserve = odyssey
                    ? pawn.CanReserveAndReach(cell, PathEndMode.Touch, Danger.Some, 1, -1, ReservationLayerDefOf.Floor)
                    : pawn.CanReserveAndReach(cell, PathEndMode.Touch, Danger.Some);
                if (!canReserve) continue;
                if (odyssey && zone is Zone_Fishing fishingZone && !fishingZone.IsFishable(cell)) continue;
                IntVec3 stand = odyssey ? WorkGiver_Fish.BestStandSpotFor(pawn, cell) : IntVec3.Invalid;
                if (odyssey && !stand.IsValid) continue;
                float distance = cell.DistanceToSquared(container.parent.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = cell;
                    bestStand = stand;
                }
            }
            if (!bestCell.IsValid) return;
            job.SetTarget(TargetIndex.A, bestCell);
            if (odyssey) job.SetTarget(TargetIndex.B, bestStand);
        }
    }

    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
    public static class RouteCaughtFishPatch
    {
        public static void Postfix(Thing __result, IntVec3 loc, Map map)
        {
            if (__result == null || !FishUtility.IsRuntimeFish(__result.def) || map == null) return;
            if (loc.InBounds(map) && map.terrainGrid.TerrainAt(loc).defName == "AF_Pond") return;
            Pawn fisher = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn => FishContainerUtility.IsFishing(pawn) && pawn.Position.DistanceToSquared(loc) <= 64f);
            if (fisher == null) return;
            AquacultureJournalComponent.Current?.NotifyCaught(__result.TryGetComp<CompFishTraits>(), fisher);
            Zone zone = fisher.CurJob.GetTarget(TargetIndex.A).Cell.GetZone(map);
            CompFishContainer container = FishContainerUtility.BestForZone(map, zone?.Cells ?? new List<IntVec3> { loc });
            map.GetComponent<FishRoutingMapComponent>().QueueCatch(__result, container);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Fish), nameof(WorkGiver_Fish.NonScanJob))]
    public static class OdysseyFishingContainerPreferencePatch
    {
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null) return;
            Zone zone = __result.GetTarget(TargetIndex.A).Cell.GetZone(pawn.Map);
            FishContainerUtility.PreferContainerCell(pawn, zone, __result, true);
        }
    }
}
