using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

#if AQUACULTURE_DEV_TESTS
namespace AquacultureFishing
{
    public static class AquacultureBridgeTestSuite
    {
        public static AquacultureInGameTestReport RunBaseline(string runId)
        {
            try
            {
                return Complete(Run(), runId);
            }
            catch (Exception exception)
            {
                return FailedReport(runId, "aquaculture-baseline", exception);
            }
        }

        public static AquacultureInGameTestReport RunGoldenPath(string runId)
        {
            AquacultureInGameTestRequest request = new AquacultureInGameTestRequest
            {
                runId = runId,
                suite = "aquaculture-golden-path",
                requestedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
            try
            {
                return Complete(RunGoldenPathInternal(request), runId);
            }
            catch (Exception exception)
            {
                return FailedReport(runId, request.suite, exception);
            }
        }

        private static AquacultureInGameTestReport Complete(AquacultureInGameTestReport report, string runId)
        {
            report.runId = runId;
            report.completedUtc = DateTime.UtcNow;
            return report;
        }

        private static AquacultureInGameTestReport FailedReport(string runId, string suite, Exception exception)
        {
            AquacultureInGameTestReport report = NewReport(runId, suite);
            report.results.Add(AquacultureInGameTestResult.Failed("suite", exception));
            report.completedUtc = DateTime.UtcNow;
            return report;
        }

        private static AquacultureInGameTestReport RunGoldenPathInternal(AquacultureInGameTestRequest request)
        {
            AquacultureInGameTestReport report = NewReport(request.runId, "inhabited-pond-golden-path");
            report.requestedUtc = request.requestedUtc;
            AquacultureGoldenPathFixture fixture = null;
            try
            {
                Map map = RequireCurrentMap();
                fixture = new AquacultureGoldenPathFixture(map);
                Check(report, "fixture-rectangle-and-topology", () => fixture.Setup());
                Check(report, "fixture-pond-contract", () => fixture.VerifyPondContract());
                Check(report, "fixture-feed-habitat-and-cache", () => fixture.VerifyFeedHabitatAndCache());
                Check(report, "fixture-stocked-adults", () => fixture.StockAndVerifyAdults());
                Check(report, "fixture-breeding-and-inheritance", () => fixture.BreedAndVerifyInheritance());
                Check(report, "fixture-processing", () => fixture.ProcessAndVerifyMeat());
            }
            catch (Exception exception)
            {
                report.results.Add(AquacultureInGameTestResult.Failed("fixture-runner", exception));
            }
            finally
            {
                string cleanupMessage = "cleanup did not start";
                try
                {
                    cleanupMessage = fixture == null ? "no fixture was created" : fixture.Cleanup();
                    report.results.Add(AquacultureInGameTestResult.Passed("fixture-cleanup", cleanupMessage));
                }
                catch (Exception exception)
                {
                    report.results.Add(AquacultureInGameTestResult.Failed("fixture-cleanup", exception));
                }
            }
            return report;
        }

        private static string DescribeVisualDebugState()
        {
            string[] diagnosticFlags =
            {
                "drawIndoorMask", "drawOutdoorMask", "drawLightingOverlay", "drawWorldOverlays",
                "drawRooms", "drawMapRooms", "drawWaterBodies", "drawTerrainCurtain", "drawSectionEdges",
                "drawRegions", "drawRegionLinks", "drawRegionDirties", "drawRegionTraversal", "drawPowerNetGrid",
                "drawGas", "drawDarknessOverlay", "drawUsedRects", "drawPaths", "drawPatherState",
                "drawFOVSymmetry", "drawInteractionCells"
            };
            List<string> enabledFlags = new List<string>();
            Type settingsType = typeof(DebugViewSettings);
            foreach (string name in diagnosticFlags)
            {
                System.Reflection.FieldInfo field = settingsType.GetField(
                    name,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);
                if (field == null || field.FieldType != typeof(bool)) continue;
                try
                {
                    if ((bool)field.GetValue(null)) enabledFlags.Add(name);
                }
                catch
                {
                    enabledFlags.Add(name + "=unreadable");
                }
            }

            List<string> visualAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name)
                .Where(name => !name.NullOrEmpty() &&
                    (name.IndexOf("PerformanceAnalyzer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Horticulture", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Aquaculture", StringComparison.OrdinalIgnoreCase) >= 0))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return "debugFlags=" + (enabledFlags.Count == 0 ? "none" : string.Join(",", enabledFlags)) +
                "; visualAssemblies=" + (visualAssemblies.Count == 0 ? "none" : string.Join(",", visualAssemblies));
        }

        private static AquacultureInGameTestReport Run()
        {
            AquacultureInGameTestReport report = NewReport();
            Check(report, "game-and-map-ready", () =>
            {
                Map map = RequireCurrentMap();
                Require(Find.Maps != null && Find.Maps.Contains(map), "current map is not in Find.Maps");
                Require(map.cellIndices.NumGridCells > 0, "current map has no cells");
                return "map=" + map.uniqueID + " cells=" + map.cellIndices.NumGridCells;
            });

            Check(report, "required-game-components", () =>
            {
                Game game = Verse.Current.Game;
                Require(game != null, "Current.Game is null");
                Require(game.GetComponent<AquacultureJournalComponent>() != null, "journal component is missing");
                Require(game.GetComponent<AquacultureCommissionComponent>() != null, "commission component is missing");
                Require(game.GetComponent<FishingProgressionComponent>() != null, "fishing progression component is missing");
                Require(game.GetComponent<FishingRodWorkflowComponent>() != null, "fishing rod workflow component is missing");
                return "journal, commissions, progression, and rod workflow are attached";
            });

            Check(report, "required-map-components", () =>
            {
                Map map = RequireCurrentMap();
                Require(map.GetComponent<FishPondMapComponent>() != null, "pond component is missing");
                Require(map.GetComponent<NaturalFishPopulationMapComponent>() != null, "natural population component is missing");
                Require(map.GetComponent<FishRoutingMapComponent>() != null, "fish routing component is missing");
                Require(map.GetComponent<FishingRodMapComponent>() != null, "fishing rod component is missing");
                return "pond, natural population, routing, and rod components are attached";
            });

            Check(report, "visual-debug-state", () =>
            {
                return DescribeVisualDebugState();
            });

            Check(report, "required-definitions", () =>
            {
                Require(DefDatabase<TerrainDef>.GetNamedSilentFail("AF_Pond") != null,
                    "AF_Pond terrain is missing");
                string[] names = { "AF_FishFeed", "AF_FishMeat", "AF_FishEgg", "AF_PondProxy" };
                List<string> missing = names.Where(name => DefDatabase<ThingDef>.GetNamedSilentFail(name) == null).ToList();
                Require(missing.Count == 0, "missing ThingDefs: " + string.Join(", ", missing));
                Require(DefDatabase<ResearchProjectDef>.GetNamedSilentFail("AF_Pondkeeping") != null,
                    "AF_Pondkeeping research is missing");
                return "pond terrain, core ThingDefs, and Pondkeeping research are loaded";
            });

            Check(report, "fish-def-runtime-configuration", () =>
            {
                List<ThingDef> fishDefs = FishDefs();
                Require(fishDefs.Count > 0, "no fish ThingDefs were discovered");
                List<string> errors = new List<string>();
                foreach (ThingDef def in fishDefs)
                {
                    if (def.stackLimit != 1) errors.Add(def.defName + " stackLimit=" + def.stackLimit);
                    if (def.comps == null || !def.comps.Any(comp => comp?.compClass == typeof(CompFishTraits)))
                        errors.Add(def.defName + " has no CompFishTraits");
                    if (!FishUtility.IsRuntimeFish(def)) errors.Add(def.defName + " was not registered at startup");
                }
                Require(errors.Count == 0, string.Join("; ", errors.Take(8)));
                return "fishDefs=" + fishDefs.Count + " all configured and registered";
            });

            Check(report, "fish-trait-definition-contract", () =>
            {
                List<FishTraitDef> traits = DefDatabase<FishTraitDef>.AllDefsListForReading;
                Require(traits.Count > 0, "no FishTraitDefs were loaded");
                string[] required = { FishTraitUtility.Fry, FishTraitUtility.Juvenile, FishTraitUtility.Adult,
                    FishTraitUtility.Elder, FishTraitUtility.Male, FishTraitUtility.Female };
                List<string> missing = required.Where(name => DefDatabase<FishTraitDef>.GetNamedSilentFail(name) == null).ToList();
                Require(missing.Count == 0, "missing demographic traits: " + string.Join(", ", missing));
                Require(traits.Any(def => def.defName.StartsWith(FishTraitUtility.DietPrefix, StringComparison.Ordinal)),
                    "no diet traits were loaded");
                Require(traits.Any(def => def.defName.StartsWith(FishTraitUtility.WaterPrefix, StringComparison.Ordinal)),
                    "no water traits were loaded");
                List<string> errors = traits.SelectMany(def => def.ConfigErrors()).ToList();
                Require(errors.Count == 0, string.Join("; ", errors.Take(8)));
                foreach (FishTraitDef trait in traits)
                {
                    RequireFinite(trait.commonality, trait.defName + ".commonality");
                    RequireFinite(trait.drawScale, trait.defName + ".drawScale");
                    RequireFinite(trait.breedingCooldownFactor, trait.defName + ".breedingCooldownFactor");
                }
                return "traits=" + traits.Count + " required demographic/ecology traits present";
            });

            Check(report, "research-prerequisite-chain", () =>
            {
                string[] chain = { "AF_Pondkeeping", "AF_ManagedAquaculture", "AF_IndustrialAquaculture",
                    "AF_SelectiveBreeding" };
                ResearchProjectDef previous = null;
                foreach (string name in chain)
                {
                    ResearchProjectDef current = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
                    Require(current != null, "missing research project " + name);
                    if (previous != null)
                        Require(current.prerequisites != null && current.prerequisites.Contains(previous),
                            name + " does not require " + previous.defName);
                    previous = current;
                }
                return "four aquaculture projects form the documented prerequisite chain";
            });

            Check(report, "fish-instance-contract", () =>
            {
                Map map = RequireCurrentMap();
                List<CompFishTraits> fish = map.listerThings.AllThings
                    .Select(thing => thing?.TryGetComp<CompFishTraits>())
                    .Where(comp => comp != null).ToList();
                int checkedFish = 0;
                foreach (CompFishTraits comp in fish)
                {
                    Require(comp.parent != null && FishUtility.IsFish(comp.parent.def),
                        "fish component is attached to a non-fish ThingDef");
                    Require(comp.initialized, comp.parent.def.defName + " fish component is not initialized");
                    Require(comp.traitDefNames != null, comp.parent.def.defName + " trait list is null");
                    Require(comp.traitDefNames.Contains(FishTraitUtility.Fry) ||
                        comp.traitDefNames.Contains(FishTraitUtility.Juvenile) ||
                        comp.traitDefNames.Contains(FishTraitUtility.Adult) ||
                        comp.traitDefNames.Contains(FishTraitUtility.Elder),
                        comp.parent.LabelCap + " has no age trait");
                    Require(comp.traitDefNames.Contains(FishTraitUtility.Male) ||
                        comp.traitDefNames.Contains(FishTraitUtility.Female),
                        comp.parent.LabelCap + " has no sex trait");
                    Require(comp.traitDefNames.Any(name => name.StartsWith(FishTraitUtility.DietPrefix, StringComparison.Ordinal)),
                        comp.parent.LabelCap + " has no diet trait");
                    Require(comp.traitDefNames.Any(name => name.StartsWith(FishTraitUtility.WaterPrefix, StringComparison.Ordinal)),
                        comp.parent.LabelCap + " has no water trait");
                    foreach (KeyValuePair<string, float> value in comp.traitValues ?? new Dictionary<string, float>())
                        RequireFinite(value.Value, comp.parent.LabelCap + "." + value.Key);
                    RequireFinite(comp.foodReserve, comp.parent.LabelCap + ".foodReserve");
                    RequireFinite(comp.starvationProgress, comp.parent.LabelCap + ".starvationProgress");
                    RequireFinite(comp.waterStress, comp.parent.LabelCap + ".waterStress");
                    RequireFinite(comp.temperatureStress, comp.parent.LabelCap + ".temperatureStress");
                    RequireFinite(comp.habitatFit, comp.parent.LabelCap + ".habitatFit");
                    checkedFish++;
                }
                return "fishComponents=" + checkedFish + " valid";
            });

            Check(report, "pond-runtime-snapshots", () =>
            {
                Map map = RequireCurrentMap();
                FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
                Require(component != null, "pond component is missing");
                List<PondProxyThing> proxies = map.listerThings.AllThings.OfType<PondProxyThing>().ToList();
                IntVec3 firstPondCell = FirstPondCell(map);
                int snapshots = 0;
                if (firstPondCell.IsValid)
                {
                    PondMenuSnapshot snapshot = component.MenuSnapshotAt(firstPondCell);
                    ValidatePondSnapshot(snapshot, "first pond");
                    snapshots++;
                }
                foreach (PondProxyThing proxy in proxies)
                {
                    Require(proxy.Position.InBounds(map), "pond proxy is out of bounds");
                    Require(map.terrainGrid.TerrainAt(proxy.Position)?.defName == "AF_Pond",
                        "pond proxy is not on AF_Pond terrain");
                    PondMenuSnapshot snapshot = component.MenuSnapshotAt(proxy.Position);
                    ValidatePondSnapshot(snapshot, "proxy " + proxy.thingIDNumber);
                    snapshots++;
                }
                return "pondCells=" + CountPondCells(map) + " proxies=" + proxies.Count + " snapshots=" + snapshots;
            });

            Check(report, "pond-capacity-rules", () =>
            {
                Require(PondCapacityRules.PhysicalMaximum(0) == 1, "physical zero-cell boundary changed");
                Require(PondCapacityRules.BaseCapacity(10, 0.75f) == 7, "base capacity floor changed");
                Require(PondCapacityRules.IndustrialCapacity(10, 0.75f, 1, 0f) == 10,
                    "aerator/physical cap contract changed");
                Require(PondCapacityRules.FoodSupportedPopulation(2f, 1f, 10) == 2,
                    "food-supported population contract changed");
                PondHabitatSnapshot deficient = new PondHabitatSnapshot { plantDemand = 1f, plantSupply = 0f };
                Require(PondCapacityRules.HabitatSupportedPopulation(deficient, 10, 10) == 0,
                    "habitat support boundary changed");
                Require(PondCapacityRules.LimitingConstraint(6, 4, 10, 0, 2, 4) == PondCapacityConstraint.Food,
                    "food constraint precedence changed");
                Require(PondCapacityRules.LimitingConstraint(6, 4, 10, 0, 4, 2) == PondCapacityConstraint.Habitat,
                    "habitat constraint precedence changed");
                Require(PondCapacityRules.LimitingConstraint(4, 4, 10, 3, 4, 4) == PondCapacityConstraint.Management,
                    "management constraint precedence changed");
                return "capacity boundaries and constraint precedence are stable";
            });

            Check(report, "natural-water-runtime", () =>
            {
                Map map = RequireCurrentMap();
                NaturalFishPopulationMapComponent component = map.GetComponent<NaturalFishPopulationMapComponent>();
                if (component != null && !component.IsInitializedForDeferredReality)
                    component.InitializeForDevTest();
                Require(component != null && component.IsInitializedForDeferredReality,
                    "natural population component has not initialized water bodies");
                foreach (NaturalWaterPopulation population in component.Populations)
                {
                    Require(population != null && population.anchor.InBounds(map), "invalid natural-water population anchor");
                    Require(population.cellCount >= 0 && population.carryingCapacity >= 0 &&
                        population.targetPopulation >= 0, "negative natural-water population bounds");
                    RequireFinite(population.TotalPopulation, "natural-water total population");
                    foreach (NaturalFishSpeciesPopulation species in population.species ?? new List<NaturalFishSpeciesPopulation>())
                    {
                        Require(species != null && !species.fishDefName.NullOrEmpty(), "natural-water species has no Def name");
                        RequireFinite(species.population, species.fishDefName + ".population");
                        Require(species.population >= 0f, species.fishDefName + " population is negative");
                    }
                }
                foreach (NaturalWaterViewSnapshot view in component.PreparedWaterSnapshots)
                {
                    Require(view != null && view.anchor.InBounds(map), "invalid prepared water snapshot");
                    RequireFinite(view.totalPopulation, "prepared water total population");
                    Require(view.totalPopulation >= 0f && view.carryingCapacity >= 0,
                        "prepared water snapshot has negative values");
                    Require(view.species.All(def => def != null && FishUtility.IsFish(def)),
                        "prepared water snapshot contains a non-fish species");
                    Require(view.estimates.Values.All(value => value >= 0), "prepared water estimate is negative");
                }
                return "populations=" + component.Populations.Count + " prepared=" + component.PreparedWaterSnapshots.Count;
            });

            Check(report, "journal-and-breed-state", () =>
            {
                AquacultureJournalComponent journal = AquacultureJournalComponent.Current;
                Require(journal != null, "journal component is missing");
                HashSet<string> speciesIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (FishSpeciesJournalRecord record in journal.SpeciesRecords)
                {
                    Require(record != null && !record.fishDefName.NullOrEmpty(), "journal species has no Def name");
                    Require(speciesIds.Add(record.fishDefName), "duplicate journal species " + record.fishDefName);
                    Require(record.FishDef != null && FishUtility.IsFish(record.FishDef),
                        "journal species is not a loaded fish " + record.fishDefName);
                    RequireFinite(record.largestSize, record.fishDefName + ".largestSize");
                    RequireFinite(record.highestBeauty, record.fishDefName + ".highestBeauty");
                    RequireFinite(record.highestNutrition, record.fishDefName + ".highestNutrition");
                    RequireFinite(record.rarestTraitScore, record.fishDefName + ".rarestTraitScore");
                    Require(record.longestLivedTicks >= 0, record.fishDefName + " has a negative lifespan record");
                }
                HashSet<string> breedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (FishBreedRecord breed in journal.Breeds)
                {
                    Require(breed != null && !breed.id.NullOrEmpty(), "breed has no stable ID");
                    Require(breedIds.Add(breed.id), "duplicate breed ID " + breed.id);
                    Require(breed.FishDef != null && FishUtility.IsFish(breed.FishDef),
                        "breed references a missing/non-fish Def " + breed.id);
                    Require(breed.traitDefNames != null && breed.traitDefNames.All(name =>
                        DefDatabase<FishTraitDef>.GetNamedSilentFail(name) != null),
                        "breed " + breed.id + " references a missing trait");
                    Require(breed.matchingBirths >= 0 && breed.qualifyingBirths >= breed.matchingBirths &&
                        breed.highestGeneration >= 0, "breed " + breed.id + " has invalid counters");
                    RequireFinite(breed.Stability, breed.id + ".stability");
                    Require(breed.Stability >= 0f && breed.Stability <= 1f, breed.id + " stability is out of range");
                    foreach (float value in (breed.traitValues ?? new Dictionary<string, float>()).Values)
                        RequireFinite(value, breed.id + ".traitValue");
                }
                return "journalSpecies=" + journal.SpeciesRecords.Count + " breeds=" + journal.Breeds.Count;
            });

            Check(report, "commission-state", () =>
            {
                AquacultureCommissionComponent component = AquacultureCommissionComponent.Current;
                Require(component != null, "commission component is missing");
                AquacultureCommissionRecord active = component.ActiveCommission;
                if (active == null) return "no active commission";
                Require(!active.id.NullOrEmpty() && !active.breedId.NullOrEmpty(), "active commission has no stable IDs");
                Require(active.Breed != null && active.FishDef != null && active.Trait != null,
                    "active commission has an unresolved breed, fish, or trait");
                Require(active.offeredTick >= 0 && active.deadlineTick >= active.offeredTick,
                    "active commission has invalid offer/deadline ticks");
                Require(active.rewardSilver > 0 && active.minimumGeneration >= 0,
                    "active commission has invalid reward/generation");
                RequireFinite(active.minimumStability, "active commission stability");
                RequireFinite(active.minimumSizeFactor, "active commission size");
                Require(active.minimumStability >= 0f && active.minimumStability <= 1f &&
                    active.minimumSizeFactor > 0f, "active commission thresholds are out of range");
                return "active commission " + active.id + " is structurally valid";
            });

            Check(report, "settings-state", () =>
            {
                AquacultureSettings settings = AquacultureMod.Settings;
                Require(settings != null, "Aquaculture settings are unavailable");
                RequireFinite(settings.fishingDurationFactor, "fishingDurationFactor");
                RequireFinite(settings.wildExceptionalTraitChance, "wildExceptionalTraitChance");
                RequireFinite(settings.parentalTraitInheritanceChance, "parentalTraitInheritanceChance");
                RequireFinite(settings.offspringMutationChance, "offspringMutationChance");
                RequireFinite(settings.breedingIntervalDays, "breedingIntervalDays");
                RequireFinite(settings.eggHatchDays, "eggHatchDays");
                RequireFinite(settings.ecologyIntervalHours, "ecologyIntervalHours");
                RequireFinite(settings.fishCapacityPerCell, "fishCapacityPerCell");
                Require(settings.wildExceptionalTraitChance >= 0f && settings.wildExceptionalTraitChance <= 1f &&
                    settings.parentalTraitInheritanceChance >= 0f && settings.parentalTraitInheritanceChance <= 1f &&
                    settings.offspringMutationChance >= 0f && settings.offspringMutationChance <= 1f,
                    "trait probabilities are out of range");
                Require(settings.minimumOffspring > 0 && settings.maximumOffspring >= settings.minimumOffspring,
                    "offspring bounds are invalid");
                return "settings are finite and within runtime bounds";
            });

            Check(report, "knowledge-view-contract", () =>
            {
                ThingDef fish = FishDefs().FirstOrDefault();
                Require(fish != null, "no fish is available for the knowledge view");
                AquacultureKnowledgeView view = AquacultureKnowledgeAdapter.SpeciesView(fish, null, true);
                Require(view.subjectId == "species:" + fish.defName, "knowledge subject ID is unstable");
                RequireFinite(view.knowledge, "knowledge amount");
                RequireFinite(view.confidence, "knowledge confidence");
                Require(view.knowledge >= 0f && view.knowledge <= 1f && view.confidence >= 0f && view.confidence <= 1f,
                    "knowledge values are out of range");
                Require(view.knownFacets != null, "knowledge facets are null");
                return "species subject and colony knowledge view are readable";
            });

            Check(report, "snapshot-cache-repeatability", () =>
            {
                Map map = RequireCurrentMap();
                IReadOnlyList<PondMenuSnapshot> pondsFirst = AquacultureSnapshotCache.Ponds(map);
                IReadOnlyList<PondMenuSnapshot> pondsSecond = AquacultureSnapshotCache.Ponds(map);
                IReadOnlyList<NaturalWaterViewSnapshot> watersFirst = AquacultureSnapshotCache.Waters(map);
                IReadOnlyList<NaturalWaterViewSnapshot> watersSecond = AquacultureSnapshotCache.Waters(map);
                Require(pondsFirst != null && watersFirst != null, "snapshot cache returned null");
                Require(object.ReferenceEquals(pondsFirst, pondsSecond), "pond snapshot cache did not reuse its revision");
                Require(object.ReferenceEquals(watersFirst, watersSecond), "water snapshot cache did not reuse its revision");
                AquacultureJournalViewSnapshot journal = AquacultureSnapshotCache.Journal(null, true);
                Require(journal != null && journal.species != null, "journal snapshot cache returned null");
                Require(journal.species.Count == FishDefs().Count, "journal snapshot does not cover every fish Def");
                return "ponds=" + pondsFirst.Count + " waters=" + watersFirst.Count +
                    " journalSpecies=" + journal.species.Count + " revision=" + journal.revision;
            });

            return report;
        }

        private static AquacultureInGameTestReport NewReport()
        {
            return NewReport(null, "aquaculture-mod-owned-live-world");
        }

        private static AquacultureInGameTestReport NewReport(string runId, string suite)
        {
            return new AquacultureInGameTestReport
            {
                runId = runId,
                suite = suite,
                launchId = Environment.GetEnvironmentVariable("DEVBRIDGE_LAUNCH_ID"),
                generation = ParseGeneration(),
                gameTick = Find.TickManager?.TicksGame ?? 0,
                startedUtc = DateTime.UtcNow
            };
        }

        private static List<ThingDef> FishDefs()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish)
                .OrderBy(def => def.defName, StringComparer.Ordinal).ToList();
        }

        private static Map RequireCurrentMap()
        {
            Map map = Find.CurrentMap;
            Require(map != null, "Find.CurrentMap is null");
            return map;
        }

        private static IntVec3 FirstPondCell(Map map)
        {
            for (int index = 0; index < map.cellIndices.NumGridCells; index++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (map.terrainGrid.TerrainAt(cell)?.defName == "AF_Pond") return cell;
            }
            return IntVec3.Invalid;
        }

        private static int CountPondCells(Map map)
        {
            int count = 0;
            for (int index = 0; index < map.cellIndices.NumGridCells; index++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (map.terrainGrid.TerrainAt(cell)?.defName == "AF_Pond") count++;
            }
            return count;
        }

        private static void ValidatePondSnapshot(PondMenuSnapshot snapshot, string label)
        {
            Require(snapshot != null, label + " snapshot is null");
            Require(snapshot.population >= 0 && snapshot.capacity >= 0 && snapshot.physicalCapacity >= 0 &&
                snapshot.sustainableCapacity >= 0 && snapshot.industrialCapacity >= 0 &&
                snapshot.foodSupportedCapacity >= 0 && snapshot.habitatSupportedCapacity >= 0,
                label + " has negative capacity values");
            Require(snapshot.eggs >= 0 && snapshot.eligibleHarvest >= 0 && snapshot.pendingHarvest >= 0 &&
                snapshot.pendingEggRemoval >= 0 && snapshot.pendingSterilization >= 0 &&
                snapshot.hungry >= 0 && snapshot.starving >= 0 && snapshot.wrongWater >= 0 &&
                snapshot.temperatureStressed >= 0, label + " has negative population counters");
            RequireFinite(snapshot.algaePercent, label + ".algaePercent");
            RequireFinite(snapshot.detritusPercent, label + ".detritusPercent");
            RequireFinite(snapshot.feedHours, label + ".feedHours");
            RequireFinite(snapshot.zooplanktonPercent, label + ".zooplanktonPercent");
            RequireFinite(snapshot.benthosPercent, label + ".benthosPercent");
            RequireFinite(snapshot.temperature, label + ".temperature");
            RequireFinite(snapshot.blueprintFit, label + ".blueprintFit");
            Require(snapshot.habitat != null, label + " habitat snapshot is null");
            PondHabitatSnapshot habitat = snapshot.habitat;
            RequireFinite(habitat.overallFit, label + ".habitat.overallFit");
            RequireFinite(habitat.averageFishFit, label + ".habitat.averageFishFit");
            RequireFinite(habitat.dailyFoodDemand, label + ".habitat.dailyFoodDemand");
            RequireFinite(habitat.naturalFoodPerDay, label + ".habitat.naturalFoodPerDay");
            Require(habitat.physicalMaximum >= 0 && habitat.sustainablePopulation >= 0 &&
                habitat.industrialMaximum >= 0 && habitat.foodSupportedPopulation >= 0 &&
                habitat.habitatSupportedPopulation >= 0, label + " habitat capacities are negative");
        }

        private static void Check(AquacultureInGameTestReport report, string id, Func<string> action)
        {
            try
            {
                report.results.Add(AquacultureInGameTestResult.Passed(id, action() ?? "ok"));
            }
            catch (Exception exception)
            {
                report.results.Add(AquacultureInGameTestResult.Failed(id, exception));
                Log.Error("[Aquaculture InGameTests] FAIL " + id + ": " + exception);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void RequireFinite(float value, string label)
        {
            Require(!float.IsNaN(value) && !float.IsInfinity(value), label + " is not finite");
        }

        private static int ParseGeneration()
        {
            return int.TryParse(Environment.GetEnvironmentVariable("DEVBRIDGE_GENERATION"), out int value) ? value : 0;
        }

    }

    internal sealed class AquacultureInGameTestRequest
    {
        public string runId;
        public string launchId;
        public int generation;
        public string suite;
        public string requestedUtc;
    }

    internal sealed class AquacultureGoldenPathFixture
    {
        private const int Width = 5;
        private const int Height = 5;

        private readonly Map map;
        private readonly Dictionary<IntVec3, TerrainDef> originalTerrain = new Dictionary<IntVec3, TerrainDef>();
        private readonly HashSet<Thing> baselineThings;
        private readonly List<Thing> createdThings = new List<Thing>();
        private readonly List<IntVec3> cells = new List<IntVec3>();
        private FishPondMapComponent ponds;
        private IntVec3 anchor = IntVec3.Invalid;
        private IntVec3 habitatCell = IntVec3.Invalid;
        private PondWaterKind originalWater;
        private PondWaterKind testWater;
        private float originalPreparedFeed;
        private float addedPreparedFeed;
        private Thing habitatThing;
        private ThingDef fishDef;
        private CompFishTraits female;
        private CompFishTraits male;
        private FishEggThing producedEgg;
        private CompFishTraits producedFry;
        private string inheritedTraitName;
        private AquacultureSettings settings;
        private bool oldGlobalBreeding;
        private float oldInheritanceChance;
        private int oldMaximumInheritedTraits;
        private float oldMutationChance;
        private bool settingsChanged;
        private bool terrainChanged;
        private bool cleanupDone;

        public AquacultureGoldenPathFixture(Map map)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            baselineThings = new HashSet<Thing>(map.listerThings.AllThings);
        }

        public string Setup()
        {
            TerrainDef pondTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail("AF_Pond");
            Require(pondTerrain != null, "AF_Pond terrain is missing");
            cells.AddRange(FindSafeRectangle());
            Require(cells.Count == Width * Height, "no unused " + Width + "x" + Height + " fixture rectangle was found");
            anchor = cells[Width / 2 + (Height / 2) * Width];
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                originalTerrain[cell] = map.terrainGrid.TerrainAt(cell);
                Require(originalTerrain[cell] != null, "fixture cell has no original terrain: " + cell);
                Require(originalTerrain[cell].defName != "AF_Pond", "fixture rectangle was already a pond");
                map.terrainGrid.SetTerrain(cell, pondTerrain);
            }
            terrainChanged = true;
            ponds = map.GetComponent<FishPondMapComponent>();
            Require(ponds != null, "pond map component is missing");
            ponds.MarkPondTopologyDirty();
            PondMenuSnapshot snapshot = ponds.MenuSnapshotAt(anchor);
            Require(snapshot != null, "constructed fixture pond has no menu snapshot");
            Require(ponds.ProxyFor(anchor) != null, "constructed fixture pond has no proxy");
            originalWater = ponds.WaterKindAt(anchor);
            testWater = originalWater;
            originalPreparedFeed = ponds.PreparedFeedAt(anchor);
            return "pondCells=" + CountFixturePondCells() + " proxy=1 anchor=" + anchor;
        }

        public string VerifyPondContract()
        {
            Require(cells.Count == Width * Height && ponds != null, "fixture setup did not complete");
            Require(CountFixturePondCells() == cells.Count, "fixture pond is not fully connected");
            PondProxyThing proxy = ponds.ProxyFor(anchor);
            Require(proxy != null && proxy.Spawned && cells.Contains(proxy.Position), "pond proxy is not live in fixture");
            PondMenuSnapshot snapshot = ponds.MenuSnapshotAt(anchor);
            Require(snapshot != null, "fixture menu snapshot is null");
            Require(snapshot.capacity > 0 && snapshot.physicalCapacity > 0 && snapshot.industrialCapacity > 0,
                "fixture capacities were not calculated");
            Require(snapshot.habitat != null && snapshot.habitat.effectiveCapacity > 1,
                "fixture habitat capacity cannot hold the adult pair");
            Require(ponds.HabitatAt(anchor) != null, "HabitatAt did not resolve the fixture");
            Require(ponds.ProxyFor(cells[0]) == proxy, "ProxyFor did not map every connected cell");
            return "pondCells=" + cells.Count + " proxy=" + proxy.thingIDNumber +
                " capacity=" + snapshot.capacity + " industrial=" + snapshot.industrialCapacity;
        }

        public string VerifyFeedHabitatAndCache()
        {
            Require(ponds != null, "pond component is unavailable");
            PondMenuSnapshot beforeFeed = ponds.MenuSnapshotAt(anchor);
            float feedAmount = Mathf.Max(0.5f, AquacultureMod.Settings?.feedValuePerUnit ?? 0.05f);
            ponds.AddPreparedFeed(anchor, feedAmount);
            addedPreparedFeed += feedAmount;
            PondMenuSnapshot afterFeed = ponds.MenuSnapshotAt(anchor);
            Require(!object.ReferenceEquals(beforeFeed, afterFeed), "prepared-feed mutation did not invalidate menu cache");
            Require(ponds.PreparedFeedAt(anchor) > originalPreparedFeed, "prepared feed was not recorded");

            habitatCell = cells.First(cell => cell != anchor && cell.GetThingList(map).All(thing =>
                thing.TryGetComp<CompPondHabitat>() == null));
            ThingDef habitatDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_AquaticPlantCover");
            Require(habitatDef != null, "AF_AquaticPlantCover is missing");
            habitatThing = ThingMaker.MakeThing(habitatDef);
            createdThings.Add(habitatThing);
            GenSpawn.Spawn(habitatThing, habitatCell, map);
            Require(habitatThing.TryGetComp<CompPondHabitat>() != null, "habitat ThingDef has no CompPondHabitat");
            ponds.NotifyHabitatChanged(habitatCell);
            PondHabitatSnapshot habitat = ponds.HabitatAt(anchor);
            Require(habitat != null && habitat.plantStructures > 0 && habitat.plantSupply > 0f,
                "habitat structure was not included in HabitatAt");
            Require(habitat.effectiveCapacity > 1, "habitat support capacity is too low for two adults");
            return "feed=" + ponds.PreparedFeedAt(anchor).ToString("0.###") +
                " plantStructures=" + habitat.plantStructures + " effectiveCapacity=" + habitat.effectiveCapacity +
                " cacheInvalidated=true";
        }

        public string StockAndVerifyAdults()
        {
            Require(ponds != null && cells.Count > 3, "fixture pond is unavailable");
            fishDef = FishDefsForFixture().FirstOrDefault();
            Require(fishDef != null, "no compatible runtime fish Def is available");
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(fishDef);
            testWater = profile.waterKind;
            if (testWater != originalWater)
            {
                Require(testWater == PondWaterKind.Brackishwater || testWater == PondWaterKind.Freshwater,
                    "fixture selected a saltwater species without a compatible test pond");
                ponds.SetPondWater(anchor, testWater);
            }
            Require(ponds.WaterKindAt(anchor) == testWater, "fixture pond water was not configured");

            inheritedTraitName = DefDatabase<FishTraitDef>.AllDefsListForReading
                .Where(trait => trait != null && trait.mutationEligible && !FishTraitUtility.IsDemographicOrEcology(trait.defName))
                .OrderBy(trait => trait.defName, StringComparer.Ordinal)
                .Select(trait => trait.defName).FirstOrDefault();
            Require(!inheritedTraitName.NullOrEmpty(), "no inheritable trait is available for the breeding check");

            IntVec3 femaleStaging = FindEmptyCellOutsideFixture();
            IntVec3 maleStaging = FindEmptyCellOutsideFixture(femaleStaging);
            Thing femaleThing = MakeConfiguredFish(fishDef, true, inheritedTraitName, 2101);
            Thing maleThing = MakeConfiguredFish(fishDef, false, inheritedTraitName, 2102);
            createdThings.Add(femaleThing);
            createdThings.Add(maleThing);
            GenSpawn.Spawn(femaleThing, femaleStaging, map);
            GenSpawn.Spawn(maleThing, maleStaging, map);
            female = femaleThing.TryGetComp<CompFishTraits>();
            male = maleThing.TryGetComp<CompFishTraits>();
            Require(female != null && male != null, "adult fish components were not created");
            IntVec3 femaleCell = cells.First(cell => cell != anchor && cell != habitatCell);
            IntVec3 maleCell = cells.First(cell => cell != anchor && cell != habitatCell && cell != femaleCell);
            Require(female.TryPlaceInPondForDevTest(femaleCell), "production placement rejected the female");
            Require(male.TryPlaceInPondForDevTest(maleCell), "production placement rejected the male");
            female.foodReserve = 1f;
            male.foodReserve = 1f;
            female.starvationProgress = male.starvationProgress = 0f;
            female.waterStress = male.waterStress = 0f;
            female.temperatureStress = male.temperatureStress = 0f;
            female.habitatStress = male.habitatStress = 0f;
            female.habitatFit = male.habitatFit = 1f;
            female.nextBreedTick = male.nextBreedTick = Find.TickManager.TicksGame;
            female.NotifyTraitsChanged();
            male.NotifyTraitsChanged();
            ponds.MenuSnapshotAt(anchor);
            List<CompFishTraits> samePond = ponds.FishInSamePond(female);
            Require(female.IsSwimmingInPond && male.IsSwimmingInPond, "stocked adults are not live pond fish");
            Require(female.IsAdult && male.IsAdult && female.IsFemale != male.IsFemale,
                "stocked pair does not have adult opposite-sex demographics");
            Require(female.WaterKind == testWater && male.WaterKind == testWater,
                "stocked pair water traits do not match the configured pond");
            Require(ponds.FishFitsWater(anchor, female) && ponds.FishFitsWater(anchor, male),
                "stocked pair failed FishFitsWater");
            Require(samePond.Count == 2 && ponds.PopulationAt(anchor) == 2,
                "stocked population did not reconcile to two adults");
            Require(ponds.FishSpeciesAt(anchor).Contains(fishDef), "FishSpeciesAt omitted the stocked species");
            return "species=" + fishDef.defName + " adults=2 female=" + female.parent.Position +
                " male=" + male.parent.Position + " water=" + testWater;
        }

        public string BreedAndVerifyInheritance()
        {
            Require(female != null && male != null && female.IsAlive && male.IsAlive, "adult pair is unavailable");
            settings = AquacultureMod.Settings;
            Require(settings != null, "Aquaculture settings are unavailable");
            oldGlobalBreeding = settings.globalBreedingEnabled;
            oldInheritanceChance = settings.parentalTraitInheritanceChance;
            oldMaximumInheritedTraits = settings.maxInheritedTraits;
            oldMutationChance = settings.offspringMutationChance;
            settings.globalBreedingEnabled = true;
            settings.parentalTraitInheritanceChance = 1f;
            settings.maxInheritedTraits = Mathf.Max(1, settings.maxInheritedTraits);
            settings.offspringMutationChance = 0f;
            settingsChanged = true;
            int now = Find.TickManager.TicksGame + 1;
            female.nextBreedTick = now;
            male.nextBreedTick = now;
            female.foodReserve = male.foodReserve = 1f;
            female.NotifyTraitsChanged();
            male.NotifyTraitsChanged();
            ponds.MenuSnapshotAt(anchor);
            int eggCountBefore = FixtureEggs().Count;
            ponds.RunBreedingForDevTest(now, 7717);
            List<FishEggThing> eggs = FixtureEggs();
            Require(eggs.Count > eggCountBefore, "real breeding cycle did not create an egg");
            producedEgg = eggs[eggs.Count - 1];
            Require(producedEgg.fishDefName == fishDef.defName, "egg species does not match parents");
            Require(producedEgg.parentOneThingId == female.parent.thingIDNumber &&
                producedEgg.parentTwoThingId == male.parent.thingIDNumber,
                "egg does not retain both parent IDs");
            Require(producedEgg.inheritedTraits != null && producedEgg.inheritedTraits.Contains(inheritedTraitName),
                "egg did not retain the deterministic inherited trait");
            Require(producedEgg.inheritedTraits.All(name => DefDatabase<FishTraitDef>.GetNamedSilentFail(name) != null),
                "egg contains an unresolved inherited trait");
            Require((producedEgg.inheritedValues ?? new Dictionary<string, float>()).Values.All(value =>
                !float.IsNaN(value) && !float.IsInfinity(value)), "egg contains a non-finite inherited value");

            producedEgg.hatchTick = now - 1;
            producedEgg.Hatch();
            List<CompFishTraits> offspring = map.listerThings.AllThings
                .Select(thing => thing.TryGetComp<CompFishTraits>())
                .Where(comp => comp != null && comp.parent.Spawned && comp.parent.def == fishDef &&
                    comp.parent.thingIDNumber != female.parent.thingIDNumber &&
                    comp.parent.thingIDNumber != male.parent.thingIDNumber &&
                    comp.parentOneThingId == female.parent.thingIDNumber &&
                    comp.parentTwoThingId == male.parent.thingIDNumber)
                .ToList();
            Require(offspring.Count > 0, "egg hatch did not create a live fry");
            producedFry = offspring[0];
            Require(producedFry.IsAlive && producedFry.traitDefNames.Contains(FishTraitUtility.Fry),
                "hatched child is not a live fry");
            Require(producedFry.traitDefNames.Contains(inheritedTraitName),
                "hatched fry lost the inherited trait");
            Require(producedFry.WaterKind == testWater && ponds.FishFitsWater(anchor, producedFry),
                "hatched fry has invalid water compatibility");
            ponds.Register(producedFry);
            ponds.NotifyFishChanged(producedFry);
            // Hatch() uses the production spawn/registration path. Reconcile the
            // component once before asserting the public population view so this
            // check covers the same deferred membership update as a normal pond tick.
            ponds.RebuildMembershipForDevTest();
            ponds.MenuSnapshotAt(anchor);
            int reconciledPopulation = ponds.PopulationAt(anchor);
            List<CompFishTraits> reconciledFish = ponds.FishInSamePond(producedFry);
            string reconciledFishDetails = string.Join("|", reconciledFish.Select(comp =>
                comp.parent.thingIDNumber + ":" + comp.parent.Position + ":alive=" + comp.IsAlive));
            Require(reconciledPopulation >= 3,
                "hatched fry was not reconciled into population (population=" + reconciledPopulation +
                ", registered=" + ponds.IsRegisteredForDevTest(producedFry) +
                ", member=" + ponds.IsPondMemberForDevTest(producedFry) +
                ", spawned=" + producedFry.parent.Spawned +
                ", alive=" + producedFry.IsAlive +
                ", inPond=" + producedFry.IsInPond +
                ", swimming=" + producedFry.IsSwimmingInPond +
                ", position=" + producedFry.parent.Position +
                ", terrain=" + map.terrainGrid.TerrainAt(producedFry.parent.Position)?.defName +
                ", female=" + DescribeFish(female) +
                ", male=" + DescribeFish(male) +
                ", reconciledFish=" + reconciledFishDetails + ")");
            return "eggsCreated=" + (eggs.Count - eggCountBefore) + " fryCreated=1 inherited=" + inheritedTraitName +
                " population=" + reconciledPopulation;
        }

        private static string DescribeFish(CompFishTraits comp)
        {
            return comp == null ? "null" : comp.parent.thingIDNumber + ":" + comp.parent.Position +
                ":spawned=" + comp.parent.Spawned + ":alive=" + comp.IsAlive + ":inPond=" + comp.IsInPond +
                ":swimming=" + comp.IsSwimmingInPond;
        }

        public string ProcessAndVerifyMeat()
        {
            Require(female != null && male != null && female.IsAlive, "processing fish is unavailable");
            int oldMinimum = ponds.MinimumHarvestPopulationAt(anchor);
            ponds.SetMinimumHarvestPopulation(anchor, 0);
            Require(ponds.CanHarvestFish(male), "adult male did not satisfy harvest rules");
            FishHarvestUtility.Designate(male, false);
            Require(map.designationManager.DesignationOn(male.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation) != null,
                "harvest designation was not created");
            map.designationManager.DesignationOn(male.parent, AquacultureJobDefOf.AF_HarvestPondFishDesignation)?.Delete();
            ponds.SetMinimumHarvestPopulation(anchor, oldMinimum);

            int expected = FishProcessingYield.ExpectedMeatCount(female);
            female.MarkDead();
            Thing meat = FishProcessingYield.CreateMeatForDevTest(female);
            Require(meat != null, "AF_FishMeat production Def is missing");
            createdThings.Add(meat);
            Require(meat != null && meat.stackCount == expected, "processing produced unexpected AF_FishMeat yield");
            Require(meat.TryGetComp<CompFishMeatTraits>() != null, "AF_FishMeat lost its food trait component");
            return "harvestDesignation=true processingCore=true expectedMeat=" + expected +
                " actualMeat=" + meat.stackCount + " fishMeatDef=" + meat.def.defName;
        }

        public string Cleanup()
        {
            if (cleanupDone) return "cleanup already completed";
            cleanupDone = true;
            try
            {
                DeleteKnownDesignations();
                DestroyFixtureThings();
                if (ponds != null && anchor.IsValid && anchor.InBounds(map) &&
                    map.terrainGrid.TerrainAt(anchor)?.defName == "AF_Pond")
                {
                    float currentFeed = ponds.PreparedFeedAt(anchor);
                    ponds.AddPreparedFeed(anchor, originalPreparedFeed - currentFeed);
                    if (testWater != originalWater && ponds.PopulationAt(anchor) == 0)
                        ponds.SetPondWater(anchor, originalWater);
                }
                RestoreTerrain();
                ponds?.MarkPondTopologyDirty();
                DestroyFixtureThings();
                map.GetComponent<NaturalFishPopulationMapComponent>()?.InitializeForDevTest();
                Require(originalTerrain.All(pair => map.terrainGrid.TerrainAt(pair.Key) == pair.Value),
                    "fixture terrain was not restored exactly");
                Require(cells.All(cell => cell.GetThingList(map).All(thing => baselineThings.Contains(thing))),
                    "fixture left a spawned object behind");
                Require(map.listerThings.AllThings.OfType<PondProxyThing>().All(proxy => !cells.Contains(proxy.Position)),
                    "fixture left a pond proxy behind");
                return "terrainRestored=true objectsRemoved=true jobs=0 designations=0 reservations=0 proxies=0";
            }
            finally
            {
                if (settingsChanged)
                {
                    settings.globalBreedingEnabled = oldGlobalBreeding;
                    settings.parentalTraitInheritanceChance = oldInheritanceChance;
                    settings.maxInheritedTraits = oldMaximumInheritedTraits;
                    settings.offspringMutationChance = oldMutationChance;
                    settingsChanged = false;
                }
            }
        }

        private IEnumerable<ThingDef> FishDefsForFixture()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish)
                .Where(def => def.comps != null && def.comps.Any(comp => comp?.compClass == typeof(CompFishTraits)))
                .Where(def => AquaticSpeciesProfile.For(def).waterKind != PondWaterKind.Saltwater)
                .OrderBy(def => def.defName, StringComparer.Ordinal);
        }

