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
        private CommissionUiSnapshot commissionSnapshot;

        private InsightUiVirtualList pondList;
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

        private static readonly string[] BreedingModeNames = Enum.GetNames(typeof(PondBreedingMode));

        public AquacultureJournalWorkspaceDocument()
        {
            document = new InsightUiDocument("aquaculture.journal.workspace.v2", BuildRoot())
            {
                Theme = AquacultureInsightTheme.Create(),
                Density = InsightUiDensity.Normal,
                HighContrast = false,
                ReducedMotion = false,
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(document);
        }

        public InsightUiDocument Document => document;
        public InsightUiHost Host { get; private set; }
        public int SnapshotRevision => uiCache.Revision;

        public void Draw(Rect rect)
        {
            RefreshSnapshots();
            UpdateDynamicControls();
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

            waters.Clear();
            waters.AddRange((AquacultureSnapshotCache.Waters(map) ?? Array.Empty<NaturalWaterViewSnapshot>())
                .OrderBy(item => item.anchor.z).ThenBy(item => item.anchor.x));

            if (ponds.Count > 0 && !ponds.Any(item => item.StableId == selectedPondId)) selectedPondId = ponds[0].StableId;
            if (species.Count > 0 && !species.Any(item => SpeciesId(item) == selectedSpeciesId)) selectedSpeciesId = SpeciesId(species[0]);
            if (breeds.Count > 0 && !breeds.Any(item => item.id == selectedBreedId)) selectedBreedId = breeds[0].id;
            if (waters.Count > 0 && !waters.Any(item => WaterId(item) == selectedWaterId)) selectedWaterId = WaterId(waters[0]);
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
            FilterInto(ponds, filteredPonds, pondSearch, item => item.Display.Label);
            FilterInto(species, filteredSpecies, speciesSearch, SpeciesLabel);
            FilterInto(breeds, filteredBreeds, breedSearch, item => item.name);
            FilterInto(waters, filteredWaters, waterSearch, item => item.habitat.ToString());
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
            return InsightUi.Column("workspace.root").SetGap(10f).SetPadding(4f).Add(
                InsightUi.SectionHeader("workspace.header", L("AquacultureFishing.WorkspaceTitle"),
                    L("AquacultureFishing.WorkspaceSubtitle"), null, null, true), navigation);
        }

        private InsightUiElement BuildOverviewPage()
        {
            InsightUiStack content = InsightUi.Column("workspace.overview.content").SetGap(8f);
            content.Add(InsightUi.SectionHeader("overview.header", L("AquacultureFishing.WorkspaceOverview"),
                L("AquacultureFishing.WorkspaceOverviewSubtitle"), null, null, true));
            content.Add(DynamicStat("overview.species", L("AquacultureFishing.WorkspaceDiscovered"),
                () => JournalDiscovered() + " / " + species.Count));
            content.Add(DynamicStat("overview.ponds", L("AquacultureFishing.WorkspacePondCount"),
                () => ponds.Count.ToString()));
            content.Add(DynamicStat("overview.waters", L("AquacultureFishing.WorkspaceWaterCount"),
                () => waters.Count.ToString()));
            content.Add(DynamicStat("overview.commission", L("AquacultureFishing.WorkspaceActiveCommission"),
                () => commissionSnapshot == null
                    ? L("AquacultureFishing.WorkspaceNone") : L("AquacultureFishing.WorkspaceAvailable")));
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

        private InsightUiElement BuildPondsPage()
        {
            InsightUiSearchField search = InsightUi.SearchField("ponds.search", string.Empty, L("AquacultureFishing.WorkspaceSearchPonds"))
                .Bind(() => pondSearch, value =>
                {
                    pondSearch = value ?? string.Empty;
                    filterRevision = -1;
                    RefreshFilters();
                });
            pondList = InsightUi.VirtualList("ponds.list", filteredPonds.Count, 66f, index => BuildPondRow(filteredPonds[index]));
            pondList.SetHeight(InsightLength.Fixed(300f));
            pondList.Overscan = 2;
            pondList.CacheLimit = 64;

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

            InsightUiElement detail = InsightUi.Scroll("pond.detail.scroll", InsightUi.Column("pond.detail.content").SetGap(7f).Add(
                InsightUi.SectionHeader("pond.detail.header", L("AquacultureFishing.WorkspacePondDetail"),
                    L("AquacultureFishing.WorkspacePondDetailSubtitle"), null, null, true),
                InsightUi.Label("pond.detail.status", string.Empty).SetTextProvider(() => SelectedPond?.StatusLine ?? L("AquacultureFishing.WorkspaceSelectPond")),
                pondPopulationMeter, pondFeedMeter,
                InsightUi.Label("pond.detail.issues", string.Empty, InsightUiTextStyle.Caption).SetTextProvider(() => SelectedPond == null
                    ? string.Empty : string.Join("\n", SelectedPond.Details.ToArray())),
                InsightUi.Callout("pond.detail.research", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceResearchDisclosure"),
                    L("AquacultureFishing.WorkspaceResearchDisclosureBody")),
                InsightUi.SectionHeader("pond.control.header", L("AquacultureFishing.WorkspacePondControls"),
                    L("AquacultureFishing.WorkspacePondControlsSubtitle"), null, null, true),
                breedingModeSelect, adultsOnlyToggle, protectFemalesToggle, automaticFeedingToggle,
                feedDaysSlider, predationToggle, surplusHarvestToggle, populationLimitSlider, populationTargetSlider));

            InsightUiStack root = InsightUi.Column("workspace.ponds.content").SetGap(8f).Add(
                InsightUi.SectionHeader("ponds.header", L("AquacultureFishing.WorkspacePonds"),
                    L("AquacultureFishing.WorkspacePondsSubtitle"), null, null, true), search,
                InsightUi.Split("ponds.split", InsightUi.Column("ponds.master").SetGap(6f).Add(pondList), detail, 0.34f));
            return InsightUi.Scroll("workspace.ponds.scroll", root);
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
                    InsightUi.Label(AquacultureUiStableIds.For("pond.row.status", entry.StableId), entry.StatusLine, InsightUiTextStyle.Caption)),
                InsightUi.Label(AquacultureUiStableIds.For("pond.row.detail", entry.StableId),
                    entry.Display.Population + " / " + entry.Display.Capacity + "   " + entry.Display.Temperature.ToString("0.#") + " C",
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
            speciesList.SetHeight(InsightLength.Fixed(300f));
            speciesList.Overscan = 2;
            speciesList.CacheLimit = 64;
            InsightUiElement detail = InsightUi.Scroll("species.detail.scroll", InsightUi.Column("species.detail.content").SetGap(7f).Add(
                InsightUi.SectionHeader("species.detail.header", L("AquacultureFishing.WorkspaceSpeciesDetail"),
                    L("AquacultureFishing.WorkspaceKnowledgeCanonical"), null, null, true),
                InsightUi.Label("species.detail.name", string.Empty).SetTextProvider(() => SelectedSpeciesLabel()),
                InsightUi.Label("species.detail.knowledge", string.Empty).SetTextProvider(() => SelectedSpeciesKnowledge()),
                InsightUi.Label("species.detail.facets", string.Empty, InsightUiTextStyle.Caption).SetTextProvider(SelectedSpeciesFacets),
                InsightUi.Callout("species.detail.knowledge-callout", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceKnowledgeTitle"), L("AquacultureFishing.WorkspaceKnowledgeBody"))));
            return InsightUi.Scroll("workspace.species.scroll", InsightUi.Column("workspace.species.content").SetGap(8f).Add(
                InsightUi.SectionHeader("species.header", L("AquacultureFishing.WorkspaceSpecies"),
                    L("AquacultureFishing.WorkspaceSpeciesSubtitle"), null, null, true), search,
                InsightUi.Split("species.split", InsightUi.Column("species.master").SetGap(6f).Add(speciesList), detail, 0.34f)));
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
                InsightUi.Label(AquacultureUiStableIds.For("species.stage", id), KnowledgeLine(snapshot), InsightUiTextStyle.Caption));
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
            breedList.SetHeight(InsightLength.Fixed(300f));
            breedList.Overscan = 2;
            breedList.CacheLimit = 64;
            InsightUiElement detail = InsightUi.Scroll("breeds.detail.scroll", InsightUi.Column("breeds.detail.content").SetGap(7f).Add(
                InsightUi.SectionHeader("breeds.detail.header", L("AquacultureFishing.WorkspaceBreedDetail"),
                    L("AquacultureFishing.WorkspaceBreedsSubtitle"), null, null, true),
                InsightUi.Label("breeds.detail.name", string.Empty).SetTextProvider(() => SelectedBreedName()),
                InsightUi.Label("breeds.detail.metrics", string.Empty).SetTextProvider(() => SelectedBreedMetrics()),
                InsightUi.Label("breeds.detail.traits", string.Empty, InsightUiTextStyle.Caption).SetTextProvider(SelectedBreedTraits),
                InsightUi.Callout("breeds.detail.rules", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceBreedRules"), L("AquacultureFishing.WorkspaceBreedRulesBody"))));
            return InsightUi.Scroll("workspace.breeds.scroll", InsightUi.Column("workspace.breeds.content").SetGap(8f).Add(
                InsightUi.SectionHeader("breeds.header", L("AquacultureFishing.WorkspaceBreeds"),
                    L("AquacultureFishing.WorkspaceBreedsSubtitle"), null, null, true), search,
                InsightUi.Split("breeds.split", InsightUi.Column("breeds.master").SetGap(6f).Add(breedList), detail, 0.34f)));
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
                InsightUi.Label(AquacultureUiStableIds.For("breed.stability", id), breed.Stability.ToStringPercent(), InsightUiTextStyle.Caption));
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
            waterList.SetHeight(InsightLength.Fixed(320f));
            waterList.Overscan = 2;
            waterList.CacheLimit = 64;
            return InsightUi.Scroll("workspace.conservation.scroll", InsightUi.Column("workspace.conservation.content").SetGap(8f).Add(
                InsightUi.SectionHeader("conservation.header", L("AquacultureFishing.WorkspaceConservation"),
                    L("AquacultureFishing.WorkspaceConservationSubtitle"), null, null, true), search, waterList,
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
                InsightUi.Label(AquacultureUiStableIds.For("water.abundance", id), water.AbundanceLabel, InsightUiTextStyle.Caption));
        }

        private InsightUiElement BuildCommissionsPage()
        {
            return InsightUi.Scroll("workspace.commissions.scroll", InsightUi.Column("workspace.commissions.content").SetGap(8f).Add(
                InsightUi.SectionHeader("commissions.header", L("AquacultureFishing.WorkspaceCommissions"),
                    L("AquacultureFishing.WorkspaceCommissionsSubtitle"), null, null, true),
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
            if (pondPopulationMeter != null)
            {
                pondPopulationMeter.Current = selected?.Display.Population ?? 0f;
                pondPopulationMeter.Maximum = Mathf.Max(1f, selected?.Display.Capacity ?? 1f);
                pondPopulationMeter.SetValueText(selected == null ? "" : selected.Display.Population + " / " + selected.Display.Capacity);
            }
            if (pondFeedMeter != null)
            {
                pondFeedMeter.Current = selected?.Display.FeedHours ?? 0f;
                pondFeedMeter.Maximum = Mathf.Max(1f, selected?.Display.FeedHours ?? 1f);
                pondFeedMeter.SetValueText(selected == null ? "" : selected.Display.FeedHours.ToString("0.#") + " h");
            }
            bool enabled = selected != null;
            adultsOnlyToggle.Enabled = enabled;
            protectFemalesToggle.Enabled = enabled;
            automaticFeedingToggle.Enabled = enabled;
            predationToggle.Enabled = enabled;
            surplusHarvestToggle.Enabled = enabled && selected?.PopulationTarget > 0;
            feedDaysSlider.Enabled = enabled;
            populationLimitSlider.Enabled = enabled;
            populationTargetSlider.Enabled = enabled;
            breedingModeSelect.Enabled = enabled && AquacultureProgression.IsAvailable("AF_SelectiveBreeding");
        }

        private void SetSelectedPond(Action<FishPondMapComponent, IntVec3> action)
        {
            PondEntry selected = SelectedPond;
            if (selected == null || action == null) return;
            action(selected.Component, selected.Cell);
            document.Invalidate();
        }

        private PondEntry SelectedPond => ponds.FirstOrDefault(item => item.StableId == selectedPondId);
        private AquacultureSpeciesViewSnapshot SelectedSpecies => species.FirstOrDefault(item => SpeciesId(item) == selectedSpeciesId);
        private FishBreedRecord SelectedBreed => breeds.FirstOrDefault(item => item.id == selectedBreedId);
        private NaturalWaterViewSnapshot SelectedWater => waters.FirstOrDefault(item => WaterId(item) == selectedWaterId);

        private string OverviewHealthText()
        {
            PondEntry worst = ponds.OrderByDescending(item => AquacultureHealthPriorityRules.Rank(item.Priority)).FirstOrDefault();
            return worst == null ? L("AquacultureFishing.WorkspaceNoPonds") :
                L("AquacultureFishing.WorkspaceWorstPond") + ": " + worst.Display.Label + " — " + worst.StatusLine;
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

        private string SelectedWaterDetails()
        {
            NaturalWaterViewSnapshot water = SelectedWater;
            if (water == null) return L("AquacultureFishing.WorkspaceSelectWater");
            return L("AquacultureFishing.WorkspaceWaterDetails") + ": " + water.AbundanceLabel + "  •  " +
                L("AquacultureFishing.WorkspaceSpeciesCount") + " " + water.species.Count;
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
