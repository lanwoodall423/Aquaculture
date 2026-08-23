using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class Dialog_PondStockingPlanner : Window
    {
        private readonly PondProxyThing pond;
        private readonly FishPondMapComponent component;
        private readonly List<ThingDef> species;
        private readonly Dictionary<ThingDef, int> plan = new Dictionary<ThingDef, int>();
        private PondWaterKind plannedWater;
        private AquacultureStockingPlannerDocument insightDocument;
        private string cachedForecastKey;
        private AquaculturePlannerForecastSnapshot cachedForecastSnapshot;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Clamp(UI.screenWidth * 0.78f, 860f, 1280f),
            Mathf.Clamp(UI.screenHeight * 0.72f, 560f, 900f));

        public Dialog_PondStockingPlanner(PondProxyThing pond, FishPondMapComponent component)
        {
            this.pond = pond;
            this.component = component;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = false;
            plannedWater = component?.StockingBlueprintWaterAt(pond.Position) ?? PondWaterKind.Freshwater;
            species = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(FishUtility.IsFish)
                .Where(PlannerFactsKnownToColony)
                .OrderBy(def => def.label)
                .ThenBy(def => def.defName)
                .ToList();
            if (!LoadBlueprint()) LoadCurrentStock();
            insightDocument = new AquacultureStockingPlannerDocument(this);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (pond?.Spawned != true || component == null)
            {
                Widgets.Label(inRect, "AquacultureFishing.PondPlannerUnavailable".Translate());
                return;
            }

            insightDocument.Draw(inRect);
        }

        public override void PostClose()
        {
            insightDocument?.PostClose();
            base.PostClose();
        }

        internal IReadOnlyList<ThingDef> SpeciesCatalog => species;
        internal IEnumerable<KeyValuePair<ThingDef, int>> PlanEntries => plan
            .Where(pair => PlannerFactsKnownToColony(pair.Key));
        internal PondWaterKind PlannedWaterValue => plannedWater;

        internal void SetPlannedWater(PondWaterKind value)
        {
            plannedWater = value;
            cachedForecastKey = null;
        }

        internal void ChangeCountForUi(ThingDef def, int delta)
        {
            ChangeCount(def, delta);
        }

        internal void LoadCurrentForUi()
        {
            LoadCurrentStock();
        }

        internal void LoadBlueprintForUi()
        {
            LoadBlueprint();
        }

        internal void ClearPlanForUi()
        {
            plan.Clear();
            cachedForecastKey = null;
        }

        internal void SavePlanForUi()
        {
            component.SetStockingBlueprint(pond.Position, plan, plannedWater);
            int total = PlanEntries.Sum(pair => pair.Value);
            Messages.Message(total > 0
                    ? "AquacultureFishing.PondPlannerSaved".Translate(total).ToString()
                    : "AquacultureFishing.PondPlannerCleared".Translate().ToString(),
                MessageTypeDefOf.TaskCompletion, false);
        }

        internal AquaculturePlannerForecastSnapshot ForecastSnapshot
        {
            get
            {
                IEnumerable<KeyValuePair<ThingDef, int>> visiblePlan = PlanEntries.ToList();
                string key = AquaculturePlannerCacheContract.Key(visiblePlan
                    .Where(pair => pair.Value > 0)
                    .OrderBy(pair => pair.Key.defName, StringComparer.Ordinal)
                    .Select(pair => pair.Key.defName + ":" + pair.Value),
                    plannedWater.ToString(), AquacultureSnapshotCache.Revision);
                if (cachedForecastSnapshot != null && cachedForecastKey == key) return cachedForecastSnapshot;
                Forecast data = CalculateForecast(visiblePlan);
                cachedForecastKey = key;
                cachedForecastSnapshot = new AquaculturePlannerForecastSnapshot(data.totalFish,
                    data.physicalCapacity, data.sustainableCapacity, data.industrialCapacity,
                    data.dailyDemand, data.feedNeeded, data.danger, data.predationRisk,
                    data.warnings, key, AquacultureSnapshotCache.Revision);
                return cachedForecastSnapshot;
            }
        }

        private Forecast CalculateForecast(IEnumerable<KeyValuePair<ThingDef, int>> entries)
        {
            IEnumerable<KeyValuePair<ThingDef, int>> forecastEntries =
                entries ?? Enumerable.Empty<KeyValuePair<ThingDef, int>>();
            PondMenuSnapshot snapshot = component.MenuSnapshotAt(pond.Position) ?? new PondMenuSnapshot();
            float demandMultiplier = AquacultureMod.Settings?.foodDemandMultiplier ?? 1f;
            float algaeMultiplier = AquacultureMod.Settings?.algaeGrowthMultiplier ?? 1f;
            PondMovementUtility.PondInfo pondInfo = PondMovementUtility.InfoAt(pond.Map, pond.Position);
            var data = new Forecast
            {
                capacity = snapshot.capacity,
                physicalCapacity = snapshot.physicalCapacity > 0 ? snapshot.physicalCapacity : snapshot.capacity,
                industrialCapacity = snapshot.industrialCapacity > 0 ? snapshot.industrialCapacity : snapshot.capacity,
                habitatSupportedCapacity = snapshot.habitatSupportedCapacity > 0
                    ? snapshot.habitatSupportedCapacity : snapshot.capacity
            };
            float temperature = snapshot.temperature;

            foreach (KeyValuePair<ThingDef, int> pair in forecastEntries)
            {
                if (pair.Value <= 0) continue;
                ThingDef def = pair.Key;
                int count = pair.Value;
                AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
                float daily = profile.hourlyDemand * 24f * demandMultiplier * count;
                DietShares(profile.diet, out float algae, out float detritus, out float prey);
                data.totalFish += count;
                data.dailyDemand += daily;
                data.algaeDemand += daily * algae;
                data.detritusDemand += daily * detritus;
                data.preyDemand += daily * prey;
                data.beauty += count * (1f + profile.beautyOffset);
                data.meatIndex += count * profile.meatYieldFactor;
                if (profile.diet == FishDiet.Carnivore) data.predators += count;
                else if (profile.diet == FishDiet.Omnivore) data.omnivores += count;
                else data.plantEaters += count;
                if (count >= 2) data.breedingSpecies++;
                if (!AquaticSpeciesProfile.WaterCompatible(profile.waterKind, plannedWater))
                {
                    data.unsafeCompatibility = true;
                    data.warnings.Add("AquacultureFishing.PondPlannerWrongWater".Translate(def.LabelCap, WaterLabel(profile.waterKind)).ToString());
                }
                if (temperature < profile.minimumTemperature || temperature > profile.maximumTemperature)
                {
                    data.unsafeCompatibility = true;
                    data.warnings.Add("AquacultureFishing.PondPlannerTemperature".Translate(def.LabelCap,
                        profile.minimumTemperature.ToString("0.#"), profile.maximumTemperature.ToString("0.#")).ToString());
                }
            }

            data.sustainableAlgae = snapshot.habitat?.naturalFoodPerDay ??
                PondCapacityRules.EstimatedNaturalFoodPerDay(pondInfo?.cells.Count ?? 1, 0, algaeMultiplier);
            float naturalSupport = Mathf.Min(data.algaeDemand, data.sustainableAlgae);
            data.feedNeeded = Mathf.Max(0f, data.dailyDemand - naturalSupport);
            float dailyDemandPerFish = data.totalFish > 0 ? data.dailyDemand / data.totalFish :
                0.0025f * demandMultiplier * 24f;
            data.foodSupportedCapacity = PondCapacityRules.FoodSupportedPopulation(data.sustainableAlgae,
                dailyDemandPerFish, data.industrialCapacity);
            data.sustainableCapacity = Mathf.Min(data.industrialCapacity,
                Mathf.Min(data.foodSupportedCapacity, data.habitatSupportedCapacity));
            data.predationRisk = data.predators + data.omnivores > 0 &&
                forecastEntries.Count(pair => pair.Value > 0) > 1;
            if (data.totalFish > data.physicalCapacity)
                data.warnings.Insert(0, "AquacultureFishing.PondPlannerOverPhysical".Translate(
                    data.totalFish - data.physicalCapacity).ToString());
            else if (data.totalFish > data.industrialCapacity)
                data.warnings.Insert(0, "AquacultureFishing.PondPlannerOverIndustrial".Translate(
                    data.totalFish - data.industrialCapacity).ToString());
            else if (data.totalFish > data.sustainableCapacity)
                data.warnings.Insert(0, "AquacultureFishing.PondPlannerUnsupported".Translate(
                    data.foodSupportedCapacity <= data.habitatSupportedCapacity ?
                        "AquacultureFishing.PondCapacityConstraintFood".Translate().ToString() :
                        "AquacultureFishing.PondCapacityConstraintHabitat".Translate().ToString()).ToString());
            if (data.algaeDemand > data.sustainableAlgae * 1.1f)
                data.warnings.Add("AquacultureFishing.PondPlannerAlgaeWarning".Translate());
            if (data.preyDemand > 0f && !data.predationRisk)
                data.warnings.Add("AquacultureFishing.PondPlannerNoPrey".Translate());
            else if (data.predationRisk)
                data.warnings.Add("AquacultureFishing.PondPlannerPredatorWarning".Translate());
            if (data.totalFish == 0) data.warnings.Add("AquacultureFishing.PondPlannerEmpty".Translate());
            data.danger = data.totalFish > data.physicalCapacity || data.unsafeCompatibility;
            return data;
        }

        private void LoadCurrentStock()
        {
            plan.Clear();
            cachedForecastKey = null;
            PondMenuSnapshot snapshot = component?.MenuSnapshotAt(pond.Position);
            if (snapshot == null) return;
            for (int i = 0; i < snapshot.fish.Count; i++)
            {
                ThingDef def = snapshot.fish[i].fish?.parent?.def;
                if (PlannerFactsKnownToColony(def)) plan[def] = Count(def) + 1;
            }
        }

        private bool LoadBlueprint()
        {
            Dictionary<ThingDef, int> saved = component?.StockingBlueprintAt(pond.Position);
            if (saved == null || saved.Count == 0) return false;
            plan.Clear();
            cachedForecastKey = null;
            foreach (KeyValuePair<ThingDef, int> pair in saved)
                if (pair.Key != null && pair.Value > 0) plan[pair.Key] = pair.Value;
            plannedWater = component.StockingBlueprintWaterAt(pond.Position);
            return true;
        }

        private void ChangeCount(ThingDef def, int delta)
        {
            if (!PlannerFactsKnownToColony(def)) return;
            int changed = Mathf.Clamp(Count(def) + delta, 0, 999);
            if (changed <= 0) plan.Remove(def);
            else plan[def] = changed;
            cachedForecastKey = null;
        }

        private int Count(ThingDef def) => plan.TryGetValue(def, out int count) ? count : 0;

        private static bool PlannerFactsKnownToColony(ThingDef def)
        {
            if (def == null) return false;
            AquacultureKnowledgeView view = AquacultureKnowledgeAdapter.SpeciesView(def, null, true);
            return view.identityKnown && HasFacet(view, "feeding") && HasFacet(view, "habitat") &&
                HasFacet(view, "pond_compatibility");
        }

        private static bool HasFacet(AquacultureKnowledgeView view, string facet)
        {
            return view.knownFacets != null && view.knownFacets.Contains(facet, StringComparer.Ordinal);
        }

        private static string WaterLabel(PondWaterKind kind)
        {
            switch (kind)
            {
                case PondWaterKind.Saltwater: return "AquacultureFishing.WorkspaceWaterSaltwater".Translate().ToString();
                case PondWaterKind.Brackishwater: return "AquacultureFishing.WorkspaceWaterBrackishwater".Translate().ToString();
                default: return "AquacultureFishing.WorkspaceWaterFreshwater".Translate().ToString();
            }
        }

        private static void DietShares(FishDiet diet, out float algae, out float detritus, out float prey)
        {
            algae = detritus = prey = 0f;
            switch (diet)
            {
                case FishDiet.Herbivore: algae = 1f; break;
                case FishDiet.Carnivore: prey = 1f; break;
                case FishDiet.Detritivore: detritus = 0.9f; algae = 0.1f; break;
                case FishDiet.FilterFeeder: algae = 0.8f; detritus = 0.2f; break;
                default: algae = 0.55f; detritus = 0.15f; prey = 0.3f; break;
            }
        }

        private sealed class Forecast
        {
            public int totalFish;
            public int capacity;
            public int physicalCapacity;
            public int sustainableCapacity;
            public int industrialCapacity;
            public int foodSupportedCapacity;
            public int habitatSupportedCapacity;
            public int plantEaters;
            public int omnivores;
            public int predators;
            public int breedingSpecies;
            public float dailyDemand;
            public float algaeDemand;
            public float detritusDemand;
            public float preyDemand;
            public float sustainableAlgae;
            public float feedNeeded;
            public float beauty;
            public float meatIndex;
            public bool unsafeCompatibility;
            public bool predationRisk;
            public bool danger;
            public readonly List<string> warnings = new List<string>();
        }
    }
}
