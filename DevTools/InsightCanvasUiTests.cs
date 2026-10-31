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
            TestRimWorld17BuildContracts(root);
            TestStandaloneDependencyInventory(root);
            TestNativeFishingRankBoundary(root);
            TestExplicitNativeFishIdentityBoundary(root);
            TestSelfContainedFishProcessingDefs(root);
            TestFirstPartyFreshwaterFishPrototype(root);
            TestNativeFishingJobBoundary(root);
            TestNativeFishingReadOnlyRodPreflight(root);
            TestNativeFishingRuntimeSmokeCoverage(root);
            TestNativeKnowledgeBuildSeam(root);
            TestKnowledgeJournalAdapterBoundary(root);
            TestJournalWorkspaceProviderBoundary(root);
            TestNativeJournalOverviewSpeciesPrototype(root);
            TestStockingPlannerRendererBoundary(root);
            TestNativeStockingPlannerRendererPrototype(root);
            TestBreedRegistrationRendererBoundary(root);
            TestNativeBreedRegistrationRendererPrototype(root);
            TestCommissionDeliveryRendererBoundary(root);
            TestNativeCommissionDeliveryRendererPrototype(root);
            TestFishDossierRendererBoundary(root);
            TestNativeFishDossierRendererPrototype(root);
            TestNativeJournalExpertiseFallback(root);
            TestNativeKnowledgeFallbackBoundary(root);
            TestNativeColonyKnowledgeBoundary(root);
            TestConditionalDeferredRealityPackageContract(root);
            TestSerializedSettingsContract(root);
            TestSettingsRendererProviderBoundary(root);
            TestNativeGeneralSettingsRendererPrototype(root);
            TestNativeSpecializedSettingsRendererPrototype(root);
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
            string knowledgeAdapter = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeAdapter.cs"));
            Check(journal.Contains("expertiseMenu.Draw(knowledgeRect);") &&
                journal.Contains("AquacultureKnowledgeAdapter.ExpertiseMenu") &&
                journal.Contains("insightWorkspaceDocument") &&
                knowledgeAdapter.Contains("KnowledgeMenuUI.Draw(bounds, state, ExpertiseModelFor, ExpertiseFor);"),
                "Journal keeps canonical Knowledge browsing behind the adapter and embeds the workspace");
            Check(!journal.Contains("DrawHeader") && !journal.Contains("DrawSpeciesList") &&
                !journal.Contains("listScroll") && !journal.Contains("detailScroll"),
                "obsolete Journal renderer and scroll state are removed");
            string commissions = File.ReadAllText(Path.Combine(root, "Source", "AquacultureCommissions.cs"));
            Check(commissions.Contains("IAquacultureCommissionDeliveryView") &&
                commissions.Contains("AquacultureCommissionDeliveryViewFactory.Create(this)") &&
                commissions.Contains("TryDeliverForUi") && commissions.Contains("TryDeliver("),
                "commission delivery remains owner-authoritative behind an Aquaculture-owned view");
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
                project.Contains("$(RimWorldDir)/../../workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll") &&
                !project.Contains("$(RimWorldDir)/../../steamapps/workshop"),
                "primary and provider 1.7 builds use explicit Harmony overrides or the versioned workshop layout");
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

        private static void TestRimWorld17BuildContracts(string root)
        {
            string main = File.ReadAllText(Path.Combine(root, "Source", "AquacultureFishing.csproj"));
            string provider = File.ReadAllText(Path.Combine(root, "Source", "DeferredRealityProvider",
                "DeferredReality.Aquaculture.csproj"));
            string bridge = File.ReadAllText(Path.Combine(root, "DevTools", "RimBridgeTools",
                "AquacultureFishing.BridgeTools.csproj"));

            foreach (string project in new[] { main, provider, bridge })
            {
                XDocument xml = XDocument.Parse(project);
                Check(xml.Descendants("TargetFramework").Single().Value == "net10.0" &&
                    project.Contains("<GenerateDependencyFile>false</GenerateDependencyFile>") &&
                    !project.Contains("net472") &&
                    !project.Contains("RimWorldWin64_Data") &&
                    !project.Contains("RimWorldManagedDir") &&
                    !project.Contains("UnityEngine.CoreModule") &&
                    !project.Contains("UnityEngine.IMGUIModule") &&
                    !project.Contains("1.6/") &&
                    !project.Contains(@"1.6\"),
                    "owned RimWorld build projects use only .NET 10 and never silently select 1.6 game references");
            }

            Check(main.Contains("Reference Include=\"Assembly-CSharp\"") &&
                main.Contains("Reference Include=\"Waylith\"") &&
                main.Contains("Reference Include=\"UnityStubs\"") &&
                main.Contains("RimWorldAssemblyDir") &&
                main.Contains("RimWorldDir") &&
                main.Contains("$(RIMWORLD_ROOT)") &&
                main.Contains("!Exists('$(RimWorldAssemblyDir)/Waylith.dll')") &&
                main.Contains("!Exists('$(RimWorldAssemblyDir)/UnityStubs.dll')") &&
                main.Contains("!Exists('$(RimWorldAssemblyDir)/Assembly-CSharp.dll')"),
                "core 1.7 build references exact installed Waylith, UnityStubs and game assemblies");
            Check(main.Contains("2009463077/1.7/Assemblies/0Harmony.dll") &&
                main.Contains("Harmony 3") &&
                main.Contains("InsightCanvas/") == false &&
                main.Contains("InsightCanvasAssemblyPath") &&
                main.Contains("KnowledgeFrameworkAssemblyPath") &&
                main.Contains("KnowledgeFramework") &&
                main.Contains("1.7") &&
                main.Contains("AquacultureNativeKnowledge") &&
                main.Replace('\\', '/').Contains("1.7/Assemblies/"),
                "core build retains explicit native knowledge seam and requires 1.7 framework references");

            Check(provider.Contains("Reference Include=\"Assembly-CSharp\"") &&
                provider.Contains("Reference Include=\"Waylith\"") &&
                provider.Contains("Reference Include=\"UnityStubs\"") &&
                provider.Contains("ValidateDeferredRealityReferences") &&
                provider.Contains("$(DeferredRealityRoot)") &&
                provider.Contains("1.7") &&
                provider.Replace('\\', '/').Contains("1.7/DeferredReality/Assemblies/") &&
                !provider.Contains("UnityEngine.CoreModule"),
                "conditional DRF provider is independently 1.7-targeted without old Unity module references");
            Check(bridge.Contains("1.7") &&
                bridge.Contains("ValidateBridgeToolReferences") &&
                bridge.Contains("AquacultureFishingAssemblyPath") &&
                bridge.Contains("RimBridgeSdkAssemblyPath") &&
                bridge.Replace('\\', '/').Contains("artifacts/BridgeTools/AquacultureFishing/"),
                "developer-only bridge references a 1.7 SDK and keeps non-player output");

            XDocument about = XDocument.Load(Path.Combine(root, "About", "About.xml"));
            XDocument loaders = XDocument.Load(Path.Combine(root, "LoadFolders.xml"));
            Check(about.Descendants("supportedVersions").Elements("li")
                    .Any(version => version.Value == "1.6") &&
                loaders.Descendants("v1.6").Any(),
                "build-only stage does not prematurely advertise a runnable 1.7 player package");
            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-17-01A") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("not a runnable 1.7 release") &&
                compatibility.Contains("RimTest"),
                "1.7 build-contract prototype honestly documents unresolved runtime and packaging gates");
        }

        private static void TestExplicitNativeFishIdentityBoundary(string root)
        {
            string core = File.ReadAllText(Path.Combine(root, "Source", "AquacultureCore.cs"));
            string contracts = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeContract.cs"));
            string guide = File.ReadAllText(Path.Combine(root, "About", "CONTENT_EXTENSION_GUIDE.md"));
            string recipes = File.ReadAllText(Path.Combine(root, "1.6", "Defs", "RecipeDefs.xml"));

            int start = core.IndexOf("public static bool IsFish(ThingDef def)", StringComparison.Ordinal);
            int end = start < 0 ? -1 : core.IndexOf("[StaticConstructorOnStartup]", start, StringComparison.Ordinal);
            string identity = start >= 0 && end > start ? core.Substring(start, end - start) : string.Empty;
            Check(identity.Contains("if (def == null) return false;") &&
                identity.Contains("VCEF_RawFishCategory") &&
                identity.Contains("c.defName == \"Fish\"") &&
                identity.Contains("Aquariums.ThingComp_AquariumFish") &&
                identity.Contains("def.GetModExtension<AquaticSpeciesExtension>() != null") &&
                identity.Contains("FishIdentityRules.Recognizes(category, aquarium, nativeMarker);"),
                "fish recognition retains VFE, vanilla and Aquariums markers and adds opt-in native extension");
            Check(core.Contains("DefDatabase<ThingDef>.AllDefs.Where(FishUtility.IsFish).ToList();") &&
                core.Contains("FishUtility.RegisterRuntimeFish(def);") &&
                core.Contains("ConfigureRuntimeFishDef(def);") &&
                core.Contains("ConfigureFishProcessingRecipe(fishDefs);") &&
                core.Contains("recipe.fixedIngredientFilter.SetAllow(fishDef, true);") &&
                core.Contains("ingredient.filter.SetAllow(fishDef, true);"),
                "startup fish registration and owned processing recipe share the recognized fish list");
            Check(contracts.Contains("public static class FishIdentityRules") &&
                contracts.Contains("hasRecognizedFishCategory || hasAquariumFishComp ||") &&
                !contracts.Contains("using RimWorld;") &&
                !contracts.Contains("using Verse;") &&
                guide.Contains("category-independent positive fish marker") &&
                guide.Contains("Do not add it to non-fish items") &&
                recipes.Contains("AF_RecognizedFishIngredient") &&
                !recipes.Contains("VCEF_RawFishCategory"),
                "native identity contract is pure and fish processing no longer uses VFE categories");
        }

        private static void TestSelfContainedFishProcessingDefs(string root)
        {
            string defsRoot = Path.Combine(root, "1.6", "Defs");
            XDocument recipes = XDocument.Load(Path.Combine(defsRoot, "RecipeDefs.xml"));
            XDocument things = XDocument.Load(Path.Combine(defsRoot, "ThingDefs.xml"));
            string[] defPaths = Directory.GetFiles(defsRoot, "*.xml", SearchOption.AllDirectories);

            const string ownedCategory = "AF_RecognizedFishIngredient";
            XElement[] categories = recipes.Root.Elements("ThingCategoryDef")
                .Where(def => (string)def.Element("defName") == ownedCategory).ToArray();
            Check(categories.Length == 1 &&
                (string)categories[0].Element("parent") == "Root",
                "fish-processing placeholder category exists once and uses only vanilla Root");
            XElement[] assigned = defPaths.SelectMany(path => XDocument.Load(path).Descendants("ThingDef"))
                .Where(def => def.Descendants("thingCategories").Elements("li")
                    .Any(item => item.Value.Trim() == ownedCategory)).ToArray();
            Check(assigned.Length == 0,
                "the owned recipe-only ingredient category starts empty, including for all non-fish defs");

            XElement[] processing = recipes.Root.Elements("RecipeDef").Where(def =>
                (string)def.Element("defName") == "AF_ProcessDeadFish").ToArray();
            Check(processing.Length == 1, "the existing dead-fish recipe identity is unchanged");
            if (processing.Length != 1) return;
            XElement recipe = processing[0];
            XElement[] ingredientFilters = recipe.Element("ingredients")?.Elements("li")
                .Select(item => item.Element("filter")).ToArray() ?? Array.Empty<XElement>();
            XElement fixedFilter = recipe.Element("fixedIngredientFilter");
            Func<XElement, bool> restrictedToOwnedCategory = filter => filter != null &&
                filter.Element("categories")?.Elements("li")
                    .Select(item => item.Value.Trim()).SequenceEqual(new[] { ownedCategory }) == true &&
                !filter.Elements("thingDefs").Any() &&
                !filter.Elements("specialFiltersToAllow").Any();
            Check(ingredientFilters.Length == 1 &&
                ingredientFilters.All(restrictedToOwnedCategory) &&
                restrictedToOwnedCategory(fixedFilter) &&
                recipe.Element("ingredients")?.Element("li")?.Element("count")?.Value == "1",
                "both processing filters initially admit only the empty owned category, never other meat");
            Check((string)recipe.Element("products")?.Element("AF_FishMeat") == "5" &&
                recipe.Element("recipeUsers")?.Elements("li").Select(e => e.Value)
                    .SequenceEqual(new[] { "ButcherSpot", "TableButcher" }) == true,
                "processing products, workstations and base yield remain unchanged");

            XElement[] fishMeat = things.Root.Elements("ThingDef").Where(def =>
                (string)def.Element("defName") == "AF_FishMeat").ToArray();
            Check(fishMeat.Length == 1, "the existing saved fish-meat ThingDef identity is preserved");
            if (fishMeat.Length == 1)
            {
                XElement meat = fishMeat[0];
                Check((string)meat.Element("graphicData")?.Element("texPath") ==
                    "Things/Item/Resource/MeatFoodRaw/Meat_Small" &&
                    (string)meat.Element("graphicData")?.Element("graphicClass") == "Graphic_StackCount" &&
                    (string)meat.Element("statBases")?.Element("MarketValue") == "2" &&
                    (string)meat.Element("statBases")?.Element("Mass") == "0.03" &&
                    (string)meat.Element("statBases")?.Element("Nutrition") == "0.05" &&
                    meat.Element("thingCategories")?.Elements("li").Single().Value == "MeatRaw" &&
                    meat.Element("comps")?.Elements("li").Any(e =>
                        (string)e.Attribute("Class") == "AquacultureFishing.CompProperties_FishMeatTraits") == true,
                    "fish meat uses vanilla generic small meat art and retains existing food and save fields");
            }

            Check(defPaths.All(path => !Regex.IsMatch(File.ReadAllText(path),
                    @"\bVCEF_[A-Za-z0-9_]+\b|\bVCE_Fishing\b")),
                "Aquaculture core Def XML contains no statically linked VFE category or texture");
            string core = File.ReadAllText(Path.Combine(root, "Source", "AquacultureCore.cs"));
            string lifecycle = File.ReadAllText(Path.Combine(root, "Source", "FishLifeCycle.cs"));
            Check(core.Contains("DefDatabase<ThingDef>.AllDefs.Where(FishUtility.IsFish).ToList();") &&
                core.Contains("ConfigureFishProcessingRecipe(fishDefs);") &&
                core.Contains("recipe.fixedIngredientFilter.SetAllow(fishDef, true);") &&
                core.Contains("ingredient.filter.SetAllow(fishDef, true);") &&
                lifecycle.Contains("!FishUtility.IsRuntimeFish(t.def)") &&
                lifecycle.Contains("CompFishTraits traits = t.TryGetComp<CompFishTraits>();") &&
                lifecycle.Contains("__result = !traits.IsAlive;"),
                "recipe runtime population remains fish-only and rejects living specimens");

            XDocument about = XDocument.Load(Path.Combine(root, "About", "About.xml"));
            Check(about.Descendants("modDependencies").Descendants("packageId")
                .Any(node => node.Value == "VanillaExpanded.VCEF"),
                "VFE player dependency remains required until real load/bill/save validation");
        }

        private static void TestFirstPartyFreshwaterFishPrototype(string root)
        {
            string defsRoot = Path.Combine(root, "1.6", "Defs");
            string[] paths = Directory.GetFiles(defsRoot, "*.xml", SearchOption.AllDirectories);
            XDocument[] documents = paths.Select(XDocument.Load).ToArray();
            XElement[] allThingDefs = documents.SelectMany(document => document.Descendants("ThingDef")).ToArray();
            XElement[] categoryDefs = documents.SelectMany(document => document.Descendants("ThingCategoryDef"))
                .Where(def => (string)def.Element("defName") == "AF_NativeFish").ToArray();
            Check(categoryDefs.Length == 1 &&
                (string)categoryDefs[0].Element("parent") == "Root",
                "first-party fish storage category is owned and has a vanilla root");

            XElement[] fishDefs = allThingDefs
                .Where(def => (string)def.Element("defName") == "AF_PondMinnow").ToArray();
            Check(fishDefs.Length == 1, "one stable first-party native minnow Def is present");
            if (fishDefs.Length != 1) return;
            XElement fish = fishDefs[0];
            string[] categoryAssignments = allThingDefs.Where(def =>
                def.Element("thingCategories")?.Elements("li")
                    .Any(item => item.Value == "AF_NativeFish") == true)
                .Select(def => (string)def.Element("defName")).ToArray();
            Check(categoryAssignments.SequenceEqual(new[] { "AF_PondMinnow" }) &&
                (string)fish.Attribute("ParentName") == "ResourceBase" &&
                (string)fish.Element("thingClass") == "ThingWithComps" &&
                (string)fish.Element("stackLimit") == "1" &&
                (string)fish.Element("drawerType") == "RealtimeOnly" &&
                fish.Element("ingestible") == null &&
                fish.Element("comps")?.Elements("li").Any(node =>
                    (string)node.Attribute("Class") == "CompProperties_Forbiddable") == true,
                "native fish is uniquely categorized, unstackable, non-ingestible, and comp-ready");

            XElement species = fish.Element("modExtensions")?.Elements("li")
                .SingleOrDefault(node => (string)node.Attribute("Class") ==
                    "AquacultureFishing.AquaticSpeciesExtension");
            Check(species != null &&
                (string)species.Element("overrideDiet") == "true" &&
                (string)species.Element("diet") == "Omnivore" &&
                (string)species.Element("overrideWater") == "true" &&
                (string)species.Element("waterKind") == "Freshwater" &&
                species.Element("compatibleHabitats")?.Elements("li").Select(node => node.Value)
                    .SequenceEqual(new[] { "Pond", "Lake", "River", "Marsh" }) == true,
                "native species identity and freshwater habitats use no VFE dependencies");

            bool Bounded(XElement element, float minimum, float maximum) =>
                float.TryParse(element?.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value) &&
                !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= minimum && value <= maximum;
            Check(Bounded(fish.Element("statBases")?.Element("Mass"), 0.01f, 1f) &&
                Bounded(fish.Element("statBases")?.Element("Nutrition"), 0.01f, 0.5f) &&
                Bounded(fish.Element("statBases")?.Element("MarketValue"), 0.01f, 20f) &&
                Bounded(species?.Element("populationDensity"), 0.01f, 1f) &&
                (string)species?.Element("populationRarity") == "Common" &&
                Bounded(species?.Element("populationMinimumTemperature"), 0f, 10f) &&
                Bounded(species?.Element("populationMaximumTemperature"), 25f, 38f) &&
                Bounded(species?.Element("minimumTemperature"), -5f, 10f) &&
                Bounded(species?.Element("maximumTemperature"), 25f, 40f),
                "first-party fish has conservative finite ecology, nutrition, and population values");

            XDocument things = XDocument.Load(Path.Combine(defsRoot, "ThingDefs.xml"));
            XElement fishMeat = things.Descendants("ThingDef").Single(def =>
                (string)def.Element("defName") == "AF_FishMeat");
            Check((string)fish.Element("graphicData")?.Element("texPath") ==
                    (string)fishMeat.Element("graphicData")?.Element("texPath") &&
                (string)fish.Element("graphicData")?.Element("graphicClass") == "Graphic_Single" &&
                (string)fish.Element("graphicData")?.Element("color") == "(0.55,0.70,0.75)" &&
                fish.Element("thingCategories")?.Elements("li")
                    .All(item => item.Value != "Fish" && item.Value != "VCEF_RawFishCategory") == true,
                "fish uses only existing generic vanilla placeholder art and Aquaculture categories");

            string core = File.ReadAllText(Path.Combine(root, "Source", "AquacultureCore.cs"));
            string audit = File.ReadAllText(Path.Combine(root, "DevTools", "InsightCanvasUiTests.cs"));
            Check(core.Contains("def.GetModExtension<AquaticSpeciesExtension>() != null") &&
                core.Contains("DefDatabase<ThingDef>.AllDefs.Where(FishUtility.IsFish).ToList();") &&
                core.Contains("ConfigureFishProcessingRecipe(fishDefs);") &&
                audit.Contains("extension.Attribute(\"Class\") == \"AquacultureFishing.AquaticSpeciesExtension\""),
                "runtime discovers extension-marked fish and static readiness inventory recognizes it");

            Check(paths.All(path => !Regex.IsMatch(File.ReadAllText(path),
                @"\bVCEF_[A-Za-z0-9_]+\b|\bVCE_Fishing\b")) &&
                allThingDefs.Any(def => (string)def.Element("defName") == "AF_FishMeat") &&
                allThingDefs.Any(def => (string)def.Element("defName") == "AF_FishEgg") &&
                allThingDefs.Any(def => (string)def.Element("defName") == "AF_FishFeed"),
                "first-party fish adds content without restoring VFE Def refs or renaming saved items");

            XDocument recipe = XDocument.Load(Path.Combine(defsRoot, "RecipeDefs.xml"));
            XElement fishRecipe = recipe.Descendants("RecipeDef").Single(def =>
                (string)def.Element("defName") == "AF_ProcessDeadFish");
            Check(fishRecipe.Descendants("categories").Elements("li")
                    .All(node => node.Value == "AF_RecognizedFishIngredient") &&
                !fish.Descendants("thingCategories").Elements("li")
                    .Any(node => node.Value == "AF_RecognizedFishIngredient"),
                "native fish does not pre-populate the owned dead-fish processing filter");
            string guide = File.ReadAllText(Path.Combine(root, "About", "CONTENT_EXTENSION_GUIDE.md"));
            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(guide.Contains("AF_PondMinnow") &&
                compatibility.Contains("provisional generic-meat graphic") &&
                compatibility.Contains("native fishing job prototype") &&
                compatibility.Contains("RimTest") &&
                !compatibility.Contains("VFE is now optional"),
                "developer prototype discloses art, native job and game/save qualification gaps");
        }

        private static void TestNativeFishingJobBoundary(string root)
        {
            XDocument jobs = XDocument.Load(Path.Combine(root, "1.6", "Defs", "PondWorkDefs.xml"));
            XElement[] jobDefs = jobs.Descendants("JobDef").Where(def =>
                (string)def.Element("defName") == "AF_NativeFishing").ToArray();
            XElement[] giverDefs = jobs.Descendants("WorkGiverDef").Where(def =>
                (string)def.Element("defName") == "AF_NativeFishing").ToArray();
            Check(jobDefs.Length == 1 && giverDefs.Length == 1 &&
                (string)jobDefs[0].Element("driverClass") == "AquacultureFishing.JobDriver_NativeFishing" &&
                (string)giverDefs[0].Element("giverClass") == "AquacultureFishing.WorkGiver_NativeFishing" &&
                (string)giverDefs[0].Element("workType") == "Handling" &&
                jobs.Descendants("JobDef").Any(def =>
                    (string)def.Element("defName") == "AF_HarvestPondFish"),
                "owned native fishing jobs are declared once without displacing pond harvests");

            string code = File.ReadAllText(Path.Combine(root, "Source", "PondHarvesting.cs"));
            string progression = File.ReadAllText(Path.Combine(root, "Source", "FishingExpertise.cs"));
            string rods = File.ReadAllText(Path.Combine(root, "Source", "FishingRods.cs"));
            string pure = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeContract.cs"));
            Check(code.Contains("class WorkGiver_NativeFishing : WorkGiver_Scanner") &&
                code.Contains("class JobDriver_NativeFishing : JobDriver") &&
                code.Contains("NativeFishingProviderRules.CanProvide(") &&
                code.Contains("ModsConfig.IsActive(\"VanillaExpanded.VCEF\")") &&
                code.Contains("ModsConfig.IsActive(\"Ludeon.RimWorld.Odyssey\")") &&
                pure.Contains("public static class NativeFishingProviderRules") &&
                pure.Contains("return !vanillaFishingExpandedActive && !odysseyFishingActive;"),
                "native fishing does not compete with enabled VFE or Odyssey providers");
            Check(code.Contains("GenRadial.RadialCellsAround(pawn.Position, SearchRadius, true)") &&
                code.Contains("public const float SearchRadius = 14f;") &&
                code.Contains("GenAdj.CardinalDirections") &&
                code.Contains("terrain?.IsWater == true && terrain.defName != \"AF_Pond\"") &&
                !code.Contains("foreach (IntVec3 cell in map.AllCells)") &&
                code.Contains("candidate.Standable(pawn.Map)") &&
                code.Contains("pawn.CanReserveAndReach(candidate, PathEndMode.OnCell, Danger.Some,"),
                "native work scanning stays bounded to natural water with a reachable bank");
            Check(code.Contains("populations.TryGetPreparedSummary(") &&
                code.Contains("summary.presentSpecies.Any(fish => FishUtility.IsRuntimeFish(fish)") &&
                code.Contains("MinimumExpertiseFor(fish)") &&
                code.Contains("CanContinue(pawn) && FishingRodWorkflow.HasAvailableRod(pawn)") &&
                code.Contains("!pawn.Downed && !pawn.Drafted") &&
                code.Contains("!pawn.IsPrisoner && !pawn.IsSlave"),
                "native jobs require prepared fish, species rank and a selectable rod");
            Check(code.Contains("pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed)") &&
                code.Contains("FishingRodWorkflow.TryReserve(this, errorOnFailed, out _)") &&
                code.Contains("FishingRodWorkflow.PreparationToils(this)") &&
                code.Contains("FishingRodUtility.FishingPhases(") &&
                code.Contains("FishingAttemptIntegration.NativeFramework") &&
                rods.Contains("public static IEnumerable<Toil> FishingPhases(object driver, string framework)"),
                "native driver reuses existing safe rod workflow and cast/wait/reel phases");

            int driverStart = code.IndexOf("public sealed class JobDriver_NativeFishing", StringComparison.Ordinal);
            int driverEnd = driverStart < 0 ? -1 : code.IndexOf("public static class FishHarvestUtility", driverStart,
                StringComparison.Ordinal);
            string driver = driverStart >= 0 && driverEnd > driverStart
                ? code.Substring(driverStart, driverEnd - driverStart) : string.Empty;
            int place = driver.IndexOf("GenPlace.TryPlaceThing(", StringComparison.Ordinal);
            int complete = driver.IndexOf("progression.Complete(caught)", StringComparison.Ordinal);
            Check(driver.Contains("AddFinishAction(_ => FishingProgressionComponent.Current?") &&
                driver.Contains(".CancelAttempt(pawn, FishingAttemptIntegration.NativeFramework)") &&
                driver.Contains("progression.Resolve(pawn,") &&
                driver.Contains("NativeFishingWork.StillAvailable(pawn, attempt)") &&
                driver.Contains("caught.ApplyTo(fish);") &&
                driver.Contains("fish.stackCount = 1;") &&
                place >= 0 && complete > place &&
                driver.Contains("else if (!fish.Destroyed) fish.Destroy();"),
                "native catches consume populations and credit knowledge only after successful placement");

            Check(progression.Contains("public const string NativeFramework = \"AquacultureNative\"") &&
                progression.Contains("public static FishingAttemptRecord ProgressNative(") &&
                progression.Contains("populations.TryGetPreparedSummary(cell, out NaturalFishPopulationSummary summary)") &&
                progression.Contains("if (framework == NativeFramework) return ProgressNative(") &&
                progression.Contains("if (framework == VfeFramework) return ProgressVfe(") &&
                progression.Contains("if (framework == OdysseyFramework) return ProgressOdyssey(") &&
                progression.Contains("public void CancelAttempt(Pawn pawn, string framework)") &&
                progression.Contains("attempts.RemoveAll(item => item?.pawn == pawn && item.framework == framework)") &&
                progression.Contains("completedCatchEventIds.Contains(logicalEventId)") &&
                progression.Contains("\"aquacultureFishingCompletedCatchEvents\"") &&
                progression.Contains("\"aquacultureFishingAttempts\""),
                "native completion reuses exactly-once event ledger without changing legacy saved IDs");
            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-04D1") &&
                compatibility.Contains("native fishing job prototype") &&
                compatibility.Contains("RimTest") &&
                compatibility.Contains("not a qualified standalone fishing release"),
                "native prototype documents live build, job and save validation requirements");
        }

        private static void TestNativeFishingReadOnlyRodPreflight(string root)
        {
            string rods = File.ReadAllText(Path.Combine(root, "Source", "FishingRodWorkflow.cs"));
            int probeStart = rods.IndexOf("public static bool HasAvailableRod(Pawn pawn)",
                StringComparison.Ordinal);
            int probeEnd = probeStart < 0 ? -1 : rods.IndexOf(
                "public static bool TryEnsureSelection(Pawn pawn, Job job, out string reason)",
                probeStart, StringComparison.Ordinal);
            string probe = probeStart >= 0 && probeEnd > probeStart
                ? rods.Substring(probeStart, probeEnd - probeStart) : string.Empty;
            Check(probe.Contains("FishingRodUtility.EquippedRodThing(pawn) != null") &&
                probe.Contains("FishingRodWorkflowComponent.Current == null") &&
                probe.Contains("pawn.IsPrisoner || pawn.IsSlave") &&
                probe.Contains("BestInventoryRod(pawn) != null || BestStoredRod(pawn) != null"),
                "speculative native fishing rod eligibility checks equipped, inventory and approved storage paths");
            Check(!Regex.IsMatch(probe,
                    @"\b(?:TryEnsureSelection|NewSession|RestoreStaleSession|AddEquipment|TryReserve|" +
                    @"PrepareToil|Reserve|TryStartCarry|TryTransferEquipmentToContainer)\s*\(") &&
                !probe.Contains("component.Add(") && !probe.Contains("component.Remove(") &&
                !probe.Contains("FishingRodWorkflowComponent.Current?.Find("),
                "read-only rod probe cannot reserve, equip, restore or create saved fishing-rod sessions");

            string code = File.ReadAllText(Path.Combine(root, "Source", "PondHarvesting.cs"));
            int giverStart = code.IndexOf("public sealed class WorkGiver_NativeFishing",
                StringComparison.Ordinal);
            int driverStart = giverStart < 0 ? -1 : code.IndexOf(
                "public sealed class JobDriver_NativeFishing", giverStart, StringComparison.Ordinal);
            string giver = giverStart >= 0 && driverStart > giverStart
                ? code.Substring(giverStart, driverStart - giverStart) : string.Empty;
            Check(code.Contains("CanContinue(pawn) && FishingRodWorkflow.HasAvailableRod(pawn)") &&
                code.Contains("if (!CanGive(pawn) || !HasEligibleFish(pawn, water)) return false;") &&
                giver.Contains("NativeFishingWork.CanGive(pawn)") &&
                giver.Contains("NativeFishingWork.TryFindShore(pawn, cell, forced, out") &&
                giver.Contains("return JobMaker.MakeJob(def, cell, shore);") &&
                !giver.Contains("TryEnsureSelection(") &&
                !giver.Contains("FishingRodWorkflowComponent.") &&
                !giver.Contains("FishingRodWorkflow.TryReserve("),
                "native workgiver may offer jobs but never mutate rod sessions before assignment");
            Check(code.Contains("if (!CanContinue(pawn) || !NaturalWaterAt(pawn.Map, water)) return false;") &&
                code.Contains("FishingRodWorkflow.TryReserve(this, errorOnFailed, out _)") &&
                code.Contains("FishingRodWorkflow.PreparationToils(this)") &&
                rods.Contains("if (!TryEnsureSelection(pawn, job, out reason))"),
                "carried rod does not abort active catch and actual job start remains authoritative");
        }

        private static void TestNativeFishingRuntimeSmokeCoverage(string root)
        {
            string source = File.ReadAllText(Path.Combine(root, "Source", "AquacultureInGameTests.cs"));
            string bridge = File.ReadAllText(Path.Combine(root, "DevTools", "RimBridgeTools",
                "AquacultureFishingBridgeTools.cs"));
            int start = source.IndexOf(
                "Check(report, \"native-fishing-def-and-provider-contract\", () =>",
                StringComparison.Ordinal);
            int end = start < 0 ? -1 : source.IndexOf(
                "Check(report, \"fish-def-runtime-configuration\", () =>", start,
                StringComparison.Ordinal);
            string smoke = start >= 0 && end > start ? source.Substring(start, end - start) : "";
            Check(source.Contains("#if AQUACULTURE_DEV_TESTS") &&
                source.Contains("private static AquacultureInGameTestReport Run()") &&
                source.Contains("return Complete(Run(), runId);") &&
                bridge.Contains("\"aquaculture/run_baseline\"") &&
                bridge.Contains("AquacultureBridgeTestSuite.RunBaseline(runId)") &&
                start >= 0 && end > start,
                "native fishing smoke is invoked by the existing authorized developer baseline entry point");
            Check(smoke.Contains("DefDatabase<JobDef>.GetNamedSilentFail(\"AF_NativeFishing\")") &&
                smoke.Contains("typeof(JobDriver_NativeFishing)") &&
                smoke.Contains("DefDatabase<WorkGiverDef>.GetNamedSilentFail(\"AF_NativeFishing\")") &&
                smoke.Contains("typeof(WorkGiver_NativeFishing)") &&
                smoke.Contains("DefDatabase<ThingDef>.GetNamedSilentFail(\"AF_PondMinnow\")") &&
                smoke.Contains("minnow.GetModExtension<AquaticSpeciesExtension>()") &&
                smoke.Contains("FishUtility.IsRuntimeFish(minnow)") &&
                smoke.Contains("NativeFishingWork.ProviderAvailable") &&
                smoke.Contains("NativeFishingProviderRules.CanProvide("),
                "baseline tests native job/workgiver Def links, first-party fish registration and provider isolation");
            Check(!Regex.IsMatch(smoke,
                    @"\b(?:GenSpawn\.Spawn|ThingMaker\.MakeThing|JobMaker\.MakeJob|" +
                    @"BeginAttempt|Complete|TryReserve|TryEnsureSelection|TryStartCarry|" +
                    @"InitializeForDevTest|GenPlace\.TryPlaceThing)\s*\(") &&
                !smoke.Contains("ThingMaker.MakeThing(") &&
                !smoke.Contains("map.listerThings") &&
                !smoke.Contains("new Pawn("),
                "baseline native fishing assertions do not mutate map, pawns, populations, rods or saves");

            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                root, "TestCatalog", "rimtest.catalog.json"))))
            {
                JsonElement catalog = document.RootElement;
                JsonElement[] tests = catalog.GetProperty("tests").EnumerateArray()
                    .Where(item => item.GetProperty("id").GetString() == "aquaculture-runtime-smoke")
                    .ToArray();
                Check(tests.Length == 1, "RimTest retains one original Aquaculture smoke test identity");
                if (tests.Length != 1) return;
                JsonElement test = tests[0];
                HashSet<string> covers = new HashSet<string>(test.GetProperty("covers")
                    .EnumerateArray().Select(item => item.GetProperty("kind").GetString() +
                        ":" + item.GetProperty("name").GetString()), StringComparer.Ordinal);
                string[] expected =
                {
                    "csharp_type:AquacultureFishing.AquacultureBridgeTestSuite",
                    "csharp_type:AquacultureFishing.WorkGiver_NativeFishing",
                    "csharp_type:AquacultureFishing.JobDriver_NativeFishing",
                    "csharp_type:AquacultureFishing.NativeFishingProviderRules",
                    "csharp_type:AquacultureFishing.FishingRodWorkflow",
                    "csharp_type:AquacultureFishing.FishUtility",
                    "def:AF_NativeFishing",
                    "def:AF_PondMinnow"
                };
                Check(expected.All(covers.Contains) &&
                    covers.Count == test.GetProperty("covers").GetArrayLength(),
                    "RimTest affected selection covers native driver/giver, rod workflow, rules and owned Defs without duplicates");
                Check(test.GetProperty("recipe").GetString() == "aquaculture-development-smoke" &&
                    test.GetProperty("artifactFreshnessAnchor").GetBoolean() &&
                    test.GetProperty("isolation").GetProperty("mode").GetString() == "SameGenerationSafe" &&
                    test.GetProperty("isolation").GetProperty("reuseKey").GetString() == "aquaculture-runtime-smoke" &&
                    catalog.GetProperty("suites").EnumerateArray().Any(suite =>
                        suite.GetProperty("id").GetString() == "smoke" &&
                        suite.GetProperty("tests").EnumerateArray().Any(id =>
                            id.GetString() == "aquaculture-runtime-smoke")),
                    "RimTest catalog retains its existing bounded smoke recipe and explicitly anchors build freshness");
            }

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-04F1") &&
                compatibility.Contains("aquaculture/run_baseline") &&
                compatibility.Contains("quicktest readiness") &&
                compatibility.Contains("does not execute") &&
                compatibility.Contains("RimTest"),
                "RimTest preparation does not falsely claim that quicktest alone runs baseline assertions");
        }

        private static void TestNativeFishingRankBoundary(string root)
        {
            string[] owners = {
                "Source/FishingExpertise.cs", "Source/FishingRods.cs",
                "Source/SettingsAndMasks.cs", "Source/UI/AquacultureSettingsDocument.cs"
            };
            foreach (string relative in owners)
            {
                string source = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                Check(!Regex.IsMatch(source, @"\busing\s+KnowledgeFramework\s*;") &&
                    !Regex.IsMatch(source, @"\bKnowledgeRank\b") &&
                    !source.Contains("KnowledgeRanks."),
                    "AQ-DEP-03A native fishing and settings code has no framework rank references: " + relative);
            }
            string contract = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeContract.cs"));
            string progression = File.ReadAllText(Path.Combine(root, "Source", "FishingExpertise.cs"));
            string adapter = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeAdapter.cs"));
            string journal = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            Check(contract.Contains("public enum FishingRank") &&
                contract.Contains("public static class FishingRankRules") &&
                !contract.Contains("using KnowledgeFramework;"),
                "native fishing ranks are declared in a portable contract");
            Check(progression.Contains("\"minimumFishingExpertise\"") &&
                progression.Contains("\"expertiseExperience\"") &&
                progression.Contains("\"speciesKnowledge\"") &&
                progression.Contains("FishingRankRules.FromLegacySetting(value)") &&
                progression.Contains("FishingRankRules.LegacySettingForRank(rank)"),
                "existing serialized progression and legacy setting mapping remain in Aquaculture");
            Check(adapter.Contains("KnowledgeRanks.ForExperience(") &&
                adapter.Contains("KnowledgeRanks.Bonus(") &&
                adapter.Contains("private static FishingRank FromKnowledgeRank(") &&
                adapter.Contains("public static KnowledgeRank ToKnowledgeRank(") &&
                adapter.Contains("KnowledgeQuery.Expertise("),
                "original framework rank calculation and expertise projection remain within the adapter");
            Check(!journal.Contains("KnowledgeMenuUI.Draw(") &&
                journal.Contains("expertiseMenu.Draw(knowledgeRect);") &&
                adapter.Contains("KnowledgeMenuUI.Draw(bounds, state, ExpertiseModelFor, ExpertiseFor);") &&
                adapter.Contains("AquacultureKnowledgeAdapter.ToKnowledgeRank("),
                "canonical Knowledge Journal menu and rank conversion live behind the adapter boundary");
        }

        private static void TestNativeKnowledgeBuildSeam(string root)
        {
            XDocument project = XDocument.Load(Path.Combine(root, "Source", "AquacultureFishing.csproj"));
            XElement nativeFlag = project.Descendants().SingleOrDefault(element =>
                element.Name.LocalName == "AquacultureNativeKnowledge" &&
                ((string)element.Attribute("Condition") ?? string.Empty)
                    .Contains("'$(AquacultureNativeKnowledge)' == ''"));
            Check(nativeFlag?.Value.Trim() == "false",
                "developer-only Knowledge-free compiler variant defaults disabled");

            XElement nativeGroup = project.Root.Elements().SingleOrDefault(element =>
                element.Name.LocalName == "PropertyGroup" &&
                ((string)element.Attribute("Condition") ?? string.Empty) ==
                    "'$(AquacultureNativeKnowledge)' == 'true'");
            Check(nativeGroup != null &&
                nativeGroup.Descendants().Any(e => e.Name.LocalName == "DefineConstants" &&
                    e.Value.Contains("AQUACULTURE_NATIVE_KNOWLEDGE")) &&
                nativeGroup.Descendants().Any(e => e.Name.LocalName == "OutputPath" &&
                    e.Value.Replace('\\', '/').Contains("artifacts/NativeKnowledge/")),
                "native-Knowledge compiler symbol outputs only to a developer artifact folder");

            XElement exclude = project.Descendants().SingleOrDefault(element =>
                element.Name.LocalName == "Compile" &&
                (string)element.Attribute("Remove") == "AquacultureKnowledgeAdapter.cs");
            Check(exclude != null &&
                ((string)exclude.Parent.Attribute("Condition") ?? string.Empty) ==
                    "'$(AquacultureNativeKnowledge)' == 'true'",
                "Knowledge Framework adapter is excluded only in native build");

            XElement[] knowledgeRefs = project.Descendants().Where(element =>
                element.Name.LocalName == "Reference" &&
                (string)element.Attribute("Include") == "KnowledgeFramework").ToArray();
            Check(knowledgeRefs.Length == 2 &&
                knowledgeRefs.All(reference => ((string)reference.Parent.Attribute("Condition") ??
                    string.Empty).Contains("'$(AquacultureNativeKnowledge)' != 'true'")),
                "both Knowledge Framework reference paths remain default-only");

            XElement[] knowledgeErrors = project.Descendants().Where(element =>
                element.Name.LocalName == "Error" &&
                ((string)element.Attribute("Text") ?? string.Empty).Contains("requires KnowledgeFramework")).ToArray();
            Check(knowledgeErrors.Length == 1 &&
                ((string)knowledgeErrors[0].Attribute("Condition") ?? string.Empty)
                    .Contains("'$(AquacultureNativeKnowledge)' != 'true'"),
                "missing Knowledge assembly blocks the default build but not native variant");

            string legacy = File.ReadAllText(Path.Combine(root, "Source", "SharedKnowledgeIntegration.cs"));
            int start = legacy.IndexOf("#if AQUACULTURE_NATIVE_KNOWLEDGE", StringComparison.Ordinal);
            int end = start < 0 ? -1 : legacy.LastIndexOf("#endif", StringComparison.Ordinal);
            string native = start >= 0 && end > start ? legacy.Substring(start, end - start) : string.Empty;
            Check(start >= 0 && end > start &&
                native.Contains("public static class AquacultureKnowledgeAdapter") &&
                native.Contains("public sealed class ExpertiseMenu") &&
                native.Contains("public static bool IsFrameworkReady => false;") &&
                native.Contains("NativeFishingKnowledgeRules.SpeciesKnowledge(") &&
                native.Contains("NativeFishingKnowledgeRules.ColonySpeciesKnowledge<PawnFishingProgress>(") &&
                native.Contains("NativeFishingKnowledgeRules.RankForExperience(") &&
                native.Contains("NativeFishingKnowledgeRules.ProgressForExperience(") &&
                native.Contains("public static void Handle(AquacultureEvent value) { }") &&
                !native.Contains("using KnowledgeFramework;") &&
                !Regex.IsMatch(native, @"\bKnowledge(?:Query|Registry|ClaimService|MenuUI|ProviderRegistry)\b"),
                "native adapter implements read-only fishing projection without V3 APIs");

            string sourceRoot = Path.Combine(root, "Source");
            string[] members = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    string relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
                    return relative != "AquacultureKnowledgeAdapter.cs" &&
                        relative != "SharedKnowledgeIntegration.cs" &&
                        !relative.StartsWith("DeferredRealityProvider/", StringComparison.OrdinalIgnoreCase) &&
                        !relative.Contains("/obj/") && !relative.Contains("/bin/");
                })
                .SelectMany(path => Regex.Matches(File.ReadAllText(path),
                    @"\bAquacultureKnowledgeAdapter\.([A-Za-z_][A-Za-z0-9_]*)")
                    .Cast<Match>().Select(match => match.Groups[1].Value))
                .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            foreach (string member in members)
            {
                bool exposed = member == "ExpertiseMenu"
                    ? native.Contains("public sealed class ExpertiseMenu")
                    : Regex.IsMatch(native, @"(?m)^\s*public\s+(?:static\s+)?[A-Za-z0-9_<>,\[\]? \t]+\b" +
                        Regex.Escape(member) + @"\s*(?:\(|=>)");
                Check(exposed, "native adapter exports core-required member: " + member);
            }
            Check(members.Length >= 10, "native adapter surface audit covers gameplay and UI entry points");

            XDocument about = XDocument.Load(Path.Combine(root, "About", "About.xml"));
            Check(about.Descendants("modDependencies").Descendants("packageId")
                .Any(element => element.Value == "lan.knowledgeframework"),
                "AQ-DEP-03D1 leaves current player dependency metadata unchanged");
        }

        private static void TestKnowledgeJournalAdapterBoundary(string root)
        {
            string journal = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            string adapter = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeAdapter.cs"));

            Check(!Regex.IsMatch(journal, @"\busing\s+KnowledgeFramework\s*;") &&
                !Regex.IsMatch(journal, @"\bKnowledgeMenu(?:State|Scope|Model|Row|Section|UI)\b") &&
                !Regex.IsMatch(journal, @"\bKnowledgeRank\b"),
                "the core Journal is independent of framework-specific browser and rank types");
            Check(journal.Contains("private readonly AquacultureKnowledgeAdapter.ExpertiseMenu expertiseMenu") &&
                journal.Contains("new AquacultureKnowledgeAdapter.ExpertiseMenu();") &&
                journal.Contains("journal.expertiseMenu.SelectPawn(pawn);") &&
                journal.Contains("expertiseMenu.ClearSelection();") &&
                journal.Contains("expertiseMenu.Draw(knowledgeRect);"),
                "Journal owns per-window expertise selection and preserves tab/back navigation");
            Check(journal.Contains("AquacultureKnowledgeAdapter.EnsureRegistration();") &&
                journal.Contains("insightWorkspaceDocument.Draw(inRect);") &&
                journal.Contains("Widgets.ButtonText(backRect,") &&
                journal.Contains("page = JournalPage.Species;"),
                "native Journal navigation and other pages retain their existing behavior");

            Check(adapter.Contains("public sealed class ExpertiseMenu") &&
                adapter.Contains("private readonly KnowledgeMenuState state = new KnowledgeMenuState();") &&
                adapter.Contains("state.scope = KnowledgeMenuScope.Colonist;") &&
                adapter.Contains("state.selectedPawn = pawn;") &&
                adapter.Contains("state.selectedPawn = null;") &&
                adapter.Contains("KnowledgeMenuUI.Draw(bounds, state, ExpertiseModelFor, ExpertiseFor);"),
                "framework adapter owns one browser state per window and canonical browser drawing");
            Check(adapter.Contains("private KnowledgeMenuModel ExpertiseModelFor(Pawn pawn, bool colony)") &&
                adapter.Contains("private KnowledgeMenuModel FishingKnowledgeModel(") &&
                adapter.Contains("record?.pawn?.Faction?.def?.isPlayer == true") &&
                adapter.Contains("records.Sum(record => record.KnowledgeFor(fishDef))") &&
                adapter.Contains("bool locked = !colony && required > level;") &&
                adapter.Contains("section.rows.Add(new KnowledgeMenuRow") &&
                adapter.Contains("title = \"Colony Fishing Knowledge\"") &&
                adapter.Contains("expertiseProgress = personal?.ExpertiseProgress ?? 0f") &&
                adapter.Contains("AquacultureKnowledgeAdapter.ToKnowledgeRank("),
                "existing colony/personal expertise rows and rank presentation moved without gameplay changes");

            string sourceRoot = Path.Combine(root, "Source");
            string[] imports = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/DeferredRealityProvider/") &&
                    !path.Replace('\\', '/').Contains("/obj/") &&
                    !path.Replace('\\', '/').Contains("/bin/") &&
                    Regex.IsMatch(File.ReadAllText(path), @"(?m)^\s*using\s+KnowledgeFramework\s*;"))
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Check(imports.SequenceEqual(new[] { "Source/AquacultureKnowledgeAdapter.cs" }),
                "only the existing isolated adapter source imports KnowledgeFramework");
        }

        private static void TestJournalWorkspaceProviderBoundary(string root)
        {
            string core = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureJournalWorkspaceDocument.cs"));

            int contractStart = core.IndexOf("public interface IAquacultureJournalWorkspace",
                StringComparison.Ordinal);
            int contractEnd = contractStart < 0 ? -1 : core.IndexOf(
                "public sealed class MainTabWindow_AquacultureJournal", contractStart,
                StringComparison.Ordinal);
            string contract = contractStart >= 0 && contractEnd > contractStart
                ? core.Substring(contractStart, contractEnd - contractStart) : string.Empty;
            Check(contract.Contains("void Draw(Rect rect);") &&
                contract.Contains("void PostClose();") &&
                contract.Contains("void SelectPage(string page);") &&
                contract.Contains("void SelectSpecies(string defName);") &&
                !contract.Contains("InsightUi") &&
                !contract.Contains("InsightTheme") &&
                !Regex.IsMatch(core, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "Journal presentation interface uses only Aquaculture-owned methods and Unity Rect");

            string window = contractEnd >= 0 ? core.Substring(contractEnd) : string.Empty;
            Check(window.Contains("private IAquacultureJournalWorkspace insightWorkspaceDocument;") &&
                window.Contains("if (firstWorkspaceDraw) insightWorkspaceDocument = AquacultureJournalWorkspaceFactory.Create();") &&
                !window.Contains("AquacultureJournalWorkspaceDocument") &&
                window.Contains("journal.insightWorkspaceDocument?.SelectSpecies(fishDef?.defName);") &&
                window.Contains("insightWorkspaceDocument.SelectPage(page == JournalPage.Ponds ? \"ponds\" :") &&
                window.Contains("insightWorkspaceDocument.Draw(inRect);") &&
                window.Contains("journal.insightWorkspaceDocument?.PostClose();") &&
                window.Contains("insightWorkspaceDocument.PostClose();") &&
                window.Contains("public override void PostClose()"),
                "native Journal window preserves workspace construction, selection, draw, close, and expertise navigation");

            Check(canvas.Contains("internal static class AquacultureJournalWorkspaceFactory") &&
                canvas.Contains("public static IAquacultureJournalWorkspace Create() => new AquacultureJournalWorkspaceDocument();") &&
                canvas.Contains("public sealed class AquacultureJournalWorkspaceDocument : IAquacultureJournalWorkspace") &&
                canvas.Contains("document = new InsightUiDocument(") &&
                canvas.Contains("AquacultureInsightPresentation.Apply(document);") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                canvas.Contains("Host.PostClose();") &&
                canvas.Contains("public void SelectPage(string page)") &&
                canvas.Contains("public void SelectSpecies(string defName)") &&
                canvas.Contains("document.Invalidate();"),
                "default Canvas renderer remains identical behind the Aquaculture-owned Journal factory");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05A1") &&
                compatibility.Contains("IAquacultureJournalWorkspace") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "provider boundary remains explicitly unqualified for a native UI replacement");
        }

        private static void TestNativeJournalOverviewSpeciesPrototype(string root)
        {
            string source = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureJournalWorkspaceDocument.cs"));
            int start = source.IndexOf(
                "public sealed class NativeAquacultureJournalWorkspaceView : IAquacultureJournalWorkspace",
                StringComparison.Ordinal);
            int end = start < 0 ? -1 : source.IndexOf(
                "public sealed class MainTabWindow_AquacultureJournal", start, StringComparison.Ordinal);
            string native = start >= 0 && end > start
                ? source.Substring(start, end - start) : string.Empty;

            Check(native.Contains("public void Draw(Rect rect)") &&
                native.Contains("public void PostClose()") &&
                native.Contains("public void SelectPage(string page)") &&
                native.Contains("public void SelectSpecies(string defName)") &&
                !Regex.IsMatch(source, @"(?m)^\s*using\s+InsightCanvas\s*;") &&
                !native.Contains("InsightUi") &&
                !native.Contains("InsightCanvas") &&
                !native.Contains("AquacultureJournalWorkspaceDocument"),
                "native Journal implements the existing four-method contract without Canvas symbols");

            Check(native.Contains("AquacultureSnapshotCache.Journal(null, true)") &&
                native.Contains("journal?.species") &&
                native.Contains("item.identityKnown && item.fishDef != null") &&
                native.Contains("SafeSpeciesLabel(item).IndexOf(speciesSearch,") &&
                native.Contains("item.identityKnown ? 0 : 1") &&
                native.Contains("ThenBy(item => SafeSpeciesLabel(item), StringComparer.OrdinalIgnoreCase)") &&
                native.Contains("AquacultureFishing.WorkspaceUnknownSpecies") &&
                !native.Contains("item.fishDef.defName.IndexOf(speciesSearch") &&
                !native.Contains("item.fishDef.label.IndexOf(speciesSearch") &&
                !native.Contains("GetNamedSilentFail(selectedSpeciesDefName)") &&
                !native.Contains("RecordFor("),
                "native Journal searches masked snapshot labels without resolving hidden identities");

            Check(native.Contains("private AquacultureSpeciesViewSnapshot FindKnownSpecies(") &&
                native.Contains("item.identityKnown && item.fishDef != null &&") &&
                native.Contains("item.fishDef.defName == selectedSpeciesDefName") &&
                native.Contains("if (selected == null || !selected.identityKnown)") &&
                native.Contains("selected.knownFacets == null || selected.knownFacets.Count == 0") &&
                native.Contains("selected.knownFacets.ToArray()") &&
                native.Contains("selected.confidence.ToStringPercent()") &&
                native.Contains("SelectedFacetsLine(selected)") &&
                native.Contains("SelectedKnowledgeLine(selected)") &&
                !native.Contains("TraitValue(") &&
                !native.Contains("Scribe_"),
                "native Journal only displays known-species stage, confidence and facets");

            Check(native.Contains("activePage = page == \"species\" ? \"species\" : \"overview\";") &&
                native.Contains("selectedSpeciesDefName = defName.NullOrEmpty() ? null : defName;") &&
                native.Contains("selectedSpeciesDefName = null;") &&
                native.Contains("speciesSearch = string.Empty;") &&
                native.Contains("scrollPosition = Vector2.zero;") &&
                native.Contains("Widgets.BeginScrollView(viewport, ref scrollPosition, view);") &&
                native.Contains("Widgets.EndScrollView();") &&
                native.Contains("Text.Font = previousFont;") &&
                native.Contains("SpeciesContentHeight(species, filtered, viewWidth)") &&
                native.Contains("MainTabWindow_AquacultureJournal.OpenExpertise(null)") &&
                native.Contains("AquacultureFishing.WorkspaceKnowledgeBody"),
                "native Journal resets transient state, scrolls content and delegates Expertise navigation");

            Check(source.Contains("private IAquacultureJournalWorkspace insightWorkspaceDocument;") &&
                source.Contains("insightWorkspaceDocument = AquacultureJournalWorkspaceFactory.Create();") &&
                source.Contains("insightWorkspaceDocument.Draw(inRect);") &&
                canvas.Contains("public static IAquacultureJournalWorkspace Create() => new AquacultureJournalWorkspaceDocument();") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                canvas.Contains("Host.PostClose();"),
                "native Journal prototype leaves the Canvas provider as the default");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05B6B1") &&
                compatibility.Contains("NativeAquacultureJournalWorkspaceView") &&
                compatibility.Contains("Overview and Species") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "partial native Journal documentation retains dependency and gameplay qualification blockers");
        }

        private static void TestStockingPlannerRendererBoundary(string root)
        {
            string ownerSource = File.ReadAllText(Path.Combine(root, "Source", "PondStockingPlanner.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureStockingPlannerDocument.cs"));

            int contractStart = ownerSource.IndexOf("public interface IAquacultureStockingPlannerView",
                StringComparison.Ordinal);
            int windowStart = contractStart < 0 ? -1 : ownerSource.IndexOf(
                "public sealed class Dialog_PondStockingPlanner", contractStart,
                StringComparison.Ordinal);
            string contract = contractStart >= 0 && windowStart > contractStart
                ? ownerSource.Substring(contractStart, windowStart - contractStart) : string.Empty;
            Check(contract.Contains("void Draw(Rect rect);") &&
                contract.Contains("void PostClose();") &&
                Regex.Matches(contract, @"\bvoid\s+\w+\s*\(").Count == 2 &&
                !contract.Contains("InsightUi") &&
                !contract.Contains("InsightTheme") &&
                !Regex.IsMatch(ownerSource, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "stocking planner has a two-operation Aquaculture-owned renderer interface");

            string window = windowStart >= 0 ? ownerSource.Substring(windowStart) : string.Empty;
            int initialized = window.IndexOf("if (!LoadBlueprint()) LoadCurrentStock();",
                StringComparison.Ordinal);
            int factory = window.IndexOf(
                "insightDocument = AquacultureStockingPlannerViewFactory.Create(this);",
                StringComparison.Ordinal);
            Check(window.Contains("private IAquacultureStockingPlannerView insightDocument;") &&
                !window.Contains("AquacultureStockingPlannerDocument") &&
                initialized >= 0 && factory > initialized &&
                window.Contains("doCloseX = true;") &&
                window.Contains("closeOnClickedOutside = false;") &&
                window.Contains("absorbInputAroundWindow = true;") &&
                window.Contains("forcePause = false;"),
                "stocking planner keeps the native dialog owner and constructs its provider after blueprint loading");

            Check(window.Contains("if (pond?.Spawned != true || component == null)") &&
                window.Contains("AquacultureFishing.PondPlannerUnavailable") &&
                window.Contains("insightDocument.Draw(inRect);") &&
                window.Contains("insightDocument?.PostClose();") &&
                window.Contains("base.PostClose();") &&
                window.Contains("internal IReadOnlyList<ThingDef> SpeciesCatalog => species;") &&
                window.Contains("internal IEnumerable<KeyValuePair<ThingDef, int>> PlanEntries => plan") &&
                window.Contains("internal AquaculturePlannerForecastSnapshot ForecastSnapshot") &&
                window.Contains("component.SetStockingBlueprint(") &&
                window.Contains("AquaculturePlannerCacheContract.Key("),
                "native planner keeps unavailable guard, forecast/blueprint authority and cleanup");

            Check(canvas.Contains("internal static class AquacultureStockingPlannerViewFactory") &&
                canvas.Contains("public static IAquacultureStockingPlannerView Create(Dialog_PondStockingPlanner owner) =>") &&
                canvas.Contains("new AquacultureStockingPlannerDocument(owner);") &&
                canvas.Contains("public sealed class AquacultureStockingPlannerDocument : IAquacultureStockingPlannerView") &&
                canvas.Contains("public AquacultureStockingPlannerDocument(Dialog_PondStockingPlanner owner)") &&
                canvas.Contains("document = new InsightUiDocument(") &&
                canvas.Contains("AquacultureInsightPresentation.Apply(document);") &&
                canvas.Contains("RefreshSnapshot();") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                canvas.Contains("Host.PostClose();") &&
                canvas.Contains("owner.SpeciesCatalog") &&
                canvas.Contains("owner.PlanEntries") &&
                canvas.Contains("owner.ForecastSnapshot"),
                "default Canvas planner retains its original snapshot, draw, theme and owner bindings");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05A3") &&
                compatibility.Contains("IAquacultureStockingPlannerView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "planner renderer isolation is not an optional Canvas release qualification");
        }

        private static void TestNativeStockingPlannerRendererPrototype(string root)
        {
            string ownerSource = File.ReadAllText(Path.Combine(root, "Source", "PondStockingPlanner.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureStockingPlannerDocument.cs"));
            int start = ownerSource.IndexOf(
                "public sealed class NativeAquacultureStockingPlannerView : IAquacultureStockingPlannerView",
                StringComparison.Ordinal);
            string native = start < 0 ? string.Empty : ownerSource.Substring(start);

            Check(start >= 0 &&
                native.Contains("NativeAquacultureStockingPlannerView(Dialog_PondStockingPlanner owner)") &&
                native.Contains("public void Draw(Rect rect)") &&
                native.Contains("public void PostClose()") &&
                !Regex.IsMatch(ownerSource, @"(?m)^\s*using\s+InsightCanvas\s*;") &&
                !native.Contains("InsightUi") &&
                !native.Contains("InsightTheme") &&
                !native.Contains("AquacultureStockingPlannerDocument"),
                "native stocking planner implements the core provider interface without Canvas types");

            int catalog = native.IndexOf("owner.SpeciesCatalog", StringComparison.Ordinal);
            int search = native.IndexOf("Widgets.TextField(", StringComparison.Ordinal);
            int filtered = native.IndexOf("def.label.IndexOf(search,", StringComparison.Ordinal);
            int add = native.IndexOf("owner.ChangeCountForUi(def, 1);", StringComparison.Ordinal);
            int planned = native.IndexOf("owner.PlanEntries", StringComparison.Ordinal);
            int remove = native.IndexOf("owner.ChangeCountForUi(pair.Key, -1);", StringComparison.Ordinal);
            int addPlanned = native.IndexOf("owner.ChangeCountForUi(pair.Key, 1);", StringComparison.Ordinal);
            Check(search >= 0 && catalog > search && filtered > catalog &&
                add > filtered && planned > filtered &&
                remove > planned && addPlanned > remove &&
                native.Contains("def.defName.IndexOf(search,") &&
                native.Contains("AquacultureFishing.PondPlannerNoAvailableSpecies") &&
                native.Contains("AquacultureFishing.PondPlannerEmpty") &&
                native.Contains("AquacultureFishing.PondPlannerTotals") &&
                !native.Contains("DefDatabase<ThingDef>") &&
                !native.Contains("AquacultureKnowledgeAdapter.SpeciesView("),
                "native planner filters owner-disclosed species and edits counts only through owner callbacks");

            Check(native.Contains("new FloatMenuOption(WaterLabel(PondWaterKind.Freshwater)") &&
                native.Contains("new FloatMenuOption(WaterLabel(PondWaterKind.Saltwater)") &&
                native.Contains("new FloatMenuOption(WaterLabel(PondWaterKind.Brackishwater)") &&
                native.Contains("owner.SetPlannedWater(PondWaterKind.Freshwater)") &&
                native.Contains("owner.SetPlannedWater(PondWaterKind.Saltwater)") &&
                native.Contains("owner.SetPlannedWater(PondWaterKind.Brackishwater)") &&
                native.Contains("owner.PlannedWaterValue") &&
                native.Contains("owner.LoadCurrentForUi();") &&
                native.Contains("owner.LoadBlueprintForUi();") &&
                native.Contains("owner.ClearPlanForUi();") &&
                native.Contains("owner.SavePlanForUi();") &&
                !native.Contains("component.SetStockingBlueprint(") &&
                !native.Contains("LoadCurrentStock(") &&
                !native.Contains("LoadBlueprint("),
                "native planner delegates water, load, clear and save decisions to original dialog");

            Check(native.Contains("AquaculturePlannerForecastSnapshot forecast = owner.ForecastSnapshot;") &&
                native.Contains("forecast.Danger") &&
                native.Contains("forecast.Warnings.Count") &&
                native.Contains("forecast.TotalFish") &&
                native.Contains("forecast.PhysicalCapacity") &&
                native.Contains("forecast.SustainableCapacity") &&
                native.Contains("forecast.IndustrialCapacity") &&
                native.Contains("forecast.DailyDemand") &&
                native.Contains("forecast.FeedNeeded") &&
                native.Contains("forecast.PredationRisk") &&
                native.Contains("Mathf.Clamp01((float)count / Mathf.Max(1, capacity))") &&
                native.Contains("AquacultureFishing.PondPlannerWarnings") &&
                !native.Contains("CalculateForecast(") &&
                !native.Contains("AquaculturePlannerCacheContract.Key("),
                "native forecast only displays the core snapshot, including warnings and capacity limits");

            Check(native.Contains("Widgets.DrawMenuSection(rect);") &&
                native.Contains("Widgets.BeginScrollView(viewport, ref scrollPosition, view);") &&
                native.Contains("Widgets.EndScrollView();") &&
                native.Contains("float subtitleHeight = LabelHeight(subtitle, padded.width);") &&
                native.Contains("scrollPosition.y = Mathf.Clamp(scrollPosition.y") &&
                native.Contains("search = string.Empty;") &&
                native.Contains("scrollPosition = Vector2.zero;") &&
                native.Contains("Text.Font = previousFont;") &&
                native.Contains("if (Visible(y, height, viewport.height))") &&
                !native.Contains("Scribe_"),
                "native stocking layout is scrollable, content-sized and cleans up local state");

            Check(ownerSource.Contains(
                    "insightDocument = AquacultureStockingPlannerViewFactory.Create(this);") &&
                canvas.Contains("new AquacultureStockingPlannerDocument(owner);") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                canvas.Contains("Host.PostClose();") &&
                ownerSource.Contains("private IAquacultureStockingPlannerView insightDocument;") &&
                ownerSource.Contains("if (!LoadBlueprint()) LoadCurrentStock();"),
                "native stocking prototype preserves owner lifecycle and the default Canvas renderer");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05B4") &&
                compatibility.Contains("NativeAquacultureStockingPlannerView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "native stocking prototype does not claim optional dependency or live qualification");
        }

        private static void TestBreedRegistrationRendererBoundary(string root)
        {
            string owner = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureBreedRegistrationDocument.cs"));

            int contractStart = owner.IndexOf("public interface IAquacultureBreedRegistrationView",
                StringComparison.Ordinal);
            int dialogStart = contractStart < 0 ? -1 : owner.IndexOf(
                "public sealed class Dialog_RegisterFishBreed", contractStart,
                StringComparison.Ordinal);
            string contract = contractStart >= 0 && dialogStart > contractStart
                ? owner.Substring(contractStart, dialogStart - contractStart) : string.Empty;
            Check(contract.Contains("void Draw(Rect rect);") &&
                contract.Contains("void PostClose();") &&
                Regex.Matches(contract, @"\bvoid\s+\w+\s*\(").Count == 2 &&
                !contract.Contains("InsightUi") &&
                !Regex.IsMatch(owner, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "breed-registration renderer interface has no Insight Canvas types");

            int dialogEnd = dialogStart < 0 ? -1 : owner.IndexOf(
                "public interface IAquacultureJournalWorkspace", dialogStart,
                StringComparison.Ordinal);
            string dialog = dialogStart >= 0 && dialogEnd > dialogStart
                ? owner.Substring(dialogStart, dialogEnd - dialogStart) : string.Empty;
            int flags = dialog.IndexOf("absorbInputAroundWindow = true;", StringComparison.Ordinal);
            int factory = dialog.IndexOf(
                "insightDocument = AquacultureBreedRegistrationViewFactory.Create(this);",
                StringComparison.Ordinal);
            Check(dialog.Contains("private IAquacultureBreedRegistrationView insightDocument;") &&
                !dialog.Contains("AquacultureBreedRegistrationDocument") &&
                dialog.Contains("doCloseX = true;") &&
                dialog.Contains("closeOnAccept = false;") &&
                flags >= 0 && factory > flags &&
                dialog.Contains("insightDocument.Draw(inRect);") &&
                dialog.Contains("insightDocument?.PostClose();") &&
                dialog.Contains("base.PostClose();"),
                "native breed registration retains dialog lifecycle, flag order, draw and cleanup");

            Check(dialog.Contains("if (canAcceptForUi && Event.current.type == EventType.KeyDown") &&
                dialog.Contains("Event.current.keyCode == KeyCode.Return") &&
                dialog.Contains("Event.current.keyCode == KeyCode.KeypadEnter") &&
                dialog.Contains("Event.current.Use();") &&
                dialog.Contains("AcceptForUi();") &&
                dialog.Contains("internal void SetBreedNameForUi(string value)") &&
                dialog.Contains("internal void RefreshValidationForUi()") &&
                dialog.Contains("AquacultureFishing.BreedRegistrationEnterName") &&
                dialog.Contains("AquacultureFishing.BreedRegistrationDuplicate") &&
                dialog.Contains("CanRegisterBreed(founder, out reason, out cohort)") &&
                dialog.Contains("RegisterBreed(founder, breedName)") &&
                dialog.Contains("if (breed != null) Close();") &&
                dialog.Contains("internal void CancelForUi() => Close();"),
                "native dialog retains name validation, duplicate detection, Enter, accept and cancel authority");

            Check(canvas.Contains("internal static class AquacultureBreedRegistrationViewFactory") &&
                canvas.Contains("public static IAquacultureBreedRegistrationView Create(Dialog_RegisterFishBreed owner) =>") &&
                canvas.Contains("new AquacultureBreedRegistrationDocument(owner);") &&
                canvas.Contains("public sealed class AquacultureBreedRegistrationDocument : IAquacultureBreedRegistrationView") &&
                canvas.Contains("public AquacultureBreedRegistrationDocument(Dialog_RegisterFishBreed owner)") &&
                canvas.Contains("document = new InsightUiDocument(") &&
                canvas.Contains("AquacultureInsightPresentation.Apply(document);") &&
                canvas.Contains("owner.RefreshValidationForUi();") &&
                canvas.Contains("registerButton.Enabled = owner.CanAcceptForUi;") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                canvas.Contains("Host.PostClose();") &&
                canvas.Contains("owner.SetBreedNameForUi(value);") &&
                canvas.Contains("owner.ValidationMessageForUi") &&
                canvas.Contains("owner.AcceptForUi") &&
                canvas.Contains("owner.CancelForUi"),
                "default Canvas document retains original presentation, validation, callbacks and cleanup");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05A4") &&
                compatibility.Contains("IAquacultureBreedRegistrationView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "breed registration renderer isolation is not a qualified optional UI build");
        }

        private static void TestNativeBreedRegistrationRendererPrototype(string root)
        {
            string owner = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureBreedRegistrationDocument.cs"));
            int start = owner.IndexOf(
                "public sealed class NativeAquacultureBreedRegistrationView : IAquacultureBreedRegistrationView",
                StringComparison.Ordinal);
            string native = start < 0 ? string.Empty : owner.Substring(start);
            Check(start >= 0 &&
                native.Contains("public NativeAquacultureBreedRegistrationView(Dialog_RegisterFishBreed owner)") &&
                native.Contains("public void Draw(Rect rect)") &&
                native.Contains("public void PostClose()") &&
                !native.Contains("InsightUi") &&
                !native.Contains("InsightCanvas") &&
                !native.Contains("AquacultureBreedRegistrationDocument") &&
                !Regex.IsMatch(owner, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "native breed registration implements only the native owner interface without Canvas types");

            int initialValidation = native.IndexOf("owner.RefreshValidationForUi();", StringComparison.Ordinal);
            int textField = native.IndexOf("Widgets.TextField(", StringComparison.Ordinal);
            int updatedName = native.IndexOf("owner.SetBreedNameForUi(enteredName);", StringComparison.Ordinal);
            int refreshedValidation = native.IndexOf("owner.RefreshValidationForUi();",
                initialValidation + 1, StringComparison.Ordinal);
            int disabledRegister = native.IndexOf(
                "GUI.enabled = previousEnabled && owner.CanAcceptForUi;", StringComparison.Ordinal);
            int accept = native.IndexOf("owner.AcceptForUi();", StringComparison.Ordinal);
            Check(native.Contains("owner.BreedNameForUi") &&
                native.Contains("owner.ValidationMessageForUi") &&
                initialValidation >= 0 && textField > initialValidation &&
                updatedName > textField && refreshedValidation > updatedName &&
                disabledRegister > refreshedValidation && accept > disabledRegister &&
                native.Contains("owner.CancelForUi();") &&
                native.Contains("GUI.enabled = previousEnabled;") &&
                !native.Contains("RegisterBreed(") &&
                !native.Contains("CanRegisterBreed(") &&
                !native.Contains("Scribe_"),
                "native breed UI delegates name changes, validation, disabled accept and cancel to owner");

            Check(native.Contains("Widgets.DrawMenuSection(rect);") &&
                native.Contains("Widgets.BeginScrollView(viewport, ref scrollPosition, view);") &&
                native.Contains("Widgets.EndScrollView();") &&
                native.Contains("scrollPosition = Vector2.zero;") &&
                native.Contains("Text.Font = previousFont;") &&
                native.Contains("AquacultureFishing.BreedRegistrationTitle") &&
                native.Contains("AquacultureFishing.BreedRegistrationSubtitle") &&
                native.Contains("AquacultureFishing.BreedRegistrationName") &&
                native.Contains("AquacultureFishing.BreedRegistrationCancel") &&
                native.Contains("AquacultureFishing.BreedRegistrationRegister"),
                "native breed UI uses translated text, bounded scrolling and cleanup");

            Check(owner.Contains("insightDocument = AquacultureBreedRegistrationViewFactory.Create(this);") &&
                owner.Contains("Event.current.keyCode == KeyCode.Return") &&
                owner.Contains("Event.current.keyCode == KeyCode.KeypadEnter") &&
                canvas.Contains("new AquacultureBreedRegistrationDocument(owner);") &&
                canvas.Contains("registerButton.Enabled = owner.CanAcceptForUi;"),
                "prototype preserves default Canvas renderer and owner keyboard semantics");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05B2") &&
                compatibility.Contains("NativeAquacultureBreedRegistrationView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "native breed prototype is documented without claiming a standalone release");
        }

        private static void TestCommissionDeliveryRendererBoundary(string root)
        {
            string source = File.ReadAllText(Path.Combine(root, "Source", "AquacultureCommissions.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureCommissionDeliveryDocument.cs"));

            int contractStart = source.IndexOf("public interface IAquacultureCommissionDeliveryView",
                StringComparison.Ordinal);
            int dialogStart = contractStart < 0 ? -1 : source.IndexOf(
                "public sealed class Dialog_DeliverAquacultureCommission", contractStart,
                StringComparison.Ordinal);
            string contract = contractStart >= 0 && dialogStart > contractStart
                ? source.Substring(contractStart, dialogStart - contractStart) : string.Empty;
            Check(contract.Contains("void Draw(Rect rect);") &&
                contract.Contains("void PostClose();") &&
                Regex.Matches(contract, @"\bvoid\s+\w+\s*\(").Count == 2 &&
                !contract.Contains("InsightUi") &&
                !Regex.IsMatch(source, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "commission delivery view contract uses only Aquaculture and Unity rectangle types");

            int dialogEnd = dialogStart < 0 ? -1 : source.IndexOf(
                "public static class AquacultureCommissionUi", dialogStart,
                StringComparison.Ordinal);
            string dialog = dialogStart >= 0 && dialogEnd > dialogStart
                ? source.Substring(dialogStart, dialogEnd - dialogStart) : string.Empty;
            int flags = dialog.IndexOf("layer = WindowLayer.Dialog;", StringComparison.Ordinal);
            int factory = dialog.IndexOf(
                "insightDocument = AquacultureCommissionDeliveryViewFactory.Create(this);",
                StringComparison.Ordinal);
            Check(dialog.Contains("private IAquacultureCommissionDeliveryView insightDocument;") &&
                !dialog.Contains("AquacultureCommissionDeliveryDocument") &&
                dialog.Contains("forcePause = true;") &&
                dialog.Contains("absorbInputAroundWindow = true;") &&
                dialog.Contains("doCloseX = true;") &&
                dialog.Contains("closeOnAccept = false;") &&
                dialog.Contains("resizeable = false;") &&
                dialog.Contains("draggable = true;") &&
                flags >= 0 && factory > flags &&
                dialog.Contains("insightDocument.Draw(inRect);") &&
                dialog.Contains("insightDocument?.PostClose();") &&
                dialog.Contains("base.PostClose();"),
                "native commission dialog retains window flags, first renderer construction and cleanup");

            Check(dialog.Contains("internal string RequirementsForUi =>") &&
                dialog.Contains("internal string PreviewForUi =>") &&
                dialog.Contains("internal string EligibleCountForUi =>") &&
                dialog.Contains("internal IReadOnlyList<AquacultureCommissionDeliverySnapshot> CaptureSpecimensForUi()") &&
                dialog.Contains("component?.RefreshEligibleSpecimens();") &&
                dialog.Contains("component?.EligibleSpecimens(commission)") &&
                dialog.Contains("!specimen.Destroyed && specimen.Spawned") &&
                dialog.Contains(".OrderBy(specimen => specimen.thingIDNumber)") &&
                dialog.Contains("AquacultureUiStableIds.For(\"commission.specimen\"") &&
                dialog.Contains("internal bool TryDeliverForUi(int thingId, out string reason)") &&
                dialog.Contains("return component.TryDeliver(specimen, out reason);") &&
                dialog.Contains("internal void CloseForUi()") &&
                dialog.Contains("Close();"),
                "commission eligibility snapshot and delivery authority remain native and unchanged");

            int drawStart = canvas.IndexOf("public void Draw(UnityEngine.Rect rect)",
                StringComparison.Ordinal);
            int drawEnd = drawStart < 0 ? -1 : canvas.IndexOf(
                "public void PostClose()", drawStart, StringComparison.Ordinal);
            string draw = drawStart >= 0 && drawEnd > drawStart
                ? canvas.Substring(drawStart, drawEnd - drawStart) : string.Empty;
            int capture = draw.IndexOf("CaptureSnapshot();", StringComparison.Ordinal);
            int render = draw.IndexOf("Host.Draw(rect, UnityEngine.Time.deltaTime);",
                StringComparison.Ordinal);
            Check(canvas.Contains("internal static class AquacultureCommissionDeliveryViewFactory") &&
                canvas.Contains("public static IAquacultureCommissionDeliveryView Create(Dialog_DeliverAquacultureCommission owner) =>") &&
                canvas.Contains("new AquacultureCommissionDeliveryDocument(owner);") &&
                canvas.Contains("public sealed class AquacultureCommissionDeliveryDocument : IAquacultureCommissionDeliveryView") &&
                canvas.Contains("public AquacultureCommissionDeliveryDocument(Dialog_DeliverAquacultureCommission owner)") &&
                canvas.Contains("document = new InsightUiDocument(") &&
                canvas.Contains("AquacultureInsightPresentation.Apply(document);") &&
                capture >= 0 && render > capture &&
                canvas.Contains("Host.PostClose();") &&
                canvas.Contains("owner.CaptureSpecimensForUi();") &&
                canvas.Contains("owner.RequirementsForUi") &&
                canvas.Contains("owner.PreviewForUi") &&
                canvas.Contains("owner.EligibleCountForUi") &&
                canvas.Contains("owner.TryDeliverForUi(thingId, out string reason)") &&
                canvas.Contains("owner.CloseForUi();") &&
                canvas.Contains("InsightToastSeverity.Success") &&
                canvas.Contains("InsightToastSeverity.Error") &&
                canvas.Contains("document.Invalidate();"),
                "Canvas renderer still refreshes copied specimens before draw, invokes owner delivery and preserves toasts/cleanup");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05A5") &&
                compatibility.Contains("IAquacultureCommissionDeliveryView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "commission renderer seam remains explicitly unqualified for optional UI or release");
        }

        private static void TestNativeCommissionDeliveryRendererPrototype(string root)
        {
            string core = File.ReadAllText(Path.Combine(root, "Source", "AquacultureCommissions.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureCommissionDeliveryDocument.cs"));
            int nativeStart = core.IndexOf(
                "public sealed class NativeAquacultureCommissionDeliveryView : IAquacultureCommissionDeliveryView",
                StringComparison.Ordinal);
            int nativeEnd = nativeStart < 0 ? -1 : core.IndexOf(
                "public static class AquacultureCommissionUi", nativeStart, StringComparison.Ordinal);
            string native = nativeStart >= 0 && nativeEnd > nativeStart
                ? core.Substring(nativeStart, nativeEnd - nativeStart) : string.Empty;

            Check(Regex.Matches(core, "public sealed class AquacultureCommissionDeliverySnapshot").Count == 1 &&
                !canvas.Contains("public sealed class AquacultureCommissionDeliverySnapshot") &&
                core.Contains("AquacultureCommissionDeliverySnapshot(string stableId, int thingId, string label, string details)") &&
                core.Contains("public string StableId { get; private set; }") &&
                core.Contains("public int ThingId { get; private set; }") &&
                core.Contains("public string Label { get; private set; }") &&
                core.Contains("public string Details { get; private set; }") &&
                canvas.Contains("List<AquacultureCommissionDeliverySnapshot>") &&
                canvas.Contains("IReadOnlyList<AquacultureCommissionDeliverySnapshot> next"),
                "commission snapshot lives exactly once in Canvas-free core with existing members");
            
            Check(native.Contains("public void Draw(Rect rect)") &&
                native.Contains("public void PostClose()") &&
                native.Contains("NativeAquacultureCommissionDeliveryView(Dialog_DeliverAquacultureCommission owner)") &&
                !native.Contains("InsightUi") &&
                !native.Contains("AquacultureCommissionDeliveryDocument") &&
                !Regex.IsMatch(core, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "native commission view implements the owner interface without Canvas types");

            int captured = native.IndexOf("owner.CaptureSpecimensForUi()", StringComparison.Ordinal);
            int scroll = native.IndexOf("Widgets.BeginScrollView(viewport, ref scrollPosition, view);",
                StringComparison.Ordinal);
            int button = native.IndexOf("Widgets.ButtonText(", StringComparison.Ordinal);
            int deliveryClick = native.IndexOf("Deliver(row.ThingId);", StringComparison.Ordinal);
            int deliverMethod = native.IndexOf("private void Deliver(int thingId)", StringComparison.Ordinal);
            Check(captured >= 0 && scroll > captured && button > scroll &&
                deliveryClick > button && deliverMethod > deliveryClick &&
                native.Contains("owner.RequirementsForUi") &&
                native.Contains("owner.PreviewForUi") &&
                native.Contains("owner.EligibleCountForUi") &&
                native.Contains("row.Label") && native.Contains("row.Details") &&
                native.Contains("AquacultureFishing.CommissionNoEligible") &&
                native.Contains("AquacultureFishing.WorkspaceCommissionDisclosure") &&
                native.Contains("AquacultureFishing.CommissionDeliverButton"),
                "native commission view captures eligible snapshots before drawing translated actions");

            Check(native.Contains("owner.TryDeliverForUi(thingId, out string reason)") &&
                native.Contains("owner.CloseForUi();") &&
                native.Contains("MessageTypeDefOf.TaskCompletion") &&
                native.Contains("MessageTypeDefOf.RejectInput") &&
                native.Contains("reason ?? L(\"AquacultureFishing.CommissionSpecimenInvalid\")") &&
                !native.Contains("component.TryDeliver(") &&
                !native.Contains("Scribe_") &&
                !native.Contains("AquacultureCommissionComponent.Current"),
                "native commission delivery uses owner authority and reports both outcomes");

            Check(native.Contains("scrollPosition = Vector2.zero;") &&
                native.Contains("Mathf.Clamp(scrollPosition.y") &&
                native.Contains("Widgets.EndScrollView();") &&
                native.Contains("Text.Font = previousFont;") &&
                core.Contains(".Where(specimen => specimen != null && !specimen.Destroyed && specimen.Spawned)") &&
                core.Contains(".OrderBy(specimen => specimen.thingIDNumber)") &&
                core.Contains("return component.TryDeliver(specimen, out reason);"),
                "native view clears scroll, preserves eligibility filtering and delegates delivery");

            Check(core.Contains("insightDocument = AquacultureCommissionDeliveryViewFactory.Create(this);") &&
                canvas.Contains("new AquacultureCommissionDeliveryDocument(owner);") &&
                canvas.Contains("Host.Draw(rect, UnityEngine.Time.deltaTime);") &&
                canvas.Contains("Host.PostClose();"),
                "native prototype leaves the existing Canvas provider and window lifecycle unchanged");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05B3") &&
                compatibility.Contains("NativeAquacultureCommissionDeliveryView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "native commission prototype documents remaining dependency and runtime blockers");
        }

        private static void TestFishDossierRendererBoundary(string root)
        {
            string owner = File.ReadAllText(Path.Combine(root, "Source", "FishTraitsTab.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureFishDossierDocument.cs"));

            int contractStart = owner.IndexOf("public interface IAquacultureFishDossierView",
                StringComparison.Ordinal);
            int tabStart = contractStart < 0 ? -1 : owner.IndexOf(
                "public sealed class ITab_FishTraits : ITab", contractStart,
                StringComparison.Ordinal);
            string contract = contractStart >= 0 && tabStart > contractStart
                ? owner.Substring(contractStart, tabStart - contractStart) : string.Empty;
            Check(contract.Contains("void Draw(Rect rect, CompFishTraits comp);") &&
                contract.Contains("void PostClose();") &&
                Regex.Matches(contract, @"\bvoid\s+\w+\s*\(").Count == 2 &&
                !contract.Contains("InsightUi") &&
                !Regex.IsMatch(owner, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "fish dossier view contract owns only drawing and cleanup, without Canvas types");

            string tab = tabStart >= 0 ? owner.Substring(tabStart) : string.Empty;
            int rect = tab.IndexOf("Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);",
                StringComparison.Ordinal);
            int selected = tab.IndexOf("CompFishTraits selected = SelThing?.TryGetComp<CompFishTraits>();",
                StringComparison.Ordinal);
            int factory = tab.IndexOf(
                "if (insightDossier == null) insightDossier = AquacultureFishDossierViewFactory.Create();",
                StringComparison.Ordinal);
            int draw = tab.IndexOf("insightDossier.Draw(rect, selected);", StringComparison.Ordinal);
            int close = tab.IndexOf("insightDossier?.PostClose();", StringComparison.Ordinal);
            Check(tab.Contains("private IAquacultureFishDossierView insightDossier;") &&
                !tab.Contains("AquacultureFishDossierDocument") &&
                tab.Contains("public override bool IsVisible => SelThing?.TryGetComp<CompFishTraits>() != null;") &&
                tab.Contains("size = new Vector2(520f, 484f);") &&
                tab.Contains("labelKey = \"AF_TraitsTab\";") &&
                tab.Contains("if (selected != null)") &&
                rect >= 0 && selected > rect && factory > selected && draw > factory &&
                close > draw &&
                tab.Contains("Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f)") &&
                tab.Contains("RefreshSnapshot(viewWidth);") &&
                tab.Contains("DrawTrait(new Rect("),
                "fish trait tab preserves selection, first-create, inset, native fallback and cleanup");

            int drawStart = canvas.IndexOf("public void Draw(Rect rect, CompFishTraits comp)",
                StringComparison.Ordinal);
            int drawEnd = drawStart < 0 ? -1 : canvas.IndexOf(
                "public void PostClose()", drawStart, StringComparison.Ordinal);
            string drawBody = drawStart >= 0 && drawEnd > drawStart
                ? canvas.Substring(drawStart, drawEnd - drawStart) : string.Empty;
            int capture = drawBody.IndexOf("Capture(comp);", StringComparison.Ordinal);
            int render = drawBody.IndexOf("Host.Draw(rect, Time.deltaTime);",
                StringComparison.Ordinal);
            Check(canvas.Contains("internal static class AquacultureFishDossierViewFactory") &&
                canvas.Contains("public static IAquacultureFishDossierView Create() => new AquacultureFishDossierDocument();") &&
                canvas.Contains("public sealed class AquacultureFishDossierDocument : IAquacultureFishDossierView") &&
                canvas.Contains("public AquacultureFishDossierDocument()") &&
                canvas.Contains("document = new InsightUiDocument(") &&
                canvas.Contains("AquacultureInsightPresentation.Apply(document);") &&
                capture >= 0 && render > capture &&
                canvas.Contains("Host.PostClose();") &&
                canvas.Contains("if (capturedComp != null && !ReferenceEquals(comp, capturedComp)) PostClose();") &&
                canvas.Contains("AquacultureKnowledgeAdapter.SpeciesView(") &&
                canvas.Contains("AquacultureUiDisclosure.ForFish(") &&
                canvas.Contains("AquacultureKnowledgeAdapter.IsPlayerReadableTrait(") &&
                canvas.Contains("ReferenceEquals(comp, capturedComp) && snapshot != null") &&
                canvas.Contains("AquacultureFishDossierSnapshot("),
                "Canvas fish dossier retains its original snapshot, disclosure, capture, draw and selection cleanup");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05A6") &&
                compatibility.Contains("IAquacultureFishDossierView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "fish dossier isolation is not a native renderer or release qualification");
        }

        private static void TestNativeFishDossierRendererPrototype(string root)
        {
            string source = File.ReadAllText(Path.Combine(root, "Source", "FishTraitsTab.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureFishDossierDocument.cs"));
            int start = source.IndexOf("public sealed class NativeAquacultureFishDossierView",
                StringComparison.Ordinal);
            string native = start < 0 ? string.Empty : source.Substring(start);
            Check(native.Contains(": IAquacultureFishDossierView") &&
                native.Contains("public void Draw(Rect rect, CompFishTraits comp)") &&
                native.Contains("public void PostClose()") &&
                !source.Contains("using InsightCanvas;") &&
                !native.Contains("InsightUi") &&
                !native.Contains("AquacultureFishDossierDocument"),
                "native dossier uses only Aquaculture and RimWorld UI types");
            Check(native.Contains("AquacultureKnowledgeAdapter.SpeciesView(") &&
                native.Contains("AquacultureUiDisclosure.ForFish(") &&
                native.Contains("AquacultureUiDisclosure.CaptureCapabilities()") &&
                native.Contains("HasFacet(knowledge, \"traits\")") &&
                native.Contains("HasFacet(knowledge, \"size\")") &&
                native.Contains("HasFacet(knowledge, \"health\")") &&
                native.Contains("HasFacet(knowledge, \"breeding\")") &&
                native.Contains("disclosure.SpeciesIdentityVisible") &&
                native.Contains("disclosure.HealthClassificationVisible") &&
                native.Contains("disclosure.ExactHealthMetricsVisible") &&
                native.Contains("disclosure.ExpectedMeatYieldVisible") &&
                native.Contains("disclosure.BreedVisible") &&
                native.Contains("disclosure.GenerationVisible") &&
                native.Contains("disclosure.SterilizationVisible"),
                "native dossier honors knowledge, research and breed disclosure");
            Check(native.Contains("if (disclosure.TraitsVisible)") &&
                native.Contains("AquacultureKnowledgeAdapter.IsPlayerReadableTrait(trait.defName)") &&
                native.Contains("trait.LabelCap") &&
                !native.Contains("TraitValue(") &&
                !native.Contains("FishTraitUtility.EffectLine(") &&
                !native.Contains("trait.description"),
                "native dossier only displays known trait labels, not hidden numerics");
            Check(native.Contains("if (comp == null)") &&
                native.Contains("if (!ReferenceEquals(selectedComp, comp))") &&
                native.Contains("scrollPosition = Vector2.zero;") &&
                native.Contains("Widgets.BeginScrollView(") &&
                native.Contains("Widgets.EndScrollView();") &&
                native.Contains("disclosure.KnowledgeVisible && parent?.def != null") &&
                native.Contains("MainTabWindow_AquacultureJournal.OpenSpecies(parent.def)"),
                "native dossier resets selection and gates its Journal link");
            Check(canvas.Contains(
                "public static IAquacultureFishDossierView Create() => new AquacultureFishDossierDocument();") &&
                source.Contains("insightDossier = AquacultureFishDossierViewFactory.Create();"),
                "native prototype does not switch the default Canvas provider");
            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05B1") &&
                compatibility.Contains("NativeAquacultureFishDossierView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "native dossier prototype is documented without standalone claims");
        }

        private static void TestNativeJournalExpertiseFallback(string root)
        {
            string journal = File.ReadAllText(Path.Combine(root, "Source", "AquacultureJournal.cs"));
            string adapter = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeAdapter.cs"));
            string progress = File.ReadAllText(Path.Combine(root, "Source", "FishingExpertise.cs"));

            Check(journal.Contains("private readonly NativeExpertiseMenu nativeExpertiseMenu = new NativeExpertiseMenu();") &&
                journal.Contains("journal.nativeExpertiseMenu.SelectPawn(pawn);") &&
                journal.Contains("nativeExpertiseMenu.ClearSelection();") &&
                journal.Contains("if (AquacultureKnowledgeAdapter.IsFrameworkReady)") &&
                journal.Contains("expertiseMenu.Draw(knowledgeRect);") &&
                journal.Contains("nativeExpertiseMenu.Draw(knowledgeRect);") &&
                adapter.Contains("KnowledgeMenuUI.Draw(bounds, state, ExpertiseModelFor, ExpertiseFor);"),
                "native Journal mode is selected only when framework V3 is unready; canonical UI stays intact");

            int start = journal.IndexOf("private sealed class NativeExpertiseMenu", StringComparison.Ordinal);
            int end = start < 0 ? -1 : journal.IndexOf("public override void PostClose()", start, StringComparison.Ordinal);
            string native = start >= 0 && end > start ? journal.Substring(start, end - start) : string.Empty;
            Check(native.Contains("private Pawn selectedPawn;") &&
                native.Contains("private bool colonyScope = true;") &&
                native.Contains("private Vector2 scrollPosition;") &&
                native.Contains("void SelectPawn(Pawn pawn)") &&
                native.Contains("void ClearSelection()") &&
                native.Contains("ChooseAngler(playerRecords)") &&
                native.Contains("Find.WindowStack.Add(new FloatMenu(options))"),
                "native expertise state, angler switching and scroll remain window-scoped");
            Check(native.Contains("record?.pawn?.Faction?.def?.isPlayer == true") &&
                native.Contains("selectedPawn?.Faction?.def?.isPlayer != true") &&
                native.Contains("NativeFishingKnowledgeRules.ColonySpeciesKnowledge(") &&
                native.Contains("NativeFishingKnowledgeRules.SpeciesKnowledge("),
                "native knowledge is limited to player-faction legacy records");
            Check(native.Contains("NativeFishingKnowledgeRules.Experience(") &&
                native.Contains("NativeFishingKnowledgeRules.RankForExperience(") &&
                native.Contains("NativeFishingKnowledgeRules.ProgressForExperience(") &&
                native.Contains("knowledge > 0f && (colonyScope || rank >= required)") &&
                native.Contains("Widgets.BeginScrollView(") &&
                native.Contains("Widgets.EndScrollView()"),
                "native display handles rank/progress and hides unknown or expertise-locked species");
            Check(!native.Contains("KnowledgeQuery.") &&
                !native.Contains("KnowledgeMenuUI") &&
                !native.Contains("KnowledgeFramework.") &&
                !native.Contains("?.KnowledgeFor(") &&
                !native.Contains("?.ExpertiseLevel") &&
                !Regex.IsMatch(journal, @"(?m)^\s*using\s+KnowledgeFramework\s*;") &&
                progress.Contains("\"expertiseExperience\"") &&
                progress.Contains("\"speciesKnowledge\""),
                "native Journal does not call framework-owned APIs or change saved progress contracts");
        }

        private static void TestNativeKnowledgeFallbackBoundary(string root)
        {
            string adapter = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeAdapter.cs"));
            string contract = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeContract.cs"));
            string progress = File.ReadAllText(Path.Combine(root, "Source", "FishingExpertise.cs"));

            int start = adapter.IndexOf("public static float ExpertiseProgressFor(Pawn pawn)", StringComparison.Ordinal);
            int end = start < 0 ? -1 : adapter.IndexOf("public static void Invalidate(Pawn pawn)", start, StringComparison.Ordinal);
            string body = start >= 0 && end > start ? adapter.Substring(start, end - start) : string.Empty;
            Check(body.Contains("NativeFishingKnowledgeRules.ProgressForExperience(") &&
                body.Contains("?.expertiseExperience ?? 0f") &&
                !body.Contains("?.ExpertiseProgress") &&
                body.Contains("KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrackId).progress"),
                "native expertise-progress fallback reads saved experience without calling itself");
            Check(adapter.Contains("NativeFishingKnowledgeRules.SpeciesKnowledge(progress?.speciesKnowledge, fishDef?.defName)") &&
                adapter.Contains("NativeFishingKnowledgeRules.RankForExperience(legacy)") &&
                adapter.Contains("NativeFishingKnowledgeRules.Experience(") &&
                adapter.Contains("KnowledgeRanks.ForExperience(") &&
                adapter.Contains("KnowledgeRanks.Bonus("),
                "native fallback covers species, experience and rank without displacing framework authority");
            Check(progress.Contains("public float ExpertiseProgress => AquacultureKnowledgeAdapter.ExpertiseProgressFor(pawn);") &&
                progress.Contains("\"expertiseExperience\"") && progress.Contains("\"speciesKnowledge\"") &&
                contract.Contains("public static class NativeFishingKnowledgeRules") &&
                !contract.Contains("using KnowledgeFramework;") &&
                !contract.Contains("using UnityEngine;") &&
                !contract.Contains("using Verse;"),
                "fallback is portable and legacy pawn progression save contracts are unchanged");
        }

        private static void TestNativeColonyKnowledgeBoundary(string root)
        {
            string adapter = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeAdapter.cs"));
            string pure = File.ReadAllText(Path.Combine(root, "Source", "AquacultureKnowledgeContract.cs"));
            string progress = File.ReadAllText(Path.Combine(root, "Source", "FishingExpertise.cs"));

            int queryStart = adapter.IndexOf("public static float ColonySpeciesKnowledgeFor(ThingDef fishDef)",
                StringComparison.Ordinal);
            int queryEnd = queryStart < 0 ? -1 : adapter.IndexOf("public static IEnumerable<ThingDef> KnownSpeciesFor",
                queryStart, StringComparison.Ordinal);
            string query = queryStart >= 0 && queryEnd > queryStart
                ? adapter.Substring(queryStart, queryEnd - queryStart) : string.Empty;
            Check(query.Contains("if (!FrameworkReady) return LegacyColonySpeciesKnowledge(fishDef);") &&
                query.Contains("KnowledgeQuery.Facet(DomainId, SpeciesSubjectId(fishDef)") &&
                query.Contains("KnowledgeScope.Colony"),
                "colony query uses native fallback without displacing framework colony authority");

            int viewStart = adapter.IndexOf("public static AquacultureKnowledgeView SpeciesView(", StringComparison.Ordinal);
            int viewEnd = viewStart < 0 ? -1 : adapter.IndexOf("private static float StageKnowledge(", viewStart,
                StringComparison.Ordinal);
            string view = viewStart >= 0 && viewEnd > viewStart
                ? adapter.Substring(viewStart, viewEnd - viewStart) : string.Empty;
            Check(view.Contains("colony ? LegacyColonySpeciesKnowledge(fishDef) : LegacySpeciesKnowledge(pawn, fishDef)") &&
                view.Contains("KnowledgeQuery.Subject(DomainId, SpeciesSubjectId(fishDef)") &&
                view.Contains("KnowledgeDiscovery.StageSnapshot("),
                "colony dossier and direct query use identical native projection; V3 view remains intact");

            int fallbackStart = adapter.IndexOf("private static float LegacyColonySpeciesKnowledge(", StringComparison.Ordinal);
            int fallbackEnd = fallbackStart < 0 ? -1 : adapter.IndexOf("private static float LegacySpeciesKnowledge(",
                fallbackStart, StringComparison.Ordinal);
            string fallback = fallbackStart >= 0 && fallbackEnd > fallbackStart
                ? adapter.Substring(fallbackStart, fallbackEnd - fallbackStart) : string.Empty;
            Check(fallback.Contains("NativeFishingKnowledgeRules.ColonySpeciesKnowledge(") &&
                fallback.Contains("FishingProgressionComponent.Current?.PawnProgress") &&
                fallback.Contains("record?.pawn?.Faction?.def?.isPlayer == true") &&
                fallback.Contains("record => record?.speciesKnowledge"),
                "legacy colony projection reads only player-faction pawn progress");

            Check(pure.Contains("public static float ColonySpeciesKnowledge<T>(") &&
                pure.Contains("!belongsToPlayerFaction(record)") &&
                pure.Contains("SpeciesKnowledge(readSpeciesKnowledge(record), speciesDefName)") &&
                !pure.Contains("using KnowledgeFramework;") &&
                progress.Contains("\"aquacultureFishingProgression\"") &&
                progress.Contains("\"expertiseExperience\"") &&
                progress.Contains("\"speciesKnowledge\""),
                "colony projection is framework-free, read-only and preserves original save keys");
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
                    (def.Descendants("thingCategories").Descendants("li").Any(category =>
                        category.Value == "Fish" || category.Value == "VCEF_RawFishCategory") ||
                    def.Descendants("modExtensions").Elements("li").Any(extension =>
                        (string)extension.Attribute("Class") == "AquacultureFishing.AquaticSpeciesExtension")));

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
                blockers.Add("no first-party fish ThingDef has an explicitly recognized category or species extension");
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
            Console.WriteLine("  First-party fish ThingDefs with explicit recognized category or extension: " +
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
            Check(providerOutput.Contains("1.7/DeferredReality/Assemblies/") &&
                !providerOutput.Contains("1.7/Assemblies/"),
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
            Check(settings.Contains("DoSettingsWindowContents") &&
                settings.Contains("IAquacultureSettingsView") &&
                settings.Contains("AquacultureSettingsViewFactory.Create(Settings)"),
                "settings entry delegates through its Aquaculture-owned renderer boundary");
        }

        private static void TestSettingsRendererProviderBoundary(string root)
        {
            string source = File.ReadAllText(Path.Combine(root, "Source", "SettingsAndMasks.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureSettingsDocument.cs"));

            int interfaceStart = source.IndexOf("public interface IAquacultureSettingsView",
                StringComparison.Ordinal);
            int modStart = interfaceStart < 0 ? -1 : source.IndexOf(
                "public sealed class AquacultureMod : Mod", interfaceStart, StringComparison.Ordinal);
            string contract = interfaceStart >= 0 && modStart > interfaceStart
                ? source.Substring(interfaceStart, modStart - interfaceStart) : string.Empty;
            Check(contract.Contains("void Draw(Rect rect);") &&
                contract.Contains("void OnSettingsWritten();") &&
                Regex.Matches(contract, @"\bvoid\s+\w+\s*\(").Count == 2 &&
                !contract.Contains("InsightUi") &&
                !contract.Contains("InsightTheme") &&
                !Regex.IsMatch(source, @"(?m)^\s*using\s+InsightCanvas\s*;"),
                "settings renderer interface contains only Aquaculture-owned drawing and settings-written methods");

            int modEnd = modStart < 0 ? -1 : source.IndexOf(
                "public sealed class FishTextureVariation", modStart, StringComparison.Ordinal);
            string mod = modStart >= 0 && modEnd > modStart
                ? source.Substring(modStart, modEnd - modStart) : string.Empty;
            Check(mod.Contains("private IAquacultureSettingsView insightSettingsDocument;") &&
                mod.Contains("insightSettingsDocument = AquacultureSettingsViewFactory.Create(Settings);") &&
                mod.Contains("if (insightSettingsDocument == null)") &&
                mod.Contains("insightSettingsDocument.Draw(inRect.ContractedBy(8f));") &&
                !mod.Contains("AquacultureSettingsDocument") &&
                !mod.Contains("insightSettingsDocument?.Host") &&
                !mod.Contains("insightSettingsDocument?.Document"),
                "AquacultureMod uses only its renderer interface and preserves first draw and settings inset");

            int writeStart = mod.IndexOf("public override void WriteSettings()", StringComparison.Ordinal);
            string write = writeStart >= 0 ? mod.Substring(writeStart) : string.Empty;
            int revision = write.IndexOf("Settings.maskRevision++;", StringComparison.Ordinal);
            int visuals = write.IndexOf("FishVisualCache.Clear();", StringComparison.Ordinal);
            int pondMenus = write.IndexOf("InvalidatePondMenus();", StringComparison.Ordinal);
            int presentation = write.IndexOf("insightSettingsDocument?.OnSettingsWritten();",
                StringComparison.Ordinal);
            int save = write.IndexOf("base.WriteSettings();", StringComparison.Ordinal);
            Check(revision >= 0 && revision < visuals && visuals < pondMenus &&
                pondMenus < presentation && presentation < save &&
                write.Contains("if (Current.Game?.Maps != null)"),
                "settings save preserves mask, visual, pond-menu, presentation and base-write order");

            int onWriteStart = canvas.IndexOf("public void OnSettingsWritten()", StringComparison.Ordinal);
            int onWriteEnd = onWriteStart < 0 ? -1 : canvas.IndexOf(
                "private void BuildCatalog()", onWriteStart, StringComparison.Ordinal);
            string onWrite = onWriteStart >= 0 && onWriteEnd > onWriteStart
                ? canvas.Substring(onWriteStart, onWriteEnd - onWriteStart) : string.Empty;
            int close = onWrite.IndexOf("Host.PostClose();", StringComparison.Ordinal);
            int invalidate = onWrite.IndexOf("Document.Invalidate();", StringComparison.Ordinal);
            Check(canvas.Contains("internal static class AquacultureSettingsViewFactory") &&
                canvas.Contains("public static IAquacultureSettingsView Create(AquacultureSettings settings) =>") &&
                canvas.Contains("new AquacultureSettingsDocument(settings);") &&
                canvas.Contains("public sealed class AquacultureSettingsDocument : IAquacultureSettingsView") &&
                canvas.Contains("public AquacultureSettingsDocument(AquacultureSettings settings)") &&
                canvas.Contains("BuildCatalog();") &&
                canvas.Contains("AquacultureInsightPresentation.Apply(Document, settings);") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                canvas.Contains("public void PostClose()") &&
                close >= 0 && invalidate > close,
                "Canvas settings provider retains its existing presentation, catalog, draw and ordered cleanup");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05A2") &&
                compatibility.Contains("IAquacultureSettingsView") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "settings renderer seam does not qualify an optional Canvas build or runtime release");
        }

        private static void TestNativeGeneralSettingsRendererPrototype(string root)
        {
            string source = File.ReadAllText(Path.Combine(root, "Source", "SettingsAndMasks.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureSettingsDocument.cs"));
            int start = source.IndexOf(
                "public sealed class NativeAquacultureSettingsView : IAquacultureSettingsView",
                StringComparison.Ordinal);
            int end = start < 0 ? -1 : source.IndexOf(
                "public sealed class FishTextureVariation", start, StringComparison.Ordinal);
            string native = start >= 0 && end > start
                ? source.Substring(start, end - start) : string.Empty;

            Check(native.Contains("public NativeAquacultureSettingsView(AquacultureSettings settings)") &&
                native.Contains("public void Draw(Rect rect)") &&
                native.Contains("public void OnSettingsWritten()") &&
                !Regex.IsMatch(source, @"(?m)^\s*using\s+InsightCanvas\s*;") &&
                !native.Contains("InsightUi") &&
                !native.Contains("AquacultureSettingsDocument"),
                "native general settings view satisfies core interface with no Canvas dependencies");

            Check(native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsGameplay\")") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsEcology\")") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsBreeding\")") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsAppearance\")") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsFishing\")") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsAdvanced\")") &&
                native.Contains("private void SelectPage(string selected)") &&
                native.Contains("scrollPosition = Vector2.zero;") &&
                native.Contains("Widgets.BeginScrollView(viewport, ref scrollPosition, content);") &&
                native.Contains("Widgets.EndScrollView();") &&
                native.Contains("Text.Font = previousFont;") &&
                native.Contains("TooltipHandler.TipRegion(row, L(tooltipKey))"),
                "native settings has six discoverable pages with scrolling and local cleanup");

            Check(native.Contains("ref settings.enableResearchProgression") &&
                native.Contains("ref settings.showPondAlerts") &&
                native.Contains("ref settings.fishingDurationFactor, 0.1f, 1f") &&
                native.Contains("ref settings.animaFishPerTree, 1, 100") &&
                native.Contains("settings.fishingDurationFactor = 0.30f;") &&
                native.Contains("settings.animaFishPerTree = 20;") &&
                native.Contains("ResetButton(ref y, width, \"AquacultureFishing.SettingsResetGameplay\", ResetGameplay);"),
                "native Gameplay page preserves original fields, ranges and reset defaults");

            Check(native.Contains("ref settings.ecologyIntervalHours, 0.25f, 6f") &&
                native.Contains("ref settings.foodDemandMultiplier, 0.1f, 5f") &&
                native.Contains("ref settings.algaeGrowthMultiplier, 0f, 5f") &&
                native.Contains("ref settings.starvationHours, 6f, 600f") &&
                native.Contains("ref settings.waterStressHours, 1f, 240f") &&
                native.Contains("ref settings.temperatureStressHours, 1f, 240f") &&
                native.Contains("ref settings.fishCapacityPerCell, 0.25f, 10f") &&
                native.Contains("ref settings.feedValuePerUnit, 0.005f, 0.25f") &&
                native.Contains("ref settings.feederRange, 1f, 30f") &&
                native.Contains("ref settings.predationEnabled") &&
                native.Contains("if (settings.capacityTransitionWarning)") &&
                native.Contains("settings.capacityTransitionWarning = false;") &&
                native.Contains("settings.capacityModelVersion = AquacultureSettings.CurrentCapacityModelVersion;") &&
                native.Contains("ResetButton(ref y, width, \"AquacultureFishing.SettingsResetEcology\", ResetEcology);"),
                "native Ecology page preserves parameter bounds and explicit migration actions");

            Check(native.Contains("ref settings.globalBreedingEnabled") &&
                native.Contains("ref settings.breedingIntervalDays, 1f, 60f") &&
                native.Contains("ref settings.eggHatchDays, 0.25f, 30f") &&
                native.Contains("ref settings.minimumOffspring, 1, 10") &&
                native.Contains("ref settings.maximumOffspring, settings.minimumOffspring, 12") &&
                native.Contains("ref settings.minimumLifespanDays, 5, 600") &&
                native.Contains("ref settings.maximumLifespanDays, settings.minimumLifespanDays, 1200") &&
                native.Contains("Mathf.Max(settings.minimumOffspring, settings.maximumOffspring)") &&
                native.Contains("Mathf.Max(settings.minimumLifespanDays,") &&
                native.Contains("ResetButton(ref y, width, \"AquacultureFishing.SettingsResetBreeding\", ResetBreeding);"),
                "native Breeding page retains min/max coupling and reset rules");

            Check(native.Contains("ref settings.enhancedPondVisuals") &&
                native.Contains("ref settings.waterTypeTint") &&
                native.Contains("ref settings.algaeTint") &&
                native.Contains("ref settings.depthShading") &&
                native.Contains("ref settings.shorelineVisuals") &&
                native.Contains("ref settings.pondDecorations") &&
                native.Contains("ref settings.fishShadows") &&
                native.Contains("ref settings.underwaterFishTint") &&
                native.Contains("ref settings.surfaceOverlay") &&
                native.Contains("ref settings.caustics") &&
                native.Contains("ref settings.pseudoDepth") &&
                native.Contains("ref settings.pondRipples") &&
                native.Contains("Find.WindowStack.Add(new Dialog_FishMaskPainter())") &&
                native.Contains("Widgets.CheckboxLabeled(row, L(labelKey), ref value);") &&
                native.Contains("Widgets.HorizontalSlider(") &&
                !native.Contains("Scribe_") &&
                !native.Contains("Settings.Write(") &&
                !native.Contains("Current.Game"),
                "native Appearance and general settings modify explicit fields without paint-time game writes");

            Check(source.Contains("insightSettingsDocument = AquacultureSettingsViewFactory.Create(Settings);") &&
                source.Contains("insightSettingsDocument?.OnSettingsWritten();") &&
                source.Contains("base.WriteSettings();") &&
                canvas.Contains("new AquacultureSettingsDocument(settings);") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                canvas.Contains("Host.PostClose();") &&
                canvas.Contains("Document.Invalidate();"),
                "native general settings prototype does not alter the Canvas default or saved-settings lifecycle");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05B5A") &&
                compatibility.Contains("NativeAquacultureSettingsView") &&
                compatibility.Contains("Fishing and Advanced") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "partial Settings prototype explicitly documents missing pages and runtime qualification");
        }

        private static void TestNativeSpecializedSettingsRendererPrototype(string root)
        {
            string source = File.ReadAllText(Path.Combine(root, "Source", "SettingsAndMasks.cs"));
            string canvas = File.ReadAllText(Path.Combine(root, "Source", "UI",
                "AquacultureSettingsDocument.cs"));
            int start = source.IndexOf(
                "public sealed class NativeAquacultureSettingsView : IAquacultureSettingsView",
                StringComparison.Ordinal);
            int end = start < 0 ? -1 : source.IndexOf(
                "public sealed class FishTextureVariation", start, StringComparison.Ordinal);
            string native = start >= 0 && end > start
                ? source.Substring(start, end - start) : string.Empty;

            Check(native.Contains("case \"fishing\": DrawFishing(ref y, viewWidth); break;") &&
                native.Contains("case \"advanced\": DrawAdvanced(ref y, viewWidth); break;") &&
                native.Contains("case \"fishing\": return L(\"AquacultureFishing.SettingsFishing\");") &&
                native.Contains("case \"advanced\": return L(\"AquacultureFishing.SettingsAdvanced\");") &&
                native.Contains("private void DrawFishing(ref float y, float width)") &&
                native.Contains("private void DrawAdvanced(ref float y, float width)") &&
                native.Contains("private float FishingContentHeight(float width)") &&
                native.Contains("private float AdvancedContentHeight(float width)"),
                "native Settings exposes both catalog pages with content-sized scrolling");

            Check(native.Contains("DefDatabase<ThingDef>.AllDefsListForReading") &&
                native.Contains("FishUtility.IsFish(def)") &&
                native.Contains("DefDatabase<FishTraitDef>.AllDefsListForReading") &&
                native.Contains("def.mutationEligible") &&
                native.Contains("!FishTraitUtility.IsDemographicOrEcology(def.defName)") &&
                native.Contains("def.label.IndexOf(fishSearch,") &&
                native.Contains("def.defName.IndexOf(fishSearch,") &&
                native.Contains("trait.label.IndexOf(traitSearch,") &&
                native.Contains("trait.defName.IndexOf(traitSearch,") &&
                native.Contains("scroll = Vector2.zero;") &&
                native.Contains("visibleHeight = viewport.height;") &&
                native.Contains("IsVisible(y, height)") &&
                native.Contains("fishSearch = string.Empty;") &&
                native.Contains("traitSearch = string.Empty;"),
                "native Settings preserves original catalogs and filters with scroll cleanup");

            Check(native.Contains("settings.MinimumExpertiseFor(definition)") &&
                native.Contains("ChooseFishingRank(definition);") &&
                native.Contains("foreach (FishingRank rank in (FishingRank[])Enum.GetValues(typeof(FishingRank)))") &&
                native.Contains("settings.GetFishExpertiseSetting(fish).minimumFishingExpertise = selected") &&
                native.Contains("AquacultureFishing.SettingsExpertiseTip") &&
                !native.Contains("KnowledgeMenuUI") &&
                !native.Contains("InsightUi") &&
                !native.Contains("KnowledgeFramework."),
                "native Fishing uses only first-party FishingRank and explicit user selection");

            Check(native.Contains("settings.PresentationPreferences.DensityIndex") &&
                native.Contains("AquaculturePresentationPreferences.FromSerializedValues(") &&
                native.Contains("settings.SetPresentationPreferences(") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsDensityComfortable\")") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsDensityNormal\")") &&
                native.Contains("new FloatMenuOption(L(\"AquacultureFishing.SettingsDensityCompact\")") &&
                native.Contains("current.HighContrast") &&
                native.Contains("current.ReducedMotion") &&
                native.Contains("AquacultureFishing.SettingsMigrationBoundaryBody"),
                "native Advanced preserves presentation density and accessibility options");

            Check(native.Contains("ref settings.wildExceptionalTraitChance, 0f, 1f") &&
                native.Contains("ref settings.parentalTraitInheritanceChance, 0f, 1f") &&
                native.Contains("ref settings.maxInheritedTraits, 0, 8") &&
                native.Contains("ref settings.offspringMutationChance, 0f, 1f") &&
                native.Contains("ref settings.maxOffspringMutations, 0, 8") &&
                native.Contains("ref settings.registeredBreedDefiningTraitReliability, 0f, 1f") &&
                native.Contains("settings.wildExceptionalTraitChance = AquacultureSettings.DefaultWildExceptionalTraitChance;") &&
                native.Contains("settings.maxInheritedTraits = AquacultureSettings.DefaultMaximumInheritedTraits;") &&
                native.Contains("AquacultureSettings.DefaultRegisteredBreedDefiningTraitReliability;") &&
                native.Contains("ResetButton(ref y, width, \"AquacultureFishing.SettingsResetTraitRules\", ResetTraitRules);"),
                "native Advanced matches original breeding ranges and reset defaults");

            Check(native.Contains("settings.GetTraitSetting(trait, false)?.enabled ?? true") &&
                native.Contains("settings.GetTraitSetting(trait).enabled = toggled") &&
                native.Contains("settings.GetTraitSetting(trait, false)?.weight ??") &&
                native.Contains("trait.DefaultWeight;") &&
                native.Contains("Mathf.Clamp(savedWeight, 0f, 5f)") &&
                native.Contains("weight, 0f, 5f, true") &&
                native.Contains("settings.GetTraitSetting(trait).weight = changed") &&
                native.Contains("FishTraitUtility.EffectLine(trait)") &&
                native.Contains("TooltipHandler.TipRegion(row,") &&
                !native.Contains("Scribe_") &&
                !native.Contains("Settings.Write("),
                "trait rows use non-mutating reads and only write on explicit checkbox or slider changes");

            Check(source.Contains("insightSettingsDocument = AquacultureSettingsViewFactory.Create(Settings);") &&
                source.Contains("insightSettingsDocument?.OnSettingsWritten();") &&
                canvas.Contains("new AquacultureSettingsDocument(settings);") &&
                canvas.Contains("Host.Draw(rect, Time.deltaTime);") &&
                !native.Contains("InsightCanvas"),
                "six-page native Settings prototype retains Canvas as the player default");

            string compatibility = File.ReadAllText(Path.Combine(root, "About", "COMPATIBILITY.md"));
            Check(compatibility.Contains("AQ-DEP-05B5B") &&
                compatibility.Contains("NativeAquacultureSettingsView") &&
                compatibility.Contains("all six") &&
                compatibility.Contains("still requires Insight Canvas") &&
                compatibility.Contains("RimTest"),
                "complete native Settings source prototype is not a runtime-qualified standalone release");
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
