using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    /// <summary>Responsive Insight Canvas surface for the native planner window.</summary>
    public sealed class AquacultureStockingPlannerDocument
    {
        private readonly Dialog_PondStockingPlanner owner;
        private readonly InsightUiDocument document;
        private readonly List<ThingDef> filteredSpecies = new List<ThingDef>();
        private readonly List<KeyValuePair<ThingDef, int>> planned = new List<KeyValuePair<ThingDef, int>>();
        private AquaculturePlannerForecastSnapshot forecast;
        private string search = string.Empty;
        private InsightUiSearchField searchField;
        private InsightUiVirtualList availableList;
        private InsightUiVirtualList plannedList;
        private InsightUiLabel availableEmptyLabel;
        private InsightUiLabel plannedEmptyLabel;
        private InsightUiSelect waterSelect;
        private InsightUiLabel forecastStatus;
        private InsightUiMeter physicalMeter;
        private InsightUiMeter sustainableMeter;
        private InsightUiMeter industrialMeter;

        public AquacultureStockingPlannerDocument(Dialog_PondStockingPlanner owner)
        {
            this.owner = owner;
            document = new InsightUiDocument("aquaculture.stocking.planner.v2", BuildRoot())
            {
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(document);
            AquacultureInsightPresentation.Apply(document);
            RefreshSnapshot();
        }

        public InsightUiHost Host { get; private set; }

        public void Draw(Rect rect)
        {
            RefreshSnapshot();
            Host.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            Host.PostClose();
        }

        private void RefreshSnapshot()
        {
            filteredSpecies.Clear();
            string query = search ?? string.Empty;
            foreach (ThingDef def in owner.SpeciesCatalog)
            {
                if (query.NullOrEmpty() || def.label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    def.defName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) filteredSpecies.Add(def);
            }
            planned.Clear();
            planned.AddRange(owner.PlanEntries.Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key.label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.Key.defName, StringComparer.Ordinal));
            forecast = owner.ForecastSnapshot;
            if (availableList != null)
            {
                AquacultureUiComponents.ResizeContentAwareVirtualList(availableList,
                    filteredSpecies.Count, 260f);
            }
            if (plannedList != null)
            {
                AquacultureUiComponents.ResizeContentAwareVirtualList(plannedList, planned.Count, 260f);
            }
            if (availableEmptyLabel != null) availableEmptyLabel.Visible = filteredSpecies.Count == 0;
            if (plannedEmptyLabel != null) plannedEmptyLabel.Visible = planned.Count == 0;
            if (forecast != null)
            {
                physicalMeter.Current = forecast.TotalFish;
                physicalMeter.Maximum = Mathf.Max(1f, forecast.PhysicalCapacity);
                sustainableMeter.Current = forecast.TotalFish;
                sustainableMeter.Maximum = Mathf.Max(1f, forecast.SustainableCapacity);
                industrialMeter.Current = forecast.TotalFish;
                industrialMeter.Maximum = Mathf.Max(1f, forecast.IndustrialCapacity);
            }
            if (forecastStatus != null) forecastStatus.SetTextProvider(forecastStatusText);
        }

        private InsightUiElement BuildRoot()
        {
            searchField = InsightUi.SearchField("planner.search", string.Empty,
                    L("AquacultureFishing.PondPlannerSearch"))
                .Bind(() => search, value =>
                {
                    search = value ?? string.Empty;
                    document.Invalidate();
                });
            availableList = AquacultureUiComponents.ContentAwareVirtualList("planner.available.list", 0,
                52f, 260f, index => BuildAvailableRow(filteredSpecies[index]));
            availableEmptyLabel = InsightUi.Label("planner.available.empty",
                L("AquacultureFishing.PondPlannerNoAvailableSpecies"), InsightUiTextStyle.Caption);

            plannedList = AquacultureUiComponents.ContentAwareVirtualList("planner.planned.list", 0,
                52f, 260f, index => BuildPlannedRow(planned[index]));
            plannedEmptyLabel = InsightUi.Label("planner.planned.empty", L("AquacultureFishing.PondPlannerEmpty"),
                InsightUiTextStyle.Caption);

            PondWaterKind[] waterKinds =
            {
                PondWaterKind.Freshwater,
                PondWaterKind.Saltwater,
                PondWaterKind.Brackishwater
            };
            string[] waters = waterKinds.Select(WaterLabel).ToArray();
            waterSelect = InsightUi.Select("planner.water", L("AquacultureFishing.PondPlannerPlannedWater"),
                waters, Array.IndexOf(waterKinds, owner.PlannedWaterValue)).Bind(
                    () => Array.IndexOf(waterKinds, owner.PlannedWaterValue),
                    index =>
                    {
                        if (index < 0 || index >= waters.Length) return;
                        owner.SetPlannedWater(waterKinds[index]);
                        document.Invalidate();
                    });

            forecastStatus = InsightUi.Label("planner.forecast.status", string.Empty)
                .SetTextProvider(forecastStatusText);
            physicalMeter = InsightUi.Meter("planner.forecast.physical", 0f, 1f)
                .SetLabel(L("AquacultureFishing.PondPlannerPhysical"));
            sustainableMeter = InsightUi.Meter("planner.forecast.sustainable", 0f, 1f)
                .SetLabel(L("AquacultureFishing.PondPlannerSustainable"));
            industrialMeter = InsightUi.Meter("planner.forecast.industrial", 0f, 1f)
                .SetLabel(L("AquacultureFishing.PondPlannerIndustrial"));
            InsightUiElement forecastPanel = InsightUi.Scroll("planner.forecast.scroll",
                InsightUi.Column("planner.forecast.content").SetGap(AquacultureUiSpacing.Row).Add(
                    InsightUi.SectionHeader("planner.forecast.header", L("AquacultureFishing.PondPlannerForecast"),
                    L("AquacultureFishing.PondPlannerForecastSubtitle"), null, null, true),
                    forecastStatus,
                    physicalMeter,
                    sustainableMeter,
                    industrialMeter,
                    InsightUi.Label("planner.forecast.details", string.Empty, InsightUiTextStyle.Caption)
                        .SetTextProvider(forecastDetails),
                    InsightUi.Callout("planner.forecast.warnings", InsightUiCalloutSeverity.Warning,
                        L("AquacultureFishing.PondPlannerWarnings"), string.Empty),
                    InsightUi.Label("planner.forecast.warning-list", string.Empty, InsightUiTextStyle.Caption)
                        .SetTextProvider(forecastWarnings)));

            InsightUiElement available = InsightUi.Column("planner.available.column").SetGap(AquacultureUiSpacing.Row).Add(
                InsightUi.SectionHeader("planner.available.header", L("AquacultureFishing.PondPlannerAvailableSpecies"),
                    L("AquacultureFishing.PondPlannerAvailableSubtitle"), null, null, true), searchField,
                availableList, availableEmptyLabel);
            InsightUiElement planActions = InsightUi.Row("planner.plan.actions").SetGap(AquacultureUiSpacing.Micro).Add(
                InsightUi.Button("planner.current", L("AquacultureFishing.PondPlannerCurrent"), () =>
                {
                    owner.LoadCurrentForUi();
                    document.Invalidate();
                }),
                InsightUi.Button("planner.blueprint", L("AquacultureFishing.PondPlannerBlueprint"), () =>
                {
                    owner.LoadBlueprintForUi();
                    document.Invalidate();
                }),
                InsightUi.Button("planner.clear", L("AquacultureFishing.PondPlannerClear"), () =>
                {
                    owner.ClearPlanForUi();
                    document.Invalidate();
                }));
            InsightUiElement plannedPanel = InsightUi.Column("planner.planned.column").SetGap(AquacultureUiSpacing.Row).Add(
                InsightUi.SectionHeader("planner.planned.header", L("AquacultureFishing.PondPlannerPlannedStock"),
                    L("AquacultureFishing.PondPlannerPlannedSubtitle"), null, null, true), waterSelect,
                plannedList, plannedEmptyLabel, InsightUi.Label("planner.planned.total", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => L("AquacultureFishing.PondPlannerTotals", planned.Sum(item => item.Value).ToString())), planActions,
                InsightUi.Button("planner.save", L("AquacultureFishing.PondPlannerSave"), () =>
                {
                    owner.SavePlanForUi();
                    document.Invalidate();
                }));

            // Grid chooses three columns when the host is wide and stacks them when it is narrow.
            // This keeps the branch responsive without doing layout work during Paint.
            InsightUiElement responsive = InsightUi.Grid("planner.responsive", 320f).Add(
                available, plannedPanel, forecastPanel);
            return AquacultureUiComponents.Panel("planner.root",
                InsightUi.Column("planner.root.content").SetGap(AquacultureUiSpacing.Section).SetPadding(4f).Add(
                    InsightUi.SectionHeader("planner.header", L("AquacultureFishing.PondPlannerTitle"),
                        L("AquacultureFishing.PondPlannerSubtitle"), null, null, true),
                    responsive));
        }

        private InsightUiElement BuildAvailableRow(ThingDef def)
        {
            string id = AquacultureUiStableIds.For("planner.available", def.defName);
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
            return InsightUi.Row(id).SetGap(AquacultureUiSpacing.Row).Add(
                InsightUi.Label(id + ".name", def.LabelCap),
                InsightUi.Label(id + ".meta", DietLabel(profile.diet) + "  •  " + WaterLabel(profile.waterKind),
                    InsightUiTextStyle.Caption),
                InsightUi.Spacer(id + ".space").SetFlex(1f),
                InsightUi.Button(id + ".add", "+", () =>
                {
                    owner.ChangeCountForUi(def, 1);
                    document.Invalidate();
                }));
        }

        private InsightUiElement BuildPlannedRow(KeyValuePair<ThingDef, int> entry)
        {
            string id = AquacultureUiStableIds.For("planner.planned", entry.Key.defName);
            return InsightUi.Row(id).SetGap(AquacultureUiSpacing.Row).Add(
                InsightUi.Label(id + ".name", entry.Key.LabelCap),
                InsightUi.Spacer(id + ".space").SetFlex(1f),
                InsightUi.Button(id + ".remove", "-", () =>
                {
                    owner.ChangeCountForUi(entry.Key, -1);
                    document.Invalidate();
                }), InsightUi.Label(id + ".count", entry.Value.ToString()),
                InsightUi.Button(id + ".add", "+", () =>
                {
                    owner.ChangeCountForUi(entry.Key, 1);
                    document.Invalidate();
                }));
        }

        private string forecastStatusText()
        {
            if (forecast == null) return L("AquacultureFishing.PondPlannerUnavailable");
            string state = forecast.Danger ? L("AquacultureFishing.PondPlannerUnsafe") :
                forecast.Warnings.Count > 0 ? L("AquacultureFishing.PondPlannerNeedsSupport") :
                L("AquacultureFishing.PondPlannerStable");
            return state + "  •  " + forecast.TotalFish + " / " + forecast.PhysicalCapacity;
        }

        private string forecastDetails()
        {
            if (forecast == null) return string.Empty;
            return L("AquacultureFishing.PondPlannerFoodDetails", forecast.DailyDemand.ToString("0.000"),
                forecast.FeedNeeded.ToString("0.000")) + "\n" +
                L("AquacultureFishing.PondPlannerRisk", forecast.PredationRisk ?
                    L("AquacultureFishing.WorkspaceYes") : L("AquacultureFishing.WorkspaceNo"));
        }

        private string forecastWarnings()
        {
            if (forecast == null || forecast.Warnings.Count == 0) return L("AquacultureFishing.PondPlannerCompatible");
            return string.Join("\n", forecast.Warnings.ToArray());
        }

        private static string L(string key) => key.Translate().ToString();

        private static string WaterLabel(PondWaterKind kind)
        {
            switch (kind)
            {
                case PondWaterKind.Saltwater: return L("AquacultureFishing.WorkspaceWaterSaltwater");
                case PondWaterKind.Brackishwater: return L("AquacultureFishing.WorkspaceWaterBrackishwater");
                default: return L("AquacultureFishing.WorkspaceWaterFreshwater");
            }
        }

        private static string DietLabel(FishDiet diet)
        {
            switch (diet)
            {
                case FishDiet.Carnivore: return L("AquacultureFishing.WorkspaceDietCarnivore");
                case FishDiet.Omnivore: return L("AquacultureFishing.WorkspaceDietOmnivore");
                case FishDiet.Detritivore: return L("AquacultureFishing.WorkspaceDietDetritivore");
                case FishDiet.FilterFeeder: return L("AquacultureFishing.WorkspaceDietFilterFeeder");
                case FishDiet.Herbivore: return L("AquacultureFishing.WorkspaceDietHerbivore");
                default: return L("AquacultureFishing.WorkspaceDietMixed");
            }
        }

        private static string L(string key, params NamedArgument[] args) =>
            TranslatorFormattedStringExtensions.Translate(key, args).ToString();
    }
}
