using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public static class AquacultureProgression
    {
        public static bool IsAvailable(string researchDefName)
        {
            if (AquacultureMod.Settings?.enableResearchProgression == false) return true;
            ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(researchDefName);
            return research != null && research.IsFinished;
        }
    }

    [HarmonyPatch(typeof(ResearchProjectDef), nameof(ResearchProjectDef.IsFinished), MethodType.Getter)]
    public static class AquacultureResearchBypassPatch
    {
        public static bool Prefix(ResearchProjectDef __instance, ref bool __result)
        {
            if (AquacultureMod.Settings?.enableResearchProgression != false || __instance?.defName?.StartsWith("AF_", StringComparison.Ordinal) != true) return true;
            __result = true;
            return false;
        }
    }

    public sealed class CompProperties_PondFeeder : CompProperties
    {
        public bool automaticCapable;
        public CompProperties_PondFeeder() { compClass = typeof(CompPondFeeder); }
    }

    public sealed class CompPondFeeder : ThingComp
    {
        private bool enabled = true;

        private CompProperties_PondFeeder Props => (CompProperties_PondFeeder)props;
        public bool CanAutomaticallyDispense
        {
            get
            {
                CompPowerTrader power = parent?.TryGetComp<CompPowerTrader>();
                return Props.automaticCapable && enabled && parent?.Spawned == true
                    && parent.TryGetComp<CompRefuelable>()?.HasFuel == true
                    && (power == null || power.PowerOn);
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref enabled, "automaticPondFeeding", true);
        }

        public float TakeFeed(float maximumUnits)
        {
            CompRefuelable fuel = parent.TryGetComp<CompRefuelable>();
            float units = Mathf.Min(Mathf.Floor(Mathf.Max(0f, maximumUnits)), Mathf.Floor(fuel?.Fuel ?? 0f));
            if (units <= 0f) return 0f;
            fuel.ConsumeFuel(units);
            return units;
        }

        public override string CompInspectStringExtra()
        {
            if (!Props.automaticCapable) return "Pond feeding: Manual";
            CompPowerTrader power = parent?.TryGetComp<CompPowerTrader>();
            return "Automatic pond feeding: " + (enabled ? "On" : "Off") +
                (power?.PowerOn == false ? "\nStatus: No power" : "");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            var manual = new Command_Action
            {
                defaultLabel = "Dispense Feed",
                defaultDesc = "Dispense one unit of fish feed into the nearest pond within range.",
                icon = TexCommand.DesirePower,
                action = DispenseManually
            };
            if (parent.TryGetComp<CompRefuelable>()?.HasFuel != true) manual.Disable("No fish feed loaded.");
            yield return manual;
            if (!Props.automaticCapable) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Automatic Feeding: " + (enabled ? "On" : "Off"),
                defaultDesc = "Allow connected pond ecology updates to draw fish feed from this feeder.",
                icon = TexCommand.DesirePower,
                action = () => enabled = !enabled
            };
        }

        private void DispenseManually()
        {
            FishPondMapComponent ponds = parent.Map?.GetComponent<FishPondMapComponent>();
            float range = AquacultureMod.Settings?.feederRange ?? 6f;
            if (ponds == null || !ponds.TryFindPondInRange(parent.Position, range, out IntVec3 pondCell))
            {
                Messages.Message("No pond is within feeder range.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            float units = TakeFeed(1f);
            if (units <= 0f) return;
            ponds.AddPreparedFeed(pondCell, units * Mathf.Max(0.005f, AquacultureMod.Settings?.feedValuePerUnit ?? 0.05f));
            Messages.Message("Dispensed fish feed.", MessageTypeDefOf.TaskCompletion, false);
        }
    }

    public sealed partial class FishPondMapComponent
    {
        public PondBreedingMode BreedingModeAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.breedingMode : PondBreedingMode.Natural;
        }

        public void SetBreedingMode(IntVec3 cell, PondBreedingMode mode)
        {
            if (!AquacultureProgression.IsAvailable("AF_SelectiveBreeding")) return;
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.breedingMode = mode;
            pond.ecology.breedingEnabled = mode != PondBreedingMode.Paused;
            if (mode == PondBreedingMode.Paused) RemovePondEggs(pond);
            for (int i = 0; i < pond.schools.Count; i++) pond.schools[i].nextBreedingCheckTick = 0;
            InvalidatePondSnapshot(pond);
        }

        private void RemovePondEggs(PondState pond)
        {
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_FishEgg");
            if (eggDef == null) return;
            List<Thing> eggs = map.listerThings.ThingsOfDef(eggDef);
            int designated = 0;
            for (int i = 0; i < eggs.Count; i++)
            {
                Thing egg = eggs[i];
                if (!pond.info.cellSet.Contains(egg.Position)) continue;
                if (map.designationManager.DesignationOn(egg, AquacultureJobDefOf.AF_RemovePondEggDesignation) != null) continue;
                map.designationManager.AddDesignation(new Designation(egg, AquacultureJobDefOf.AF_RemovePondEggDesignation));
                designated++;
            }
            InvalidatePondSnapshot(pond);
            if (designated > 0) Messages.Message("Marked " + designated + " pond egg" + (designated == 1 ? "" : "s") + " for removal by a handler.", MessageTypeDefOf.TaskCompletion, false);
        }

        public int MinimumHarvestPopulationAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.minimumHarvestPopulation : 2;
        }

        public void SetMinimumHarvestPopulation(IntVec3 cell, int value)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.minimumHarvestPopulation = Mathf.Clamp(value, 0, 200);
            InvalidatePondSnapshot(pond);
        }

        public bool HarvestAdultsOnlyAt(IntVec3 cell)
        {
            EnsurePondState();
            return !pondByCell.TryGetValue(cell, out PondState pond) || pond.ecology.harvestAdultsOnly;
        }

        public void SetHarvestAdultsOnly(IntVec3 cell, bool value)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.harvestAdultsOnly = value;
            InvalidatePondSnapshot(pond);
        }

        public bool ProtectBreedingFemalesAt(IntVec3 cell)
        {
            EnsurePondState();
            return !pondByCell.TryGetValue(cell, out PondState pond) || pond.ecology.protectBreedingFemales;
        }

        public void SetProtectBreedingFemales(IntVec3 cell, bool value)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.protectBreedingFemales = value;
            InvalidatePondSnapshot(pond);
        }

        public bool AutomaticFeedingAt(IntVec3 cell)
        {
            EnsurePondState();
            return !pondByCell.TryGetValue(cell, out PondState pond) || pond.ecology.automaticFeeding;
        }

        public void SetAutomaticFeeding(IntVec3 cell, bool enabled)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.automaticFeeding = enabled;
            InvalidatePondSnapshot(pond);
        }

        public float TargetFeedDaysAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.targetFeedDays : 1f;
        }

        public void SetTargetFeedDays(IntVec3 cell, float days)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.targetFeedDays = Mathf.Clamp(days, 0.25f, 5f);
            InvalidatePondSnapshot(pond);
        }

        public bool PredationEnabledAt(IntVec3 cell)
        {
            EnsurePondState();
            return !pondByCell.TryGetValue(cell, out PondState pond) || pond.ecology.predationEnabled;
        }

        public void SetPredationEnabled(IntVec3 cell, bool enabled)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.predationEnabled = enabled;
            InvalidatePondSnapshot(pond);
        }

        public int PopulationLimitAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.populationLimit : 0;
        }

        public void SetPopulationLimit(IntVec3 cell, int limit)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.populationLimit = Mathf.Max(0, limit);
            InvalidatePondSnapshot(pond);
        }

        public int ManagementPopulationTargetAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.managementPopulationTarget : 0;
        }

        public void SetManagementPopulationTarget(IntVec3 cell, int target)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.managementPopulationTarget = Mathf.Clamp(target, 0, 200);
            InvalidatePondSnapshot(pond);
        }

        public bool AutomaticSurplusHarvestAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) && pond.ecology.automaticSurplusHarvest;
        }

        public void SetAutomaticSurplusHarvest(IntVec3 cell, bool enabled)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            if (enabled && pond.ecology.managementPopulationTarget <= 0)
            {
                Messages.Message("Set a population goal before enabling surplus harvest.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            pond.ecology.automaticSurplusHarvest = enabled;
            if (enabled) ApplyAutomaticHarvestPolicy(pond);
            InvalidatePondSnapshot(pond);
        }

        public Dictionary<ThingDef, int> StockingBlueprintAt(IntVec3 cell)
        {
            EnsurePondState();
            var result = new Dictionary<ThingDef, int>();
            if (!pondByCell.TryGetValue(cell, out PondState pond) || pond.ecology.stockingTargets == null) return result;
            for (int i = 0; i < pond.ecology.stockingTargets.Count; i++)
            {
                PondStockingTarget target = pond.ecology.stockingTargets[i];
                ThingDef species = target?.Species;
                if (species != null && target.targetCount > 0) result[species] = target.targetCount;
            }
            return result;
        }

        public PondWaterKind StockingBlueprintWaterAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.blueprintWater : PondWaterKind.Freshwater;
        }

        public void SetStockingBlueprint(IntVec3 cell, IDictionary<ThingDef, int> targets, PondWaterKind water)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.stockingTargets = (targets ?? new Dictionary<ThingDef, int>())
                .Where(pair => pair.Key != null && FishUtility.IsFish(pair.Key) && pair.Value > 0)
                .OrderBy(pair => pair.Key.defName)
                .Select(pair => new PondStockingTarget(pair.Key, Mathf.Clamp(pair.Value, 1, 999)))
                .ToList();
            pond.ecology.blueprintWater = water;
            int total = pond.ecology.stockingTargets.Sum(target => target.targetCount);
            pond.ecology.managementPopulationTarget = total;
            if (pond.ecology.automaticSurplusHarvest) ApplyAutomaticHarvestPolicy(pond);
            InvalidatePondSnapshot(pond);
        }

        public void ClearStockingBlueprint(IntVec3 cell)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            pond.ecology.stockingTargets.Clear();
            InvalidatePondSnapshot(pond);
        }

        public int PopulationAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.fish.Count : 0;
        }

        public float PreparedFeedAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.preparedFeed : 0f;
        }

        public string AutomaticFeederStatusAt(IntVec3 cell)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return "No connected pond.";
            ThingDef feederDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_AutomaticPondFeeder");
            if (feederDef == null) return "Automatic feeder definition unavailable.";
            float range = AquacultureMod.Settings?.feederRange ?? 6f;
            int feeders = 0;
            int powered = 0;
            float feedUnits = 0f;
            List<Thing> things = map.listerThings.ThingsOfDef(feederDef);
            for (int i = 0; i < things.Count; i++)
            {
                if (!WithinPondRange(things[i].Position, pond.info, range)) continue;
                feeders++;
                feedUnits += things[i].TryGetComp<CompRefuelable>()?.Fuel ?? 0f;
                if (things[i].TryGetComp<CompPowerTrader>()?.PowerOn != false) powered++;
            }
            return feeders == 0
                ? "No automatic feeder is in range."
                : feeders + " feeder" + (feeders == 1 ? "" : "s") + " in range; " + powered + " powered; " +
                    Mathf.FloorToInt(feedUnits) + " fish-feed units loaded.";
        }

        public bool CanChangeWaterAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) && pond.fish.Count == 0 && PondEggCount(pond) == 0;
        }

        public List<ThingDef> FishSpeciesAt(IntVec3 cell)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return new List<ThingDef>();
            return pond.fish.Select(fish => fish.parent.def).Distinct().OrderBy(def => def.label).ToList();
        }

        public List<CompFishTraits> FishInSamePond(CompFishTraits fish)
        {
            EnsurePondState();
            if (fish != null && pondByFish.TryGetValue(fish, out PondState pond)) return pond.fish.ToList();
            return new List<CompFishTraits>();
        }

        public List<ThingDef> HarvestableFishSpeciesAt(IntVec3 cell)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)
                || pond.fish.Count <= pond.ecology.minimumHarvestPopulation) return new List<ThingDef>();
            return pond.fish
                .Where(fish => IsEligibleForHarvest(pond, fish))
                .Select(fish => fish.parent.def)
                .Distinct()
                .OrderBy(def => def.label)
                .ToList();
        }

        public List<CompFishTraits> HarvestableFishAt(IntVec3 cell, ThingDef species = null)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond) || pond.fish.Count <= pond.ecology.minimumHarvestPopulation)
                return new List<CompFishTraits>();
            return pond.fish
                .Where(fish => IsEligibleForHarvest(pond, fish)
                    && !fish.harvestReserved
                    && (species == null || fish.parent.def == species)
                    && map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) == null)
                .OrderBy(fish => fish.parent.def.label)
                .ThenByDescending(fish => Find.TickManager.TicksGame - fish.birthTick)
                .ToList();
        }

        public bool CanHarvestFish(CompFishTraits fish)
        {
            EnsurePondState();
            return fish != null
                && pondByFish.TryGetValue(fish, out PondState pond)
                && pond.fish.Count > pond.ecology.minimumHarvestPopulation
                && IsEligibleForHarvest(pond, fish);
        }

        private static bool IsEligibleForHarvest(PondState pond, CompFishTraits fish)
        {
            return fish?.IsAlive == true
                && (!pond.ecology.harvestAdultsOnly || fish.IsHarvestMature)
                && (!pond.ecology.protectBreedingFemales || !fish.IsAdult || !fish.IsFemale);
        }

        private int PondEggCount(PondState pond)
        {
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_FishEgg");
            if (eggDef == null) return 0;
            int count = 0;
            List<Thing> eggs = map.listerThings.ThingsOfDef(eggDef);
            for (int i = 0; i < eggs.Count; i++) if (pond.info.cellSet.Contains(eggs[i].Position)) count++;
            return count;
        }

        private void ApplyAutomaticHarvestPolicy(PondState pond)
        {
            if (pond?.ecology?.automaticSurplusHarvest != true || pond.ecology.managementPopulationTarget <= 0) return;
            Dictionary<ThingDef, int> blueprint = pond.ecology.stockingTargets?
                .Where(target => target?.Species != null && target.targetCount > 0)
                .GroupBy(target => target.Species)
                .ToDictionary(group => group.Key, group => group.Last().targetCount);
            if (blueprint?.Count > 0)
            {
                int removable = pond.fish.Count - pond.ecology.minimumHarvestPopulation;
                if (removable <= 0) return;
                var blueprintCandidates = new List<CompFishTraits>();
                foreach (IGrouping<ThingDef, CompFishTraits> group in pond.fish.GroupBy(fish => fish.parent.def))
                {
                    int target = blueprint.TryGetValue(group.Key, out int desired) ? desired : 0;
                    int designated = group.Count(fish =>
                        map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) != null);
                    int speciesSurplus = Mathf.Max(0, group.Count() - target - designated);
                    blueprintCandidates.AddRange(group
                        .Where(fish => IsEligibleForHarvest(pond, fish)
                            && !fish.harvestReserved
                            && map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) == null)
                        .OrderByDescending(fish => Find.TickManager.TicksGame - fish.birthTick)
                        .Take(speciesSurplus));
                }
                foreach (CompFishTraits candidate in blueprintCandidates
                    .OrderByDescending(fish => Find.TickManager.TicksGame - fish.birthTick)
                    .Take(removable))
                    FishHarvestUtility.Designate(candidate, false);
                return;
            }
            int surplus = pond.fish.Count - Mathf.Max(pond.ecology.managementPopulationTarget, pond.ecology.minimumHarvestPopulation);
            if (surplus <= 0) return;
            List<CompFishTraits> candidates = pond.fish
                .Where(fish => IsEligibleForHarvest(pond, fish)
                    && !fish.harvestReserved
                    && map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) == null)
                .OrderByDescending(fish => Find.TickManager.TicksGame - fish.birthTick)
                .Take(surplus)
                .ToList();
            for (int i = 0; i < candidates.Count; i++) FishHarvestUtility.Designate(candidates[i], false);
        }

        public bool TryGetShoreCellForFish(CompFishTraits fish, out IntVec3 shore)
        {
            EnsurePondState();
            if (fish != null && pondByFish.TryGetValue(fish, out PondState pond)) return TryFindShoreCell(pond, out shore);
            shore = IntVec3.Invalid;
            return false;
        }

        public bool TryFindPondInRange(IntVec3 origin, float range, out IntVec3 pondCell)
        {
            EnsurePondState();
            PondState closest = null;
            float closestDistance = range * range + 0.001f;
            for (int i = 0; i < pondStates.Count; i++)
            {
                PondState pond = pondStates[i];
                if (!WithinPondRange(origin, pond.info, range)) continue;
                float distance = (new Vector2(origin.x + 0.5f, origin.z + 0.5f) - pond.info.center).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = pond;
            }
            pondCell = closest?.info.anchor ?? IntVec3.Invalid;
            return closest != null;
        }

        public void TryRemoveFish(IntVec3 cell, ThingDef species, bool harvest)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            if (harvest && pond.fish.Count <= pond.ecology.minimumHarvestPopulation)
            {
                Messages.Message("The pond has reached its protected population.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            CompFishTraits fish = pond.fish.FirstOrDefault(candidate => candidate.parent.def == species
                && candidate.IsAlive
                && (!harvest || !pond.ecology.harvestAdultsOnly || candidate.IsHarvestMature));
            if (fish == null || !TryFindShoreCell(pond, out IntVec3 shore))
            {
                Messages.Message("No suitable pond shore cell is available.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Thing thing = fish.parent;
            if (harvest) fish.MarkDead();
            thing.DeSpawn();
            GenSpawn.Spawn(thing, shore, map);
            pondMembershipDirty = true;
            InvalidatePondSnapshot(pond);
            Messages.Message((harvest ? "Harvested " : "Removed ") + species.LabelCap + ".", MessageTypeDefOf.TaskCompletion, false);
        }

        private bool TryFindShoreCell(PondState pond, out IntVec3 shore)
        {
            for (int i = 0; i < pond.info.cells.Count; i++)
            {
                IntVec3 pondCell = pond.info.cells[i];
                for (int direction = 0; direction < GenAdj.CardinalDirections.Length; direction++)
                {
                    IntVec3 candidate = pondCell + GenAdj.CardinalDirections[direction];
                    if (!candidate.InBounds(map) || pond.info.cellSet.Contains(candidate) || !candidate.Standable(map)) continue;
                    shore = candidate;
                    return true;
                }
            }
            shore = IntVec3.Invalid;
            return false;
        }

        private void TransferAutomaticFeed(PondState pond)
        {
            if (pond?.ecology?.automaticFeeding != true || !AquacultureProgression.IsAvailable("AF_IndustrialAquaculture")) return;
            ThingDef feederDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_AutomaticPondFeeder");
            if (feederDef == null) return;
            float target = 0f;
            float demandMultiplier = AquacultureMod.Settings?.foodDemandMultiplier ?? 1f;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                target += AquaticSpeciesProfile.For(fish.parent.def).hourlyDemand * Mathf.Max(0.2f, fish.SizeFactor)
                    * demandMultiplier * 24f * Mathf.Clamp(pond.ecology.targetFeedDays, 0.25f, 5f);
            }
            float feedValue = Mathf.Max(0.005f, AquacultureMod.Settings?.feedValuePerUnit ?? 0.05f);
            float missing = Mathf.Max(0f, target - pond.ecology.preparedFeed);
            if (missing <= 0f) return;
            float range = AquacultureMod.Settings?.feederRange ?? 6f;
            List<Thing> feeders = map.listerThings.ThingsOfDef(feederDef);
            for (int i = 0; i < feeders.Count && missing > 0f; i++)
            {
                Thing feeder = feeders[i];
                if (!WithinPondRange(feeder.Position, pond.info, range)) continue;
                CompPondFeeder comp = feeder.TryGetComp<CompPondFeeder>();
                if (comp?.CanAutomaticallyDispense != true) continue;
                float units = comp.TakeFeed(Mathf.Ceil(missing / feedValue));
                pond.ecology.preparedFeed += units * feedValue;
                missing = Mathf.Max(0f, target - pond.ecology.preparedFeed);
            }
        }

        private static bool WithinPondRange(IntVec3 cell, PondMovementUtility.PondInfo pond, float range)
        {
            int dx = cell.x < pond.minX ? pond.minX - cell.x : cell.x > pond.maxX ? cell.x - pond.maxX : 0;
            int dz = cell.z < pond.minZ ? pond.minZ - cell.z : cell.z > pond.maxZ ? cell.z - pond.maxZ : 0;
            return dx * dx + dz * dz <= range * range;
        }

        private void PopulateOverview(PondState pond, PondMenuSnapshot snapshot)
        {
            PondHabitatSnapshot habitat = EnsureHabitat(pond);
            int capacity = habitat.effectiveCapacity;
            float algaeCapacity = Mathf.Max(0.1f, pond.info.cells.Count * 0.25f);
            PondCausalSummary causal = PondCausalSummaryBuilder.Build(pond.ecology, habitat, pond.fish, map,
                pond.fish.Count, Find.TickManager?.TicksGame ?? 0);
            int wrongWater = causal.wrongWater;
            int hungry = causal.hungry;
            int starving = causal.starving;
            int temperatureStressed = causal.temperatureStressed;
            float hourlyDemand = causal.hourlyDemand;
            int eggs = 0;
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_FishEgg");
            if (eggDef != null)
            {
                List<Thing> allEggs = map.listerThings.ThingsOfDef(eggDef);
                for (int i = 0; i < allEggs.Count; i++) if (pond.info.cellSet.Contains(allEggs[i].Position)) eggs++;
            }
            int pendingHarvest = 0;
            int pendingSterilization = 0;
            int eligibleHarvest = 0;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                if (IsEligibleForHarvest(pond, fish)) eligibleHarvest++;
                if (map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) != null) pendingHarvest++;
                if (map.designationManager.DesignationOn(fish.parent, AquacultureJobDefOf.AF_SterilizePondFishDesignation) != null) pendingSterilization++;
            }
            int pendingEggRemoval = 0;
            if (eggDef != null)
            {
                List<Thing> allEggs = map.listerThings.ThingsOfDef(eggDef);
                for (int i = 0; i < allEggs.Count; i++)
                    if (pond.info.cellSet.Contains(allEggs[i].Position)
                        && map.designationManager.DesignationOn(allEggs[i], AquacultureJobDefOf.AF_RemovePondEggDesignation) != null)
                        pendingEggRemoval++;
            }
            snapshot.population = pond.fish.Count;
            snapshot.capacity = capacity;
            snapshot.physicalCapacity = habitat.physicalMaximum;
            snapshot.sustainableCapacity = habitat.sustainablePopulation;
            snapshot.industrialCapacity = habitat.industrialMaximum;
            snapshot.foodSupportedCapacity = habitat.foodSupportedPopulation;
            snapshot.habitatSupportedCapacity = habitat.habitatSupportedPopulation;
            snapshot.capacityConstraint = habitat.limitingConstraint;
            snapshot.eggs = eggs;
            snapshot.eligibleHarvest = eligibleHarvest;
            snapshot.pendingHarvest = pendingHarvest;
            snapshot.pendingEggRemoval = pendingEggRemoval;
            snapshot.pendingSterilization = pendingSterilization;
            snapshot.hungry = hungry;
            snapshot.starving = starving;
            snapshot.wrongWater = wrongWater;
            snapshot.temperatureStressed = temperatureStressed;
            snapshot.algaePercent = pond.ecology.algae / algaeCapacity;
            snapshot.detritusPercent = pond.ecology.detritus / algaeCapacity;
            snapshot.feedHours = causal.feedHours;
            snapshot.temperature = causal.temperature;
            snapshot.feederStatus = AutomaticFeederStatusAt(pond.info.anchor);
            snapshot.habitat = habitat;
            snapshot.causalSummary = causal;
            if (pond.ecology.organisms != null)
                for (int i = 0; i < pond.ecology.organisms.Count; i++)
                {
                    PondOrganismPopulation population = pond.ecology.organisms[i];
                    PondOrganismDef organism = population.Organism;
                    if (organism == null) continue;
                    float organismCapacity = Mathf.Max(0.05f, pond.info.cells.Count * organism.capacityPerCell);
                    snapshot.organisms.Add(new PondOrganismSnapshot
                    {
                        label = organism.LabelCap,
                        role = organism.role.ToString(),
                        biomass = population.biomass,
                        capacity = organismCapacity,
                        percent = Mathf.Clamp01(population.biomass / organismCapacity)
                    });
                }
            snapshot.organisms.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
            PopulateBlueprint(pond, snapshot, wrongWater, temperatureStressed, starving);
            snapshot.overview =
                "AquacultureFishing.PondArea".Translate(pond.info.cells.Count).ToString() + "\n" +
                "AquacultureFishing.PondPopulation".Translate(pond.fish.Count).ToString() + "\n" +
                "AquacultureFishing.PondPhysicalSpace".Translate(habitat.physicalMaximum).ToString() + "\n" +
                "AquacultureFishing.PondSustainableCapacity".Translate(habitat.sustainablePopulation).ToString() + "\n" +
                "AquacultureFishing.PondIndustrialCapacity".Translate(habitat.industrialMaximum).ToString() + "\n" +
                "AquacultureFishing.PondEffectiveCapacity".Translate(capacity).ToString() + "\n" +
                (habitat.hasManagementLimit ? "AquacultureFishing.PondManagementCapacity".Translate(pond.ecology.populationLimit).ToString() + "\n" : "") +
                "AquacultureFishing.PondLimitingFactor".Translate(PondCapacityRules.ConstraintLabel(habitat.limitingConstraint)).ToString() + "\n" +
                "AquacultureFishing.PondEggs".Translate(eggs).ToString() + "\n" +
                "AquacultureFishing.PondWaterTemperature".Translate(WaterLabel(pond.ecology.waterKind), causal.temperature.ToString("0.#")).ToString() + "\n" +
                "AquacultureFishing.PondAlgae".Translate((pond.ecology.algae / algaeCapacity).ToStringPercent()).ToString() + "\n" +
                "AquacultureFishing.PondDetritus".Translate((pond.ecology.detritus / algaeCapacity).ToStringPercent()).ToString() + "\n" +
                "AquacultureFishing.PondPreparedFeed".Translate(pond.ecology.preparedFeed,
                    hourlyDemand > 0f ? causal.feedHours : 0f).ToString() + "\n" +
                "AquacultureFishing.PondHarvestReserve".Translate(pond.ecology.minimumHarvestPopulation,
                    (pond.ecology.protectBreedingFemales ? "AquacultureFishing.PondYes" : "AquacultureFishing.PondNo").Translate()).ToString() + "\n" +
                "AquacultureFishing.PondManagementTarget".Translate(pond.ecology.managementPopulationTarget).ToString() + "\n" +
                "AquacultureFishing.PondBreedingMode".Translate(pond.ecology.breedingMode.ToString()).ToString() + "\n" +
                "AquacultureFishing.PondAutomaticFeeding".Translate((pond.ecology.automaticFeeding ? "AquacultureFishing.PondYes" : "AquacultureFishing.PondNo").Translate(),
                    pond.ecology.targetFeedDays).ToString() + "\n" +
                "AquacultureFishing.PondPredation".Translate(AquacultureMod.Settings?.predationEnabled == false
                    ? "AquacultureFishing.PondPredationGlobalDisabled".Translate().ToString()
                    : pond.ecology.predationEnabled ? "AquacultureFishing.PondPredationNatural".Translate().ToString()
                    : "AquacultureFishing.PondPredationPondDisabled".Translate().ToString()).ToString() + "\n" +
                "AquacultureFishing.PondHabitatPath".Translate(snapshot.habitat.developmentPath,
                    snapshot.habitat.averageFishFit.ToStringPercent()).ToString() + "\n" +
                "AquacultureFishing.PondOrganisms".Translate(snapshot.organisms.Count).ToString();
            if (snapshot.blueprintTarget > 0)
                snapshot.overview += "\n" + "AquacultureFishing.PondBlueprintStatus".Translate(snapshot.blueprintStatus,
                    snapshot.blueprintFit.ToStringPercent(), snapshot.blueprintDeficit, snapshot.blueprintSurplus).ToString();

            if (pond.fish.Count > capacity && habitat.hasManagementLimit &&
                habitat.managementLimit < habitat.industrialMaximum && pond.fish.Count > habitat.managementLimit)
                snapshot.warnings.Add("AquacultureFishing.PondOverManagementCapacity".Translate(habitat.managementLimit).ToString());
            if (pond.fish.Count > habitat.industrialMaximum)
                snapshot.warnings.Add("AquacultureFishing.PondOverIndustrialCapacity".Translate(pond.fish.Count - habitat.industrialMaximum).ToString());
            else if (pond.fish.Count > habitat.sustainablePopulation)
                snapshot.warnings.Add("AquacultureFishing.PondEcologicallyUnsupported".Translate(
                    PondCapacityRules.ConstraintLabel(habitat.limitingConstraint)).ToString());
            if (wrongWater > 0) snapshot.warnings.Add("AquacultureFishing.PondCausalWaterRisk".Translate(wrongWater).ToString());
            if (temperatureStressed > 0) snapshot.warnings.Add("AquacultureFishing.PondCausalTemperatureRisk".Translate(temperatureStressed).ToString());
            if (starving > 0) snapshot.warnings.Add("AquacultureFishing.PondCausalStarvation".Translate(starving).ToString());
            else if (hungry > 0) snapshot.warnings.Add("AquacultureFishing.PondCausalHunger".Translate(hungry).ToString());
            if (pendingHarvest + pendingEggRemoval + pendingSterilization > 0)
                    snapshot.warnings.Add("AquacultureFishing.PondPendingLabor".Translate(
                        pendingHarvest + pendingEggRemoval + pendingSterilization).ToString());
            if (pond.fish.Count > 0 && pond.ecology.automaticFeeding && snapshot.feederStatus.StartsWith("No automatic", StringComparison.Ordinal))
                snapshot.warnings.Add("AquacultureFishing.PondMissingAutomaticFeeder".Translate().ToString());
            if (pendingSterilization > 0 && !map.listerThings.AllThings.Any(thing => thing.def.IsMedicine))
                snapshot.warnings.Add("AquacultureFishing.PondMissingSterilizationMedicine".Translate().ToString());
            if (snapshot.habitat.stressedFish > 0)
                snapshot.warnings.Add("AquacultureFishing.PondCausalHabitatDeficit".Translate(snapshot.habitat.stressedFish).ToString());
        }

        private static void PopulateBlueprint(PondState pond, PondMenuSnapshot snapshot, int wrongWater, int temperatureStressed, int starving)
        {
            List<PondStockingTarget> targets = pond.ecology.stockingTargets?
                .Where(target => target?.Species != null && target.targetCount > 0).ToList();
            if (targets == null || targets.Count == 0)
            {
                snapshot.blueprintStatus = "AquacultureFishing.PondNoBlueprint".Translate().ToString();
                return;
            }
            Dictionary<ThingDef, int> actual = pond.fish.GroupBy(fish => fish.parent.def)
                .ToDictionary(group => group.Key, group => group.Count());
            int targetTotal = targets.Sum(target => target.targetCount);
            int matched = 0;
            int deficit = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                PondStockingTarget target = targets[i];
                int count = actual.TryGetValue(target.Species, out int value) ? value : 0;
                matched += Mathf.Min(count, target.targetCount);
                deficit += Mathf.Max(0, target.targetCount - count);
            }
            int surplus = actual.Sum(pair =>
            {
                PondStockingTarget target = targets.FirstOrDefault(value => value.Species == pair.Key);
                return Mathf.Max(0, pair.Value - (target?.targetCount ?? 0));
            });
            float stockFit = matched / (float)Mathf.Max(1, Mathf.Max(targetTotal, pond.fish.Count));
            float healthPenalty = (wrongWater + temperatureStressed + starving) / (float)Mathf.Max(1, pond.fish.Count);
            float waterFactor = pond.ecology.waterKind == pond.ecology.blueprintWater ? 1f : 0.75f;
            snapshot.blueprintTarget = targetTotal;
            snapshot.blueprintDeficit = deficit;
            snapshot.blueprintSurplus = surplus;
            snapshot.blueprintFit = Mathf.Clamp01(stockFit * waterFactor - healthPenalty * 0.25f);
            snapshot.blueprintStatus = snapshot.blueprintFit >= 0.999f && deficit == 0 && surplus == 0
                ? "AquacultureFishing.PondBlueprintBalanced".Translate().ToString()
                : wrongWater + temperatureStressed + starving > 0 || pond.ecology.waterKind != pond.ecology.blueprintWater
                    ? "AquacultureFishing.PondBlueprintAtRisk".Translate().ToString()
                    : "AquacultureFishing.PondBlueprintEstablishing".Translate().ToString();
        }

        public List<Thing> CriticalPondProxies()
        {
            EnsurePondState();
            var result = new List<Thing>();
            for (int i = 0; i < pondStates.Count; i++)
            {
                PondState pond = pondStates[i];
                PondMenuSnapshot snapshot = pond.menuSnapshot ?? (pond.menuSnapshot = BuildMenuSnapshot(pond));
                if (snapshot.causalSummary?.issues?.Any(issue => issue.priority <= PondCausalPriority.Reproduction) == true &&
                    pond.proxy?.Spawned == true) result.Add(pond.proxy);
            }
            return result;
        }
    }

    public sealed class ITab_PondOverview : ITab
    {
        public ITab_PondOverview() { size = new Vector2(520f, 760f); labelKey = "AF_PondOverviewTab"; }
        public override bool IsVisible => SelThing is PondProxyThing && AquacultureProgression.IsAvailable("AF_IndustrialAquaculture");

        protected override void FillTab()
        {
            PondProxyThing pond = SelThing as PondProxyThing;
            PondMenuSnapshot snapshot = pond?.Map?.GetComponent<FishPondMapComponent>()?.MenuSnapshotAt(pond.Position);
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(14f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "AquacultureFishing.PondOverviewTitle".Translate());
            Text.Font = GameFont.Small;
            if (snapshot?.causalSummary != null)
                PondCausalUi.DrawSummary(new Rect(rect.x, rect.y + 42f, rect.width, 286f), snapshot, null);
            Widgets.Label(new Rect(rect.x, rect.y + 336f, rect.width, 250f), snapshot?.overview ?? "AquacultureFishing.PondNoData".Translate());
            if (snapshot?.warnings == null || snapshot.warnings.Count == 0)
            {
                GUI.color = new Color(0.58f, 0.86f, 0.64f);
                Widgets.Label(new Rect(rect.x, rect.y + 594f, rect.width, 30f), "AquacultureFishing.PondNoActiveWarnings".Translate());
                GUI.color = Color.white;
                return;
            }
            GUI.color = new Color(1f, 0.62f, 0.40f);
            Widgets.Label(new Rect(rect.x, rect.y + 594f, rect.width, 28f), "AquacultureFishing.PondWarnings".Translate());
            GUI.color = Color.white;
            Widgets.Label(new Rect(rect.x, rect.y + 624f, rect.width, rect.height - 624f), string.Join("\n", snapshot.warnings.Select(warning => "- " + warning)));
        }
    }

    public sealed class ITab_PondManagement : ITab
    {
        private int page;

        public ITab_PondManagement() { size = new Vector2(720f, 800f); labelKey = "AF_PondManagementTab"; }
        public override bool IsVisible => SelThing is PondProxyThing && AquacultureProgression.IsAvailable("AF_ManagedAquaculture");

        protected override void FillTab()
        {
            PondProxyThing pond = SelThing as PondProxyThing;
            FishPondMapComponent component = pond?.Map?.GetComponent<FishPondMapComponent>();
            if (component == null) return;
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(14f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "AquacultureFishing.PondManagementTitle".Translate());
            Text.Font = GameFont.Small;
            float tabY = rect.y + 40f;
            float tabWidth = (rect.width - 12f) / 4f;
            DrawPageButton(new Rect(rect.x, tabY, tabWidth, 34f), 0, "AquacultureFishing.PondPageBasic".Translate().ToString());
            DrawPageButton(new Rect(rect.x + tabWidth + 4f, tabY, tabWidth, 34f), 1, "AquacultureFishing.PondPageAdvanced".Translate().ToString());
            DrawPageButton(new Rect(rect.x + (tabWidth + 4f) * 2f, tabY, tabWidth, 34f), 2, "AquacultureFishing.PondPageBreeding".Translate().ToString());
            DrawPageButton(new Rect(rect.x + (tabWidth + 4f) * 3f, tabY, tabWidth, 34f), 3, "AquacultureFishing.PondPagePlanner".Translate().ToString());

            PondMenuSnapshot snapshot = component.MenuSnapshotAt(pond.Position) ?? new PondMenuSnapshot();
            Rect content = new Rect(rect.x, tabY + 44f, rect.width, rect.height - 84f);
            if (page == 0) DrawBasicPage(content, pond, component, snapshot, selectedPage => page = selectedPage);
            else if (page == 1) DrawAdvancedPage(content, pond, component, snapshot);
            else if (page == 2) DrawBreedingPage(content, pond, component, snapshot);
            else DrawPlannerPage(content, pond, component, snapshot);
        }

        private void DrawPageButton(Rect rect, int index, string label)
        {
            Color old = GUI.color;
            if (page == index) GUI.color = new Color(0.62f, 0.90f, 0.68f);
            if (Widgets.ButtonText(rect, label)) page = index;
            GUI.color = old;
        }

        private static void DrawBasicPage(Rect rect, PondProxyThing pond, FishPondMapComponent component,
            PondMenuSnapshot snapshot, Action<int> navigate)
        {
            DrawMetrics(rect, new[]
            {
                new Metric(TexCommand.SelectCarriedThing, "AquacultureFishing.PondPhysicalSpaceShort".Translate().ToString(),
                    snapshot.population + " / " + snapshot.physicalCapacity,
                    snapshot.population > snapshot.physicalCapacity ? new Color(1f, 0.55f, 0.35f) : Color.white),
                new Metric(TexCommand.DesirePower, "AquacultureFishing.PondSustainableShort".Translate().ToString(),
                    "~" + snapshot.sustainableCapacity,
                    snapshot.population > snapshot.sustainableCapacity ? new Color(1f, 0.78f, 0.35f) : Color.white),
                new Metric(TexCommand.Attack, "AquacultureFishing.PondIndustrialShort".Translate().ToString(),
                    snapshot.industrialCapacity.ToString(),
                    snapshot.population > snapshot.industrialCapacity ? new Color(1f, 0.55f, 0.35f) : Color.white),
                new Metric(TexCommand.ForbidOff, "AquacultureFishing.PondWorkShort".Translate().ToString(),
                    snapshot.eligibleHarvest + " / " + (snapshot.pendingHarvest + snapshot.pendingEggRemoval + snapshot.pendingSterilization),
                    snapshot.pendingHarvest + snapshot.pendingEggRemoval + snapshot.pendingSterilization > 0 ? new Color(1f, 0.82f, 0.35f) : Color.white)
            });
            PondCausalUi.DrawSummary(new Rect(rect.x, rect.y + 78f, rect.width, 286f), snapshot, navigate);
            float y = rect.y + 378f;
            bool canChangeWater = component.CanChangeWaterAt(pond.Position);
            Widgets.Label(new Rect(rect.x, y, 250f, 30f), "AquacultureFishing.PondWaterLabel".Translate());
            if (Widgets.ButtonText(new Rect(rect.x + 260f, y, 210f, 30f), WaterLabel(component.WaterKindAt(pond.Position))))
            {
                if (!canChangeWater) Messages.Message("AquacultureFishing.PondWaterLockedMessage".Translate(),
                    MessageTypeDefOf.RejectInput, false);
                else Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(PondWaterKind)).Cast<PondWaterKind>()
                    .Select(kind => new FloatMenuOption(WaterLabel(kind), () => component.SetPondWater(pond.Position, kind))).ToList()));
            }
            TooltipHandler.TipRegion(new Rect(rect.x, y, rect.width, 30f), canChangeWater
                ? "AquacultureFishing.PondWaterChooseTooltip".Translate().ToString()
                : "AquacultureFishing.PondWaterLockedTooltip".Translate().ToString());
            y += 48f;
            DrawSubheading(rect, ref y, "AquacultureFishing.PondHarvestPolicy".Translate().ToString());
            int reserve = component.MinimumHarvestPopulationAt(pond.Position);
            DrawIntSlider(rect, ref y, "AquacultureFishing.PondMinimumBreedingStock".Translate().ToString(), reserve, 0, 50,
                value => component.SetMinimumHarvestPopulation(pond.Position, value),
                "AquacultureFishing.PondMinimumBreedingStockTooltip".Translate().ToString());
            bool adultsOnly = component.HarvestAdultsOnlyAt(pond.Position);
            bool oldAdultsOnly = adultsOnly;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, rect.width, 30f), "AquacultureFishing.PondHarvestAdultsOnly".Translate(), ref adultsOnly);
            if (adultsOnly != oldAdultsOnly) component.SetHarvestAdultsOnly(pond.Position, adultsOnly);
            y += 38f;
            bool protectFemales = component.ProtectBreedingFemalesAt(pond.Position);
            bool oldProtectFemales = protectFemales;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, rect.width, 30f), "AquacultureFishing.PondProtectBreedingFemales".Translate(), ref protectFemales);
            if (protectFemales != oldProtectFemales) component.SetProtectBreedingFemales(pond.Position, protectFemales);
            y += 48f;
            DrawPendingWork(rect, y, snapshot);
        }

        private static void DrawAdvancedPage(Rect rect, PondProxyThing pond, FishPondMapComponent component, PondMenuSnapshot snapshot)
        {
            DrawMetrics(rect, new[]
            {
                new Metric(TexCommand.DesirePower, "Feed Reserve", snapshot.feedHours <= 0f ? "None" : snapshot.feedHours.ToString("0.#") + " h",
                    snapshot.feedHours < 12f ? new Color(1f, 0.65f, 0.35f) : Color.white),
                new Metric(TexCommand.Install, "Algae", snapshot.algaePercent.ToStringPercent(), Color.white),
                new Metric(TexCommand.SelectCarriedThing, "Hungry", (snapshot.hungry + snapshot.starving).ToString(),
                    snapshot.hungry + snapshot.starving > 0 ? new Color(1f, 0.55f, 0.35f) : Color.white),
                new Metric(TexCommand.ForbidOff, "Stress", (snapshot.wrongWater + snapshot.temperatureStressed).ToString(),
                    snapshot.wrongWater + snapshot.temperatureStressed > 0 ? new Color(1f, 0.55f, 0.35f) : Color.white)
            });
            float y = rect.y + 86f;
            if (!AquacultureProgression.IsAvailable("AF_IndustrialAquaculture"))
            {
                DrawLocked(rect, y, "Industrial Aquaculture", "Stocking goals, automated harvest designations, feeder control, and ecosystem diagnostics.");
                return;
            }
            DrawSubheading(rect, ref y, "Stocking And Labor");
            int target = component.ManagementPopulationTargetAt(pond.Position);
            DrawIntSlider(rect, ref y, "Population Goal", target, 0, 200,
                value => component.SetManagementPopulationTarget(pond.Position, value),
                "A management goal, not a biological cap. Zero disables it.");
            bool autoHarvest = component.AutomaticSurplusHarvestAt(pond.Position);
            bool oldAutoHarvest = autoHarvest;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, rect.width, 30f), "Mark Surplus Fish For Harvest", ref autoHarvest);
            if (autoHarvest != oldAutoHarvest) component.SetAutomaticSurplusHarvest(pond.Position, autoHarvest);
            y += 46f;
            DrawSubheading(rect, ref y, "Feed Program");
            bool autoFeed = component.AutomaticFeedingAt(pond.Position);
            bool oldAutoFeed = autoFeed;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, rect.width, 30f), "Automatic Feeding", ref autoFeed);
            if (autoFeed != oldAutoFeed) component.SetAutomaticFeeding(pond.Position, autoFeed);
            y += 38f;
            float feedDays = component.TargetFeedDaysAt(pond.Position);
            Widgets.Label(new Rect(rect.x, y, 250f, 30f), "Target Feed Reserve");
            float changed = Mathf.Round(Widgets.HorizontalSlider(new Rect(rect.x + 260f, y + 5f, rect.width - 330f, 20f), feedDays, 0.25f, 5f, true) * 4f) / 4f;
            Widgets.Label(new Rect(rect.xMax - 60f, y, 60f, 30f), changed.ToString("0.##") + " d");
            if (!Mathf.Approximately(changed, feedDays)) component.SetTargetFeedDays(pond.Position, changed);
            y += 42f;
            bool pondPredation = component.PredationEnabledAt(pond.Position);
            bool oldPondPredation = pondPredation;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, rect.width, 30f), "Allow Natural Predation", ref pondPredation);
            if (pondPredation != oldPondPredation) component.SetPredationEnabled(pond.Position, pondPredation);
            y += 38f;
            Color old = GUI.color;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rect.x, y, rect.width, 52f), snapshot.feederStatus ?? "No feeder data.");
            GUI.color = old;
            y += 58f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 46f), AquacultureMod.Settings?.predationEnabled == false
                ? "Predation is disabled globally. Enable it in mod settings before allowing it in this pond."
                : pondPredation
                    ? "Predation is enabled for this pond. Separate incompatible species or harvest predators to control losses."
                    : "Predation is disabled for this pond.");
        }

        private static void DrawBreedingPage(Rect rect, PondProxyThing pond, FishPondMapComponent component, PondMenuSnapshot snapshot)
        {
            PondBreedingMode mode = component.BreedingModeAt(pond.Position);
            DrawMetrics(rect, new[]
            {
                new Metric(TexCommand.DesirePower, "Program", mode.ToString(), Color.white),
                new Metric(TexCommand.Install, "Eggs", snapshot.eggs.ToString(), Color.white),
                new Metric(TexCommand.ForbidOff, "Egg Removal", snapshot.pendingEggRemoval.ToString(),
                    snapshot.pendingEggRemoval > 0 ? new Color(1f, 0.82f, 0.35f) : Color.white),
                new Metric(TexCommand.SelectCarriedThing, "Sterilization", snapshot.pendingSterilization.ToString(),
                    snapshot.pendingSterilization > 0 ? new Color(1f, 0.82f, 0.35f) : Color.white)
            });
            float y = rect.y + 86f;
            if (!AquacultureProgression.IsAvailable("AF_SelectiveBreeding"))
            {
                DrawLocked(rect, y, "Selective Fish Breeding", "Breeding programs, egg-removal work, and permanent medicine-backed sterilization.");
                return;
            }
            DrawSubheading(rect, ref y, "Breeding Program");
            DrawBreedingChoice(component, pond.Position, rect, ref y, mode, PondBreedingMode.Natural,
                "Natural", "Normal interval and offspring yield. No intervention cost.");
            DrawBreedingChoice(component, pond.Position, rect, ref y, mode, PondBreedingMode.Paused,
                "Paused", "Stops breeding and designates eggs for handler removal.");
            DrawBreedingChoice(component, pond.Position, rect, ref y, mode, PondBreedingMode.Encouraged,
                "Encouraged", "25% faster, +1 maximum. Uses 0.03 prepared feed per offspring.");
            DrawBreedingChoice(component, pond.Position, rect, ref y, mode, PondBreedingMode.Intensive,
                "Intensive", "50% faster, +1 minimum and +2 maximum. Uses 0.08 feed per offspring.");
            Color old = GUI.color;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rect.x, y + 4f, rect.width, 42f), "Sterilization is designated on an individual fish and permanently consumes one medicine plus handler labor.");
            GUI.color = old;
        }

        private static void DrawPlannerPage(Rect rect, PondProxyThing pond, FishPondMapComponent component, PondMenuSnapshot snapshot)
        {
            bool hasBlueprint = snapshot.blueprintTarget > 0;
            DrawMetrics(rect, new[]
            {
                new Metric(TexCommand.SelectCarriedThing, "Stock / Target",
                    hasBlueprint ? snapshot.population + " / " + snapshot.blueprintTarget : snapshot.population + " fish", Color.white),
                new Metric(TexCommand.Install, "Missing", hasBlueprint ? snapshot.blueprintDeficit.ToString() : "-",
                    snapshot.blueprintDeficit > 0 ? new Color(1f, 0.78f, 0.30f) : Color.white),
                new Metric(TexCommand.Attack, "Surplus", hasBlueprint ? snapshot.blueprintSurplus.ToString() : "-",
                    snapshot.blueprintSurplus > 0 ? new Color(1f, 0.65f, 0.30f) : Color.white),
                new Metric(TexCommand.ForbidOff, "Ecosystem Fit", hasBlueprint ? snapshot.blueprintFit.ToStringPercent() : "No plan",
                    hasBlueprint && snapshot.blueprintFit >= 0.999f ? new Color(0.52f, 0.92f, 0.58f) :
                    hasBlueprint ? new Color(1f, 0.78f, 0.30f) : Color.white)
            });
            float y = rect.y + 94f;
            if (!AquacultureProgression.IsAvailable("AF_IndustrialAquaculture"))
            {
                DrawLocked(rect, y, "Industrial Aquaculture", "Predictive stocking, diet balance, feed demand, compatibility, and population planning.");
                return;
            }
            DrawSubheading(rect, ref y, "Plan Before Stocking");
            Widgets.Label(new Rect(rect.x, y, rect.width, 68f),
                "Compare loaded fish species against this pond's water, temperature, carrying capacity, natural food production, predation pressure, breeding potential, beauty, and expected yield.");
            y += 82f;
            if (hasBlueprint)
            {
                Color statusColor = snapshot.blueprintStatus == "Balanced" ? new Color(0.52f, 0.92f, 0.58f)
                    : snapshot.blueprintStatus == "At risk" ? new Color(1f, 0.52f, 0.35f)
                    : new Color(1f, 0.78f, 0.30f);
                GUI.color = statusColor;
                Widgets.Label(new Rect(rect.x, y, rect.width, 28f), "Living Blueprint: " + snapshot.blueprintStatus);
                GUI.color = Color.white;
                y += 34f;
            }
            Rect callout = new Rect(rect.x, y, rect.width, 112f);
            Widgets.DrawMenuSection(callout);
            GUI.DrawTexture(new Rect(callout.x + 18f, callout.y + 28f, 52f, 52f), TexCommand.SelectCarriedThing, ScaleMode.ScaleToFit);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(callout.x + 88f, callout.y + 18f, callout.width - 106f, 32f), "Interactive Ecosystem Forecast");
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(callout.x + 88f, callout.y + 51f, callout.width - 106f, 48f),
                "The planner starts with the pond's current population. It does not move fish or run in the background.");
            GUI.color = Color.white;
            y += 132f;
            if (Widgets.ButtonText(new Rect(rect.x + rect.width * 0.24f, y, rect.width * 0.52f, 44f),
                hasBlueprint ? "Edit Living Blueprint" : "Open Stocking Planner"))
                Find.WindowStack.Add(new Dialog_PondStockingPlanner(pond, component));
        }

        private static void DrawMetrics(Rect rect, Metric[] metrics)
        {
            float gap = 6f;
            float width = (rect.width - gap * 3f) / 4f;
            for (int i = 0; i < metrics.Length; i++)
            {
                Rect box = new Rect(rect.x + i * (width + gap), rect.y, width, 74f);
                Widgets.DrawMenuSection(box);
                Texture2D icon = metrics[i].icon;
                if (icon != null) GUI.DrawTexture(new Rect(box.x + 9f, box.y + 18f, 30f, 30f), icon, ScaleMode.ScaleToFit);
                Color old = GUI.color;
                GUI.color = metrics[i].color;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(box.x + 46f, box.y + 10f, box.width - 52f, 30f), metrics[i].value);
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(box.x + 46f, box.y + 42f, box.width - 52f, 22f), metrics[i].label);
                Text.Font = GameFont.Small;
                GUI.color = old;
            }
        }

        private static void DrawPendingWork(Rect rect, float y, PondMenuSnapshot snapshot)
        {
            DrawSubheading(rect, ref y, "Pending Handler Work");
            Widgets.Label(new Rect(rect.x, y, rect.width, 32f),
                "Harvest: " + snapshot.pendingHarvest + "     Egg removal: " + snapshot.pendingEggRemoval +
                "     Sterilization: " + snapshot.pendingSterilization);
        }

        private static void DrawIntSlider(Rect rect, ref float y, string label, int value, int minimum, int maximum, Action<int> setter, string tooltip)
        {
            Widgets.Label(new Rect(rect.x, y, 250f, 30f), label);
            int changed = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(rect.x + 260f, y + 5f, rect.width - 330f, 20f), value, minimum, maximum, true));
            Widgets.Label(new Rect(rect.xMax - 60f, y, 60f, 30f), changed == 0 && label == "Population Goal" ? "None" : changed.ToString());
            if (changed != value) setter(changed);
            TooltipHandler.TipRegion(new Rect(rect.x, y, rect.width, 30f), tooltip);
            y += 40f;
        }

        private static void DrawSubheading(Rect rect, ref float y, string label)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), label);
            Text.Font = GameFont.Small;
            y += 31f;
            Widgets.DrawLineHorizontal(rect.x, y, rect.width);
            y += 10f;
        }

        private static void DrawLocked(Rect rect, float y, string research, string features)
        {
            Widgets.DrawMenuSection(new Rect(rect.x, y, rect.width, 100f));
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 16f, y + 14f, rect.width - 32f, 30f), "Requires " + research);
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rect.x + 16f, y + 48f, rect.width - 32f, 42f), features);
            GUI.color = Color.white;
        }

        private static void DrawBreedingChoice(FishPondMapComponent component, IntVec3 pondCell, Rect rect, ref float y,
            PondBreedingMode selected, PondBreedingMode option, string label, string description)
        {
            bool active = selected == option;
            Color old = GUI.color;
            if (active) GUI.color = new Color(0.62f, 0.90f, 0.68f);
            if (Widgets.ButtonText(new Rect(rect.x, y, 160f, 34f), (active ? "Selected: " : "") + label))
                component.SetBreedingMode(pondCell, option);
            GUI.color = old;
            Widgets.Label(new Rect(rect.x + 174f, y, rect.width - 174f, 42f), description);
            y += 50f;
        }

        private readonly struct Metric
        {
            public readonly Texture2D icon;
            public readonly string label;
            public readonly string value;
            public readonly Color color;
            public Metric(Texture2D icon, string label, string value, Color color)
            {
                this.icon = icon;
                this.label = label;
                this.value = value;
                this.color = color;
            }
        }

        private static string WaterLabel(PondWaterKind kind) => kind == PondWaterKind.Brackishwater ? "Brackishwater" : kind.ToString();
    }

    public sealed class Alert_AquaculturePondHealth : Alert
    {
        public Alert_AquaculturePondHealth()
        {
            defaultLabel = "AquacultureFishing.PondAlertLabel".Translate();
            defaultExplanation = "AquacultureFishing.PondAlertExplanation".Translate();
        }

        public override AlertPriority Priority => AlertPriority.High;

        public override AlertReport GetReport()
        {
            if (AquacultureMod.Settings?.showPondAlerts == false
                || Current.Game?.Maps == null) return false;
            var culprits = new List<Thing>();
            for (int i = 0; i < Current.Game.Maps.Count; i++)
                culprits.AddRange(Current.Game.Maps[i].GetComponent<FishPondMapComponent>()?.CriticalPondProxies() ?? Enumerable.Empty<Thing>());
            return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : false;
        }
    }
}
