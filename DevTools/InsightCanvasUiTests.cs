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
            TestPrompt2Contracts(root);
            TestMetadataAndBuildContract(root);
            TestSerializedSettingsContract(root);
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
                "maximumLifespanDays"
            };
            foreach (string key in requiredKeys) Check(settings.Contains("\"" + key + "\""), "serialized key preserved: " + key);
            Check(settings.Contains("DoSettingsWindowContents") && settings.Contains("AquacultureSettingsDocument"),
                "settings entry delegates to the Insight Canvas document");
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
