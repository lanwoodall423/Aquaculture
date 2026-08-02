using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class JoyGiver_WatchPond : JoyGiver_WatchBuilding
    {
        protected override bool CanInteractWith(Pawn pawn, Thing t, bool inBed)
        {
            return t is PondProxyThing && t.Map?.GetComponent<FishPondMapComponent>()?.PondHasLivingFish(t.Position) == true && base.CanInteractWith(pawn, t, inBed);
        }
    }

    public sealed class JobDriver_WatchPond : JobDriver_WatchBuilding
    {
        protected override void WatchTickAction(int delta)
        {
            base.WatchTickAction(delta);
            Thing pond = TargetThingA;
            if (pond?.Spawned == true) pond.Map.GetComponent<FishPondMapComponent>()?.NotifyPondWatcher(pawn, pond.Position);
        }
    }

    public sealed partial class FishPondMapComponent
    {
        private sealed class PondWatcherState
        {
            public PondState pond;
            public Vector2 target;
            public int expiresAt;
            public int nextObservationTick;
        }

        private readonly Dictionary<Pawn, PondWatcherState> pondWatchers = new Dictionary<Pawn, PondWatcherState>();
        private readonly List<Pawn> expiredWatchers = new List<Pawn>();
        private int nextWatcherPruneTick;

        public bool PondHasLivingFish(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) && pond.fish.Count > 0;
        }

        public void NotifyPondWatcher(Pawn pawn, IntVec3 pondCell)
        {
            if (pawn == null) return;
            EnsurePondState();
            if (!pondByCell.TryGetValue(pondCell, out PondState pond)) return;
            if (!pondWatchers.TryGetValue(pawn, out PondWatcherState state) || state.pond != pond)
            {
                state = new PondWatcherState { pond = pond };
                float best = float.MaxValue;
                for (int i = 0; i < pond.info.cells.Count; i++)
                {
                    IntVec3 cell = pond.info.cells[i];
                    float distance = cell.DistanceToSquared(pawn.Position);
                    if (distance >= best) continue;
                    best = distance;
                    state.target = new Vector2(cell.x + 0.5f, cell.z + 0.5f);
                }
                pondWatchers[pawn] = state;
            }
            int now = Find.TickManager?.TicksGame ?? 0;
            state.expiresAt = now + 120;
            if (now < state.nextObservationTick) return;
            state.nextObservationTick = now + 2500;
            foreach (IGrouping<ThingDef, CompFishTraits> group in pond.fish.Where(fish => fish?.IsAlive == true)
                .GroupBy(fish => fish.parent.def).Take(3))
            {
                CompFishTraits observed = group.FirstOrDefault();
                AquacultureEventRouter.FishObserved(observed, pawn, "watched_pond");
                AquacultureEventRouter.Ecology(AquacultureEventKind.PopulationSurveyed, map, pond.info.anchor,
                    group.Key, group.Count(), "Pond watch produced an uncertainty-bounded population estimate.");
            }
        }

        private bool TryGetCuriousTarget(PondState pond, out Vector2 target)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (KeyValuePair<Pawn, PondWatcherState> pair in pondWatchers)
            {
                if (pair.Value.pond == pond && pair.Value.expiresAt >= now && pair.Key?.Spawned == true)
                {
                    target = pair.Value.target;
                    return true;
                }
            }
            target = default;
            return false;
        }

        private void PrunePondWatchers(int now)
        {
            if (now < nextWatcherPruneTick) return;
            nextWatcherPruneTick = now + 250;
            expiredWatchers.Clear();
            foreach (KeyValuePair<Pawn, PondWatcherState> pair in pondWatchers)
                if (pair.Key?.Spawned != true || pair.Value.expiresAt < now) expiredWatchers.Add(pair.Key);
            for (int i = 0; i < expiredWatchers.Count; i++) pondWatchers.Remove(expiredWatchers[i]);
        }
    }
}