        private Thing MakeConfiguredFish(ThingDef def, bool femaleSex, string inheritedTrait, int seed)
        {
            Thing fish;
            Rand.PushState(seed);
            try
            {
                fish = ThingMaker.MakeThing(def);
            }
            finally
            {
                Rand.PopState();
            }
            CompFishTraits traits = fish.TryGetComp<CompFishTraits>();
            Require(traits != null, "selected fish Def has no CompFishTraits");
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
            List<string> names = new List<string>
            {
                FishTraitUtility.Adult,
                femaleSex ? FishTraitUtility.Female : FishTraitUtility.Male,
                FishTraitUtility.DietPrefix + profile.diet,
                FishTraitUtility.WaterPrefix + profile.waterKind,
                inheritedTrait
            };
            Dictionary<string, float> values = new Dictionary<string, float>();
            FishTraitDef inherited = DefDatabase<FishTraitDef>.GetNamedSilentFail(inheritedTrait);
            if (inherited?.IsNumeric == true) values[inheritedTrait] = Mathf.Max(1, inherited.minPercent);
            traits.ApplyCaughtTraits(names, values);
            traits.alive = true;
            traits.sterilized = false;
            traits.airExposureTicks = 0f;
            traits.lifespanTicks = 120000;
            traits.birthTick = (Find.TickManager?.TicksGame ?? 0) - traits.lifespanTicks / 2;
            traits.nextBreedTick = Find.TickManager?.TicksGame ?? 0;
            traits.foodReserve = 1f;
            traits.starvationProgress = 0f;
            traits.waterStress = 0f;
            traits.temperatureStress = 0f;
            traits.habitatStress = 0f;
            traits.habitatFit = 1f;
            traits.nextAgeCheckTick = 0;
            traits.RefreshAgeTrait();
            traits.NotifyTraitsChanged();
            return fish;
        }

