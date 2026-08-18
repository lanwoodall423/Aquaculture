using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    /// <summary>
    /// Document-owned Insight Canvas settings UI. It reads immutable catalog snapshots,
    /// binds directly to AquacultureSettings, and never performs map/world work during paint.
    /// </summary>
    public sealed class AquacultureSettingsDocument
    {
        private static readonly KnowledgeRank[] KnowledgeRanks = (KnowledgeRank[])Enum.GetValues(typeof(KnowledgeRank));
        private static readonly string[] KnowledgeRankLabels = KnowledgeRanks.Select(rank => rank.ToString()).ToArray();

        private readonly AquacultureSettings settings;
        private readonly Dictionary<string, ThingDef> fishDefinitions = new Dictionary<string, ThingDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, FishTraitDef> traitDefinitions = new Dictionary<string, FishTraitDef>(StringComparer.Ordinal);
        private readonly AquacultureUiSnapshotCache snapshotCache = new AquacultureUiSnapshotCache();
        private readonly SettingsUiSnapshot catalog;
        private readonly List<FishUiSnapshot> filteredFish = new List<FishUiSnapshot>();
        private readonly List<TraitUiSnapshot> filteredTraits = new List<TraitUiSnapshot>();

        private InsightUiVirtualList fishList;
        private InsightUiVirtualList traitList;
        private string fishSearch = string.Empty;
        private string traitSearch = string.Empty;
        private string activePage = "gameplay";
        private InsightUiDensity density = InsightUiDensity.Normal;
        private bool highContrast;
        private bool reducedMotion;
        private bool appearanceExpanded = true;
        private bool capacityWarningExpanded;
        private int uiRevision;

        public AquacultureSettingsDocument(AquacultureSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            BuildCatalog();
            catalog = new SettingsUiSnapshot(snapshotCache.Revision,
                new[] { "gameplay", "fishing", "ecology", "breeding", "appearance", "advanced" },
                BuildFishSnapshots(), BuildTraitSnapshots());
            filteredFish.AddRange(catalog.Fish);
            filteredTraits.AddRange(catalog.Traits);
            capacityWarningExpanded = settings.capacityTransitionWarning;

            InsightUiElement root = BuildRoot();
            Document = new InsightUiDocument("aquaculture.settings.v2", root)
            {
                Theme = AquacultureInsightTheme.Create(),
                Density = density,
                HighContrast = highContrast,
                ReducedMotion = reducedMotion,
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(Document);
        }

        public InsightUiDocument Document { get; private set; }
        public InsightUiHost Host { get; private set; }
        public int UiRevision => uiRevision;
        public int SnapshotRevision => snapshotCache.Revision;
        public SettingsUiSnapshot Catalog => catalog;

        public void Draw(Rect rect)
        {
            Host.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            Host.PostClose();
        }

        private void BuildCatalog()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(FishUtility.IsFish).OrderBy(def => def.label))
                if (def != null && !fishDefinitions.ContainsKey(def.defName)) fishDefinitions.Add(def.defName, def);

            foreach (FishTraitDef trait in DefDatabase<FishTraitDef>.AllDefsListForReading
                .Where(def => def != null && def.mutationEligible && !FishTraitUtility.IsDemographicOrEcology(def.defName))
                .OrderBy(def => def.kind).ThenBy(def => def.label))
                if (!traitDefinitions.ContainsKey(trait.defName)) traitDefinitions.Add(trait.defName, trait);
        }

        private IEnumerable<FishUiSnapshot> BuildFishSnapshots()
        {
            return fishDefinitions.Values.Select(def => new FishUiSnapshot(
                AquacultureUiStableIds.For("fish", def.defName), def.defName, def.LabelCap.ToString(),
                settings.MinimumExpertiseFor(def).ToString())).ToList();
        }

        private IEnumerable<TraitUiSnapshot> BuildTraitSnapshots()
        {
            return traitDefinitions.Values.Select(trait => new TraitUiSnapshot(
                AquacultureUiStableIds.For("trait", trait.defName), trait.defName, trait.LabelCap.ToString(),
                FishTraitUtility.EffectLine(trait), trait.description, trait.commonality)).ToList();
        }

        private InsightUiElement BuildRoot()
        {
            InsightUiNavigation navigation = InsightUi.Navigation("settings.navigation", 720f)
                .Bind(() => activePage, page =>
                {
                    activePage = page;
                    Invalidate();
                });
            navigation.Add("gameplay", L("AquacultureFishing.SettingsGameplay"), BuildGameplayPage());
            navigation.Add("fishing", L("AquacultureFishing.SettingsFishing"), BuildFishingPage());
            navigation.Add("ecology", L("AquacultureFishing.SettingsEcology"), BuildEcologyPage());
            navigation.Add("breeding", L("AquacultureFishing.SettingsBreeding"), BuildBreedingPage());
            navigation.Add("appearance", L("AquacultureFishing.SettingsAppearance"), BuildAppearancePage());
            navigation.Add("advanced", L("AquacultureFishing.SettingsAdvanced"), BuildAdvancedPage());
            navigation.SetFlex(1f);

            InsightUiSectionHeader header = InsightUi.SectionHeader("settings.title", L("AquacultureFishing.SettingsTitle"),
                L("AquacultureFishing.SettingsSubtitle"),
                InsightUiIcon.FromText("≈").WithAccessibleDescription(L("AquacultureFishing.SettingsAccessibleDescription")), null, true);
            return InsightUi.Column("settings.root").SetGap(10f).SetPadding(4f).Add(header, navigation);
        }

        private InsightUiElement BuildGameplayPage()
        {
            return AquacultureUiComponents.Page("page.gameplay", L("AquacultureFishing.SettingsGameplay"),
                L("AquacultureFishing.SettingsGameplaySubtitle"),
                AquacultureUiComponents.ToggleSetting("gameplay.research", L("AquacultureFishing.SettingsResearchProgression"),
                    L("AquacultureFishing.SettingsResearchProgressionTip"),
                    () => settings.enableResearchProgression, value => Set(() => settings.enableResearchProgression = value)),
                AquacultureUiComponents.ToggleSetting("gameplay.alerts", L("AquacultureFishing.SettingsPondAlerts"),
                    L("AquacultureFishing.SettingsPondAlertsTip"),
                    () => settings.showPondAlerts, value => Set(() => settings.showPondAlerts = value)),
                AquacultureUiComponents.SliderSetting("gameplay.duration", L("AquacultureFishing.SettingsFishingDuration"),
                    L("AquacultureFishing.SettingsFishingDurationTip"), 0.1f, 1f,
                    () => settings.fishingDurationFactor, value => Set(() => settings.fishingDurationFactor = value),
                    value => value.ToStringPercent()),
                AquacultureUiComponents.IntSliderSetting("gameplay.anima", L("AquacultureFishing.SettingsAnimaFishPerTree"),
                    L("AquacultureFishing.SettingsAnimaFishPerTreeTip"), 1, 100,
                    () => settings.animaFishPerTree, value => Set(() => settings.animaFishPerTree = value),
                    value => value.ToString()),
                InsightUi.Callout("gameplay.note", InsightUiCalloutSeverity.Info, L("AquacultureFishing.SettingsGameplayScope"),
                    L("AquacultureFishing.SettingsGameplayScopeBody")),
                AquacultureUiComponents.ResetButton("gameplay", L("AquacultureFishing.SettingsResetGameplay"), ResetGameplay));
        }

        private InsightUiElement BuildFishingPage()
        {
            InsightUiSearchField search = InsightUi.SearchField("fishing.search", string.Empty, L("AquacultureFishing.SettingsSearchFish"))
                .Bind(() => fishSearch, value =>
                {
                    fishSearch = value ?? string.Empty;
                    RefreshFishFilter();
                    Invalidate();
                });
            fishList = InsightUi.VirtualList("fishing.species.list", filteredFish.Count, 44f,
                index => BuildFishRow(filteredFish[index]));
            fishList.SetHeight(InsightLength.Fixed(340f));
            fishList.CacheLimit = 64;
            fishList.Overscan = 2;
            InsightUiStack content = InsightUi.Column("fishing.list.content").SetGap(8f).Add(
                InsightUi.Label("fishing.explanation", L("AquacultureFishing.SettingsFishingExplanation")),
                search,
                fishList);
            return content;
        }

        private InsightUiElement BuildFishRow(FishUiSnapshot fish)
        {
            ThingDef definition = fishDefinitions[fish.DefName];
            InsightUiSelect select = InsightUi.Select(AquacultureUiStableIds.For("fishing.expertise", fish.DefName),
                L("AquacultureFishing.SettingsExpertise"), KnowledgeRankLabels, RankIndex(settings.MinimumExpertiseFor(definition)));
            select.Bind(() => RankIndex(settings.MinimumExpertiseFor(definition)), index =>
            {
                if (index < 0 || index >= KnowledgeRanks.Length) return;
                Set(() => settings.GetFishExpertiseSetting(definition).minimumFishingExpertise = KnowledgeRanks[index]);
            });
            select.SetTooltip(L("AquacultureFishing.SettingsExpertiseTip"));
            return InsightUi.Row(AquacultureUiStableIds.For("fishing.row", fish.DefName))
                .SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Center)
                .Add(InsightUi.Label(AquacultureUiStableIds.For("fishing.label", fish.DefName), fish.Label).SetFlex(1f), select);
        }

        private InsightUiElement BuildEcologyPage()
        {
            InsightUiElement warning = InsightUi.Expander("ecology.capacity.transition", L("AquacultureFishing.SettingsCapacityTransition"),
                InsightUi.Column("ecology.capacity.transition.content").SetGap(6f).Add(
                    InsightUi.Label("ecology.capacity.transition.text", string.Empty).SetTextProvider(() =>
                        settings.capacityTransitionWarning
                            ? "AquacultureFishing.PondCapacityTransitionWarning".Translate(
                                settings.fishCapacityPerCell.ToString("0.##"),
                                PondCapacityRules.DefaultCapacityPerCell.ToString("0.##")).ToString()
                            : "AquacultureFishing.PondCapacityBaselineTooltip".Translate(
                                PondCapacityRules.DefaultCapacityPerCell,
                                PondCapacityRules.PoweredAeratorCapacity).ToString()),
                    InsightUi.Button("ecology.capacity.use-new", "AquacultureFishing.PondCapacityUseNewBaseline".Translate().ToString(), () =>
                    {
                        if (!settings.capacityTransitionWarning) return;
                        Set(() =>
                        {
                            settings.fishCapacityPerCell = PondCapacityRules.DefaultCapacityPerCell;
                            settings.capacityTransitionWarning = false;
                        });
                    }),
                    InsightUi.Button("ecology.capacity.keep-existing", "AquacultureFishing.PondCapacityKeepExisting".Translate().ToString(), () =>
                    {
                        if (settings.capacityTransitionWarning) Set(() => settings.capacityTransitionWarning = false);
                    })), false)
                .Bind(() => capacityWarningExpanded, value =>
                {
                    capacityWarningExpanded = value;
                    Invalidate();
                });

            return AquacultureUiComponents.Page("page.ecology", L("AquacultureFishing.SettingsEcology"),
                L("AquacultureFishing.SettingsEcologySubtitle"),
                AquacultureUiComponents.SliderSetting("ecology.interval", L("AquacultureFishing.SettingsSimulationInterval"), L("AquacultureFishing.SettingsSimulationIntervalTip"),
                    0.25f, 6f, () => settings.ecologyIntervalHours, value => Set(() => settings.ecologyIntervalHours = value),
                    value => L("AquacultureFishing.SettingsHoursValue", value.ToString("0.##"))),
                AquacultureUiComponents.SliderSetting("ecology.food", L("AquacultureFishing.SettingsFoodDemand"), L("AquacultureFishing.SettingsFoodDemandTip"),
                    0.1f, 5f, () => settings.foodDemandMultiplier, value => Set(() => settings.foodDemandMultiplier = value),
                    value => L("AquacultureFishing.SettingsMultiplierValue", value.ToString("0.00"))),
                AquacultureUiComponents.SliderSetting("ecology.algae", L("AquacultureFishing.SettingsAlgaeGrowth"), L("AquacultureFishing.SettingsAlgaeGrowthTip"),
                    0f, 5f, () => settings.algaeGrowthMultiplier, value => Set(() => settings.algaeGrowthMultiplier = value),
                    value => L("AquacultureFishing.SettingsMultiplierValue", value.ToString("0.00"))),
                AquacultureUiComponents.SliderSetting("ecology.starvation", L("AquacultureFishing.SettingsStarvationTime"), L("AquacultureFishing.SettingsStarvationTimeTip"),
                    6f, 600f, () => settings.starvationHours, value => Set(() => settings.starvationHours = value),
                    value => L("AquacultureFishing.SettingsHoursValue", Mathf.RoundToInt(value))),
                AquacultureUiComponents.SliderSetting("ecology.water-stress", L("AquacultureFishing.SettingsWaterStress"), L("AquacultureFishing.SettingsWaterStressTip"),
                    1f, 240f, () => settings.waterStressHours, value => Set(() => settings.waterStressHours = value),
                    value => L("AquacultureFishing.SettingsHoursValue", Mathf.RoundToInt(value))),
                AquacultureUiComponents.SliderSetting("ecology.temperature-stress", L("AquacultureFishing.SettingsTemperatureStress"), L("AquacultureFishing.SettingsTemperatureStressTip"),
                    1f, 240f, () => settings.temperatureStressHours, value => Set(() => settings.temperatureStressHours = value),
                    value => L("AquacultureFishing.SettingsHoursValue", Mathf.RoundToInt(value))),
                AquacultureUiComponents.SliderSetting("ecology.capacity", "AquacultureFishing.PondBaseCapacitySetting".Translate().ToString(),
                    "AquacultureFishing.PondCapacityBaselineTooltip".Translate(
                        PondCapacityRules.DefaultCapacityPerCell, PondCapacityRules.PoweredAeratorCapacity).ToString(),
                    0.25f, 10f, () => settings.fishCapacityPerCell, value => Set(() => settings.fishCapacityPerCell = value),
                    value => value.ToString("0.00")),
                warning,
                AquacultureUiComponents.SliderSetting("ecology.feed-value", L("AquacultureFishing.SettingsFeedValue"), L("AquacultureFishing.SettingsFeedValueTip"),
                    0.005f, 0.25f, () => settings.feedValuePerUnit, value => Set(() => settings.feedValuePerUnit = value),
                    value => value.ToString("0.000")),
                AquacultureUiComponents.SliderSetting("ecology.feeder-range", L("AquacultureFishing.SettingsFeederRange"), L("AquacultureFishing.SettingsFeederRangeTip"),
                    1f, 30f, () => settings.feederRange, value => Set(() => settings.feederRange = value),
                    value => L("AquacultureFishing.SettingsCellsValue", Mathf.RoundToInt(value))),
                AquacultureUiComponents.ToggleSetting("ecology.predation", L("AquacultureFishing.SettingsGlobalPredation"),
                    L("AquacultureFishing.SettingsGlobalPredationTip"),
                    () => settings.predationEnabled, value => Set(() => settings.predationEnabled = value)),
                AquacultureUiComponents.ResetButton("ecology", L("AquacultureFishing.SettingsResetEcology"), ResetEcology));
        }

        private InsightUiElement BuildBreedingPage()
        {
            return AquacultureUiComponents.Page("page.breeding", L("AquacultureFishing.SettingsBreeding"),
                L("AquacultureFishing.SettingsBreedingSubtitle"),
                AquacultureUiComponents.ToggleSetting("breeding.enabled", L("AquacultureFishing.SettingsEnableBreeding"),
                    L("AquacultureFishing.SettingsEnableBreedingTip"),
                    () => settings.globalBreedingEnabled, value => Set(() => settings.globalBreedingEnabled = value)),
                AquacultureUiComponents.SliderSetting("breeding.interval", L("AquacultureFishing.SettingsBreedingInterval"), L("AquacultureFishing.SettingsBreedingIntervalTip"),
                    1f, 60f, () => settings.breedingIntervalDays, value => Set(() => settings.breedingIntervalDays = value),
                    value => L("AquacultureFishing.SettingsDaysValue", Mathf.RoundToInt(value))),
                AquacultureUiComponents.SliderSetting("breeding.hatch", L("AquacultureFishing.SettingsEggHatch"), L("AquacultureFishing.SettingsEggHatchTip"),
                    0.25f, 30f, () => settings.eggHatchDays, value => Set(() => settings.eggHatchDays = value),
                    value => L("AquacultureFishing.SettingsDaysValue", value.ToString("0.##"))),
                AquacultureUiComponents.IntSliderSetting("breeding.minimum-offspring", L("AquacultureFishing.SettingsMinimumOffspring"), L("AquacultureFishing.SettingsMinimumOffspringTip"),
                    1, 10, () => settings.minimumOffspring, value => Set(() =>
                    {
                        settings.minimumOffspring = value;
                        settings.maximumOffspring = Mathf.Max(settings.minimumOffspring, settings.maximumOffspring);
                    }), value => value.ToString()),
                AquacultureUiComponents.IntSliderSetting("breeding.maximum-offspring", L("AquacultureFishing.SettingsMaximumOffspring"), L("AquacultureFishing.SettingsMaximumOffspringTip"),
                    1, 12, () => settings.maximumOffspring, value => Set(() =>
                    {
                        settings.maximumOffspring = Mathf.Max(settings.minimumOffspring, value);
                    }), value => value.ToString()),
                AquacultureUiComponents.IntSliderSetting("breeding.minimum-lifespan", L("AquacultureFishing.SettingsMinimumLifespan"), L("AquacultureFishing.SettingsMinimumLifespanTip"),
                    5, 600, () => settings.minimumLifespanDays, value => Set(() =>
                    {
                        settings.minimumLifespanDays = value;
                        settings.maximumLifespanDays = Mathf.Max(settings.minimumLifespanDays, settings.maximumLifespanDays);
                    }), value => L("AquacultureFishing.SettingsDaysValue", value)),
                AquacultureUiComponents.IntSliderSetting("breeding.maximum-lifespan", L("AquacultureFishing.SettingsMaximumLifespan"), L("AquacultureFishing.SettingsMaximumLifespanTip"),
                    5, 1200, () => settings.maximumLifespanDays, value => Set(() =>
                    {
                        settings.maximumLifespanDays = Mathf.Max(settings.minimumLifespanDays, value);
                    }), value => L("AquacultureFishing.SettingsDaysValue", value)),
                AquacultureUiComponents.ResetButton("breeding", L("AquacultureFishing.SettingsResetBreeding"), ResetBreeding));
        }

        private InsightUiElement BuildAppearancePage()
        {
            InsightUiStack details = InsightUi.Column("appearance.details.content").SetGap(6f).Add(
                AquacultureUiComponents.ToggleSetting("appearance.water-type", L("AquacultureFishing.SettingsWaterTypeTint"), L("AquacultureFishing.SettingsWaterTypeTintTip"),
                    () => settings.waterTypeTint, value => Set(() => settings.waterTypeTint = value)),
                AquacultureUiComponents.ToggleSetting("appearance.algae", L("AquacultureFishing.SettingsAlgaeTint"), L("AquacultureFishing.SettingsAlgaeTintTip"),
                    () => settings.algaeTint, value => Set(() => settings.algaeTint = value)),
                AquacultureUiComponents.ToggleSetting("appearance.depth", L("AquacultureFishing.SettingsDepthShading"), L("AquacultureFishing.SettingsDepthShadingTip"),
                    () => settings.depthShading, value => Set(() => settings.depthShading = value)),
                AquacultureUiComponents.ToggleSetting("appearance.shoreline", L("AquacultureFishing.SettingsShoreline"), L("AquacultureFishing.SettingsShorelineTip"),
                    () => settings.shorelineVisuals, value => Set(() => settings.shorelineVisuals = value)),
                AquacultureUiComponents.ToggleSetting("appearance.decorations", L("AquacultureFishing.SettingsDecorations"), L("AquacultureFishing.SettingsDecorationsTip"),
                    () => settings.pondDecorations, value => Set(() => settings.pondDecorations = value)),
                AquacultureUiComponents.ToggleSetting("appearance.shadows", L("AquacultureFishing.SettingsFishShadows"), L("AquacultureFishing.SettingsFishShadowsTip"),
                    () => settings.fishShadows, value => Set(() => settings.fishShadows = value)),
                AquacultureUiComponents.ToggleSetting("appearance.underwater", L("AquacultureFishing.SettingsUnderwaterFishTint"), L("AquacultureFishing.SettingsUnderwaterFishTintTip"),
                    () => settings.underwaterFishTint, value => Set(() => settings.underwaterFishTint = value)),
                AquacultureUiComponents.ToggleSetting("appearance.surface", L("AquacultureFishing.SettingsSurfaceOverlay"), L("AquacultureFishing.SettingsSurfaceOverlayTip"),
                    () => settings.surfaceOverlay, value => Set(() => settings.surfaceOverlay = value)),
                AquacultureUiComponents.ToggleSetting("appearance.caustics", L("AquacultureFishing.SettingsCaustics"), L("AquacultureFishing.SettingsCausticsTip"),
                    () => settings.caustics, value => Set(() => settings.caustics = value)),
                AquacultureUiComponents.ToggleSetting("appearance.depth-motion", L("AquacultureFishing.SettingsPseudoDepth"), L("AquacultureFishing.SettingsPseudoDepthTip"),
                    () => settings.pseudoDepth, value => Set(() => settings.pseudoDepth = value)),
                AquacultureUiComponents.ToggleSetting("appearance.ripples", L("AquacultureFishing.SettingsRipples"), L("AquacultureFishing.SettingsRipplesTip"),
                    () => settings.pondRipples, value => Set(() => settings.pondRipples = value)));
            InsightUiExpander expander = InsightUi.Expander("appearance.details", L("AquacultureFishing.SettingsAdditionalPondVisuals"), details, true)
                .Bind(() => appearanceExpanded, value =>
                {
                    appearanceExpanded = value;
                    Invalidate();
                });
            return AquacultureUiComponents.Page("page.appearance", L("AquacultureFishing.SettingsAppearance"),
                L("AquacultureFishing.SettingsAppearanceSubtitle"),
                AquacultureUiComponents.ToggleSetting("appearance.enhanced", L("AquacultureFishing.SettingsEnhancedPondVisuals"),
                    L("AquacultureFishing.SettingsEnhancedPondVisualsTip"),
                    () => settings.enhancedPondVisuals, value => Set(() => settings.enhancedPondVisuals = value)),
                expander,
                InsightUi.Button("appearance.masks.open", L("AquacultureFishing.SettingsOpenMaskPainter"), () => Find.WindowStack.Add(new Dialog_FishMaskPainter()))
                    .SetTooltip(L("AquacultureFishing.SettingsMaskPainterTip")));
        }

        private InsightUiElement BuildAdvancedPage()
        {
            InsightUiSearchField search = InsightUi.SearchField("advanced.traits.search", string.Empty, L("AquacultureFishing.SettingsSearchTraits"))
                .Bind(() => traitSearch, value =>
                {
                    traitSearch = value ?? string.Empty;
                    RefreshTraitFilter();
                    Invalidate();
                });
            traitList = InsightUi.VirtualList("advanced.traits.list", filteredTraits.Count, 76f,
                index => BuildTraitRow(filteredTraits[index]));
            traitList.SetHeight(InsightLength.Fixed(360f));
            traitList.CacheLimit = 64;
            InsightUiSegmented densitySelector = InsightUi.Segmented("advanced.density",
                new[] { L("AquacultureFishing.SettingsDensityComfortable"), L("AquacultureFishing.SettingsDensityNormal"), L("AquacultureFishing.SettingsDensityCompact") }, (int)density,
                (index, value) =>
                {
                    density = (InsightUiDensity)Mathf.Clamp(index, 0, 2);
                    Document.Density = density;
                    Document.Invalidate();
                }).Bind(() => (int)density, index =>
                {
                    density = (InsightUiDensity)Mathf.Clamp(index, 0, 2);
                    Document.Density = density;
                    Document.Invalidate();
                });
            return AquacultureUiComponents.Page("page.advanced", L("AquacultureFishing.SettingsAdvanced"),
                L("AquacultureFishing.SettingsAdvancedSubtitle"),
                AquacultureUiComponents.SliderSetting("advanced.wild", "AquacultureFishing.TraitWildExceptionalChance".Translate().ToString(), L("AquacultureFishing.SettingsWildExceptionalTip"),
                    0f, 1f, () => settings.wildExceptionalTraitChance, value => Set(() => settings.wildExceptionalTraitChance = value),
                    value => value.ToStringPercent()),
                AquacultureUiComponents.SliderSetting("advanced.parental", "AquacultureFishing.TraitParentalInheritanceChance".Translate().ToString(), L("AquacultureFishing.SettingsParentalInheritanceTip"),
                    0f, 1f, () => settings.parentalTraitInheritanceChance, value => Set(() => settings.parentalTraitInheritanceChance = value),
                    value => value.ToStringPercent()),
                AquacultureUiComponents.IntSliderSetting("advanced.inherited-cap", "AquacultureFishing.TraitMaximumInherited".Translate().ToString(), L("AquacultureFishing.SettingsMaximumInheritedTip"),
                    0, 8, () => settings.maxInheritedTraits, value => Set(() => settings.maxInheritedTraits = value), value => value.ToString()),
                AquacultureUiComponents.SliderSetting("advanced.mutation", "AquacultureFishing.TraitOffspringMutationChance".Translate().ToString(), L("AquacultureFishing.SettingsOffspringMutationTip"),
                    0f, 1f, () => settings.offspringMutationChance, value => Set(() => settings.offspringMutationChance = value),
                    value => value.ToStringPercent()),
                AquacultureUiComponents.IntSliderSetting("advanced.mutation-cap", "AquacultureFishing.TraitMaximumOffspringMutations".Translate().ToString(), L("AquacultureFishing.SettingsMaximumMutationsTip"),
                    0, 8, () => settings.maxOffspringMutations, value => Set(() => settings.maxOffspringMutations = value), value => value.ToString()),
                AquacultureUiComponents.SliderSetting("advanced.reliability", "AquacultureFishing.TraitRegisteredBreedReliability".Translate().ToString(), L("AquacultureFishing.SettingsBreedReliabilityTip"),
                    0f, 1f, () => settings.registeredBreedDefiningTraitReliability, value => Set(() => settings.registeredBreedDefiningTraitReliability = value),
                    value => value.ToStringPercent()),
                InsightUi.Label("advanced.traits.explanation", "AquacultureFishing.TraitSettingsExplanation".Translate().ToString()),
                search,
                traitList,
                InsightUi.SectionHeader("advanced.presentation.header", L("AquacultureFishing.SettingsPresentation"), L("AquacultureFishing.SettingsPresentationSubtitle")),
                densitySelector,
                AquacultureUiComponents.ToggleSetting("advanced.contrast", L("AquacultureFishing.SettingsHighContrast"), L("AquacultureFishing.SettingsHighContrastTip"),
                    () => highContrast, value =>
                    {
                        highContrast = value;
                        Document.HighContrast = value;
                        Document.Invalidate();
                    }),
                AquacultureUiComponents.ToggleSetting("advanced.motion", L("AquacultureFishing.SettingsReducedMotion"), L("AquacultureFishing.SettingsReducedMotionTip"),
                    () => reducedMotion, value =>
                    {
                        reducedMotion = value;
                        Document.ReducedMotion = value;
                        Document.Invalidate();
                    }),
                InsightUi.Callout("advanced.migration", InsightUiCalloutSeverity.Info, L("AquacultureFishing.SettingsMigrationBoundary"),
                    L("AquacultureFishing.SettingsMigrationBoundaryBody")),
                AquacultureUiComponents.ResetButton("advanced", L("AquacultureFishing.SettingsResetTraitRules"), ResetTraitRules));
        }

        private InsightUiElement BuildTraitRow(TraitUiSnapshot trait)
        {
            FishTraitDef definition = traitDefinitions[trait.DefName];
            InsightUiElement enabled = AquacultureUiComponents.ToggleSetting(
                    AquacultureUiStableIds.For("advanced.trait.enabled", trait.DefName), trait.Label,
                trait.EffectLine + " " + trait.Description,
                () => settings.GetTraitSetting(definition, false)?.enabled ?? true,
                value => Set(() => settings.GetTraitSetting(definition).enabled = value));
            return InsightUi.Column(AquacultureUiStableIds.For("advanced.trait.row", trait.DefName)).SetGap(4f).Add(
                enabled,
                AquacultureUiComponents.SliderSetting(
                    AquacultureUiStableIds.For("advanced.trait.weight", trait.DefName), L("AquacultureFishing.SettingsTraitWeight"), trait.Description,
                    0f, 5f,
                    () => settings.GetTraitSetting(definition, false)?.weight ?? trait.DefaultWeight,
                    value => Set(() => settings.GetTraitSetting(definition).weight = value),
                    value => value.ToString("0.00")));
        }

        private void RefreshFishFilter()
        {
            filteredFish.Clear();
            filteredFish.AddRange(catalog.Fish.Where(fish => string.IsNullOrEmpty(fishSearch) ||
                fish.Label.IndexOf(fishSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                fish.DefName.IndexOf(fishSearch, StringComparison.OrdinalIgnoreCase) >= 0));
            if (fishList != null)
            {
                fishList.ItemCount = filteredFish.Count;
                fishList.Refresh();
            }
        }

        private void RefreshTraitFilter()
        {
            filteredTraits.Clear();
            filteredTraits.AddRange(catalog.Traits.Where(trait => string.IsNullOrEmpty(traitSearch) ||
                trait.Label.IndexOf(traitSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                trait.DefName.IndexOf(traitSearch, StringComparison.OrdinalIgnoreCase) >= 0));
            if (traitList != null)
            {
                traitList.ItemCount = filteredTraits.Count;
                traitList.Refresh();
            }
        }

        private int RankIndex(KnowledgeRank rank)
        {
            int index = Array.IndexOf(KnowledgeRanks, rank);
            return index < 0 ? 0 : index;
        }

        private void Set(Action action)
        {
            action?.Invoke();
            Invalidate();
        }

        private void Invalidate()
        {
            uiRevision++;
            snapshotCache.Invalidate();
            Document?.Invalidate();
        }

        private void ResetGameplay()
        {
            Set(() =>
            {
                settings.enableResearchProgression = true;
                settings.showPondAlerts = true;
                settings.fishingDurationFactor = 0.30f;
                settings.animaFishPerTree = 20;
            });
        }

        private void ResetEcology()
        {
            Set(() =>
            {
                settings.ecologyIntervalHours = 1f;
                settings.foodDemandMultiplier = 1f;
                settings.algaeGrowthMultiplier = 1f;
                settings.starvationHours = 96f;
                settings.waterStressHours = 12f;
                settings.temperatureStressHours = 48f;
                settings.fishCapacityPerCell = PondCapacityRules.DefaultCapacityPerCell;
                settings.capacityTransitionWarning = false;
                settings.capacityModelVersion = AquacultureSettings.CurrentCapacityModelVersion;
                settings.predationEnabled = true;
                settings.feedValuePerUnit = 0.05f;
                settings.feederRange = 6f;
            });
        }

        private void ResetBreeding()
        {
            Set(() =>
            {
                settings.globalBreedingEnabled = true;
                settings.breedingIntervalDays = 15f;
                settings.eggHatchDays = 3f;
                settings.minimumOffspring = 1;
                settings.maximumOffspring = 3;
                settings.minimumLifespanDays = 48;
                settings.maximumLifespanDays = 72;
            });
        }

        private void ResetTraitRules()
        {
            Set(() =>
            {
                settings.wildExceptionalTraitChance = AquacultureSettings.DefaultWildExceptionalTraitChance;
                settings.parentalTraitInheritanceChance = AquacultureSettings.DefaultParentalTraitInheritanceChance;
                settings.maxInheritedTraits = AquacultureSettings.DefaultMaximumInheritedTraits;
                settings.offspringMutationChance = AquacultureSettings.DefaultOffspringMutationChance;
                settings.maxOffspringMutations = AquacultureSettings.DefaultMaximumOffspringMutations;
                settings.registeredBreedDefiningTraitReliability = AquacultureSettings.DefaultRegisteredBreedDefiningTraitReliability;
            });
        }

        private static string L(string key, params NamedArgument[] args)
        {
            return TranslatorFormattedStringExtensions.Translate(key, args).ToString();
        }
    }
}
