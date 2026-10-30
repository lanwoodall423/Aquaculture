using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    /// <summary>
    /// Aquaculture-owned stocking planner presentation contract. The native
    /// dialog remains the owner of blueprint, forecast, and window lifecycle.
    /// </summary>
    public interface IAquacultureStockingPlannerView
    {
        void Draw(Rect rect);
        void PostClose();
    }

    public sealed class Dialog_PondStockingPlanner : Window
    {
        private readonly PondProxyThing pond;
        private readonly FishPondMapComponent component;
        private readonly List<ThingDef> species;
        private readonly Dictionary<ThingDef, int> plan = new Dictionary<ThingDef, int>();
        private PondWaterKind plannedWater;
        private IAquacultureStockingPlannerView insightDocument;
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
            insightDocument = AquacultureStockingPlannerViewFactory.Create(this);
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

    /// <summary>
    /// Native Widgets-only stocking planner prototype. The owner remains the
    /// authority for learned species, forecasting and persisted blueprints.
    /// The default enhanced renderer remains selected by its existing factory.
    /// </summary>
    public sealed class NativeAquacultureStockingPlannerView : IAquacultureStockingPlannerView
    {
        private readonly Dialog_PondStockingPlanner owner;
        private Vector2 scrollPosition;
        private string search = string.Empty;

        public NativeAquacultureStockingPlannerView(Dialog_PondStockingPlanner owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void PostClose()
        {
            scrollPosition = Vector2.zero;
            search = string.Empty;
        }

        public void Draw(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect padded = rect.ContractedBy(8f);
            if (padded.width <= 0f || padded.height <= 0f) return;

            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(padded.x, padded.y, padded.width, 32f),
                    L("AquacultureFishing.PondPlannerTitle"));
                Text.Font = GameFont.Small;

                float yTop = padded.y + 36f;
                string subtitle = L("AquacultureFishing.PondPlannerSubtitle");
                float subtitleHeight = LabelHeight(subtitle, padded.width);
                Widgets.Label(new Rect(padded.x, yTop, padded.width, subtitleHeight), subtitle);
                yTop += subtitleHeight + 8f;
                Widgets.Label(new Rect(padded.x, yTop, padded.width, 24f),
                    L("AquacultureFishing.PondPlannerSearch"));
                yTop += 26f;

                string entered = Widgets.TextField(new Rect(padded.x, yTop, padded.width, 30f), search);
                if (!string.Equals(search, entered, StringComparison.Ordinal))
                {
                    search = entered ?? string.Empty;
                    scrollPosition = Vector2.zero;
                }
                yTop += 36f;

                Rect viewport = new Rect(padded.x, yTop, padded.width,
                    Mathf.Max(0f, padded.yMax - yTop));
                if (viewport.width <= 0f || viewport.height <= 0f) return;

                // The catalog already excludes species whose feeding, habitat
                // or pond-compatibility knowledge is not known to the colony.
                IReadOnlyList<ThingDef> catalog = owner.SpeciesCatalog ?? new ThingDef[0];
                List<ThingDef> available = catalog
                    .Where(def => def != null && (search.NullOrEmpty() ||
                        def.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        def.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(def => def.label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(def => def.defName, StringComparer.Ordinal).ToList();
                List<KeyValuePair<ThingDef, int>> planned = (owner.PlanEntries ??
                    Enumerable.Empty<KeyValuePair<ThingDef, int>>())
                    .Where(pair => pair.Key != null && pair.Value > 0)
                    .OrderBy(pair => pair.Key.label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(pair => pair.Key.defName, StringComparer.Ordinal).ToList();
                AquaculturePlannerForecastSnapshot forecast = owner.ForecastSnapshot;

                string waterTip = L("AquacultureFishing.PondPlannerWaterTip");
                string saveTip = L("AquacultureFishing.PondPlannerSaveTip");
                string state = forecast == null
                    ? L("AquacultureFishing.PondPlannerUnavailable")
                    : forecast.Danger ? L("AquacultureFishing.PondPlannerUnsafe")
                    : forecast.Warnings.Count > 0 ? L("AquacultureFishing.PondPlannerNeedsSupport")
                    : L("AquacultureFishing.PondPlannerStable");
                string food = forecast == null ? string.Empty : string.Format(
                    L("AquacultureFishing.PondPlannerFoodDetails"),
                    forecast.DailyDemand.ToString("0.000"), forecast.FeedNeeded.ToString("0.000"));
                string risk = forecast == null ? string.Empty : L(forecast.PredationRisk
                    ? "AquacultureFishing.PondPlannerPredationRisk"
                    : "AquacultureFishing.PondPlannerNoPredation");
                List<string> warnings = forecast?.Warnings?.Where(item => !string.IsNullOrEmpty(item))
                    .ToList() ?? new List<string>();
                if (warnings.Count == 0)
                    warnings.Add(L("AquacultureFishing.PondPlannerCompatible"));

                float viewWidth = Mathf.Max(24f, viewport.width - 18f);
                float lineWidth = Mathf.Max(20f, viewWidth - 12f);
                float availableLabelWidth = Mathf.Max(20f, viewWidth - 65f);

                float contentHeight = 30f + 16f;
                if (available.Count == 0)
                    contentHeight += LabelHeight(L("AquacultureFishing.PondPlannerNoAvailableSpecies"),
                        lineWidth) + 6f;
                foreach (ThingDef def in available)
                    contentHeight += SpeciesRowHeight(def.LabelCap.ToString(), availableLabelWidth) + 6f;
                contentHeight += 30f + 40f + LabelHeight(waterTip, lineWidth) + 12f;
                if (planned.Count == 0)
                    contentHeight += LabelHeight(L("AquacultureFishing.PondPlannerEmpty"), lineWidth) + 6f;
                foreach (KeyValuePair<ThingDef, int> pair in planned)
                    contentHeight += SpeciesRowHeight(pair.Key.LabelCap.ToString(), lineWidth) + 6f;
                contentHeight += 28f + 46f + 46f + LabelHeight(saveTip, lineWidth) + 24f;
                contentHeight += 30f + LabelHeight(state, lineWidth) + 6f + 3f * 56f;
                contentHeight += LabelHeight(food, lineWidth) + LabelHeight(risk, lineWidth) + 14f;
                contentHeight += 30f;
                foreach (string warning in warnings)
                    contentHeight += LabelHeight(warning, lineWidth) + 6f;
                contentHeight += 36f;

                Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(viewport.height, contentHeight));
                scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f,
                    Mathf.Max(0f, view.height - viewport.height));
                Widgets.BeginScrollView(viewport, ref scrollPosition, view);
                try
                {
                    float y = 8f;
                    Section(ref y, viewWidth, L("AquacultureFishing.PondPlannerAvailableSpecies"));
                    if (available.Count == 0)
                        Line(ref y, lineWidth,
                            L("AquacultureFishing.PondPlannerNoAvailableSpecies"));
                    foreach (ThingDef def in available)
                    {
                        float height = SpeciesRowHeight(def.LabelCap.ToString(), availableLabelWidth);
                        if (Visible(y, height, viewport.height))
                        {
                            Widgets.Label(new Rect(6f, y + 4f, availableLabelWidth, height - 8f),
                                def.LabelCap);
                            if (Widgets.ButtonText(new Rect(viewWidth - 45f, y + 5f, 36f, 32f), "+"))
                            {
                                owner.ChangeCountForUi(def, 1);
                                return;
                            }
                        }
                        y += height + 6f;
                    }

                    y += 8f;
                    Section(ref y, viewWidth, L("AquacultureFishing.PondPlannerPlannedStock"));
                    if (Widgets.ButtonText(new Rect(6f, y, lineWidth, 34f),
                        L("AquacultureFishing.PondPlannerPlannedWater") + ": " +
                            WaterLabel(owner.PlannedWaterValue)))
                    {
                        ChooseWater();
                        return;
                    }
                    y += 40f;
                    Line(ref y, lineWidth, waterTip);
                    y += 6f;

                    if (planned.Count == 0)
                        Line(ref y, lineWidth, L("AquacultureFishing.PondPlannerEmpty"));
                    foreach (KeyValuePair<ThingDef, int> pair in planned)
                    {
                        float height = SpeciesRowHeight(pair.Key.LabelCap.ToString(), lineWidth);
                        if (Visible(y, height, viewport.height))
                        {
                            Widgets.Label(new Rect(6f, y + 2f, lineWidth,
                                Mathf.Max(24f, height - 40f)), pair.Key.LabelCap);
                            float controls = y + height - 35f;
                            if (Widgets.ButtonText(new Rect(6f, controls, 34f, 30f), "-"))
                            {
                                owner.ChangeCountForUi(pair.Key, -1);
                                return;
                            }
                            Widgets.Label(new Rect(46f, controls + 3f, 66f, 25f),
                                pair.Value.ToString());
                            if (Widgets.ButtonText(new Rect(116f, controls, 34f, 30f), "+"))
                            {
                                owner.ChangeCountForUi(pair.Key, 1);
                                return;
                            }
                        }
                        y += height + 6f;
                    }

                    Line(ref y, lineWidth, string.Format(
                        L("AquacultureFishing.PondPlannerTotals"),
                        planned.Count, planned.Sum(pair => pair.Value)));
                    float actionWidth = Mathf.Max(20f, (lineWidth - 12f) / 3f);
                    if (Widgets.ButtonText(new Rect(6f, y, actionWidth, 34f),
                        L("AquacultureFishing.PondPlannerCurrent")))
                    {
                        owner.LoadCurrentForUi();
                        return;
                    }
                    if (Widgets.ButtonText(new Rect(12f + actionWidth, y, actionWidth, 34f),
                        L("AquacultureFishing.PondPlannerBlueprint")))
                    {
                        owner.LoadBlueprintForUi();
                        return;
                    }
                    if (Widgets.ButtonText(new Rect(18f + 2f * actionWidth, y, actionWidth, 34f),
                        L("AquacultureFishing.PondPlannerClear")))
                    {
                        owner.ClearPlanForUi();
                        return;
                    }
                    y += 46f;
                    if (Widgets.ButtonText(new Rect(6f, y, lineWidth, 34f),
                        L("AquacultureFishing.PondPlannerSave")))
                    {
                        owner.SavePlanForUi();
                        return;
                    }
                    y += 46f;
                    Line(ref y, lineWidth, saveTip);
                    y += 14f;

                    Section(ref y, viewWidth, L("AquacultureFishing.PondPlannerForecast"));
                    Line(ref y, lineWidth, state);
                    if (forecast != null)
                    {
                        Meter(ref y, lineWidth, L("AquacultureFishing.PondPlannerPhysical"),
                            forecast.TotalFish, forecast.PhysicalCapacity);
                        Meter(ref y, lineWidth, L("AquacultureFishing.PondPlannerSustainable"),
                            forecast.TotalFish, forecast.SustainableCapacity);
                        Meter(ref y, lineWidth, L("AquacultureFishing.PondPlannerIndustrial"),
                            forecast.TotalFish, forecast.IndustrialCapacity);
                    }
                    Line(ref y, lineWidth, food);
                    Line(ref y, lineWidth, risk);
                    y += 8f;
                    Section(ref y, viewWidth, L("AquacultureFishing.PondPlannerWarnings"));
                    foreach (string warning in warnings) Line(ref y, lineWidth, warning);
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        private void ChooseWater()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption(WaterLabel(PondWaterKind.Freshwater),
                    () => owner.SetPlannedWater(PondWaterKind.Freshwater)),
                new FloatMenuOption(WaterLabel(PondWaterKind.Saltwater),
                    () => owner.SetPlannedWater(PondWaterKind.Saltwater)),
                new FloatMenuOption(WaterLabel(PondWaterKind.Brackishwater),
                    () => owner.SetPlannedWater(PondWaterKind.Brackishwater))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private bool Visible(float y, float height, float viewportHeight)
        {
            return y + height >= scrollPosition.y && y <= scrollPosition.y + viewportHeight;
        }

        private static void Section(ref float y, float width, string value)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(6f, y, Mathf.Max(24f, width - 12f), 28f), value);
            Text.Font = GameFont.Small;
            y += 30f;
        }

        private static void Line(ref float y, float width, string value)
        {
            float height = LabelHeight(value, width);
            Widgets.Label(new Rect(6f, y, width, height), value ?? string.Empty);
            y += height + 6f;
        }

        private static void Meter(ref float y, float width, string label, int count, int capacity)
        {
            Widgets.Label(new Rect(6f, y, width, 25f),
                label + ": " + count + " / " + capacity);
            Widgets.FillableBar(new Rect(6f, y + 27f, width, 12f),
                Mathf.Clamp01((float)count / Mathf.Max(1, capacity)));
            y += 56f;
        }

        private static float LabelHeight(string value, float width)
        {
            return Mathf.Max(24f, Text.CalcHeight(value ?? string.Empty, Mathf.Max(20f, width)));
        }

        private static float SpeciesRowHeight(string label, float width)
        {
            return Mathf.Max(64f, LabelHeight(label, width) + 40f);
        }

        private static string WaterLabel(PondWaterKind kind)
        {
            switch (kind)
            {
                case PondWaterKind.Saltwater:
                    return L("AquacultureFishing.WorkspaceWaterSaltwater");
                case PondWaterKind.Brackishwater:
                    return L("AquacultureFishing.WorkspaceWaterBrackishwater");
                default:
                    return L("AquacultureFishing.WorkspaceWaterFreshwater");
            }
        }

        private static string L(string key) => key.Translate().ToString();
    }
}
