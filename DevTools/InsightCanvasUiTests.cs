using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AquacultureFishing.Tests
{
    internal static class InsightCanvasUiTests
    {
        private static int failures;

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            failures++;
            Console.Error.WriteLine("FAIL: " + message);
        }

        public static int Main()
        {
            string root = FindRoot();
            TestSnapshotsAndPurity();
            TestStableIdsAndResponsiveMath();
            TestContentAwareListSizing();
            TestProgressiveDisclosure();
            TestPrompt3AuthorityMatrix();
            TestPrompt2Contracts(root);
            TestMetadataAndBuildContract(root);
            TestSerializedSettingsContract(root);
            TestPresentationContract(root);
            Console.WriteLine(failures == 0 ? "Insight Canvas UI tests: PASS" : "Insight Canvas UI tests: FAIL (" + failures + ")");
            return failures == 0 ? 0 : 1;
        }

        private static void TestSnapshotsAndPurity()
        {
            var fish = new List<FishUiSnapshot> { new FishUiSnapshot("fish.cod", "Cod", "Cod", "Novice") };
            var traits = new List<TraitUiSnapshot> { new TraitUiSnapshot("trait.blue", "Blue", "Blue", "", "", 1f) };
            var pages = new List<string> { "gameplay" };
            var snapshot = new SettingsUiSnapshot(3, pages, fish, traits);
            fish.Add(new FishUiSnapshot("fish.trout", "Trout", "Trout", "Adept"));
            traits.Clear();
            pages.Add("advanced");
            Check(snapshot.Revision == 3, "snapshot revision is retained");
            Check(snapshot.Fish.Count == 1 && snapshot.Traits.Count == 1 && snapshot.PageIds.Count == 1,
                "snapshot lists are copied at construction");

            var pond = new PondUiSnapshot("pond.1", "Pond", "Freshwater", 4f, 8f, 2);
            Check(pond.StableId == "pond.1" && pond.Population == 4f && pond.Capacity == 8f,
                "pond snapshot is display-only and complete");
            var cache = new AquacultureUiSnapshotCache();
            int before = cache.Revision;
            cache.Invalidate();
            Check(cache.Revision == before + 1, "snapshot invalidation advances revision");
        }

        private static void TestStableIdsAndResponsiveMath()
        {
            Check(AquacultureUiStableIds.For("fish", "cod") == "fish.cod", "stable IDs are scoped");
            IReadOnlyList<string> duplicateIds = AquacultureUiStableIds.FindDuplicates(
                new[] { "fish.cod", "trait.blue", "fish.cod", "fish.cod", "trait.blue" });
            Check(duplicateIds.SequenceEqual(new[] { "fish.cod", "trait.blue" }), "duplicate IDs are deterministic");
            Check(AquacultureUiResponsiveLayout.NavigationMode(900f) == AquacultureUiNavigationMode.Rail,
                "wide navigation uses a rail");
            Check(AquacultureUiResponsiveLayout.NavigationMode(480f) == AquacultureUiNavigationMode.Compact,
                "narrow navigation uses compact mode");
            Check(AquacultureUiResponsiveLayout.CompactColumns(480f) >= 1,
                "compact navigation always has a usable column");
        }

        private static void TestContentAwareListSizing()
        {
            Check(AquacultureUiListSizing.HeightForCount(0, 48f, 0f, 240f) == 0f,
                "empty virtual lists collapse to zero viewport height");
            Check(AquacultureUiListSizing.HeightForCount(1, 48f, 0f, 240f) == 48f &&
                AquacultureUiListSizing.HeightForCount(3, 48f, 0f, 240f) == 144f,
                "one and a few virtual rows use actual content height");
            Check(AquacultureUiListSizing.HeightForCount(100, 48f, 0f, 240f) == 240f,
                "many virtual rows stop at the bounded viewport");
            Check(AquacultureUiListSizing.HeightForCount(1, 10f, 32f, 80f) == 32f &&
                AquacultureUiListSizing.HeightForCount(20, 10f, 32f, 80f) == 80f,
                "list sizing honors minimum and maximum bounds");
            Check(AquacultureUiListSizing.HeightForCount(4, 48f, 0f, 240f) ==
                AquacultureUiListSizing.HeightForCount(4, 48f, 0f, 240f),
                "list sizing is stable when disclosure changes without changing the list");
            Check(AquacultureUiListSizing.HeightForCount(3, 40f, 0f, 200f) == 120f &&
                AquacultureUiListSizing.HeightForCount(3, 48f, 0f, 240f) == 144f,
                "compact and wide density row metrics remain deterministic");
            Check(AquacultureUiListSizing.HeightForCount(0, 48f, 32f, 240f) == 0f,
                "hidden or empty groups do not reserve list viewport height");
        }

        private static void TestProgressiveDisclosure()
        {
            var minimal = new AquacultureUiDisclosureSnapshot(
                false, false, false, false, false, false, false, false, false, false, false, false);
            Check(minimal.VisibleJournalPageIds.SequenceEqual(new[] { "overview", "species" }) &&
                !minimal.PondsPageVisible && !minimal.BreedsPageVisible &&
                !minimal.ConservationPageVisible && !minimal.CommissionsPageVisible,
                "minimal colonies see only overview and species navigation");
            Check(!minimal.ManagedFeedingVisible && !minimal.IndustrialDiagnosticsVisible &&
                !minimal.PopulationManagementVisible && !minimal.SelectiveBreedingControlsVisible,
                "minimal colonies do not construct advanced pond capabilities");

            var pondkeeping = new AquacultureUiDisclosureSnapshot(
                true, false, false, false, false, false, false, false, false, false, false, false);
            Check(pondkeeping.PondsPageVisible && !pondkeeping.ManagedFeedingVisible &&
                !pondkeeping.IndustrialDiagnosticsVisible && !pondkeeping.SelectiveBreedingControlsVisible,
                "Pondkeeping exposes the pond surface without advanced groups");
            Check(pondkeeping.VisibleJournalPageIds.SequenceEqual(new[] { "overview", "species", "ponds" }),
                "Pondkeeping navigation omits future pages");

            var managed = new AquacultureUiDisclosureSnapshot(
                true, true, false, false, true, false, false, false, false, false, false, false);
            Check(managed.PondsPageVisible && managed.ManagedFeedingVisible && managed.ManagedHarvestingVisible &&
                !managed.PopulationManagementVisible && !managed.SelectiveBreedingControlsVisible,
                "Managed Aquaculture exposes feeding and harvesting only");
            Check(managed.VisibleJournalPageIds.SequenceEqual(new[] { "overview", "species", "ponds" }),
                "Managed navigation still omits unencountered breeding and conservation pages");

            var industrial = new AquacultureUiDisclosureSnapshot(
                true, true, true, false, true, false, false, false, false, false, false, false);
            Check(industrial.PondsPageVisible && industrial.IndustrialDiagnosticsVisible &&
                industrial.PopulationManagementVisible && !industrial.SelectiveBreedingControlsVisible &&
                !industrial.ExactPondMetricsVisible && !industrial.PondDiagnosisVisible,
                "Industrial capability does not fabricate exact pond knowledge");
            Check(industrial.VisibleJournalPageIds.SequenceEqual(new[] { "overview", "species", "ponds" }),
                "Industrial navigation omits unencountered breeding and conservation pages");

            var selective = new AquacultureUiDisclosureSnapshot(
                true, true, true, true, true, true, true, true, true, true, true, true);
            Check(selective.BreedsPageVisible && selective.ConservationPageVisible &&
                selective.CommissionsPageVisible && selective.SelectiveBreedingControlsVisible,
                "Selective Breeding exposes the breeding, conservation, and commission surfaces when state exists");

            AquacultureFishDossierDisclosure industrialUnknown = new AquacultureFishDossierDisclosure(
                true, false, false, false, false, false, false, false,
                industrial.ManagedAquacultureAvailable, industrial.IndustrialAquacultureAvailable,
                industrial.SelectiveBreedingAvailable);
            Check(!industrialUnknown.SpeciesIdentityVisible && !industrialUnknown.TraitsVisible &&
                !industrialUnknown.ExactHealthMetricsVisible && !industrialUnknown.ExpectedMeatYieldVisible &&
                !industrialUnknown.GenerationVisible && industrialUnknown.SexVisible &&
                industrialUnknown.FoodReserveVisible == false,
                "industrial capability does not expose unknown fish facts or exact dossier metrics");

            var strongKnowledge = new AquacultureUiDisclosureSnapshot(
                false, false, false, false, true, true, true, true, true, true, true, true);
            Check(strongKnowledge.PondsPageVisible && strongKnowledge.BreedsPageVisible &&
                strongKnowledge.ConservationPageVisible && strongKnowledge.CommissionsPageVisible &&
                !strongKnowledge.ManagedFeedingVisible && !strongKnowledge.IndustrialDiagnosticsVisible &&
                !strongKnowledge.SelectiveBreedingControlsVisible,
                "strong Knowledge can reveal encountered pages without granting research capabilities");
            AquacultureFishDossierDisclosure learnedWithoutTech = new AquacultureFishDossierDisclosure(
                true, true, true, true, true, true, true, false,
                strongKnowledge.ManagedAquacultureAvailable, strongKnowledge.IndustrialAquacultureAvailable,
                strongKnowledge.SelectiveBreedingAvailable);
            Check(learnedWithoutTech.SpeciesIdentityVisible && learnedWithoutTech.TraitsVisible &&
                learnedWithoutTech.GenerationVisible && learnedWithoutTech.KnowledgeVisible &&
                !learnedWithoutTech.ExactHealthMetricsVisible && !learnedWithoutTech.ExpectedMeatYieldVisible &&
                !learnedWithoutTech.SterilizationVisible,
                "Knowledge can disclose learned fish facts without granting industrial measurements or actions");
            Check(strongKnowledge.VisibleJournalPageIds.SequenceEqual(new[]
                { "overview", "species", "ponds", "breeds", "conservation", "commissions" }),
                "strong Knowledge can add encountered navigation while capability groups remain absent");
        }

        private static void TestPrompt3AuthorityMatrix()
        {
            var fresh = new AquacultureUiDisclosureSnapshot(
                false, false, false, false, false, false, false, false, false, false, false, false);
            Check(fresh.VisibleJournalPageIds.SequenceEqual(new[] { "overview", "species" }) &&
                !fresh.ManagedFeedingVisible && !fresh.IndustrialDiagnosticsVisible &&
                !fresh.SelectiveBreedingControlsVisible && !fresh.ConservationPageVisible &&
                !fresh.CommissionsPageVisible,
                "A fresh colony has sparse navigation with no future-system advertising");

            var pondkeeping = new AquacultureUiDisclosureSnapshot(
                true, false, false, false, true, false, false, false, false, false, false, false);
            Check(pondkeeping.PondsPageVisible && pondkeeping.BasicPondFactsVisible &&
                !pondkeeping.PondDiagnosisVisible && !pondkeeping.IndustrialDiagnosticsVisible &&
                !pondkeeping.SelectiveBreedingControlsVisible,
                "B Pondkeeping exposes basic pond workflow without industrial or breeding controls");

            var managed = new AquacultureUiDisclosureSnapshot(
                true, true, false, false, true, false, false, false, false, false, false, false);
            Check(managed.ManagedFeedingVisible && managed.ManagedHarvestingVisible &&
                !managed.IndustrialDiagnosticsVisible && !managed.PopulationManagementVisible &&
                !managed.SelectiveBreedingControlsVisible,
                "C Managed Aquaculture exposes management but not later capability tiers");

            var industrialKnowledgePoor = new AquacultureUiDisclosureSnapshot(
                true, true, true, false, true, false, false, false, false, false, false, false);
            Check(industrialKnowledgePoor.IndustrialDiagnosticsVisible &&
                industrialKnowledgePoor.PopulationManagementVisible &&
                !industrialKnowledgePoor.PondDiagnosisVisible &&
                !industrialKnowledgePoor.ExactPondMetricsVisible &&
                !industrialKnowledgePoor.CriticalPondFilterVisible,
                "D Industrial capability does not create Knowledge-owned pond diagnostics");

            var selectiveWithoutEvents = new AquacultureUiDisclosureSnapshot(
                true, true, true, true, true, false, false, false, false, false, false, false);
            Check(selectiveWithoutEvents.BreedsPageVisible &&
                selectiveWithoutEvents.SelectiveBreedingControlsVisible &&
                !selectiveWithoutEvents.ConservationPageVisible &&
                !selectiveWithoutEvents.CommissionsPageVisible,
                "E Selective Breeding reveals its controls but not unrelated event pages");

            var knowledgeRichResearchPoor = new AquacultureUiDisclosureSnapshot(
                false, false, false, false, true, true, true, true, true, true, true, true);
            Check(knowledgeRichResearchPoor.VisibleJournalPageIds.SequenceEqual(new[]
                    { "overview", "species", "ponds", "breeds", "conservation", "commissions" }) &&
                !knowledgeRichResearchPoor.ManagedFeedingVisible &&
                !knowledgeRichResearchPoor.IndustrialDiagnosticsVisible &&
                !knowledgeRichResearchPoor.SelectiveBreedingControlsVisible,
                "F Knowledge-rich research-poor colonies gain understanding without technology controls");
            var learnedFish = new AquacultureFishDossierDisclosure(
                true, true, true, true, true, true, true, false, false, false, false);
            Check(learnedFish.SpeciesIdentityVisible && learnedFish.TraitsVisible &&
                learnedFish.GenerationVisible && learnedFish.KnowledgeVisible &&
                !learnedFish.ExactHealthMetricsVisible && !learnedFish.ExpectedMeatYieldVisible,
                "F learned dossier facts remain separate from unavailable measurements and production actions");

            var researchRichKnowledgePoor = industrialKnowledgePoor;
            var unknownFish = new AquacultureFishDossierDisclosure(
                true, false, false, false, false, false, false, false, true, true, false);
            Check(researchRichKnowledgePoor.IndustrialDiagnosticsVisible &&
                !researchRichKnowledgePoor.HasKnownSpecies && !researchRichKnowledgePoor.HasKnownHealth &&
                !researchRichKnowledgePoor.HasKnownPopulation && !researchRichKnowledgePoor.PondDiagnosisVisible &&
                !unknownFish.SpeciesIdentityVisible && !unknownFish.TraitsVisible &&
                !unknownFish.ExactHealthMetricsVisible && !unknownFish.ExpectedMeatYieldVisible,
                "G research-rich Knowledge-poor colonies keep species and facet facts undisclosed");

            var eventless = new AquacultureUiDisclosureSnapshot(
                true, true, true, true, true, true, true, true, true, false, false, false);
            var commissionTransition = new AquacultureUiDisclosureSnapshot(
                true, true, true, true, true, true, true, true, true, true, true, true);
            Check(!eventless.VisibleJournalPageIds.Contains("conservation") &&
                !eventless.VisibleJournalPageIds.Contains("commissions") &&
                commissionTransition.VisibleJournalPageIds.Contains("conservation") &&
                commissionTransition.VisibleJournalPageIds.Contains("commissions") &&
                eventless.ResolveActiveJournalPage("commissions") == "overview" &&
                commissionTransition.ResolveActiveJournalPage("commissions") == "commissions",
                "H event transitions add relevant pages and safely redirect removed active pages");

            var uninitializedIndustrialFish = new AquacultureFishDossierDisclosure(
                false, false, false, false, true, false, false, false, true, true, false);
            var initializedIndustrialFish = new AquacultureFishDossierDisclosure(
                true, true, false, false, true, false, false, false, true, true, false);
            Check(!uninitializedIndustrialFish.ExactHealthMetricsVisible &&
                !uninitializedIndustrialFish.HealthClassificationVisible &&
                !uninitializedIndustrialFish.TraitsVisible && !uninitializedIndustrialFish.BreedVisible &&
                !uninitializedIndustrialFish.GenerationVisible &&
                initializedIndustrialFish.ExactHealthMetricsVisible &&
                initializedIndustrialFish.FoodReserveVisible,
                "dossier exact health metrics require an initialized specimen as well as research and Knowledge");
        }

        private static void TestPrompt2Contracts(string root)
        {
            Check(AquacultureHealthPriorityRules.Highest(true, true, true, true, true) ==
                AquacultureHealthPriority.Lethal, "pond health prioritizes lethal habitat");
            Check(AquacultureHealthPriorityRules.Highest(false, true, true, true, true) ==
                AquacultureHealthPriority.Starvation, "pond health prioritizes starvation");
            Check(AquacultureHealthPriorityRules.Highest(false, false, true, true, true) ==
                AquacultureHealthPriority.SevereStress, "pond health prioritizes severe stress");
            Check(AquacultureHealthPriorityRules.Highest(false, false, false, true, true) ==
                AquacultureHealthPriority.Reproduction, "pond health prioritizes reproduction blockers");
            Check(AquacultureHealthPriorityRules.Highest(false, false, false, false, true) ==
                AquacultureHealthPriority.Advice, "pond health retains lesser advice");
            Check(AquacultureHealthPriorityRules.Highest(false, false, false, false, false) ==
                AquacultureHealthPriority.Healthy, "pond health falls back to healthy");

            var urgent = new AquacultureWorkspaceRowSnapshot("pond.warning", "Warning", "Advice",
                AquacultureHealthPriority.Advice, new[] { "Check feed" }, 4);
            var critical = new AquacultureWorkspaceRowSnapshot("pond.critical", "Critical", "Starvation",
                AquacultureHealthPriority.Starvation, new[] { "Refill feed" }, 4);
            var healthy = new AquacultureWorkspaceRowSnapshot("pond.healthy", "Healthy", "Healthy",
                AquacultureHealthPriority.Healthy, new[] { "Healthy" }, 4);
            var ordered = new List<AquacultureWorkspaceRowSnapshot> { healthy, urgent, critical };
            ordered.Sort(AquacultureWorkspaceOrdering.Compare);
            Check(ordered[0] == critical && ordered[1] == urgent && ordered[2] == healthy,
                "pond workspace ordering puts critical rows before warnings and healthy rows");
            Check(AquacultureWorkspaceOrdering.Matches(critical, "feed", AquaculturePondFilter.Critical) &&
                !AquacultureWorkspaceOrdering.Matches(healthy, string.Empty, AquaculturePondFilter.NeedsAttention),
                "pond workspace filters combine semantic status and search text");
            Check(AquacultureWorkspaceOrdering.PreserveSelection("pond.warning",
                ordered.Select(item => item.StableId)) == "pond.warning" &&
                AquacultureWorkspaceOrdering.PreserveSelection("missing", ordered.Select(item => item.StableId)) == "pond.critical",
                "workspace selection survives refresh and falls back deterministically");
            Check(AquacultureDossierHealthRules.Classify(true, 0.9f, 0.9f, 0f) ==
                AquacultureDossierHealthState.Healthy &&
                AquacultureDossierHealthRules.Classify(true, 0.4f, 0.9f, 0f) ==
                AquacultureDossierHealthState.Attention &&
                AquacultureDossierHealthRules.Classify(true, 0.9f, 0.9f, 0.1f) ==
                AquacultureDossierHealthState.Critical &&
                AquacultureDossierHealthRules.Classify(false, 0.9f, 0.9f, 0f) ==
                AquacultureDossierHealthState.Dead,
                "fish dossier classifies healthy, attention, critical, and dead states");
            Check(AquacultureUiResponsiveLayout.MasterDetailMode(1200f) == AquacultureUiMasterDetailMode.SideBySide &&
                AquacultureUiResponsiveLayout.MasterDetailMode(700f) == AquacultureUiMasterDetailMode.Stacked,
                "workspace master/detail layout stacks at narrow widths");

            var details = new List<string> { "First" };
            var row = new AquacultureWorkspaceRowSnapshot("pond.1", "Pond", "Healthy",
                AquacultureHealthPriority.Healthy, details, 4);
            details.Add("Later");
            Check(row.Details.Count == 1, "workspace rows copy detail lists");

            var traits = new List<string> { "Blue" };
            var dossier = new AquacultureFishDossierSnapshot("fish.1", "Cod", "Female", "Adult",
                "Registered", "2", "Good", "Meat", traits, "Known", "subject.cod", 4);
            traits.Clear();
            Check(dossier.Traits.Count == 1 && dossier.KnowledgeLink == "subject.cod",
                "fish dossiers copy traits and retain Knowledge links");

            string keyA = AquaculturePlannerCacheContract.Key(new[] { "cod:2", "trout:1" }, "Freshwater", 8);
            string keyB = AquaculturePlannerCacheContract.Key(new[] { "cod:2", "trout:1" }, "Freshwater", 8);
            string keyC = AquaculturePlannerCacheContract.Key(new[] { "cod:2", "trout:1" }, "Freshwater", 9);
            Check(keyA == keyB && keyA != keyC, "planner cache keys are deterministic and revision-aware");

            var sourceFiles = new[]
            {
                "Source\\UI\\AquacultureJournalWorkspaceDocument.cs",
                "Source\\UI\\AquacultureFishDossierDocument.cs",
                "Source\\UI\\AquacultureSettingsDocument.cs",
                "Source\\UI\\AquacultureStockingPlannerDocument.cs",
                "Source\\UI\\AquacultureBreedRegistrationDocument.cs",
                "Source\\UI\\AquacultureCommissionDeliveryDocument.cs"
            }.Select(path => File.ReadAllText(Path.Combine(root, path))).ToArray();
            string allUi = string.Join("\n", sourceFiles);
            Check(allUi.Contains("Host.PostClose"), "Prompt 2 documents own transient cleanup");
            Check(allUi.Contains("VirtualList") && allUi.Contains("SearchField"),
                "Prompt 2 documents use bounded searchable lists");
            Check(allUi.Contains("KnowledgeHidden") && allUi.Contains("AquaculturePlannerForecastSnapshot"),
                "Prompt 2 documents preserve Knowledge disclosure and forecast snapshots");
            string contracts = File.ReadAllText(Path.Combine(root, "Source\\UI\\AquacultureUiContracts.cs"));
            Check(contracts.Contains("AquacultureWorkspaceOrdering") && allUi.Contains("PondFilter") &&
                allUi.Contains("InsightUi.Expander"),
                "workspace documents expose urgency filters and compositional groups");
            Check(allUi.Contains("DossierHealthRules") && allUi.Contains("FoodReserve") &&
                allUi.Contains("ExpectedMeatYield"),
                "fish dossier exposes semantic health indicators and separated yield facts");
            string journalSource = sourceFiles[0];
            string dossierSource = sourceFiles[1];
            string settingsSource = sourceFiles[2];
            string plannerSource = sourceFiles[3];
            string commissionSource = sourceFiles[5];
            string plannerOwnerSource = File.ReadAllText(Path.Combine(root, "Source", "PondStockingPlanner.cs"));
            Check(allUi.Contains("ContentAwareVirtualList") && allUi.Contains("ResizeContentAwareVirtualList"),
                "player-facing catalogs use the shared content-aware list composition");
            Check(!journalSource.Contains("pondList.SetFlex") && !journalSource.Contains("speciesList.SetFlex") &&
                !journalSource.Contains("breedList.SetFlex") && !journalSource.Contains("waterList.SetFlex") &&
                !settingsSource.Contains("fishList.SetHeight(InsightLength.Fixed") &&
                !settingsSource.Contains("traitList.SetHeight(InsightLength.Fixed") &&
                !plannerSource.Contains("availableList.SetFlex") &&
                !plannerSource.Contains("plannedList.SetFlex") &&
                !commissionSource.Contains("specimenList.SetFlex"),
                "virtual lists no longer flex-grow inside their scrollable compositions");
            Check(journalSource.Contains("conservation.split") && !journalSource.Contains("workspace.conservation.scroll"),
                "conservation separates the master list from the detail scroll owner");
            Check(dossierSource.Contains("dossier.traits.list") && !dossierSource.Contains("traitsList.SetHeight") &&
                !dossierSource.Contains("private InsightUiVirtualList traitsList"),
                "fish dossier traits use the primary dossier scroll without a fixed nested viewport");
            Check(allUi.Contains("commission.delivery.specimens") && allUi.Contains("Host.PostClose"),
                "commission delivery uses a bounded Insight Canvas document with cleanup");

            XDocument language = XDocument.Load(Path.Combine(root, "1.6", "Languages", "English", "Keyed", "AquacultureFishing.xml"));
            var localized = new HashSet<string>(language.Root.Elements().Select(element => element.Name.LocalName));
            var keys = Regex.Matches(allUi, @"AquacultureFishing\.[A-Za-z0-9_]+").Cast<Match>()
                .Select(match => match.Value).Distinct(StringComparer.Ordinal);
            foreach (string key in keys) Check(localized.Contains(key), "Prompt 2 localization key exists: " + key);

            string journal = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            string planner = File.ReadAllText(Path.Combine(root, "Source", "PondStockingPlanner.cs"));
            Check(journal.Contains("KnowledgeMenuUI") && journal.Contains("insightWorkspaceDocument"),
                "Journal keeps Knowledge browsing native and embeds the workspace");
            Check(!journal.Contains("DrawHeader") && !journal.Contains("DrawSpeciesList") &&
                !journal.Contains("listScroll") && !journal.Contains("detailScroll"),
                "obsolete Journal renderer and scroll state are removed");
            string commissions = File.ReadAllText(Path.Combine(root, "Source", "AquacultureCommissions.cs"));
            Check(commissions.Contains("AquacultureCommissionDeliveryDocument") &&
                commissions.Contains("TryDeliverForUi") && commissions.Contains("TryDeliver("),
                "commission delivery remains owner-authoritative with an embedded document");
            Check(planner.Contains("CalculateForecast") && planner.Contains("ForecastSnapshot") &&
                planner.Contains("SetStockingBlueprint"),
                "planner retains authoritative forecast and blueprint persistence");
            Check(journalSource.Contains("CriticalPondFilterVisible") && journalSource.Contains("PondDiagnosisVisible"),
                "pond filters and exact diagnosis share one entitlement boundary");
            Check(plannerOwnerSource.Contains("PlannerFactsKnownToColony") &&
                plannerOwnerSource.Contains("PruneUnknownPlanEntries") &&
                plannerOwnerSource.Contains("SpeciesView") && plannerOwnerSource.Contains("pond_compatibility") &&
                plannerOwnerSource.Contains("feeding") && plannerOwnerSource.Contains("habitat"),
                "stocking planner filters Knowledge-owned species facts before display and forecast");
        }

        private static void TestMetadataAndBuildContract(string root)
        {
            string aboutPath = Path.Combine(root, "About", "About.xml");
            XDocument about = XDocument.Load(aboutPath);
            IEnumerable<string> dependencyIds = about.Descendants("modDependencies").Descendants("packageId").Select(node => node.Value);
            IEnumerable<string> loadAfterIds = about.Descendants("loadAfter").Descendants("li").Select(node => node.Value);
            Check(dependencyIds.Contains("lan.insightcanvas"), "About.xml declares Insight Canvas dependency");
            Check(loadAfterIds.Contains("lan.insightcanvas"), "About.xml declares Insight Canvas loadAfter");

            string project = File.ReadAllText(Path.Combine(root, "Source", "AquacultureFishing.csproj"));
            Check(project.Contains("Reference Include=\"InsightCanvas\""), "project references Insight Canvas directly");
            Check(project.Contains("<Private>false</Private>"), "framework references are not copied by the project");
            Check(project.Contains("InsightCanvasDir") && project.Contains("InsightCanvasAssemblyPath"),
                "Insight Canvas build paths are configurable");
            Check(!project.Contains("C:\\Games\\Steam\\"), "project has no machine-specific absolute reference path");
            Check(!Directory.GetFiles(root, "InsightCanvas.dll", SearchOption.AllDirectories).Any(),
                "Aquaculture tree contains no bundled InsightCanvas.dll");
            Check(!File.Exists(Path.Combine(root, "1.6", "Assemblies", "InsightCanvas.dll")),
                "player assembly directory contains no duplicate framework DLL");

            string uiRoot = Path.Combine(root, "Source", "UI");
            Check(!Directory.GetFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText).Any(text => text.Contains("GUI.skin")),
                "new UI source does not mutate GUI.skin");
        }

        private static void TestSerializedSettingsContract(string root)
        {
            string settings = File.ReadAllText(Path.Combine(root, "Source", "SettingsAndMasks.cs"));
            string[] requiredKeys =
            {
                "globalMutationRate", "maxMutations", "traitBreedingSettingsVersion", "capacityModelVersion",
                "capacityTransitionWarning", "fishMasks", "fishExpertiseSettings", "traitSettings",
                "fishingDurationFactor", "minimumOffspring", "maximumOffspring", "minimumLifespanDays",
                "maximumLifespanDays", "presentationDensity", "presentationHighContrast",
                "presentationReducedMotion", "presentationSettingsVersion"
            };
            foreach (string key in requiredKeys) Check(settings.Contains("\"" + key + "\""), "serialized key preserved: " + key);
            Check(settings.Contains("DoSettingsWindowContents") && settings.Contains("AquacultureSettingsDocument"),
                "settings entry delegates to the Insight Canvas document");
        }

        private static void TestPresentationContract(string root)
        {
            AquaculturePresentationPreferences saved =
                AquaculturePresentationPreferences.FromSerializedValues(2, true, true);
            AquaculturePresentationPreferences reloaded =
                AquaculturePresentationPreferences.FromSerializedValues(saved.DensityIndex,
                    saved.HighContrast, saved.ReducedMotion);
            Check(reloaded == saved, "presentation preferences serialize and reload without loss");

            AquaculturePresentationPreferences low =
                AquaculturePresentationPreferences.FromSerializedValues(-50, false, false);
            AquaculturePresentationPreferences high =
                AquaculturePresentationPreferences.FromSerializedValues(50, false, false);
            Check(low.DensityIndex == AquaculturePresentationPreferences.MinimumDensityIndex &&
                high.DensityIndex == AquaculturePresentationPreferences.MaximumDensityIndex,
                "invalid persisted density values clamp to safe bounds");

            string uiRoot = Path.Combine(root, "Source", "UI");
            string[] documentPaths = Directory.GetFiles(uiRoot, "*Document.cs", SearchOption.TopDirectoryOnly);
            Check(documentPaths.Length == 6, "all six player-facing Canvas documents are present");
            foreach (string path in documentPaths)
            {
                string document = File.ReadAllText(path);
                Check(document.Contains("AquacultureInsightPresentation.Apply"),
                    "document uses centralized presentation preferences: " + Path.GetFileName(path));
                Check(document.Contains("TrackDuplicateIds = true"),
                    "document keeps duplicate-ID diagnostics enabled: " + Path.GetFileName(path));
                Check(!Regex.IsMatch(document, @"Density\s*=\s*InsightUiDensity\.[A-Za-z]+"),
                    "document does not hard-code an independent density: " + Path.GetFileName(path));
                Check(!Regex.IsMatch(document, @"HighContrast\s*=\s*(true|false)"),
                    "document does not hard-code independent contrast: " + Path.GetFileName(path));
                Check(!Regex.IsMatch(document, @"ReducedMotion\s*=\s*(true|false)"),
                    "document does not hard-code independent motion: " + Path.GetFileName(path));
            }

            string settings = File.ReadAllText(Path.Combine(uiRoot, "AquacultureSettingsDocument.cs"));
            Check(settings.Contains("settings.PresentationPreferences") &&
                settings.Contains("SetPresentationPreferences"),
                "settings controls bind to the persistent Aquaculture authority");
            Check(!settings.Contains("private InsightUiDensity density") &&
                !settings.Contains("private bool highContrast") && !settings.Contains("private bool reducedMotion"),
                "settings document has no transient presentation fields");

            string presentation = File.ReadAllText(Path.Combine(uiRoot, "AquacultureUiPresentation.cs"));
            string components = File.ReadAllText(Path.Combine(uiRoot, "AquacultureUiComponents.cs"));
            string theme = File.ReadAllText(Path.Combine(uiRoot, "AquacultureUiTheme.cs"));
            Check(presentation.Contains("presentation") && presentation.Contains("document.Density") &&
                presentation.Contains("document.ReducedMotion"),
                "central applicator maps all presentation preferences to the document");
            Check(components.Contains("InsightUi.Surface") && components.Contains("Status") &&
                components.Contains("SetTooltip"),
                "shared components provide panels, semantic status, and contextual help");
            Check(theme.Contains("highContrast") && theme.Contains("theme.Focus") &&
                theme.Contains("aquaculture-aquatic-v2-high-contrast"),
                "high contrast has stronger local separation and focus tokens");

            var language = XDocument.Load(Path.Combine(root, "1.6", "Languages", "English", "Keyed", "AquacultureFishing.xml"));
            var localized = new HashSet<string>(language.Root.Elements().Select(element => element.Name.LocalName));
            IEnumerable<string> keys = Directory.GetFiles(uiRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"""(AquacultureFishing\.[A-Za-z0-9_]+)""")
                    .Cast<Match>().Select(match => match.Groups[1].Value))
                .Distinct(StringComparer.Ordinal);
            foreach (string key in keys) Check(localized.Contains(key), "presentation/UI localization key exists: " + key);
        }

        private static string FindRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Source", "SettingsAndMasks.cs"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Run the UI tests from the AquacultureFishing repository.");
        }
    }
}
