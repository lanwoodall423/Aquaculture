using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    /// <summary>
    /// The Journal main tab's document-owned workspace. Gameplay components remain the authority;
    /// this class captures their display data before Host.Draw and keeps Paint map-free.
    /// </summary>
    public sealed class AquacultureJournalWorkspaceDocument
    {
        private readonly InsightUiDocument document;
        private readonly AquacultureUiSnapshotCache uiCache = new AquacultureUiSnapshotCache();
        private readonly List<PondEntry> ponds = new List<PondEntry>();
        private readonly List<PondEntry> filteredPonds = new List<PondEntry>();
        private readonly List<PondEntry> actionablePonds = new List<PondEntry>();
        private readonly List<AquacultureSpeciesViewSnapshot> species = new List<AquacultureSpeciesViewSnapshot>();
        private readonly List<AquacultureSpeciesViewSnapshot> filteredSpecies = new List<AquacultureSpeciesViewSnapshot>();
        private readonly List<FishBreedRecord> breeds = new List<FishBreedRecord>();
        private readonly List<FishBreedRecord> filteredBreeds = new List<FishBreedRecord>();
        private readonly List<NaturalWaterViewSnapshot> waters = new List<NaturalWaterViewSnapshot>();
        private readonly List<NaturalWaterViewSnapshot> filteredWaters = new List<NaturalWaterViewSnapshot>();
        private readonly Dictionary<string, string> breedTraitText = new Dictionary<string, string>(StringComparer.Ordinal);

        private Map capturedMap;
        private int capturedRevision = -1;
        private int filterRevision = -1;
        private string activePage = "overview";
        private string selectedPondId;
        private string selectedSpeciesId;
        private string selectedBreedId;
        private string selectedWaterId;
        private string pondSearch = string.Empty;
        private string speciesSearch = string.Empty;
        private string breedSearch = string.Empty;
        private string waterSearch = string.Empty;
        private AquaculturePondFilter pondFilter = AquaculturePondFilter.All;
        private string knowledgeExpertiseText = string.Empty;
        private CommissionUiSnapshot commissionSnapshot;
        private bool pondkeepingAvailable;
        private bool managedAquacultureAvailable;
        private bool industrialAquacultureAvailable;
        private bool selectiveBreedingAvailable;

        private InsightUiVirtualList pondList;
        private InsightUiVirtualList overviewPriorityList;
        private InsightUiVirtualList speciesList;
        private InsightUiVirtualList breedList;
        private InsightUiVirtualList waterList;
        private InsightUiMeter pondPopulationMeter;
        private InsightUiMeter pondFeedMeter;
        private InsightUiToggle adultsOnlyToggle;
        private InsightUiToggle protectFemalesToggle;
        private InsightUiToggle automaticFeedingToggle;
        private InsightUiToggle predationToggle;
        private InsightUiToggle surplusHarvestToggle;
        private InsightUiSlider feedDaysSlider;
        private InsightUiSlider populationLimitSlider;
        private InsightUiSlider populationTargetSlider;
        private InsightUiSelect breedingModeSelect;
        private InsightUiSelect pondFilterSelect;
        private InsightUiSplit pondsSplit;
        private InsightUiSplit speciesSplit;
        private InsightUiSplit breedsSplit;
        private InsightUiCallout overviewAttentionCallout;
        private InsightUiLabel overviewPriorityEmptyLabel;
        private InsightUiLabel pondEmptyLabel;
        private InsightUiButton pondEmptyAction;
        private InsightUiLabel speciesEmptyLabel;
        private InsightUiLabel breedEmptyLabel;
        private InsightUiLabel waterEmptyLabel;
        private InsightUiCallout pondHealthCallout;
        private InsightUiCallout pondResearchCallout;
        private InsightUiButton pondFocusButton;
        private InsightUiButton pondPlannerButton;
        private InsightUiCallout commissionStatusCallout;

        private const float CompactSplitBreakpoint = 960f;

        private static readonly string[] BreedingModeNames = Enum.GetNames(typeof(PondBreedingMode));

        public AquacultureJournalWorkspaceDocument()
        {
            document = new InsightUiDocument("aquaculture.journal.workspace.v2", BuildRoot())
            {
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(document);
            AquacultureInsightPresentation.Apply(document);
        }

        public InsightUiDocument Document => document;
        public InsightUiHost Host { get; private set; }
        public int SnapshotRevision => uiCache.Revision;

        public void Draw(Rect rect)
        {
            RefreshSnapshots();
            CaptureResearchAvailability();
            UpdateDynamicControls();
            UpdateResponsiveLayout(rect.width);
            Host.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            Host.PostClose();
        }

        public void SelectPage(string page)
        {
            if (string.IsNullOrEmpty(page)) return;
            activePage = page;
            document.Invalidate();
        }

        public void SelectSpecies(string defName)
        {
            if (defName.NullOrEmpty()) return;
            selectedSpeciesId = AquacultureUiStableIds.For("species", defName);
            activePage = "species";
            document.Invalidate();
        }

        private void UpdateResponsiveLayout(float width)
        {
            bool compact = AquacultureUiResponsiveLayout.MasterDetailMode(width, CompactSplitBreakpoint) ==
                AquacultureUiMasterDetailMode.Stacked;
            InsightUiSplit[] splits = { pondsSplit, speciesSplit, breedsSplit };
            for (int i = 0; i < splits.Length; i++)
            {
                InsightUiSplit split = splits[i];
                if (split == null) continue;
                split.Orientation = compact
                    ? InsightUiOrientation.Vertical
                    : InsightUiOrientation.Horizontal;
            }
        }

        private void RefreshSnapshots()
        {
            Map map = Find.CurrentMap;
            int revision = AquacultureSnapshotCache.Revision;
            if (ReferenceEquals(map, capturedMap) && revision == capturedRevision) return;

            capturedMap = map;
            capturedRevision = revision;
            uiCache.Invalidate();
            AquacultureJournalViewSnapshot journal = AquacultureSnapshotCache.Journal(null, true);
            species.Clear();
            species.AddRange(journal?.species ?? Array.Empty<AquacultureSpeciesViewSnapshot>());
            knowledgeExpertiseText = CaptureKnowledgeExpertise();
            breeds.Clear();
            breeds.AddRange((journal?.breeds ?? Array.Empty<FishBreedRecord>())
                .Where(item => item != null && !item.id.NullOrEmpty())
                .OrderBy(item => item.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.id, StringComparer.Ordinal));
            breedTraitText.Clear();
            foreach (FishBreedRecord breed in breeds)
                breedTraitText[breed.id] = FormatBreedTraits(breed);
            commissionSnapshot = CaptureCommissionSnapshot(revision);

            ponds.Clear();
            FishPondMapComponent pondComponent = map?.GetComponent<FishPondMapComponent>();
            if (map != null && pondComponent != null)
            {
                List<PondProxyThing> proxies = map.listerThings.AllThings.OfType<PondProxyThing>()
                    .OrderBy(proxy => proxy.Position.z).ThenBy(proxy => proxy.Position.x).ToList();
                for (int i = 0; i < proxies.Count; i++)
                {
                    PondProxyThing proxy = proxies[i];
                    PondMenuSnapshot raw = pondComponent.MenuSnapshotAt(proxy.Position);
                    if (raw == null) continue;
                    ponds.Add(CapturePond(proxy, pondComponent, raw, i, revision));
                }
            }
            ponds.Sort((left, right) => AquacultureWorkspaceOrdering.Compare(left.Status, right.Status));
            actionablePonds.Clear();
            actionablePonds.AddRange(ponds.Where(item => item.Priority != AquacultureHealthPriority.Healthy));

            waters.Clear();
            waters.AddRange((AquacultureSnapshotCache.Waters(map) ?? Array.Empty<NaturalWaterViewSnapshot>())
                .OrderBy(item => item.anchor.z).ThenBy(item => item.anchor.x));

            selectedPondId = AquacultureWorkspaceOrdering.PreserveSelection(selectedPondId,
                ponds.Select(item => item.StableId));
            selectedSpeciesId = AquacultureWorkspaceOrdering.PreserveSelection(selectedSpeciesId,
                species.Select(SpeciesId));
            selectedBreedId = AquacultureWorkspaceOrdering.PreserveSelection(selectedBreedId,
                breeds.Select(item => item.id));
            selectedWaterId = AquacultureWorkspaceOrdering.PreserveSelection(selectedWaterId,
                waters.Select(WaterId));
            filterRevision = -1;
            RefreshFilters();
        }

        private PondEntry CapturePond(PondProxyThing proxy, FishPondMapComponent component,
            PondMenuSnapshot raw, int ordinal, int revision)
        {
            List<PondCausalIssue> issues = raw.causalSummary?.issues ?? new List<PondCausalIssue>();
            bool lethal = issues.Any(issue => issue?.priority == PondCausalPriority.Lethal);
            bool starvation = issues.Any(issue => issue?.priority == PondCausalPriority.Starvation);
            bool severeStress = issues.Any(issue => issue?.priority == PondCausalPriority.SevereStress);
            bool reproduction = issues.Any(issue => issue?.priority == PondCausalPriority.Reproduction);
            bool advice = issues.Any(issue => issue?.priority == PondCausalPriority.Advice);
            AquacultureHealthPriority priority = AquacultureHealthPriorityRules.Highest(
                lethal, starvation, severeStress, reproduction, advice);
            List<string> details = issues.Where(issue => issue != null)
                .OrderBy(issue => (int)issue.priority).ThenBy(issue => issue.key, StringComparer.Ordinal)
                .Select(issue => IssueText(issue)).Where(text => !text.NullOrEmpty()).ToList();
            if (details.Count == 0) details.Add(L("AquacultureFishing.WorkspacePondHealthy"));
            PondUiSnapshot display = new PondUiSnapshot(
                AquacultureUiStableIds.For("pond", proxy.Position.x + "." + proxy.Position.z),
                L("AquacultureFishing.PondLabel") + " " + (ordinal + 1),
                PondWaterLabel(component.WaterKindAt(proxy.Position)), raw.population, raw.capacity,
                raw.feedHours, raw.temperature, revision);
            AquacultureWorkspaceRowSnapshot row = new AquacultureWorkspaceRowSnapshot(
                display.StableId, display.Label, PriorityLabel(priority), priority, details, revision);
            return new PondEntry(proxy.Position, component, display, row);
        }

        private static string IssueText(PondCausalIssue issue)
        {
            if (issue == null) return string.Empty;
            return !issue.text.NullOrEmpty() ? issue.text : issue.textFactory?.Invoke(issue.affectedFish) ?? issue.key;
        }

        private void RefreshFilters()
        {
            if (filterRevision == capturedRevision) return;
            filterRevision = capturedRevision;
            filteredPonds.Clear();
            foreach (PondEntry item in ponds)
                if (AquacultureWorkspaceOrdering.Matches(item.Status, pondSearch, pondFilter)) filteredPonds.Add(item);
            FilterInto(species, filteredSpecies, speciesSearch, SpeciesLabel);
            FilterInto(breeds, filteredBreeds, breedSearch, item => item.name);
            FilterInto(waters, filteredWaters, waterSearch, item => item.habitat.ToString());
            if (overviewPriorityList != null)
            {
                overviewPriorityList.ItemCount = actionablePonds.Count;
                overviewPriorityList.Refresh();
            }
            if (pondList != null)
            {
                pondList.ItemCount = filteredPonds.Count;
                pondList.Refresh();
            }
            if (speciesList != null)
            {
                speciesList.ItemCount = filteredSpecies.Count;
                speciesList.Refresh();
            }
            if (breedList != null)
            {
                breedList.ItemCount = filteredBreeds.Count;
                breedList.Refresh();
            }
            if (waterList != null)
            {
                waterList.ItemCount = filteredWaters.Count;
                waterList.Refresh();
            }
            UpdateEmptyState(pondEmptyLabel, pondEmptyAction, filteredPonds.Count == 0,
                ponds.Count == 0 ? L("AquacultureFishing.WorkspaceNoPonds") :
                    L("AquacultureFishing.WorkspaceNoMatchingPonds"), ponds.Count > 0);
            UpdateEmptyState(speciesEmptyLabel, null, filteredSpecies.Count == 0,
                species.Count == 0 ? L("AquacultureFishing.WorkspaceNoSpecies") :
                    L("AquacultureFishing.WorkspaceNoMatchingSpecies"), false);
            UpdateEmptyState(breedEmptyLabel, null, filteredBreeds.Count == 0,
                breeds.Count == 0 ? L("AquacultureFishing.WorkspaceNoBreeds") :
                    L("AquacultureFishing.WorkspaceNoMatchingBreeds"), false);
            UpdateEmptyState(waterEmptyLabel, null, filteredWaters.Count == 0,
                waters.Count == 0 ? L("AquacultureFishing.WorkspaceNoWaters") :
                    L("AquacultureFishing.WorkspaceNoMatchingWaters"), false);
            if (overviewPriorityEmptyLabel != null)
                overviewPriorityEmptyLabel.Visible = actionablePonds.Count == 0;
        }

        private static void UpdateEmptyState(InsightUiLabel label, InsightUiButton action, bool visible,
            string message, bool canClear)
        {
            if (label != null)
            {
                label.Text = message ?? string.Empty;
                label.Visible = visible;
            }
            if (action != null) action.Visible = visible && canClear;
        }

        private static void FilterInto<T>(IEnumerable<T> source, List<T> target, string search, Func<T, string> label)
        {
            target.Clear();
            string query = search ?? string.Empty;
            foreach (T item in source ?? Enumerable.Empty<T>())
            {
                string value = label(item) ?? string.Empty;
                if (query.NullOrEmpty() || value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) target.Add(item);
            }
        }

        private InsightUiElement BuildRoot()
        {
            InsightUiNavigation navigation = InsightUi.Navigation("workspace.navigation", 720f)
                .Bind(() => activePage, page =>
                {
                    activePage = page;
                    document.Invalidate();
                });
            navigation.Add("overview", L("AquacultureFishing.WorkspaceOverview"), BuildOverviewPage());
            navigation.Add("ponds", L("AquacultureFishing.WorkspacePonds"), BuildPondsPage());
            navigation.Add("species", L("AquacultureFishing.WorkspaceSpecies"), BuildSpeciesPage());
            navigation.Add("breeds", L("AquacultureFishing.WorkspaceBreeds"), BuildBreedsPage());
            navigation.Add("conservation", L("AquacultureFishing.WorkspaceConservation"), BuildConservationPage());
            navigation.Add("commissions", L("AquacultureFishing.WorkspaceCommissions"), BuildCommissionsPage());
            navigation.SetFlex(1f);
            return AquacultureUiComponents.Panel("workspace.root",
                InsightUi.Column("workspace.root.content").SetGap(10f).SetPadding(4f).Add(
                    InsightUi.SectionHeader("workspace.header", L("AquacultureFishing.WorkspaceTitle"),
                        L("AquacultureFishing.WorkspaceSubtitle"), null, null, true), navigation));
        }

        private InsightUiElement BuildOverviewPage()
        {
            InsightUiStack content = InsightUi.Column("workspace.overview.content").SetGap(8f);
            content.Add(InsightUi.SectionHeader("overview.header", L("AquacultureFishing.WorkspaceOverview"),
                L("AquacultureFishing.WorkspaceOverviewSubtitle"), null, null, true));
            overviewAttentionCallout = InsightUi.Callout("overview.attention", InsightUiCalloutSeverity.Info,
                L("AquacultureFishing.WorkspaceAttentionTitle"), L("AquacultureFishing.WorkspaceAttentionEmpty"));
            overviewPriorityList = InsightUi.VirtualList("overview.priority.list", 0, 58f,
                index => BuildOverviewPriorityRow(actionablePonds[index]));
            overviewPriorityList.SetHeight(InsightLength.Fixed(238f));
            overviewPriorityList.Overscan = 2;
            overviewPriorityList.CacheLimit = 24;
            overviewPriorityEmptyLabel = InsightUi.Label("overview.priority.empty", string.Empty,
                InsightUiTextStyle.Caption);
            content.Add(overviewAttentionCallout,
                InsightUi.SectionHeader("overview.priority.header", L("AquacultureFishing.WorkspacePrioritySection"),
                    L("AquacultureFishing.WorkspacePrioritySectionSubtitle"), null, null, true),
                overviewPriorityList, overviewPriorityEmptyLabel);
            content.Add(DynamicStat("overview.species", L("AquacultureFishing.WorkspaceDiscovered"),
                () => JournalDiscovered() + " / " + species.Count));
            content.Add(DynamicStat("overview.ponds", L("AquacultureFishing.WorkspacePondCount"),
                () => ponds.Count.ToString()));
            content.Add(DynamicStat("overview.waters", L("AquacultureFishing.WorkspaceWaterCount"),
                () => waters.Count.ToString()));
            content.Add(DynamicStat("overview.commission", L("AquacultureFishing.WorkspaceActiveCommission"),
                () => commissionSnapshot == null
                    ? L("AquacultureFishing.WorkspaceNone") : L("AquacultureFishing.WorkspaceAvailable")));
            content.Add(InsightUi.SectionHeader("overview.health.header",
                L("AquacultureFishing.WorkspaceHealthSummary"),
                L("AquacultureFishing.WorkspaceHealthLegend"), null, null, true));
            content.Add(InsightUi.Grid("overview.health.buckets", 190f).Add(
                DynamicStat("overview.health.healthy", L("AquacultureFishing.WorkspaceHealthyPonds"),
                    () => PondCount(AquacultureHealthPriority.Healthy).ToString()),
                DynamicStat("overview.health.warning", L("AquacultureFishing.WorkspaceWarningPonds"),
                    () => WarningPondCount().ToString()),
                DynamicStat("overview.health.critical", L("AquacultureFishing.WorkspaceCriticalPonds"),
                    () => CriticalPondCount().ToString())));
            content.Add(InsightUi.Label("overview.conservation", string.Empty, InsightUiTextStyle.Caption)
                .SetTextProvider(OverviewConservationText));
            content.Add(InsightUi.Label("overview.milestones", string.Empty, InsightUiTextStyle.Caption)
                .SetTextProvider(OverviewMilestoneText));
            content.Add(InsightUi.Label("overview.expertise", string.Empty, InsightUiTextStyle.Caption)
                .SetTextProvider(() => knowledgeExpertiseText));
            content.Add(InsightUi.Callout("overview.knowledge", InsightUiCalloutSeverity.Info,
                L("AquacultureFishing.WorkspaceKnowledgeTitle"),
                L("AquacultureFishing.WorkspaceKnowledgeBody")));
            content.Add(InsightUi.Button("overview.knowledge.open",
                L("AquacultureFishing.WorkspaceOpenKnowledge"),
                () => MainTabWindow_AquacultureJournal.OpenExpertise(null)));
            content.Add(InsightUi.Label("overview.health", string.Empty).SetTextProvider(() => OverviewHealthText()),
                InsightUi.Label("overview.health.note", L("AquacultureFishing.WorkspaceHealthLegend"), InsightUiTextStyle.Caption));
            return InsightUi.Scroll("workspace.overview.scroll", content);
        }

        private InsightUiElement BuildOverviewPriorityRow(PondEntry entry)
        {
            InsightUiButton button = InsightUi.Button(AquacultureUiStableIds.For("overview.pond.select", entry.StableId),
                entry.Display.Label, () => SelectAndFocusPond(entry)) as InsightUiButton;
            button.SelectedProvider = () => selectedPondId == entry.StableId;
            button.SetTooltip(L("AquacultureFishing.WorkspaceOpenPondTooltip"));
            return InsightUi.Column(AquacultureUiStableIds.For("overview.pond.row", entry.StableId)).SetGap(2f).Add(
                InsightUi.Row(AquacultureUiStableIds.For("overview.pond.header", entry.StableId)).SetGap(6f).Add(
                    button, InsightUi.Spacer(AquacultureUiStableIds.For("overview.pond.space", entry.StableId)).SetFlex(1f),
                    AquacultureUiComponents.Badge("overview.pond.status." + entry.StableId, entry.StatusLine)),
                InsightUi.Label(AquacultureUiStableIds.For("overview.pond.reason", entry.StableId),
                    entry.Details.FirstOrDefault() ?? entry.StatusLine, InsightUiTextStyle.Caption));
        }

        private InsightUiElement BuildPondsPage()
        {
            InsightUiSearchField search = InsightUi.SearchField("ponds.search", string.Empty, L("AquacultureFishing.WorkspaceSearchPonds"))
                .Bind(() => pondSearch, value =>
                {
                    pondSearch = value ?? string.Empty;
                    filterRevision = -1;
                    RefreshFilters();
                });
            pondFilterSelect = InsightUi.Select("ponds.filter", L("AquacultureFishing.WorkspacePondFilter"),
                PondFilterLabels(), 0).Bind(() => (int)pondFilter, index =>
                {
                    if (index < 0 || index > (int)AquaculturePondFilter.Healthy) return;
                    pondFilter = (AquaculturePondFilter)index;
                    filterRevision = -1;
                    RefreshFilters();
                });
            pondFilterSelect.SetTooltip(L("AquacultureFishing.WorkspacePondFilterTooltip"));
            pondList = InsightUi.VirtualList("ponds.list", filteredPonds.Count, 66f, index => BuildPondRow(filteredPonds[index]));
            pondList.SetFlex(1f);
            pondList.Overscan = 2;
            pondList.CacheLimit = 64;
            pondEmptyLabel = InsightUi.Label("ponds.empty.label", string.Empty, InsightUiTextStyle.Caption);
            pondEmptyAction = InsightUi.Button("ponds.empty.clear", L("AquacultureFishing.WorkspaceClearPondFilter"),
                ClearPondFilter) as InsightUiButton;

            pondPopulationMeter = InsightUi.Meter("pond.detail.population", 0f, 1f)
                .SetLabel(L("AquacultureFishing.PondPopulation"));
            pondFeedMeter = InsightUi.Meter("pond.detail.feed", 0f, 1f)
                .SetLabel(L("AquacultureFishing.WorkspaceFeedReserve"));
            adultsOnlyToggle = InsightUi.Toggle("pond.control.adults", L("AquacultureFishing.WorkspaceAdultsOnly"))
                .Bind(() => SelectedPond?.AdultsOnly ?? true, value => SetSelectedPond((component, cell) => component.SetHarvestAdultsOnly(cell, value)));
            protectFemalesToggle = InsightUi.Toggle("pond.control.females", L("AquacultureFishing.WorkspaceProtectFemales"))
                .Bind(() => SelectedPond?.ProtectFemales ?? true, value => SetSelectedPond((component, cell) => component.SetProtectBreedingFemales(cell, value)));
            automaticFeedingToggle = InsightUi.Toggle("pond.control.feeding", L("AquacultureFishing.WorkspaceAutomaticFeeding"))
                .Bind(() => SelectedPond?.AutomaticFeeding ?? true, value => SetSelectedPond((component, cell) => component.SetAutomaticFeeding(cell, value)));
            predationToggle = InsightUi.Toggle("pond.control.predation", L("AquacultureFishing.WorkspacePredation"))
                .Bind(() => SelectedPond?.Predation ?? true, value => SetSelectedPond((component, cell) => component.SetPredationEnabled(cell, value)));
            surplusHarvestToggle = InsightUi.Toggle("pond.control.surplus", L("AquacultureFishing.WorkspaceSurplusHarvest"))
                .Bind(() => SelectedPond?.SurplusHarvest ?? false, value => SetSelectedPond((component, cell) => component.SetAutomaticSurplusHarvest(cell, value)));
            feedDaysSlider = InsightUi.Slider("pond.control.feed-days", 1f, 0.25f, 5f)
                .Bind(() => SelectedPond?.TargetFeedDays ?? 1f, value => SetSelectedPond((component, cell) => component.SetTargetFeedDays(cell, value)));
            populationLimitSlider = InsightUi.Slider("pond.control.limit", 0f, 0f, 200f)
                .Bind(() => SelectedPond?.PopulationLimit ?? 0f, value => SetSelectedPond((component, cell) => component.SetPopulationLimit(cell, Mathf.RoundToInt(value))));
            populationTargetSlider = InsightUi.Slider("pond.control.target", 0f, 0f, 200f)
                .Bind(() => SelectedPond?.PopulationTarget ?? 0f, value => SetSelectedPond((component, cell) => component.SetManagementPopulationTarget(cell, Mathf.RoundToInt(value))));
            breedingModeSelect = InsightUi.Select("pond.control.breeding", L("AquacultureFishing.WorkspaceBreedingMode"),
                BreedingModeNames.Select(ModeLabel).ToArray(), 0).Bind(
                    () => SelectedPond == null ? 0 : Array.IndexOf(BreedingModeNames, SelectedPond.BreedingMode.ToString()),
                    index =>
                    {
                        if (index < 0 || index >= BreedingModeNames.Length) return;
                        PondBreedingMode mode = (PondBreedingMode)Enum.Parse(typeof(PondBreedingMode), BreedingModeNames[index]);
                        SetSelectedPond((component, cell) => component.SetBreedingMode(cell, mode));
                    });

            InsightUiElement feedDaysSetting = AquacultureUiComponents.SliderSetting("pond.control.feed-days",
                L("AquacultureFishing.WorkspaceTargetFeedDays"), L("AquacultureFishing.WorkspaceTargetFeedDaysTooltip"),
                feedDaysSlider, () => LF("AquacultureFishing.WorkspaceFeedDaysValue",
                    (SelectedPond?.TargetFeedDays ?? 1f).ToString("0.##")));
            InsightUiElement populationLimitSetting = AquacultureUiComponents.SliderSetting("pond.control.limit",
                L("AquacultureFishing.WorkspacePopulationLimit"), L("AquacultureFishing.WorkspacePopulationLimitTooltip"),
                populationLimitSlider, () => LF("AquacultureFishing.WorkspacePopulationLimitValue",
                    Mathf.RoundToInt(SelectedPond?.PopulationLimit ?? 0f)));
            InsightUiElement populationTargetSetting = AquacultureUiComponents.SliderSetting("pond.control.target",
                L("AquacultureFishing.WorkspacePopulationTarget"), L("AquacultureFishing.WorkspacePopulationTargetTooltip"),
                populationTargetSlider, () => LF("AquacultureFishing.WorkspacePopulationTargetValue",
                    Mathf.RoundToInt(SelectedPond?.PopulationTarget ?? 0f)));

            pondHealthCallout = InsightUi.Callout("pond.detail.health", InsightUiCalloutSeverity.Info,
                L("AquacultureFishing.WorkspacePondHealthy"), L("AquacultureFishing.WorkspaceSelectPond"));
            pondResearchCallout = InsightUi.Callout("pond.detail.research", InsightUiCalloutSeverity.Info,
                L("AquacultureFishing.WorkspaceResearchDisclosure"),
                L("AquacultureFishing.WorkspaceResearchDisclosureBody"));
            pondFocusButton = InsightUi.Button("pond.detail.focus", L("AquacultureFishing.WorkspaceFocusPond"),
                FocusSelectedPond) as InsightUiButton;
            pondPlannerButton = InsightUi.Button("pond.detail.planner", L("AquacultureFishing.WorkspaceOpenPlanner"),
                OpenPlannerForSelected) as InsightUiButton;

            InsightUiElement healthGroup = InsightUi.Expander("pond.group.health",
                L("AquacultureFishing.WorkspacePondHealthGroup"), InsightUi.Column("pond.group.health.content").SetGap(6f).Add(
                    pondHealthCallout, pondPopulationMeter, pondFeedMeter,
                    InsightUi.Label("pond.detail.issues", string.Empty, InsightUiTextStyle.Caption)
                        .SetTextProvider(() => SelectedPond == null ? string.Empty : string.Join("\n", SelectedPond.Details.ToArray()))), true);
            InsightUiElement feedingGroup = InsightUi.Expander("pond.group.feeding",
                L("AquacultureFishing.WorkspacePondFeedingGroup"), InsightUi.Column("pond.group.feeding.content").SetGap(5f).Add(
                    automaticFeedingToggle, feedDaysSetting), true);
            InsightUiElement harvestingGroup = InsightUi.Expander("pond.group.harvesting",
                L("AquacultureFishing.WorkspacePondHarvestingGroup"), InsightUi.Column("pond.group.harvesting.content").SetGap(5f).Add(
                    adultsOnlyToggle, protectFemalesToggle, surplusHarvestToggle), false);
            InsightUiElement breedingGroup = InsightUi.Expander("pond.group.breeding",
                L("AquacultureFishing.WorkspacePondBreedingGroup"), InsightUi.Column("pond.group.breeding.content").SetGap(5f).Add(
                    breedingModeSelect), false);
            InsightUiElement populationGroup = InsightUi.Expander("pond.group.population",
                L("AquacultureFishing.WorkspacePondPopulationGroup"), InsightUi.Column("pond.group.population.content").SetGap(5f).Add(
                    predationToggle, populationLimitSetting, populationTargetSetting), false);

            InsightUiElement detail = InsightUi.Scroll("pond.detail.scroll", InsightUi.Column("pond.detail.content").SetGap(7f).Add(
                InsightUi.SectionHeader("pond.detail.header", L("AquacultureFishing.WorkspacePondDetail"),
                    L("AquacultureFishing.WorkspacePondDetailSubtitle"), null, null, true),
                InsightUi.Row("pond.detail.actions").SetGap(6f).Add(
                    pondFocusButton, pondPlannerButton),
                healthGroup,
                pondResearchCallout,
                InsightUi.Label("pond.detail.research-status", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(ResearchStatus),
                InsightUi.SectionHeader("pond.control.header", L("AquacultureFishing.WorkspacePondControls"),
                    L("AquacultureFishing.WorkspacePondControlsSubtitle"), null, null, true),
                feedingGroup, harvestingGroup, breedingGroup, populationGroup));

            pondsSplit = InsightUi.Split("ponds.split", InsightUi.Column("ponds.master").SetGap(6f).Add(
                search, pondFilterSelect, pondList,
                InsightUi.Column("ponds.empty").SetGap(5f).Add(pondEmptyLabel, pondEmptyAction)), detail, 0.34f);
            pondsSplit.SetFlex(1f);
            InsightUiStack root = InsightUi.Column("workspace.ponds.content").SetGap(8f).Add(
                InsightUi.SectionHeader("ponds.header", L("AquacultureFishing.WorkspacePonds"),
                    L("AquacultureFishing.WorkspacePondsSubtitle"), null, null, true),
                pondsSplit);
            return root;
        }

        private InsightUiElement BuildPondRow(PondEntry entry)
        {
            InsightUiButton button = InsightUi.Button(AquacultureUiStableIds.For("pond.select", entry.StableId),
                    entry.Display.Label, () =>
                    {
                        selectedPondId = entry.StableId;
                        document.Invalidate();
                    })
                .SetMinSize(0f, 28f) as InsightUiButton;
            button.SelectedProvider = () => selectedPondId == entry.StableId;
            return InsightUi.Column(AquacultureUiStableIds.For("pond.row", entry.StableId)).SetGap(2f).Add(
                InsightUi.Row(AquacultureUiStableIds.For("pond.row.header", entry.StableId)).SetGap(6f).Add(
                    button, InsightUi.Spacer(AquacultureUiStableIds.For("pond.row.space", entry.StableId)).SetFlex(1f),
                    AquacultureUiComponents.Badge("pond.row.status." + entry.StableId, entry.StatusLine)),
                InsightUi.Label(AquacultureUiStableIds.For("pond.row.detail", entry.StableId),
                    LF("AquacultureFishing.WorkspacePondSummary",
                        L("AquacultureFishing.WorkspacePondWater"), entry.Display.WaterKind,
                        entry.Display.Population, entry.Display.Capacity,
                        entry.Display.Temperature.ToString("0.#")),
                    InsightUiTextStyle.Caption));
        }

        private InsightUiElement BuildSpeciesPage()
        {
            InsightUiSearchField search = InsightUi.SearchField("species.search", string.Empty, L("AquacultureFishing.WorkspaceSearchSpecies"))
                .Bind(() => speciesSearch, value =>
                {
                    speciesSearch = value ?? string.Empty;
                    filterRevision = -1;
                    RefreshFilters();
                });
            speciesList = InsightUi.VirtualList("species.list", filteredSpecies.Count, 48f,
                index => BuildSpeciesRow(filteredSpecies[index]));
            speciesList.SetFlex(1f);
            speciesList.Overscan = 2;
            speciesList.CacheLimit = 64;
            speciesEmptyLabel = InsightUi.Label("species.empty", string.Empty, InsightUiTextStyle.Caption);
            InsightUiElement detail = InsightUi.Scroll("species.detail.scroll", InsightUi.Column("species.detail.content").SetGap(7f).Add(
                InsightUi.SectionHeader("species.detail.header", L("AquacultureFishing.WorkspaceSpeciesDetail"),
                    L("AquacultureFishing.WorkspaceKnowledgeCanonical"), null, null, true),
                InsightUi.Label("species.detail.name", string.Empty).SetTextProvider(() => SelectedSpeciesLabel()),
                InsightUi.Label("species.detail.knowledge", string.Empty).SetTextProvider(() => SelectedSpeciesKnowledge()),
                InsightUi.Label("species.detail.facets", string.Empty, InsightUiTextStyle.Caption).SetTextProvider(SelectedSpeciesFacets),
                InsightUi.Callout("species.detail.knowledge-callout", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceKnowledgeTitle"), L("AquacultureFishing.WorkspaceKnowledgeBody"))));
            speciesSplit = InsightUi.Split("species.split", InsightUi.Column("species.master").SetGap(6f).Add(
                search, speciesList, speciesEmptyLabel), detail, 0.34f);
            speciesSplit.SetFlex(1f);
            return InsightUi.Column("workspace.species.content").SetGap(8f).Add(
                InsightUi.SectionHeader("species.header", L("AquacultureFishing.WorkspaceSpecies"),
                    L("AquacultureFishing.WorkspaceSpeciesSubtitle"), null, null, true),
                speciesSplit);
        }

        private InsightUiElement BuildSpeciesRow(AquacultureSpeciesViewSnapshot snapshot)
        {
            string id = SpeciesId(snapshot);
            InsightUiButton button = InsightUi.Button(AquacultureUiStableIds.For("species.select", id), SpeciesLabel(snapshot), () =>
            {
                selectedSpeciesId = id;
                document.Invalidate();
            });
            button.SelectedProvider = () => selectedSpeciesId == id;
            return InsightUi.Row(AquacultureUiStableIds.For("species.row", id)).SetGap(6f).Add(button,
                InsightUi.Spacer(AquacultureUiStableIds.For("species.space", id)).SetFlex(1f),
                AquacultureUiComponents.Badge("species.knowledge." + id, snapshot.identityKnown
                    ? L("AquacultureFishing.WorkspaceIdentityKnown")
                    : L("AquacultureFishing.WorkspaceKnowledgeHidden")));
        }

        private InsightUiElement BuildBreedsPage()
        {
            InsightUiSearchField search = InsightUi.SearchField("breeds.search", string.Empty, L("AquacultureFishing.WorkspaceSearchBreeds"))
                .Bind(() => breedSearch, value =>
                {
                    breedSearch = value ?? string.Empty;
                    filterRevision = -1;
                    RefreshFilters();
                });
            breedList = InsightUi.VirtualList("breeds.list", filteredBreeds.Count, 52f,
                index => BuildBreedRow(filteredBreeds[index]));
            breedList.SetFlex(1f);
            breedList.Overscan = 2;
            breedList.CacheLimit = 64;
            breedEmptyLabel = InsightUi.Label("breeds.empty", string.Empty, InsightUiTextStyle.Caption);
            InsightUiElement detail = InsightUi.Scroll("breeds.detail.scroll", InsightUi.Column("breeds.detail.content").SetGap(7f).Add(
                InsightUi.SectionHeader("breeds.detail.header", L("AquacultureFishing.WorkspaceBreedDetail"),
                    L("AquacultureFishing.WorkspaceBreedsSubtitle"), null, null, true),
                InsightUi.Label("breeds.detail.name", string.Empty).SetTextProvider(() => SelectedBreedName()),
                InsightUi.Label("breeds.detail.metrics", string.Empty).SetTextProvider(() => SelectedBreedMetrics()),
                InsightUi.Label("breeds.detail.traits", string.Empty, InsightUiTextStyle.Caption).SetTextProvider(SelectedBreedTraits),
                InsightUi.Callout("breeds.detail.rules", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceBreedRules"), L("AquacultureFishing.WorkspaceBreedRulesBody"))));
            breedsSplit = InsightUi.Split("breeds.split", InsightUi.Column("breeds.master").SetGap(6f).Add(
                search, breedList, breedEmptyLabel), detail, 0.34f);
            breedsSplit.SetFlex(1f);
            return InsightUi.Column("workspace.breeds.content").SetGap(8f).Add(
                InsightUi.SectionHeader("breeds.header", L("AquacultureFishing.WorkspaceBreeds"),
                    L("AquacultureFishing.WorkspaceBreedsSubtitle"), null, null, true),
                breedsSplit);
        }

        private InsightUiElement BuildBreedRow(FishBreedRecord breed)
        {
            string id = breed.id;
            InsightUiButton button = InsightUi.Button(AquacultureUiStableIds.For("breed.select", id), breed.name ?? L("AquacultureFishing.WorkspaceUnnamed"), () =>
            {
                selectedBreedId = id;
                document.Invalidate();
            });
            button.SelectedProvider = () => selectedBreedId == id;
            return InsightUi.Row(AquacultureUiStableIds.For("breed.row", id)).SetGap(6f).Add(button,
                InsightUi.Spacer(AquacultureUiStableIds.For("breed.space", id)).SetFlex(1f),
                AquacultureUiComponents.Badge("breed.status." + id, BreedStatus(breed)));
        }

        private InsightUiElement BuildConservationPage()
        {
            InsightUiSearchField search = InsightUi.SearchField("conservation.search", string.Empty, L("AquacultureFishing.WorkspaceSearchWater"))
                .Bind(() => waterSearch, value =>
                {
                    waterSearch = value ?? string.Empty;
                    filterRevision = -1;
                    RefreshFilters();
                });
            waterList = InsightUi.VirtualList("conservation.list", filteredWaters.Count, 54f,
                index => BuildWaterRow(filteredWaters[index]));
            waterList.SetFlex(1f);
            waterList.Overscan = 2;
            waterList.CacheLimit = 64;
            waterEmptyLabel = InsightUi.Label("conservation.empty", string.Empty, InsightUiTextStyle.Caption);
            return InsightUi.Scroll("workspace.conservation.scroll", InsightUi.Column("workspace.conservation.content").SetGap(8f).Add(
                InsightUi.SectionHeader("conservation.header", L("AquacultureFishing.WorkspaceConservation"),
                    L("AquacultureFishing.WorkspaceConservationSubtitle"), null, null, true), search, waterList,
                waterEmptyLabel,
                InsightUi.Label("conservation.disclosure", string.Empty).SetTextProvider(() => SelectedWaterDetails()),
                InsightUi.Callout("conservation.drf", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceConservationAuthority"),
                    L("AquacultureFishing.WorkspaceConservationAuthorityBody"))));
        }

        private InsightUiElement BuildWaterRow(NaturalWaterViewSnapshot water)
        {
            string id = WaterId(water);
            InsightUiButton button = InsightUi.Button(AquacultureUiStableIds.For("water.select", id),
                water.habitat + "  " + water.anchor.x + ", " + water.anchor.z, () =>
                {
                    selectedWaterId = id;
                    document.Invalidate();
            });
            button.SelectedProvider = () => selectedWaterId == id;
            return InsightUi.Row(AquacultureUiStableIds.For("water.row", id)).SetGap(6f).Add(button,
                InsightUi.Spacer(AquacultureUiStableIds.For("water.space", id)).SetFlex(1f),
                AquacultureUiComponents.Badge("water.status." + id, WaterStatus(water)),
                InsightUi.Label(AquacultureUiStableIds.For("water.abundance", id),
                    water.AbundanceLabel, InsightUiTextStyle.Caption));
        }

        private InsightUiElement BuildCommissionsPage()
        {
            commissionStatusCallout = InsightUi.Callout("commissions.status.callout", InsightUiCalloutSeverity.Info,
                L("AquacultureFishing.WorkspaceCommissionAuthority"), L("AquacultureFishing.WorkspaceNoActiveCommission"));
            return InsightUi.Scroll("workspace.commissions.scroll", InsightUi.Column("workspace.commissions.content").SetGap(8f).Add(
                InsightUi.SectionHeader("commissions.header", L("AquacultureFishing.WorkspaceCommissions"),
                    L("AquacultureFishing.WorkspaceCommissionsSubtitle"), null, null, true),
                commissionStatusCallout,
                InsightUi.Label("commissions.status", string.Empty).SetTextProvider(CommissionStatus),
                InsightUi.Label("commissions.requirements", string.Empty).SetTextProvider(CommissionRequirements),
                InsightUi.Callout("commissions.rules", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceCommissionRules"),
                    L("AquacultureFishing.WorkspaceCommissionRulesBody")),
                InsightUi.Label("commissions.knowledge", L("AquacultureFishing.WorkspaceCommissionDisclosure"), InsightUiTextStyle.Caption)));
        }

        private void UpdateDynamicControls()
        {
            PondEntry selected = SelectedPond;
            if (overviewAttentionCallout != null)
            {
                if (ponds.Count == 0)
                {
                    overviewAttentionCallout.Severity = InsightUiCalloutSeverity.Info;
                    overviewAttentionCallout.Title = L("AquacultureFishing.WorkspaceNoPonds");
                    overviewAttentionCallout.Body = L("AquacultureFishing.WorkspaceAttentionEmpty");
                }
                else if (actionablePonds.Count == 0)
                {
                    overviewAttentionCallout.Severity = InsightUiCalloutSeverity.Success;
                    overviewAttentionCallout.Title = L("AquacultureFishing.WorkspaceAllHealthyTitle");
                    overviewAttentionCallout.Body = L("AquacultureFishing.WorkspaceAllHealthyBody");
                }
                else
                {
                    int critical = CriticalPondCount();
                    int warning = WarningPondCount();
                    PondEntry next = actionablePonds[0];
                    overviewAttentionCallout.Severity = critical > 0
                        ? InsightUiCalloutSeverity.Error : InsightUiCalloutSeverity.Warning;
                    overviewAttentionCallout.Title = critical > 0
                        ? L("AquacultureFishing.WorkspaceCriticalAttentionTitle")
                        : L("AquacultureFishing.WorkspaceAttentionTitle");
                    overviewAttentionCallout.Body = LF("AquacultureFishing.WorkspaceAttentionSummary", critical, warning) +
                        "\n" + LF("AquacultureFishing.WorkspaceNextAction", next.Display.Label,
                            next.Details.FirstOrDefault() ?? next.StatusLine);
                }
            }
            if (overviewPriorityEmptyLabel != null)
            {
                overviewPriorityEmptyLabel.Text = ponds.Count == 0
                    ? L("AquacultureFishing.WorkspaceNoPonds")
                    : L("AquacultureFishing.WorkspaceAllHealthyBody");
            }
            if (pondHealthCallout != null)
            {
                AquacultureUiStatus status = selected == null ? AquacultureUiStatus.Unknown : StatusFor(selected.Priority);
                pondHealthCallout.Severity = CalloutSeverity(status);
                pondHealthCallout.Title = selected == null ? L("AquacultureFishing.WorkspaceSelectPond") : selected.StatusLine;
                pondHealthCallout.Body = selected == null ? string.Empty :
                    (selected.Details.FirstOrDefault() ?? L("AquacultureFishing.WorkspacePondHealthy"));
            }
            if (pondPopulationMeter != null)
            {
                pondPopulationMeter.Current = selected?.Display.Population ?? 0f;
                pondPopulationMeter.Maximum = Mathf.Max(1f, selected?.Display.Capacity ?? 1f);
                pondPopulationMeter.SetValueText(selected == null ? string.Empty :
                    LF("AquacultureFishing.WorkspacePondPopulationValue",
                        selected.Display.Population, selected.Display.Capacity));
            }
            if (pondFeedMeter != null)
            {
                pondFeedMeter.Current = selected?.Display.FeedHours ?? 0f;
                pondFeedMeter.Maximum = Mathf.Max(1f, selected?.Display.FeedHours ?? 1f);
                pondFeedMeter.SetValueText(selected == null ? string.Empty :
                    LF("AquacultureFishing.WorkspaceFeedHoursValue", selected.Display.FeedHours.ToString("0.#")));
            }
            bool enabled = selected != null;
            adultsOnlyToggle.Enabled = enabled && industrialAquacultureAvailable;
            protectFemalesToggle.Enabled = enabled && industrialAquacultureAvailable;
            automaticFeedingToggle.Enabled = enabled && industrialAquacultureAvailable;
            predationToggle.Enabled = enabled && industrialAquacultureAvailable;
            surplusHarvestToggle.Enabled = enabled && industrialAquacultureAvailable && selected?.PopulationTarget > 0;
            feedDaysSlider.Enabled = enabled && industrialAquacultureAvailable;
            populationLimitSlider.Enabled = enabled && industrialAquacultureAvailable;
            populationTargetSlider.Enabled = enabled && industrialAquacultureAvailable;
            breedingModeSelect.Enabled = enabled && selectiveBreedingAvailable;
            if (pondFocusButton != null) pondFocusButton.Enabled = enabled;
            if (pondPlannerButton != null) pondPlannerButton.Enabled = enabled && industrialAquacultureAvailable;
            if (pondResearchCallout != null)
                pondResearchCallout.Body = L("AquacultureFishing.WorkspaceResearchDisclosureBody") + "\n" + ResearchStatus();
            if (commissionStatusCallout != null)
            {
                commissionStatusCallout.Title = commissionSnapshot == null
                    ? L("AquacultureFishing.WorkspaceNoActiveCommission")
                    : L("AquacultureFishing.WorkspaceCommissionActive");
                commissionStatusCallout.Body = CommissionStatus() + "\n" + CommissionRequirements();
            }
        }

        private void CaptureResearchAvailability()
        {
            pondkeepingAvailable = AquacultureProgression.IsAvailable("AF_Pondkeeping");
            managedAquacultureAvailable = AquacultureProgression.IsAvailable("AF_ManagedAquaculture");
            industrialAquacultureAvailable = AquacultureProgression.IsAvailable("AF_IndustrialAquaculture");
            selectiveBreedingAvailable = AquacultureProgression.IsAvailable("AF_SelectiveBreeding");
        }

        private void SetSelectedPond(Action<FishPondMapComponent, IntVec3> action)
        {
            PondEntry selected = SelectedPond;
            if (selected == null || action == null) return;
            action(selected.Component, selected.Cell);
            document.Invalidate();
        }

        private string[] PondFilterLabels()
        {
            return new[]
            {
                L("AquacultureFishing.WorkspacePondFilterAll"),
                L("AquacultureFishing.WorkspacePondFilterAttention"),
                L("AquacultureFishing.WorkspacePondFilterCritical"),
                L("AquacultureFishing.WorkspacePondFilterHealthy")
            };
        }

        private void ClearPondFilter()
        {
            pondSearch = string.Empty;
            pondFilter = AquaculturePondFilter.All;
            filterRevision = -1;
            RefreshFilters();
            document.Invalidate();
        }

        private void SelectAndFocusPond(PondEntry entry)
        {
            if (entry == null) return;
            selectedPondId = entry.StableId;
            activePage = "ponds";
            pondSearch = string.Empty;
            pondFilter = AquaculturePondFilter.All;
            filterRevision = -1;
            RefreshFilters();
            FocusPond(entry);
            document.Invalidate();
        }

        private void FocusSelectedPond()
        {
            FocusPond(SelectedPond);
        }

        private static void FocusPond(PondEntry entry)
        {
            PondProxyThing proxy = entry?.Component?.ProxyFor(entry.Cell);
            if (proxy == null || !proxy.Spawned) return;
            Find.Selector.ClearSelection();
            Find.Selector.Select(proxy);
            CameraJumper.TryJump(proxy);
        }

        private void OpenPlannerForSelected()
        {
            PondEntry selected = SelectedPond;
            if (selected == null || !industrialAquacultureAvailable) return;
            PondProxyThing proxy = selected.Component?.ProxyFor(selected.Cell);
            if (proxy != null && proxy.Spawned)
                Find.WindowStack.Add(new Dialog_PondStockingPlanner(proxy, selected.Component));
        }

        private PondEntry SelectedPond => ponds.FirstOrDefault(item => item.StableId == selectedPondId);
        private AquacultureSpeciesViewSnapshot SelectedSpecies => species.FirstOrDefault(item => SpeciesId(item) == selectedSpeciesId);
        private FishBreedRecord SelectedBreed => breeds.FirstOrDefault(item => item.id == selectedBreedId);
        private NaturalWaterViewSnapshot SelectedWater => waters.FirstOrDefault(item => WaterId(item) == selectedWaterId);

        private string OverviewHealthText()
        {
            if (ponds.Count == 0) return L("AquacultureFishing.WorkspaceNoPonds");
            if (actionablePonds.Count == 0) return L("AquacultureFishing.WorkspaceAllHealthyBody");
            PondEntry worst = actionablePonds[0];
            return L("AquacultureFishing.WorkspaceWorstPond") + ": " + worst.Display.Label + " — " + worst.StatusLine;
        }

        private int PondCount(AquacultureHealthPriority priority) => ponds.Count(item => item.Priority == priority);

        private int WarningPondCount() => ponds.Count(item => item.Priority == AquacultureHealthPriority.Advice ||
            item.Priority == AquacultureHealthPriority.Reproduction);

        private int CriticalPondCount() => ponds.Count(item => AquacultureHealthPriorityRules.Rank(item.Priority) >=
            AquacultureHealthPriorityRules.Rank(AquacultureHealthPriority.SevereStress));

        private static AquacultureUiStatus StatusFor(AquacultureHealthPriority priority)
        {
            return priority == AquacultureHealthPriority.Healthy ? AquacultureUiStatus.Healthy :
                AquacultureHealthPriorityRules.Rank(priority) >=
                    AquacultureHealthPriorityRules.Rank(AquacultureHealthPriority.SevereStress)
                    ? AquacultureUiStatus.Critical : AquacultureUiStatus.Attention;
        }

        private static InsightUiCalloutSeverity CalloutSeverity(AquacultureUiStatus status)
        {
            switch (status)
            {
                case AquacultureUiStatus.Healthy: return InsightUiCalloutSeverity.Success;
                case AquacultureUiStatus.Critical: return InsightUiCalloutSeverity.Error;
                case AquacultureUiStatus.Attention: return InsightUiCalloutSeverity.Warning;
                default: return InsightUiCalloutSeverity.Info;
            }
        }

        private string ResearchStatus()
        {
            if (SelectedPond == null) return L("AquacultureFishing.WorkspaceSelectPond");
            List<string> locked = new List<string>();
            if (!pondkeepingAvailable)
                locked.Add(L("AquacultureFishing.WorkspaceResearchPondkeeping"));
            if (!managedAquacultureAvailable)
                locked.Add(L("AquacultureFishing.WorkspaceResearchManaged"));
            if (!industrialAquacultureAvailable)
                locked.Add(L("AquacultureFishing.WorkspaceResearchIndustrial"));
            if (!selectiveBreedingAvailable)
                locked.Add(L("AquacultureFishing.WorkspaceResearchBreeding"));
            return locked.Count == 0
                ? L("AquacultureFishing.WorkspaceResearchAllAvailable")
                : LF("AquacultureFishing.WorkspaceResearchLocked", string.Join(", ", locked.ToArray()));
        }

        private string OverviewConservationText()
        {
            List<NaturalFishConservationViewSnapshot> statuses = waters.SelectMany(water => water.conservation.Values).ToList();
            int risk = statuses.Count(status => status.atRisk);
            int recovery = statuses.Count(status => status.recovering);
            int belowFloor = statuses.Count(status => status.breedingFloor > 0f &&
                status.approximatePopulation < Mathf.CeilToInt(status.breedingFloor));
            return L("AquacultureFishing.WorkspaceConservationSummary") + ": " +
                L("AquacultureFishing.WorkspaceConservationRisk") + " " + risk + "  •  " +
                L("AquacultureFishing.WorkspaceConservationRecovery") + " " + recovery + "  •  " +
                L("AquacultureFishing.WorkspaceBreedingFloors") + " " + belowFloor;
        }

        private string OverviewMilestoneText()
        {
            int discovered = species.Count(item => item.record?.discoveredTick >= 0);
            int established = species.Count(item => item.record?.establishedTick >= 0);
            int bred = species.Count(item => item.record?.bredTick >= 0);
            int stable = species.Count(item => item.record?.stableTick >= 0);
            return L("AquacultureFishing.WorkspaceMilestoneSummary") + ": " +
                L("AquacultureFishing.WorkspaceMilestoneDiscovered") + " " + discovered + "  •  " +
                L("AquacultureFishing.WorkspaceMilestoneEstablished") + " " + established + "  •  " +
                L("AquacultureFishing.WorkspaceMilestoneBred") + " " + bred + "  •  " +
                L("AquacultureFishing.WorkspaceMilestoneStable") + " " + stable;
        }

        private string WaterConservationLine(NaturalWaterViewSnapshot water)
        {
            int risk = water?.conservation.Values.Count(status => status.atRisk) ?? 0;
            int recovery = water?.conservation.Values.Count(status => status.recovering) ?? 0;
            return LF("AquacultureFishing.WorkspaceWaterConservationSummary",
                L("AquacultureFishing.WorkspaceConservationRisk"), risk,
                L("AquacultureFishing.WorkspaceConservationRecovery"), recovery);
        }

        private static string CaptureKnowledgeExpertise()
        {
            Pawn pawn = Find.CurrentMap?.mapPawns?.FreeColonists?.OrderBy(item => item.thingIDNumber).FirstOrDefault();
            if (pawn == null) return L("AquacultureFishing.WorkspaceKnowledgeCanonicalSummary");
            return L("AquacultureFishing.WorkspaceKnowledgeExpertise") + ": " +
                AquacultureKnowledgeAdapter.ExpertiseRankFor(pawn) + "  •  " +
                AquacultureKnowledgeAdapter.ExpertiseProgressFor(pawn).ToStringPercent();
        }

        private string SelectedSpeciesLabel() => SelectedSpecies == null ? L("AquacultureFishing.WorkspaceSelectSpecies") : SpeciesLabel(SelectedSpecies);

        private string SelectedSpeciesKnowledge()
        {
            AquacultureSpeciesViewSnapshot item = SelectedSpecies;
            return item == null ? string.Empty : KnowledgeLine(item);
        }

        private string SelectedSpeciesFacets()
        {
            AquacultureSpeciesViewSnapshot item = SelectedSpecies;
            if (item == null || !item.identityKnown) return L("AquacultureFishing.WorkspaceKnowledgeHidden");
            return item.knownFacets.Count == 0 ? L("AquacultureFishing.WorkspaceNoKnownFacets") :
                L("AquacultureFishing.WorkspaceKnownFacets") + ": " + string.Join(", ", item.knownFacets.ToArray());
        }

        private string SelectedBreedName() => SelectedBreed?.name ?? L("AquacultureFishing.WorkspaceSelectBreed");

        private string SelectedBreedMetrics()
        {
            FishBreedRecord breed = SelectedBreed;
            return breed == null ? string.Empty :
                L("AquacultureFishing.WorkspaceBreedMetrics") + ": " + breed.Stability.ToStringPercent() + "  •  " +
                L("AquacultureFishing.WorkspaceGeneration") + " " + breed.highestGeneration + "  •  " +
                L("AquacultureFishing.WorkspaceSuccess") + " " + breed.SuccessRate.ToStringPercent();
        }

        private string SelectedBreedTraits() => SelectedBreed == null
            ? L("AquacultureFishing.WorkspaceNoDefiningTraits")
            : (breedTraitText.TryGetValue(SelectedBreed.id, out string value)
                ? value : L("AquacultureFishing.WorkspaceNoDefiningTraits"));

        private string BreedStatus(FishBreedRecord breed)
        {
            if (breed == null) return L("AquacultureFishing.WorkspaceUnknown");
            return breed.Stability >= 0.80f ? L("AquacultureFishing.WorkspaceBreedStable") :
                breed.Stability >= 0.50f ? L("AquacultureFishing.WorkspaceBreedDeveloping") :
                L("AquacultureFishing.WorkspaceBreedNeedsAttention");
        }

        private string WaterStatus(NaturalWaterViewSnapshot water)
        {
            if (water == null) return L("AquacultureFishing.WorkspaceUnknown");
            int atRisk = water.conservation.Values.Count(status => status.atRisk);
            return atRisk > 0 ? L("AquacultureFishing.WorkspaceWaterAtRisk") :
                water.conservation.Values.Any(status => status.recovering)
                    ? L("AquacultureFishing.WorkspaceWaterRecovering")
                    : L("AquacultureFishing.WorkspaceWaterStable");
        }

        private string SelectedWaterDetails()
        {
            NaturalWaterViewSnapshot water = SelectedWater;
            if (water == null) return L("AquacultureFishing.WorkspaceSelectWater");
            return L("AquacultureFishing.WorkspaceWaterDetails") + ": " + water.AbundanceLabel + "  •  " +
                L("AquacultureFishing.WorkspaceSpeciesCount") + " " + water.species.Count + "\n" +
                WaterConservationLine(water);
        }

        private string CommissionStatus()
            => commissionSnapshot?.Status ?? L("AquacultureFishing.WorkspaceNoActiveCommission");

        private string CommissionRequirements()
            => commissionSnapshot?.Requirements ?? L("AquacultureFishing.WorkspaceNoCommissionRequirements");

        private static CommissionUiSnapshot CaptureCommissionSnapshot(int revision)
        {
            AquacultureCommissionComponent component = AquacultureCommissionComponent.Current;
            AquacultureCommissionRecord commission = component?.ActiveCommission;
            if (commission == null) return null;
            int tick = Find.TickManager?.TicksGame ?? 0;
            string status = L("AquacultureFishing.WorkspaceCommissionActive") + " — " +
                commission.DaysRemaining(tick) + " " + L("AquacultureFishing.WorkspaceDaysRemaining");
            string requirements = commission.RequirementsText + "\n" +
                L("AquacultureFishing.WorkspaceEligibleSpecimens") + ": " + (component.EligibleSpecimenCount);
            return new CommissionUiSnapshot(AquacultureUiStableIds.For("commission", commission.id),
                commission.id, status, requirements, component.EligibleSpecimenCount, revision);
        }

        private static string FormatBreedTraits(FishBreedRecord breed)
        {
            if (breed == null || breed.traitDefNames == null || breed.traitDefNames.Count == 0)
                return L("AquacultureFishing.WorkspaceNoDefiningTraits");
            string[] labels = breed.traitDefNames
                .Select(name => DefDatabase<FishTraitDef>.GetNamedSilentFail(name)?.LabelCap.ToString() ?? name)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return L("AquacultureFishing.WorkspaceDefiningTraits") + ": " + string.Join(", ", labels);
        }

        private static string PondWaterLabel(PondWaterKind kind)
        {
            switch (kind)
            {
                case PondWaterKind.Saltwater: return L("AquacultureFishing.WorkspaceWaterSaltwater");
                case PondWaterKind.Brackishwater: return L("AquacultureFishing.WorkspaceWaterBrackishwater");
                default: return L("AquacultureFishing.WorkspaceWaterFreshwater");
            }
        }

        private int JournalDiscovered()
        {
            return species.Count(item => item.record?.discoveredTick >= 0 || item.identityKnown);
        }

        private string SpeciesLabel(AquacultureSpeciesViewSnapshot item)
        {
            return item == null || !item.identityKnown ? L("AquacultureFishing.WorkspaceUnknownSpecies") : item.fishDef.LabelCap.ToString();
        }

        private string KnowledgeLine(AquacultureSpeciesViewSnapshot item)
        {
            if (item == null || !item.identityKnown) return L("AquacultureFishing.WorkspaceKnowledgeHidden");
            return L("AquacultureFishing.WorkspaceKnowledgeStage") + ": " + item.stageId + "  •  " +
                L("AquacultureFishing.WorkspaceConfidence") + ": " + item.confidence.ToStringPercent();
        }

        private static string SpeciesId(AquacultureSpeciesViewSnapshot item) =>
            AquacultureUiStableIds.For("species", item?.fishDef?.defName ?? "unknown");

        private static string ModeLabel(string mode)
        {
            switch (mode)
            {
                case nameof(PondBreedingMode.Natural): return L("AquacultureFishing.WorkspaceBreedingNatural");
                case nameof(PondBreedingMode.Paused): return L("AquacultureFishing.WorkspaceBreedingPaused");
                case nameof(PondBreedingMode.Encouraged): return L("AquacultureFishing.WorkspaceBreedingEncouraged");
                case nameof(PondBreedingMode.Intensive): return L("AquacultureFishing.WorkspaceBreedingIntensive");
                default: return mode;
            }
        }

        private static string PriorityLabel(AquacultureHealthPriority priority)
        {
            switch (priority)
            {
                case AquacultureHealthPriority.Lethal: return L("AquacultureFishing.WorkspacePriorityLethal");
                case AquacultureHealthPriority.Starvation: return L("AquacultureFishing.WorkspacePriorityStarvation");
                case AquacultureHealthPriority.SevereStress: return L("AquacultureFishing.WorkspacePrioritySevereStress");
                case AquacultureHealthPriority.Reproduction: return L("AquacultureFishing.WorkspacePriorityReproduction");
                case AquacultureHealthPriority.Advice: return L("AquacultureFishing.WorkspacePriorityAdvice");
                default: return L("AquacultureFishing.WorkspacePriorityHealthy");
            }
        }

        private static string WaterId(NaturalWaterViewSnapshot item) =>
            AquacultureUiStableIds.For("water", (item?.anchor.x ?? 0) + "." + (item?.anchor.z ?? 0));

        private static InsightUiElement DynamicStat(string id, string label, Func<string> value)
        {
            return InsightUi.Row(id).SetGap(8f).SetAlignment(InsightAlignment.Start, InsightAlignment.Center).Add(
                InsightUi.Label(id + ".label", label), InsightUi.Spacer(id + ".space").SetFlex(1f),
                InsightUi.Label(id + ".value", string.Empty).SetTextProvider(value));
        }

        private static string L(string key) => key.Translate().ToString();

        private static string LF(string key, params object[] args)
        {
            return string.Format(L(key), args);
        }

        private sealed class PondEntry
        {
            public PondEntry(IntVec3 cell, FishPondMapComponent component, PondUiSnapshot display,
                AquacultureWorkspaceRowSnapshot status)
            {
                Cell = cell;
                Component = component;
                Display = display;
                Status = status;
                AdultsOnly = component.HarvestAdultsOnlyAt(cell);
                ProtectFemales = component.ProtectBreedingFemalesAt(cell);
                AutomaticFeeding = component.AutomaticFeedingAt(cell);
                TargetFeedDays = component.TargetFeedDaysAt(cell);
                Predation = component.PredationEnabledAt(cell);
                PopulationLimit = component.PopulationLimitAt(cell);
                PopulationTarget = component.ManagementPopulationTargetAt(cell);
                SurplusHarvest = component.AutomaticSurplusHarvestAt(cell);
                BreedingMode = component.BreedingModeAt(cell);
            }

            public readonly IntVec3 Cell;
            public readonly FishPondMapComponent Component;
            public readonly PondUiSnapshot Display;
            public readonly AquacultureWorkspaceRowSnapshot Status;
            public AquacultureHealthPriority Priority => Status.Priority;
            public IReadOnlyList<string> Details => Status.Details;
            public readonly bool AdultsOnly;
            public readonly bool ProtectFemales;
            public readonly bool AutomaticFeeding;
            public readonly float TargetFeedDays;
            public readonly bool Predation;
            public readonly int PopulationLimit;
            public readonly int PopulationTarget;
            public readonly bool SurplusHarvest;
            public readonly PondBreedingMode BreedingMode;
            public string StableId => Display.StableId;
            public string StatusLine => Status.Status;
        }
    }
}