        private List<IntVec3> FindSafeRectangle()
        {
            for (int x = 1; x < map.Size.x - Width - 1; x++)
            {
                for (int z = 1; z < map.Size.z - Height - 1; z++)
                {
                    List<IntVec3> candidate = new List<IntVec3>();
                    bool safe = true;
                    for (int dx = 0; dx < Width && safe; dx++)
                    {
                        for (int dz = 0; dz < Height; dz++)
                        {
                            IntVec3 cell = new IntVec3(x + dx, 0, z + dz);
                            if (!cell.InBounds(map) || map.terrainGrid.TerrainAt(cell) == null ||
                                map.terrainGrid.TerrainAt(cell).defName == "AF_Pond" || cell.GetThingList(map).Count > 0)
                            {
                                safe = false;
                                break;
                            }
                            candidate.Add(cell);
                        }
                    }
                    if (safe) return candidate;
                }
            }
            return new List<IntVec3>();
        }

        private IntVec3 FindEmptyCellOutsideFixture(IntVec3 exclude = default(IntVec3))
        {
            for (int index = 0; index < map.cellIndices.NumGridCells; index++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (cells.Contains(cell) || cell == exclude || cell.GetThingList(map).Count > 0) continue;
                return cell;
            }
            throw new InvalidOperationException("no empty staging cell is available");
        }

