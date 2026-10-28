using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

        public static int Main(string[] args)
        {
            string root = FindRoot();
            // Existing managed test runners may forward unrelated CLI switches. Only the
            // explicit single-argument audit mode changes the normal full-suite behavior.
            if (args.Length == 1 && args[0] == "--dependency-audit")
            {
                TestStandaloneDependencyInventory(root);
                Console.WriteLine(failures == 0 ? "Aquaculture dependency audit: PASS (inventory only)" :
                    "Aquaculture dependency audit: FAIL (inventory errors)");
                return failures == 0 ? 0 : 1;
            }
            TestSnapshotsAndPurity();
            TestStableIdsAndResponsiveMath();
            TestContentAwareListSizing();
            TestProgressiveDisclosure();
            TestPrompt3AuthorityMatrix();
            TestPrompt2Contracts(root);
            TestMetadataAndBuildContract(root);
            TestStandaloneDependencyInventory(root);
            TestConditionalDeferredRealityPackageContract(root);
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
            Check(AquacultureUiResponsiveLayout.CompactColumns(480f) == 4,
                "finite compact navigation retains its column count");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Check(AquacultureUiResponsiveLayout.NavigationMode(invalid) == AquacultureUiNavigationMode.Compact &&
                    AquacultureUiResponsiveLayout.MasterDetailMode(invalid) == AquacultureUiMasterDetailMode.Stacked,
                    "invalid viewport widths retain a safe narrow layout");
                Check(AquacultureUiResponsiveLayout.CompactColumns(invalid) == 1,
                    "invalid compact viewport widths use one column");
                Check(AquacultureUiResponsiveLayout.NavigationMode(800f, invalid) == AquacultureUiNavigationMode.Rail &&
                    AquacultureUiResponsiveLayout.MasterDetailMode(800f, invalid) == AquacultureUiMasterDetailMode.Stacked,
                    "invalid breakpoints recover their documented defaults");
            }
            Check(AquacultureUiResponsiveLayout.CompactColumns(float.MaxValue, 1f, 0f, int.MaxValue) ==
                int.MaxValue, "huge finite widths are bounded before int conversion");
            Check(AquacultureUiResponsiveLayout.CompactColumns(480f, float.NaN, float.PositiveInfinity) == 4 &&
                AquacultureUiResponsiveLayout.CompactColumns(480f, float.NegativeInfinity, -10f) == 4,
                "invalid column metrics use finite positive defaults");
            Check(AquacultureUiResponsiveLayout.CompactColumns(480f, 96f, 5f, 0) == 1,
                "empty compact navigation keeps one usable column");
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
            Check(AquacultureUiListSizing.HeightForCount(int.MaxValue, float.MaxValue, 0f, 240f) == 240f,
                "overflow-scale finite content remains capped");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Check(AquacultureUiListSizing.HeightForCount(2, 48f, 0f, invalid) == 96f,
                    "invalid maximum heights use a finite fallback without clipping short lists");
                Check(AquacultureUiListSizing.HeightForCount(2, invalid, 12f, 100f) == 12f,
                    "invalid row heights cannot create non-finite viewports");
                Check(AquacultureUiListSizing.HeightForCount(2, 48f, invalid, 100f) == 96f,
                    "invalid minimum heights do not alter otherwise valid content");
            }
            Check(AquacultureUiListSizing.HeightForCount(int.MaxValue, float.MaxValue, 0f, float.NaN) == 480f &&
                AquacultureUiListSizing.HeightForCount(2, 48f, 999f, float.NaN) == 480f &&
                AquacultureUiListSizing.HeightForCount(0, 48f, 0f, float.NaN) == 0f,
                "invalid caps enforce a finite upper bound and empty-list collapse");
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
            string disclosureSource = File.ReadAllText(Path.Combine(root, "Source", "UI", "AquacultureUiDisclosure.cs"));
            Check(journalSource.Contains("AquacultureUiDisclosure.HasFacetForFish") &&
                 journalSource.Contains("AquacultureUiDisclosure.HasCompleteFacetForWater") &&
                 journalSource.Contains("ExactDiagnostics") && journalSource.Contains("string summary = entry.ExactDiagnostics") &&
                 disclosureSource.Contains("public static bool HasFacetForFish") &&
                 disclosureSource.Contains("public static bool HasCompleteFacetForWater") &&
                 disclosureSource.Contains("relevant.All") &&
                 disclosureSource.Contains("water.conservation.Keys.All"),
                 "pond and water diagnostics are scoped to the selected Knowledge subjects");
            Check(plannerOwnerSource.Contains("PlannerFactsKnownToColony") &&
                 !plannerOwnerSource.Contains("PruneUnknownPlanEntries") &&
                 plannerOwnerSource.Contains("PlanEntries.ToList()") &&
                 plannerOwnerSource.Contains("forecastEntries.Count") && plannerOwnerSource.Contains("int total = PlanEntries.Sum") &&
                 plannerOwnerSource.Contains("SpeciesView") && plannerOwnerSource.Contains("pond_compatibility") &&
                 plannerOwnerSource.Contains("feeding") && plannerOwnerSource.Contains("habitat"),
                 "stocking planner filters Knowledge-owned species facts for display without pruning authoritative plans");
        }

        private static void TestMetadataAndBuildContract(string root)
        {
            string aboutPath = Path.Combine(root, "About", "About.xml");
            XDocument about = XDocument.Load(aboutPath);
            IEnumerable<string> dependencyIds = about.Descendants("modDependencies").Descendants("packageId").Select(node => node.Value);
            IEnumerable<string> loadAfterIds = about.Descendants("loadAfter").Descendants("li").Select(node => node.Value);
            Check(dependencyIds.Contains("lan.insightcanvas"), "About.xml declares Insight Canvas dependency");
            Check(loadAfterIds.Contains("lan.insightcanvas"), "About.xml declares Insight Canvas loadAfter");

            using (JsonDocument stack = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".rimdev", "stack.json"))))
            using (JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "TestCatalog", "rimtest.catalog.json"))))
            {
                string recipeId = stack.RootElement.GetProperty("testRecipe").GetString();
                string owner = stack.RootElement.GetProperty("devBridgeProject").GetString();
                string recipePath = Path.Combine(root, ".rimdev", "recipes", recipeId + ".json");
                Check(recipeId == "aquaculture-development-smoke" && owner == "aquaculture" &&
                    catalog.RootElement.GetProperty("tests").EnumerateArray()
                        .Any(test => test.GetProperty("recipe").GetString() == recipeId),
                    "RimTest manifest and catalog reference the Aquaculture smoke recipe");
                Check(File.Exists(recipePath), "RimTest smoke recipe belongs to the Aquaculture repository");
                if (File.Exists(recipePath))
                {
                    using (JsonDocument recipe = JsonDocument.Parse(File.ReadAllText(recipePath)))
                    {
                        JsonElement rootRecipe = recipe.RootElement;
                        JsonElement budget = rootRecipe.GetProperty("budget");
                        Check(rootRecipe.GetProperty("schemaVersion").GetString() == "devbridge-test-recipe/v1" &&
                            rootRecipe.GetProperty("id").GetString() == recipeId &&
                            rootRecipe.GetProperty("projects").EnumerateArray()
                                .Select(projectId => projectId.GetString()).SequenceEqual(new[] { owner }),
                            "project-owned RimTest recipe has the correct schema, ID and single owner");
                        Check(rootRecipe.GetProperty("inputs").GetProperty("quicktest").GetBoolean() &&
                            rootRecipe.GetProperty("requiresReady").GetBoolean() &&
                            rootRecipe.GetProperty("success").GetProperty("quicktestReady").GetBoolean(),
                            "RimTest recipe requires real quicktest readiness evidence");
                        Check(budget.GetProperty("timeoutSeconds").GetInt32() is > 0 and <= 300 &&
                            budget.GetProperty("maxRimWorldLaunches").GetInt32() == 1 &&
                            budget.GetProperty("maxRecipeAttempts").GetInt32() == 1 &&
                            budget.GetProperty("maxCoordinatorRefreshes").GetInt32() <= 4 &&
                            budget.GetProperty("stopOnRepeatedFailureFingerprint").GetBoolean() &&
                            budget.GetProperty("maxRepeatedFailureCount").GetInt32() == 1,
                            "RimTest smoke recipe has bounded execution and repeated-failure limits");
                    }
                }
            }

            string project = File.ReadAllText(Path.Combine(root, "Source", "AquacultureFishing.csproj"));
            Check(project.Contains("Reference Include=\"InsightCanvas\""), "project references Insight Canvas directly");
            Check(project.Contains("<Private>false</Private>"), "framework references are not copied by the project");
            Check(project.Contains("InsightCanvasDir") && project.Contains("InsightCanvasAssemblyPath"),
                "Insight Canvas build paths are configurable");
            string providerProject = File.ReadAllText(Path.Combine(root, "Source", "DeferredRealityProvider", "DeferredReality.Aquaculture.csproj"));
            string harmonyEnvGuard = "'$(HarmonyPath)' == '' and '$(DEFERRED_REALITY_HARMONY_PATH)' != '' and Exists('$(DEFERRED_REALITY_HARMONY_PATH)')";
            Check(project.Contains(harmonyEnvGuard) && providerProject.Contains(harmonyEnvGuard) &&
                project.Contains("$(RimWorldDir)/../../workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll") &&
                !project.Contains("$(RimWorldDir)/../../steamapps/workshop"),
                "primary and provider builds use RimTest's verified Harmony path or the valid Steam Workshop layout");
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

        private static void TestStandaloneDependencyInventory(string root)
        {
            // AQ-DEP-01 inventories current blockers; it does not qualify a standalone game build.
            // Do not freeze today's optional dependencies as permanent test requirements.
            XDocument about = XDocument.Load(Path.Combine(root, "About", "About.xml"));
            XDocument mainProject = XDocument.Load(Path.Combine(root, "Source", "AquacultureFishing.csproj"));
            XDocument providerProject = XDocument.Load(Path.Combine(root, "Source",
                "DeferredRealityProvider", "DeferredReality.Aquaculture.csproj"));

            string[] requiredPackages = about.Descendants("modDependencies").Descendants("packageId")
                .Select(node => node.Value.Trim()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            string[] mainReferences = mainProject.Descendants()
                .Where(element => element.Name.LocalName == "Reference")
                .Select(element => (string)element.Attribute("Include"))
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] requiredBuildErrors = mainProject.Descendants()
                .Where(element => element.Name.LocalName == "Error")
                .Select(element => (string)element.Attribute("Text") ?? string.Empty).ToArray();
            bool separateDrfProvider = mainProject.Descendants()
                .Any(element => element.Name.LocalName == "Compile" &&
                    ((string)element.Attribute("Remove") ?? string.Empty).Contains("DeferredRealityProvider"));
            bool providerReferencesDrf = providerProject.Descendants()
                .Any(element => element.Name.LocalName == "Reference" &&
                    (string)element.Attribute("Include") == "DeferredRealityFramework");
            bool providerUsesPlayerAssemblyDirectory = providerProject.Descendants()
                .Where(element => element.Name.LocalName == "OutputPath")
                .Any(element => element.Value.Replace('\\', '/').Contains("1.6/Assemblies/"));
            bool packageIncludesVersionedAssemblies =
                File.ReadAllText(Path.Combine(root, ".rimdev", "stack.json")).Contains("\"1.*/**\"");

            string sourceRoot = Path.Combine(root, "Source");
            string[] coreSources = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    string relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
                    string surrounded = "/" + relative + "/";
                    return !relative.StartsWith("DeferredRealityProvider/", StringComparison.OrdinalIgnoreCase) &&
                        !surrounded.Contains("/obj/", StringComparison.OrdinalIgnoreCase) &&
                        !surrounded.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] insightUsers = coreSources
                .Where(path => UsesExternalNamespace(File.ReadAllText(path), "InsightCanvas")).ToArray();
            string[] knowledgeUsers = coreSources
                .Where(path => UsesExternalNamespace(File.ReadAllText(path), "KnowledgeFramework")).ToArray();
            string[] drfUsers = coreSources
                .Where(path => UsesExternalNamespace(File.ReadAllText(path), "DeferredReality")).ToArray();
            string[] defPaths = Directory.GetFiles(Path.Combine(root, "1.6", "Defs"), "*.xml",
                SearchOption.AllDirectories);
            string[] vfeDefs = defPaths
                .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bVCEF_[A-Za-z0-9_]+\b|\bVCE_Fishing\b"))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            int explicitAquacultureFishDefs = defPaths
                .SelectMany(path => XDocument.Load(path).Descendants("ThingDef"))
                .Count(def => ((string)def.Element("defName") ?? string.Empty)
                    .StartsWith("AF_", StringComparison.Ordinal) &&
                    def.Descendants("thingCategories").Descendants("li").Any(category =>
                        category.Value == "Fish" || category.Value == "VCEF_RawFishCategory"));

            var blockers = new List<string>();
            string[] optionalPackages = {
                "lan.insightcanvas", "lan.knowledgeframework", "lan.deferredreality.framework",
                "VanillaExpanded.VCEF"
            };
            foreach (string package in optionalPackages)
                if (requiredPackages.Contains(package))
                    blockers.Add("About.xml still requires " + package);
            if (mainReferences.Contains("InsightCanvas") ||
                requiredBuildErrors.Any(message => message.Contains("requires the installed InsightCanvas.dll")) ||
                insightUsers.Length > 0)
                blockers.Add("core build/source directly uses Insight Canvas");
            if (mainReferences.Contains("KnowledgeFramework") ||
                requiredBuildErrors.Any(message => message.Contains("requires KnowledgeFrameworkAssemblyPath")) ||
                knowledgeUsers.Length > 0)
                blockers.Add("core build/source directly uses Knowledge Framework");
            if (mainReferences.Contains("DeferredRealityFramework") || drfUsers.Length > 0)
                blockers.Add("core build/source directly uses Deferred Reality");
            if (vfeDefs.Length > 0)
                blockers.Add("core Defs reference VFE content, texture or category names");
            if (explicitAquacultureFishDefs == 0)
                blockers.Add("no first-party fish ThingDef has an explicit recognized Fish category");
            if (separateDrfProvider && providerReferencesDrf &&
                providerUsesPlayerAssemblyDirectory && packageIncludesVersionedAssemblies)
                blockers.Add("separate DRF provider outputs into a player-packaged assembly folder");
            bool legacyProviderArtifact = File.Exists(Path.Combine(root, "1.6", "Assemblies",
                "DeferredReality.Aquaculture.dll")) || File.Exists(Path.Combine(root, "1.6", "Assemblies",
                "DeferredReality.Aquaculture.manifest.json"));
            if (legacyProviderArtifact)
                blockers.Add("legacy provider artifact still exists in the source-tree core Assemblies directory");

            Console.WriteLine("AQ-DEP-01 static dependency audit (not runtime/release qualification)");
            Console.WriteLine("  Required mod packages: " + string.Join(", ", requiredPackages));
            Console.WriteLine("  Main project assembly references: " + string.Join(", ", mainReferences));
            Console.WriteLine("  Insight Canvas source references: " + DescribeDependencyPaths(root, insightUsers));
            Console.WriteLine("  Knowledge Framework source references: " + DescribeDependencyPaths(root, knowledgeUsers));
            Console.WriteLine("  Deferred Reality core source references: " + DescribeDependencyPaths(root, drfUsers));
            Console.WriteLine("  VFE content Def references: " + DescribeDependencyPaths(root, vfeDefs));
            Console.WriteLine("  First-party fish ThingDefs with explicit recognized category: " +
                explicitAquacultureFishDefs);
            Console.WriteLine("  DRF provider excluded from core: " + separateDrfProvider +
                "; DRF reference: " + providerReferencesDrf +
                "; build outputs in core package folder: " +
                (providerUsesPlayerAssemblyDirectory && packageIncludesVersionedAssemblies) +
                "; legacy source artifact: " + legacyProviderArtifact);
            foreach (string blocker in blockers)
                Console.WriteLine("  BLOCKER: " + blocker);
            Console.WriteLine("  Standalone readiness: " +
                (blockers.Count > 0 ? "BLOCKED (static blockers found)" :
                    "UNVERIFIED (requires standalone build, RimTest and player scenarios)"));

            Check(requiredPackages.Contains("brrainz.harmony"),
                "AQ-DEP-01 retains Harmony as the baseline required runtime mod");
            Check(UsesExternalNamespace("using InsightCanvas;", "InsightCanvas") &&
                UsesExternalNamespace("KnowledgeFramework.KnowledgeQuery.Facet()", "KnowledgeFramework") &&
                !UsesExternalNamespace("class MyKnowledgeFrameworkFeature {}", "KnowledgeFramework"),
                "AQ-DEP-01 detects using and qualified source references without substring false positives");
            Check(mainProject.Root != null && providerProject.Root != null,
                "AQ-DEP-01 parses both core and provider build contracts");
        }

        private static bool UsesExternalNamespace(string source, string name)
        {
            return Regex.IsMatch(source, @"(?m)^\s*using\s+" + Regex.Escape(name) +
                @"(?:\.[A-Za-z_][A-Za-z0-9_]*)*\s*;") ||
                source.Contains(name + ".", StringComparison.Ordinal);
        }

        private static string DescribeDependencyPaths(string root, IEnumerable<string> paths)
        {
            string[] names = paths.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            return names.Length == 0 ? "none" : string.Join(", ", names);
        }

        private static void TestConditionalDeferredRealityPackageContract(string root)
        {
            XDocument folders = XDocument.Load(Path.Combine(root, "LoadFolders.xml"));
            XElement version = folders.Root?.Element("v1.6");
            XElement conditional = version?.Elements("li").SingleOrDefault(element =>
                element.Value.Trim() == "1.6/DeferredReality");
            Check(version?.Elements("li").Any(element => element.Value.Trim() == "1.6" &&
                !element.HasAttributes) == true, "core load folder remains unconditional");
            Check(conditional != null &&
                (string)conditional.Attribute("IfModActive") == "lan.deferredreality.framework" &&
                conditional.Attributes().Count() == 1,
                "optional provider folder is gated exclusively by Deferred Reality activation");

            XDocument provider = XDocument.Load(Path.Combine(root, "Source", "DeferredRealityProvider",
                "DeferredReality.Aquaculture.csproj"));
            string providerOutput = provider.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "OutputPath")?.Value
                .Replace('\\', '/') ?? string.Empty;
            Check(providerOutput.Contains("1.6/DeferredReality/Assemblies/") &&
                !providerOutput.Contains("1.6/Assemblies/"),
                "provider output cannot overwrite the ordinary core Assemblies directory");

            string builder = File.ReadAllText(Path.Combine(root, "DevTools", "Build-DeferredRealityProvider.ps1"));
            string verifier = File.ReadAllText(Path.Combine(root, "DevTools", "Test-DeferredRealityProvider.ps1"));
            const string folder = @"1.6\DeferredReality\Assemblies\DeferredReality.Aquaculture.";
            Check(builder.Contains(folder + "dll") && builder.Contains(folder + "manifest.json") &&
                verifier.Contains(folder + "dll") && verifier.Contains(folder + "manifest.json"),
                "provider manifest generation and verification agree on conditional folder");

            string packager = File.ReadAllText(Path.Combine(root, "DevTools", "New-PlayerPackage.ps1"));
            string packageVerifier = File.ReadAllText(Path.Combine(root, "DevTools", "Test-PlayerPackage.ps1"));
            Check(packager.Contains("Copy-Item -LiteralPath $sourceAssembly") &&
                packager.Contains("$providerRelative = '1.6\\DeferredReality\\Assemblies'") &&
                !packager.Contains("foreach ($name in @('AquacultureFishing.dll', 'DeferredReality.Aquaculture.dll'"),
                "player package copies only the core DLL into unconditional Assemblies");
            Check(packager.Contains("$hasProviderAssembly -ne $hasProviderManifest") &&
                packager.Contains("Get-FileHash -LiteralPath $providerAssembly") &&
                packager.Contains("IfModActive") &&
                packager.Contains("Copy-Item -LiteralPath $providerManifest"),
                "optional provider packaging requires a verified paired assembly and manifest");
            Check(packageVerifier.Contains("Unconditional Assemblies folder must contain only AquacultureFishing.dll.") &&
                packageVerifier.Contains("unexpectedProviderPaths") &&
                packageVerifier.Contains("$baseAssemblies.Count -ne 1"),
                "package verifier rejects any provider assembly in the unconditional core directory");
            Check(packageVerifier.Contains("Optional DRF provider DLL/manifest pair is incomplete.") &&
                packageVerifier.Contains("ComputeHash($providerStream)") &&
                packageVerifier.Contains("IfModActive") &&
                packageVerifier.Contains("DeferredReality.Aquaculture.manifest.json"),
                "package verifier enforces conditional path and provider file integrity");

            using (JsonDocument stack = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".rimdev", "stack.json"))))
            {
                JsonElement rootStack = stack.RootElement;
                JsonElement runtime = rootStack.GetProperty("runtimePackage");
                string[] excludes = runtime.GetProperty("exclude").EnumerateArray()
                    .Select(item => item.GetString()).ToArray();
                string[] includes = runtime.GetProperty("include").EnumerateArray()
                    .Select(item => item.GetString()).ToArray();
                Check(rootStack.GetProperty("deploymentTarget").GetString() ==
                    "1.6/Assemblies/AquacultureFishing.dll" &&
                    includes.Contains("1.*/**") &&
                    excludes.Contains("1.6/Assemblies/DeferredReality.Aquaculture.dll") &&
                    excludes.Contains("1.6/Assemblies/DeferredReality.Aquaculture.manifest.json"),
                    "RimTest keeps its core DLL target and excludes both legacy unconditional provider artifacts");
            }
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
