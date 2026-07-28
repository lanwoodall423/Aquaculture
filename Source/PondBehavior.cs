using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Swimming), MethodType.Getter)]
    public static class ConstructedPondSwimmingPatch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result || __instance?.Spawned != true) return;
            __result = __instance.Map.terrainGrid.TerrainAt(__instance.Position).defName == "AF_Pond";
        }
    }

    public static class PondProtectionUtility
    {
        public static bool IsProtected(Thing thing)
        {
            if (thing?.Spawned != true || (!(thing is FishEggThing) && !FishUtility.IsRuntimeFish(thing.def)) || thing.Map.terrainGrid.TerrainAt(thing.Position).defName != "AF_Pond") return false;
            CompFishTraits fish = thing.TryGetComp<CompFishTraits>();
            return fish?.IsAlive == true || thing is FishEggThing;
        }

        public static bool HasManagementDesignation(Thing thing)
        {
            if (thing?.Spawned != true) return false;
            DesignationManager manager = thing.Map.designationManager;
            return HasDesignation(manager, thing, "AF_HarvestPondFishDesignation")
                || HasDesignation(manager, thing, "AF_RemovePondEggDesignation")
                || HasDesignation(manager, thing, "AF_SterilizePondFishDesignation");
        }

        private static bool HasDesignation(DesignationManager manager, Thing thing, string defName)
        {
            DesignationDef def = DefDatabase<DesignationDef>.GetNamedSilentFail(defName);
            return def != null && manager.DesignationOn(thing, def) != null;
        }
    }

    public static class PondMovementUtility
    {
        internal sealed class PondInfo
        {
            public readonly List<IntVec3> cells = new List<IntVec3>();
            public readonly HashSet<IntVec3> cellSet = new HashSet<IntVec3>();
            public IntVec3 anchor;
            public Vector2 center;
            public int minX;
            public int maxX;
            public int minZ;
            public int maxZ;

            public bool Intersects(CellRect rect)
            {
                return maxX >= rect.minX && minX <= rect.maxX && maxZ >= rect.minZ && minZ <= rect.maxZ;
            }
        }

        private sealed class MapPonds
        {
            public readonly Dictionary<IntVec3, PondInfo> byCell = new Dictionary<IntVec3, PondInfo>();
            public readonly List<PondInfo> all = new List<PondInfo>();
            public bool fullyScanned;
        }

        private static readonly Dictionary<Map, MapPonds> PondsByMap = new Dictionary<Map, MapPonds>();
        private static readonly IntVec3[] Cardinal = GenAdj.CardinalDirections;

        public static void Reset(CompFishTraits fish)
        {
            if (fish == null) return;
            fish.schoolInitialized = false;
            fish.schoolVelocity = Vector2.zero;
            fish.schoolDrawRotation = 0f;
        }

        public static void Invalidate(Map map)
        {
            if (map != null) PondsByMap.Remove(map);
        }

        public static Vector3 DrawPosition(CompFishTraits fish, Vector3 logicalPosition)
        {
            if (fish?.parent?.Spawned != true || !fish.IsSwimmingInPond)
            {
                Reset(fish);
                return logicalPosition;
            }
            FishPondMapComponent component = fish.parent.Map.GetComponent<FishPondMapComponent>();
            return component?.SchoolDrawPosition(fish, logicalPosition) ?? logicalPosition;
        }

        internal static PondInfo InfoAt(Map map, IntVec3 start)
        {
            if (map == null || !IsPond(map, start)) return null;
            if (!PondsByMap.TryGetValue(map, out MapPonds mapPonds))
            {
                mapPonds = new MapPonds();
                PondsByMap[map] = mapPonds;
            }
            if (mapPonds.byCell.TryGetValue(start, out PondInfo cached)) return cached;

            PondInfo component = BuildComponent(map, start);
            foreach (IntVec3 cell in component.cells) mapPonds.byCell[cell] = component;
            mapPonds.all.Add(component);
            return component;
        }

        internal static IReadOnlyList<PondInfo> AllPonds(Map map)
        {
            if (map == null) return Array.Empty<PondInfo>();
            if (!PondsByMap.TryGetValue(map, out MapPonds mapPonds))
            {
                mapPonds = new MapPonds();
                PondsByMap[map] = mapPonds;
            }
            if (!mapPonds.fullyScanned)
            {
                mapPonds.fullyScanned = true;
                foreach (IntVec3 cell in map.AllCells)
                    if (IsPond(map, cell) && !mapPonds.byCell.ContainsKey(cell)) InfoAt(map, cell);
            }
            return mapPonds.all;
        }

        public static bool SamePond(Map map, IntVec3 first, IntVec3 second)
        {
            PondInfo a = InfoAt(map, first);
            return a != null && ReferenceEquals(a, InfoAt(map, second));
        }

        public static IReadOnlyList<IntVec3> ConnectedCells(Map map, IntVec3 cell)
        {
            return InfoAt(map, cell)?.cells ?? (IReadOnlyList<IntVec3>)Array.Empty<IntVec3>();
        }

        private static PondInfo BuildComponent(Map map, IntVec3 start)
        {
            var result = new PondInfo { anchor = start, minX = start.x, maxX = start.x, minZ = start.z, maxZ = start.z };
            var queue = new Queue<IntVec3>();
            long sumX = 0;
            long sumZ = 0;
            queue.Enqueue(start);
            result.cellSet.Add(start);
            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                result.cells.Add(cell);
                sumX += cell.x;
                sumZ += cell.z;
                if (cell.z < result.anchor.z || cell.z == result.anchor.z && cell.x < result.anchor.x) result.anchor = cell;
                result.minX = Math.Min(result.minX, cell.x);
                result.maxX = Math.Max(result.maxX, cell.x);
                result.minZ = Math.Min(result.minZ, cell.z);
                result.maxZ = Math.Max(result.maxZ, cell.z);
                foreach (IntVec3 direction in Cardinal)
                {
                    IntVec3 next = cell + direction;
                    if (next.InBounds(map) && IsPond(map, next) && result.cellSet.Add(next)) queue.Enqueue(next);
                }
            }
            result.center = new Vector2(sumX / (float)result.cells.Count + 0.5f, sumZ / (float)result.cells.Count + 0.5f);
            return result;
        }

        private static bool IsPond(Map map, IntVec3 cell) => map.terrainGrid.TerrainAt(cell).defName == "AF_Pond";
    }

    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new Type[] { typeof(Thing), typeof(Faction) })]
    public static class PondFishForbiddenFactionPatch
    {
        public static void Postfix(Thing __0, ref bool __result)
        {
            if (!PondProtectionUtility.IsProtected(__0)) return;
            __result = !PondProtectionUtility.HasManagementDesignation(__0);
        }
    }

    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new Type[] { typeof(Thing), typeof(Pawn) })]
    public static class PondFishForbiddenPawnPatch
    {
        public static void Postfix(Thing __0, ref bool __result)
        {
            if (!PondProtectionUtility.IsProtected(__0)) return;
            __result = !PondProtectionUtility.HasManagementDesignation(__0);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.IngestibleNow), MethodType.Getter)]
    public static class PondFishIngestiblePatch
    {
        public static void Postfix(Thing __instance, ref bool __result)
        {
            if (PondProtectionUtility.IsProtected(__instance)) __result = false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_HaulGeneral), nameof(WorkGiver_HaulGeneral.JobOnThing))]
    public static class PondFishHaulJobPatch
    {
        public static void Postfix(Thing t, ref Job __result)
        {
            if (PondProtectionUtility.IsProtected(t)) __result = null;
        }
    }

    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaulFast))]
    public static class PondFishAutomaticHaulPatch
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            if (PondProtectionUtility.IsProtected(t)) __result = false;
        }
    }
}