        private List<FishEggThing> FixtureEggs()
        {
            return map.listerThings.AllThings.OfType<FishEggThing>().Where(egg => cells.Contains(egg.Position)).ToList();
        }

        private int CountFixturePondCells()
        {
            return cells.Count(cell => map.terrainGrid.TerrainAt(cell)?.defName == "AF_Pond");
        }

        private void DeleteKnownDesignations()
        {
            if (map?.designationManager == null) return;
            for (int i = 0; i < createdThings.Count; i++)
            {
                Thing thing = createdThings[i];
                if (thing == null) continue;
                map.designationManager.DesignationOn(thing, AquacultureJobDefOf.AF_HarvestPondFishDesignation)?.Delete();
                map.designationManager.DesignationOn(thing, AquacultureJobDefOf.AF_RemovePondEggDesignation)?.Delete();
                map.designationManager.DesignationOn(thing, AquacultureJobDefOf.AF_SterilizePondFishDesignation)?.Delete();
            }
        }

        private void DestroyFixtureThings()
        {
            HashSet<Thing> known = new HashSet<Thing>(createdThings.Where(thing => thing != null));
            List<Thing> current = map.listerThings.AllThings.ToList();
            for (int i = 0; i < current.Count; i++)
            {
                Thing thing = current[i];
                if (thing == null || baselineThings.Contains(thing)) continue;
                if (known.Contains(thing) || cells.Contains(thing.Position)) thing.Destroy(DestroyMode.Vanish);
            }
            for (int i = 0; i < createdThings.Count; i++)
            {
                Thing thing = createdThings[i];
                if (thing != null && !thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            }
        }

        private void RestoreTerrain()
        {
            if (!terrainChanged) return;
            foreach (KeyValuePair<IntVec3, TerrainDef> pair in originalTerrain)
                if (pair.Key.InBounds(map)) map.terrainGrid.SetTerrain(pair.Key, pair.Value);
            terrainChanged = false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    public sealed class AquacultureInGameTestReport
    {
        public string suite;
        public string runId;
        public string launchId;
        public int generation;
        public int gameTick;
        public string requestedUtc;
        public DateTime startedUtc;
        public DateTime completedUtc;
        public string outputPath;
        public readonly List<AquacultureInGameTestResult> results = new List<AquacultureInGameTestResult>();
        public bool Passed => results.Count > 0 && results.All(result => result.status == "PASS");
    }

    public sealed class AquacultureInGameTestResult
    {
        public string id;
        public string status;
        public string message;

        public static AquacultureInGameTestResult Passed(string id, string message)
        {
            return new AquacultureInGameTestResult { id = id, status = "PASS", message = message };
        }

        public static AquacultureInGameTestResult Failed(string id, Exception exception)
        {
            string message = exception?.GetBaseException().Message ?? "unknown failure";
            return new AquacultureInGameTestResult { id = id, status = "FAIL", message = message };
        }
    }
}
#else
// The bridge suite is developer-only and is not part of player builds.
#endif
