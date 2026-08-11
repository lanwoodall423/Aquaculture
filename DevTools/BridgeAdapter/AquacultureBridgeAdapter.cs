using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public static class AquacultureBridgeAdapter
    {
        public static string[] BridgeCommandSpecs() => new[]
        {
            "AQUACULTURE|R|Compact Aquaculture map and ecosystem summary",
            "AQUA_PONDS|R|List connected ponds and cached health summaries",
            "AQUA_POND|R|Inspect a pond by proxy thing ID or x,z cell",
            "AQUA_HABITAT|R|Inspect habitat supply, demand, fit, and stress for a pond",
            "AQUA_BLUEPRINT|R|Inspect a pond living blueprint by proxy thing ID or x,z cell",
            "AQUA_FISH|R|Inspect a fish by thing ID",
            "AQUA_SPECIES|R|Aggregate loaded map fish by species",
            "AQUA_CATALOG|R|Summarize all loaded fish definitions and ecological roles",
            "AQUA_JOURNAL|R|Summarize species milestones and registered fish breeds",
            "AQUA_OPPORTUNITIES|R|Analyze loaded content for high-value Aquaculture feature opportunities",
            "AQUA_SETTINGS|R|List active simulation and visual settings",
            "AQUA_DEFERRED_REALITY|R|Report Deferred Reality provider, ownership, process, migration, and exactly-once diagnostics",
            "AQUA_ADAPTER_STATUS|R|Report Aquaculture hot adapter identity and capabilities",
            "AQUA_PERFORMANCE|R|Report scheduler scale and cached workload",
            "AQUA_VALIDATE|R|Run read-only Aquaculture state invariants",
            "AQUA_OPEN_PLANNER|W|Open the stocking planner for a pond ID or x,z cell"
        };

        public static List<string> ExecuteBridgeCommand(string command, string argument, Map map)
        {
            switch ((command ?? string.Empty).ToUpperInvariant())
            {
                case "AQUACULTURE": return Summary(map);
                case "AQUA_PONDS": return Ponds(map);
                case "AQUA_POND": return Pond(map, argument);
                case "AQUA_HABITAT": return Habitat(map, argument);
                case "AQUA_BLUEPRINT": return Blueprint(map, argument);
                case "AQUA_FISH": return Fish(map, argument);
                case "AQUA_SPECIES": return Species(map, argument);
                case "AQUA_CATALOG": return Catalog(argument);
                case "AQUA_JOURNAL": return Journal();
                case "AQUA_OPPORTUNITIES": return Opportunities();
                case "AQUA_SETTINGS": return Settings();
                case "AQUA_DEFERRED_REALITY": return DeferredReality();
                case "AQUA_ADAPTER_STATUS": return AdapterStatus();
                case "AQUA_PERFORMANCE": return Performance(map);
                case "AQUA_VALIDATE": return Validate(map);
                case "AQUA_OPEN_PLANNER": return OpenPlanner(map, argument);
                default: return null;
            }
        }

        private static List<string> Summary(Map map)
        {
            if (map == null) return NoMap();
            Stopwatch watch = Stopwatch.StartNew();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            List<CompFishTraits> fish = FishOnMap(map);
            List<PondProxyThing> ponds = PondProxies(map);
            int alive = fish.Count(item => item.IsAlive);
            int pondFish = fish.Count(item => item.IsSwimmingInPond);
            int hungry = 0;
            int starving = 0;
            int wrongWater = 0;
            int temperature = 0;
            int eggs = 0;
            int warnings = 0;
            int schools = 0;
            for (int i = 0; i < ponds.Count; i++)
            {
                PondMenuSnapshot snapshot = component?.MenuSnapshotAt(ponds[i].Position);
                if (snapshot == null) continue;
                hungry += snapshot.hungry;
                starving += snapshot.starving;
                wrongWater += snapshot.wrongWater;
                temperature += snapshot.temperatureStressed;
                eggs += snapshot.eggs;
                warnings += snapshot.warnings.Count;
                schools += snapshot.schools.Count;
            }
            watch.Stop();
            var result = new List<string>
            {
                "map=" + map.uniqueID + " tick=" + (Find.TickManager?.TicksGame ?? 0),
                "ponds=" + ponds.Count + " fish=" + fish.Count + " alive=" + alive + " inPond=" + pondFish + " eggs=" + eggs + " schools=" + schools,
                "health=hungry:" + hungry + " starving:" + starving + " wrongWater:" + wrongWater + " temperature:" + temperature + " warnings:" + warnings,
                "progression=pondkeeping:" + Available("AF_Pondkeeping") + " managed:" + Available("AF_ManagedAquaculture") +
                    " industrial:" + Available("AF_IndustrialAquaculture") + " breeding:" + Available("AF_SelectiveBreeding"),
                "queryMs=" + watch.Elapsed.TotalMilliseconds.ToString("0.00")
            };
            return result;
        }

        private static List<string> DeferredReality()
        {
            var result = new List<string>();
            try
            {
                Type providerType = Type.GetType(
                    "DeferredReality.Aquaculture.AquacultureRealityProvider, DeferredReality.Aquaculture", false);
                if (providerType == null)
                {
                    result.Add("provider=lan.aquaculture.natural-water available=false reason=assembly-not-loaded");
                    return result;
                }

                PropertyInfo currentProperty = providerType.GetProperty("Current",
                    BindingFlags.Public | BindingFlags.Static);
                object provider = currentProperty?.GetValue(null, null);
                if (provider == null)
                {
                    result.Add("provider=lan.aquaculture.natural-water available=false reason=not-registered");
                    return result;
                }

                MethodInfo diagnostics = providerType.GetMethod("BridgeDiagnostics",
                    BindingFlags.Public | BindingFlags.Instance);
                if (diagnostics == null)
                {
                    result.Add("provider=lan.aquaculture.natural-water available=false reason=diagnostics-unavailable");
                    return result;
                }

                if (diagnostics.Invoke(provider, null) is IEnumerable lines)
                {
                    foreach (object line in lines)
                    {
                        if (line != null) result.Add(line.ToString());
                    }
                }
                if (result.Count == 0) result.Add("provider=lan.aquaculture.natural-water available=false reason=empty-diagnostics");
            }
            catch (Exception ex)
            {
                result.Clear();
                result.Add("provider=lan.aquaculture.natural-water available=false reason=reflection-failure");
                result.Add("error=" + ex.GetType().Name);
            }
            return result;
        }

        private static List<string> Ponds(Map map)
        {
            if (map == null) return NoMap();
            Stopwatch watch = Stopwatch.StartNew();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            List<PondProxyThing> ponds = PondProxies(map);
            var result = new List<string> { "ponds=" + ponds.Count };
            for (int i = 0; i < ponds.Count; i++)
            {
                PondProxyThing proxy = ponds[i];
                PondMenuSnapshot snapshot = component?.MenuSnapshotAt(proxy.Position);
                if (snapshot == null)
                {
                    result.Add("pond=id:" + proxy.thingIDNumber + " cell:" + Cell(proxy.Position) + " snapshot:missing");
                    continue;
                }
                result.Add("pond=id:" + proxy.thingIDNumber + " cell:" + Cell(proxy.Position) +
                    " water:" + component.WaterKindAt(proxy.Position) + " fish:" + snapshot.population +
                    " physical:" + snapshot.physicalCapacity + " sustainable:" + snapshot.sustainableCapacity +
                    " industrial:" + snapshot.industrialCapacity + " effective:" + snapshot.capacity +
                    " eggs:" + snapshot.eggs + " schools:" + snapshot.schools.Count + " algae:" + snapshot.algaePercent.ToStringPercent() +
                    "% feed:" + snapshot.feedHours.ToString("0.0") + "h warnings:" + snapshot.warnings.Count);
            }
            watch.Stop();
            result.Add("queryMs=" + watch.Elapsed.TotalMilliseconds.ToString("0.00"));
            return result;
        }

        private static List<string> Pond(Map map, string argument)
        {
            if (map == null) return NoMap();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            PondProxyThing proxy = FindPond(map, component, argument);
            if (proxy == null) return new List<string> { "error=pond not found", "usage=AQUA_POND proxyThingId|x,z" };
            PondMenuSnapshot snapshot = component?.MenuSnapshotAt(proxy.Position);
            if (snapshot == null) return new List<string> { "error=pond snapshot unavailable", "pond=id:" + proxy.thingIDNumber };
            var result = new List<string>
            {
                "pond=id:" + proxy.thingIDNumber + " cell:" + Cell(proxy.Position) + " water:" + component.WaterKindAt(proxy.Position),
                "population=" + snapshot.population + " physical=" + snapshot.physicalCapacity +
                    " sustainable=" + snapshot.sustainableCapacity + " industrial=" + snapshot.industrialCapacity +
                    " effective=" + snapshot.capacity + " eggs=" + snapshot.eggs + " harvestable=" + snapshot.eligibleHarvest +
                    " schools=" + snapshot.schools.Count + " beauty=" + component.PondBeautyAt(proxy.Position).ToString("0.0"),
                "ecology=algae:" + snapshot.algaePercent.ToStringPercent() + " detritus:" + snapshot.detritusPercent.ToStringPercent() +
                    "% feed:" + snapshot.feedHours.ToString("0.0") + "h organisms:" + snapshot.organisms.Count +
                    " temperature:" + snapshot.temperature.ToString("0.0") + "C",
                "health=hungry:" + snapshot.hungry + " starving:" + snapshot.starving + " wrongWater:" + snapshot.wrongWater +
                    " temperature:" + snapshot.temperatureStressed,
                "work=harvest:" + snapshot.pendingHarvest + " eggs:" + snapshot.pendingEggRemoval + " sterilize:" + snapshot.pendingSterilization,
                "policy=breeding:" + component.BreedingModeAt(proxy.Position) + " minimumStock:" + component.MinimumHarvestPopulationAt(proxy.Position) +
                    " populationGoal:" + component.ManagementPopulationTargetAt(proxy.Position) + " autoHarvest:" + component.AutomaticSurplusHarvestAt(proxy.Position) +
                    " autoFeed:" + component.AutomaticFeedingAt(proxy.Position),
                "feeder=" + Clean(snapshot.feederStatus)
            };
            PondHabitatSnapshot habitat = component.HabitatAt(proxy.Position);
            if (habitat != null)
                result.Add("habitat=fit:" + habitat.averageFishFit.ToStringPercent() +
                    " supplyFit:" + habitat.overallFit.ToStringPercent() +
                    " stressed:" + habitat.stressedFish + " activeAerators:" + habitat.activeAerators);
            if (snapshot.warnings.Count == 0) result.Add("warnings=none");
            else
                for (int i = 0; i < snapshot.warnings.Count; i++) result.Add("warning=" + Clean(snapshot.warnings[i]));
            for (int i = 0; i < snapshot.schools.Count; i++)
            {
                FishSchoolSnapshot school = snapshot.schools[i];
                result.Add("school=" + Clean(school.title) + " " + Clean(school.leftStats) + " " + Clean(school.rightStats));
            }
            for (int i = 0; i < snapshot.organisms.Count; i++)
            {
                PondOrganismSnapshot organism = snapshot.organisms[i];
                result.Add("organism=" + Clean(organism.label) + " role:" + organism.role +
                    " biomass:" + organism.biomass.ToString("0.000") + "/" + organism.capacity.ToString("0.000") +
                    " fill:" + organism.percent.ToStringPercent());
            }
            return result;
        }

        private static List<string> Habitat(Map map, string argument)
        {
            if (map == null) return NoMap();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            PondProxyThing proxy = FindPond(map, component, argument);
            if (proxy == null) return new List<string> { "error=pond not found", "usage=AQUA_HABITAT proxyThingId|x,z" };
            PondHabitatSnapshot habitat = component?.HabitatAt(proxy.Position);
            if (habitat == null) return new List<string> { "error=habitat snapshot unavailable", "pond=id:" + proxy.thingIDNumber };
            var result = new List<string>
            {
                "pond=id:" + proxy.thingIDNumber + " cell:" + Cell(proxy.Position),
                    "path=" + Clean(habitat.developmentPath) + " capacity:physical:" + habitat.physicalMaximum +
                    " sustainable:" + habitat.sustainablePopulation + " industrial:" + habitat.industrialMaximum +
                    " effective:" + habitat.effectiveCapacity + " constraint:" + habitat.limitingConstraint,
                "fit=fish:" + habitat.averageFishFit.ToStringPercent() + " supply:" + habitat.overallFit.ToStringPercent() +
                    " stressed:" + habitat.stressedFish + " beauty:" + habitat.beauty.ToString("0.0"),
                HabitatLine("plant", habitat.plantStructures, habitat.plantSupply, habitat.plantDemand, habitat.PlantFit),
                HabitatLine("shelter", habitat.shelterStructures, habitat.shelterSupply, habitat.shelterDemand, habitat.ShelterFit),
                HabitatLine("substrate", habitat.substrateStructures, habitat.substrateSupply, habitat.substrateDemand, habitat.SubstrateFit),
                HabitatLine("current", habitat.activeAerators, habitat.currentSupply, habitat.currentDemand, habitat.CurrentFit) +
                    " installedAerators:" + habitat.aerators,
                HabitatLine("openWater", 0, habitat.openWaterSupply, habitat.openWaterDemand, habitat.OpenWaterFit)
            };
            PondMenuSnapshot snapshot = component.MenuSnapshotAt(proxy.Position);
            if (snapshot != null)
                for (int i = 0; i < snapshot.organisms.Count; i++)
                {
                    PondOrganismSnapshot organism = snapshot.organisms[i];
                    result.Add("organism=" + Clean(organism.label) + " role:" + organism.role +
                        " biomass:" + organism.biomass.ToString("0.000") + "/" + organism.capacity.ToString("0.000") +
                        " fill:" + organism.percent.ToStringPercent());
                }
            return result;
        }

        private static string HabitatLine(string kind, int structures, float supply, float demand, float fit) =>
            "habitat=" + kind + " structures:" + structures + " supply:" + supply.ToString("0.0") +
            " demand:" + demand.ToString("0.0") + " fit:" + fit.ToStringPercent();

        private static List<string> Fish(Map map, string argument)
        {
            if (map == null) return NoMap();
            if (!int.TryParse(argument, out int id)) return new List<string> { "error=invalid fish thing ID", "usage=AQUA_FISH thingId" };
            CompFishTraits fish = FishOnMap(map).FirstOrDefault(item => item.parent.thingIDNumber == id);
            if (fish == null) return new List<string> { "error=fish not found", "thingId=" + id };
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(fish.parent.def);
            int now = Find.TickManager?.TicksGame ?? 0;
            int remaining = Mathf.Max(0, fish.lifespanTicks - (now - fish.birthTick));
            return new List<string>
            {
                "fish=id:" + id + " def:" + fish.parent.def.defName + " label:" + Clean(fish.parent.LabelCap),
                "state=alive:" + fish.IsAlive + " pond:" + fish.IsInPond + " cell:" + Cell(fish.parent.Position) + " sterilized:" + fish.sterilized,
                "traits=" + Clean(fish.TraitSummary),
                "ecology=diet:" + fish.Diet + " water:" + fish.WaterKind + " food:" + fish.foodReserve.ToStringPercent() +
                    " starvation:" + fish.starvationProgress.ToStringPercent() + " waterStress:" + fish.waterStress.ToStringPercent() +
                    " temperatureStress:" + fish.temperatureStress.ToStringPercent() +
                    " habitatFit:" + fish.habitatFit.ToStringPercent() + " habitatStress:" + fish.habitatStress.ToStringPercent(),
                "biology=lifespanRemaining:" + remaining.ToStringTicksToPeriod() + " size:" + fish.SizeFactor.ToString("0.00") +
                    " speed:" + fish.MovementSpeed.ToString("0.00") + " meat:" + fish.MeatYield.ToString("0.00") +
                    " beauty:" + fish.BeautyOffset.ToString("0.0"),
                "species=temp:" + profile.minimumTemperature.ToString("0.#") + ".." + profile.maximumTemperature.ToString("0.#") +
                    "C hourlyDemand:" + profile.hourlyDemand.ToString("0.0000") + " offspring:" + profile.offspringFactor.ToString("0.00") +
                    " breeding:" + profile.breedingIntervalFactor.ToString("0.00")
            };
        }

        private static List<string> Blueprint(Map map, string argument)
        {
            if (map == null) return NoMap();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            PondProxyThing proxy = FindPond(map, component, argument);
            if (proxy == null) return new List<string> { "error=pond not found", "usage=AQUA_BLUEPRINT proxyThingId|x,z" };
            PondMenuSnapshot snapshot = component?.MenuSnapshotAt(proxy.Position);
            if (snapshot == null) return new List<string> { "error=pond snapshot unavailable" };
            Dictionary<ThingDef, int> targets = null;
            try
            {
                object value = component.GetType().GetMethod("StockingBlueprintAt")?.Invoke(component, new object[] { proxy.Position });
                targets = value as Dictionary<ThingDef, int>;
            }
            catch { }
            if (targets == null || targets.Count == 0)
                return new List<string> { "pond=id:" + proxy.thingIDNumber, "blueprint=none" };
            Dictionary<ThingDef, int> actual = snapshot.fish.Where(entry => entry.fish?.parent?.def != null)
                .GroupBy(entry => entry.fish.parent.def).ToDictionary(group => group.Key, group => group.Count());
            var result = new List<string>
            {
                "pond=id:" + proxy.thingIDNumber + " cell:" + Cell(proxy.Position),
                "blueprint=species:" + targets.Count + " target:" + targets.Values.Sum() + " actual:" + snapshot.population
            };
            foreach (KeyValuePair<ThingDef, int> pair in targets.OrderBy(pair => pair.Key.label))
            {
                int count = actual.TryGetValue(pair.Key, out int value) ? value : 0;
                result.Add("target=" + pair.Key.defName + " desired:" + pair.Value + " actual:" + count +
                    " delta:" + (count - pair.Value));
            }
            foreach (KeyValuePair<ThingDef, int> pair in actual.Where(pair => !targets.ContainsKey(pair.Key)).OrderBy(pair => pair.Key.label))
                result.Add("surplus=" + pair.Key.defName + " actual:" + pair.Value + " desired:0");
            return result;
        }

        private static List<string> Species(Map map, string argument)
        {
            if (map == null) return NoMap();
            string filter = (argument ?? string.Empty).Trim();
            IEnumerable<IGrouping<ThingDef, CompFishTraits>> groups = FishOnMap(map)
                .Where(fish => filter.NullOrEmpty() || fish.parent.def.defName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fish.parent.def.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(fish => fish.parent.def)
                .OrderBy(group => group.Key.label);
            var result = new List<string>();
            foreach (IGrouping<ThingDef, CompFishTraits> group in groups)
            {
                AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(group.Key);
                result.Add("species=" + group.Key.defName + " label:" + Clean(group.Key.LabelCap) + " count:" + group.Count() +
                    " pond:" + group.Count(fish => fish.IsSwimmingInPond) + " diet:" + profile.diet + " water:" + profile.waterKind +
                    " temp:" + profile.minimumTemperature.ToString("0.#") + ".." + profile.maximumTemperature.ToString("0.#") +
                    "C meat:" + profile.meatYieldFactor.ToString("0.00") + " breeding:" + profile.breedingIntervalFactor.ToString("0.00"));
            }
            result.Insert(0, "species=" + result.Count);
            return result;
        }

        private static List<string> Catalog(string argument)
        {
            string filter = (argument ?? string.Empty).Trim();
            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(FishUtility.IsFish)
                .Where(def => filter.NullOrEmpty() || def.defName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    def.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(def => def.label)
                .ThenBy(def => def.defName)
                .ToList();
            var result = new List<string> { "catalog=" + defs.Count };
            for (int i = 0; i < defs.Count && i < 100; i++)
            {
                ThingDef def = defs[i];
                AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
                result.Add("fish=" + def.defName + " label:" + Clean(def.LabelCap) + " diet:" + profile.diet +
                    " water:" + profile.waterKind + " temp:" + profile.minimumTemperature.ToString("0.#") + ".." +
                    profile.maximumTemperature.ToString("0.#") + "C demand:" + profile.hourlyDemand.ToString("0.0000") +
                    " breed:" + profile.breedingIntervalFactor.ToString("0.00") + " offspring:" + profile.offspringFactor.ToString("0.00") +
                    " meat:" + profile.meatYieldFactor.ToString("0.00") + " beauty:" + profile.beautyOffset.ToString("0.0"));
            }
            if (defs.Count > 100) result.Add("truncated=" + (defs.Count - 100));
            return result;
        }

        private static List<string> Opportunities()
        {
            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish).ToList();
            var diets = defs.GroupBy(def => AquaticSpeciesProfile.For(def).diet)
                .OrderBy(group => group.Key.ToString())
                .Select(group => group.Key + ":" + group.Count());
            var waters = defs.GroupBy(def => AquaticSpeciesProfile.For(def).waterKind)
                .OrderBy(group => group.Key.ToString())
                .Select(group => group.Key + ":" + group.Count());
            int cold = defs.Count(def => AquaticSpeciesProfile.For(def).maximumTemperature <= 24f);
            int warm = defs.Count(def => AquaticSpeciesProfile.For(def).minimumTemperature >= 10f);
            int ornamental = defs.Count(def => AquaticSpeciesProfile.For(def).beautyOffset > 0f);
            int fastBreeders = defs.Count(def => AquaticSpeciesProfile.For(def).breedingIntervalFactor < 0.9f);
            int predators = defs.Count(def => AquaticSpeciesProfile.For(def).diet == FishDiet.Carnivore);
            int prey = defs.Count(def => AquaticSpeciesProfile.For(def).diet != FishDiet.Carnivore);
            int traits = DefDatabase<FishTraitDef>.AllDefsListForReading.Count;
            return new List<string>
            {
                "loaded=fish:" + defs.Count + " traits:" + traits,
                "diet=" + string.Join(" ", diets),
                "water=" + string.Join(" ", waters),
                "roles=cold:" + cold + " warm:" + warm + " ornamental:" + ornamental +
                    " fastBreeders:" + fastBreeders + " predators:" + predators + " prey:" + prey,
                "opportunity=Turn Stocking Planner plans into persistent pond blueprints with visible deficits and surplus.",
                "opportunity=Use existing handler jobs and containers to fulfill blueprints without new per-fish ticking.",
                "opportunity=Grade ecosystem fit from cached ecology: compatibility, food web, breeding resilience, and diversity.",
                "opportunity=Unlock manual blueprint guidance at Managed and automatic stewardship at Industrial.",
                "opportunity=Extend Selective Breeding blueprints with sex, age, and trait targets."
            };
        }

        private static List<string> Journal()
        {
            Type journalType = typeof(CompFishTraits).Assembly.GetType("AquacultureFishing.AquacultureJournalComponent");
            if (journalType == null) return new List<string> { "error=journal gameplay update is not loaded" };
            MethodInfo getComponent = typeof(Game).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name == "GetComponent" && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 0);
            object journal = getComponent?.MakeGenericMethod(journalType).Invoke(Current.Game, null);
            if (journal == null) return new List<string> { "error=journal component unavailable" };
            IEnumerable species = journalType.GetProperty("SpeciesRecords")?.GetValue(journal) as IEnumerable;
            IEnumerable breeds = journalType.GetProperty("Breeds")?.GetValue(journal) as IEnumerable;
            var result = new List<string>();
            int discovered = 0;
            int established = 0;
            int bred = 0;
            int stable = 0;
            if (species != null)
            {
                foreach (object record in species)
                {
                    string defName = Field<string>(record, "fishDefName");
                    int discoveredTick = Field<int>(record, "discoveredTick");
                    int establishedTick = Field<int>(record, "establishedTick");
                    int bredTick = Field<int>(record, "bredTick");
                    int stableTick = Field<int>(record, "stableTick");
                    if (discoveredTick >= 0) discovered++;
                    if (establishedTick >= 0) established++;
                    if (bredTick >= 0) bred++;
                    if (stableTick >= 0) stable++;
                    result.Add("species=" + defName + " discovered:" + (discoveredTick >= 0) +
                        " established:" + (establishedTick >= 0) + " bred:" + (bredTick >= 0) +
                        " stable:" + (stableTick >= 0) + " size:" + Field<float>(record, "largestSize").ToString("0.00") +
                        " beauty:" + Field<float>(record, "highestBeauty").ToString("0.0") +
                        " nutrition:" + Field<float>(record, "highestNutrition").ToString("0.00"));
                }
            }
            int breedCount = 0;
            int mastered = 0;
            if (breeds != null)
            {
                foreach (object breed in breeds)
                {
                    breedCount++;
                    float stability = (float)(breed.GetType().GetProperty("Stability")?.GetValue(breed) ?? 0f);
                    bool isMastered = (bool)(breed.GetType().GetProperty("Mastered")?.GetValue(breed) ?? false);
                    if (isMastered) mastered++;
                    result.Add("breed=" + Clean(Field<string>(breed, "name")) + " species:" + Field<string>(breed, "fishDefName") +
                        " stability:" + stability.ToStringPercent() + " generation:" + Field<int>(breed, "highestGeneration") +
                        " founders:" + Field<int>(breed, "founderCount") + " births:" + Field<int>(breed, "births") +
                        " matching:" + Field<int>(breed, "matchingBirths") + " qualifying:" + Field<int>(breed, "qualifyingBirths") +
                        " mastered:" + isMastered);
                }
            }
            result.Insert(0, "journal=discovered:" + discovered + " established:" + established + " bred:" + bred +
                " stable:" + stable + " breeds:" + breedCount + " mastered:" + mastered);
            return result;
        }

        private static List<string> Settings()
        {
            AquacultureSettings settings = AquacultureMod.Settings;
            if (settings == null) return new List<string> { "error=settings unavailable" };
            return new List<string>
            {
                "traits=wildChance:" + settings.wildExceptionalTraitChance.ToStringPercent() +
                    " parentalChance:" + settings.parentalTraitInheritanceChance.ToStringPercent() +
                    " inheritedMax:" + settings.maxInheritedTraits + " offspringChance:" + settings.offspringMutationChance.ToStringPercent() +
                    " offspringMax:" + settings.maxOffspringMutations + " breedReliability:" + settings.registeredBreedDefiningTraitReliability.ToStringPercent(),
                "breeding=enabled:" + settings.globalBreedingEnabled + " interval:" + settings.breedingIntervalDays.ToString("0.0") +
                    "d eggs:" + settings.eggHatchDays.ToString("0.0") + "d offspring:" + settings.minimumOffspring + ".." + settings.maximumOffspring,
                "lifespan=" + settings.minimumLifespanDays + ".." + settings.maximumLifespanDays + "d",
                "ecology=interval:" + settings.ecologyIntervalHours.ToString("0.00") + "h food:" + settings.foodDemandMultiplier.ToString("0.00") +
                    " algae:" + settings.algaeGrowthMultiplier.ToString("0.00") + " starvation:" + settings.starvationHours.ToString("0.0") + "h",
                "stress=water:" + settings.waterStressHours.ToString("0.0") + "h temperature:" + settings.temperatureStressHours.ToString("0.0") + "h",
                "capacity=base:" + settings.fishCapacityPerCell.ToString("0.00") + "/cell aerator:" + PondCapacityRules.PoweredAeratorCapacity +
                    " predation:" + settings.predationEnabled +
                    " feedValue:" + settings.feedValuePerUnit.ToString("0.000") + " feederRange:" + settings.feederRange.ToString("0.0"),
                "visuals=enhanced:" + settings.enhancedPondVisuals + " surface:" + settings.surfaceOverlay + " tint:" + settings.underwaterFishTint +
                    " depth:" + settings.pseudoDepth + " caustics:" + settings.caustics + " ripples:" + settings.pondRipples,
                "configuredTraits=" + settings.traitSettings.Count + " maskedFish=" + settings.masks.Count + " maskRevision=" + settings.maskRevision
            };
        }

        private static List<string> AdapterStatus()
        {
            return new List<string>
            {
                "adapter=" + typeof(AquacultureBridgeAdapter).Assembly.GetName().Name,
                "gameplayAssembly=" + typeof(CompFishTraits).Assembly.GetName().Name,
                "coordinator=DevBridge2",
                "hotReload=False",
                "commands=" + BridgeCommandSpecs().Length,
                "scope=read-only on-demand diagnostics; no bridge registration or gameplay mutation"
            };
        }

        public static string BridgeAdapterInfo() =>
            "AquacultureFishing|1.6.0|DevBridge2-coordinated read-only diagnostics for ponds, fish, ecology, journal, breeds, settings, and validation.";

        private static List<string> Performance(Map map)
        {
            if (map == null) return NoMap();
            Stopwatch watch = Stopwatch.StartNew();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            List<PondProxyThing> ponds = PondProxies(map);
            int fish = 0;
            int schools = 0;
            int cells = 0;
            int warnings = 0;
            int habitatStressed = 0;
            for (int i = 0; i < ponds.Count; i++)
            {
                PondMenuSnapshot snapshot = component?.MenuSnapshotAt(ponds[i].Position);
                if (snapshot == null) continue;
                fish += snapshot.population;
                schools += snapshot.schools.Count;
                warnings += snapshot.warnings.Count;
                PondHabitatSnapshot habitat = component?.HabitatAt(ponds[i].Position);
                habitatStressed += habitat?.stressedFish ?? 0;
                cells += snapshot.physicalCapacity > 0 ? snapshot.physicalCapacity : snapshot.capacity;
            }
            watch.Stop();
            float ecologyHours = AquacultureMod.Settings?.ecologyIntervalHours ?? 1f;
            return new List<string>
            {
                "scale=ponds:" + ponds.Count + " cellsApprox:" + cells + " pondFish:" + fish + " schools:" + schools +
                    " warnings:" + warnings + " habitatStressed:" + habitatStressed,
                "schedulers=schoolBase:6ticks ecology:" + Mathf.Max(625, Mathf.RoundToInt(ecologyHours * 2500f)) + "ticks age:eventBound egg:eventBound watcher:250ticks",
                "architecture=school buckets; offscreen throttling; cached menu snapshots; batched ecology; event-invalidated beauty",
                "snapshotQueryMs=" + watch.Elapsed.TotalMilliseconds.ToString("0.00"),
                "note=bridge diagnostics run only on request and add no game tick work"
            };
        }

        private static List<string> Validate(Map map)
        {
            if (map == null) return NoMap();
            Stopwatch watch = Stopwatch.StartNew();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            List<CompFishTraits> fish = FishOnMap(map);
            List<PondProxyThing> proxies = PondProxies(map);
            int connectedPonds = proxies.Count;
            var issues = new List<string>();
            if (component == null) issues.Add("missing FishPondMapComponent");
            for (int i = 0; i < fish.Count; i++)
            {
                CompFishTraits item = fish[i];
                List<string> names = item.traitDefNames ?? new List<string>();
                RequireSingle(item, names, "AF_Age_", "Age", issues);
                RequireSingle(item, names, "AF_Sex_", "Sex", issues);
                RequireSingle(item, names, FishTraitUtility.DietPrefix, "Diet", issues);
                RequireSingle(item, names, FishTraitUtility.WaterPrefix, "Water", issues);
                if (!item.IsAlive && item.IsInPond) issues.Add("dead fish remains in pond id:" + item.parent.thingIDNumber);
                if (item.IsSwimmingInPond && component?.ProxyFor(item.parent.Position) == null)
                    issues.Add("pond fish has no proxy id:" + item.parent.thingIDNumber);
                if (float.IsNaN(item.foodReserve) || float.IsNaN(item.starvationProgress))
                    issues.Add("invalid ecology values id:" + item.parent.thingIDNumber);
                if (float.IsNaN(item.habitatFit) || float.IsNaN(item.habitatStress))
                    issues.Add("invalid habitat values id:" + item.parent.thingIDNumber);
            }
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_FishEgg");
            if (eggDef != null)
            {
                List<Thing> eggs = map.listerThings.ThingsOfDef(eggDef);
                for (int i = 0; i < eggs.Count; i++)
                    if (map.terrainGrid.TerrainAt(eggs[i].Position).defName != "AF_Pond")
                        issues.Add("fish egg outside pond id:" + eggs[i].thingIDNumber);
            }
            watch.Stop();
            var result = new List<string>
            {
                "validation=" + (issues.Count == 0 ? "PASS" : "FAIL") + " issues=" + issues.Count,
                "checked=ponds:" + connectedPonds + " proxies:" + proxies.Count + " fish:" + fish.Count,
                "queryMs=" + watch.Elapsed.TotalMilliseconds.ToString("0.00")
            };
            for (int i = 0; i < issues.Count && i < 40; i++) result.Add("issue=" + issues[i]);
            if (issues.Count > 40) result.Add("issuesTruncated=" + (issues.Count - 40));
            return result;
        }

        private static List<string> OpenPlanner(Map map, string argument)
        {
            if (map == null) return NoMap();
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            PondProxyThing proxy = FindPond(map, component, argument);
            if (proxy == null) return new List<string> { "error=pond not found", "usage=AQUA_OPEN_PLANNER proxyThingId|x,z" };
            if (!AquacultureProgression.IsAvailable("AF_IndustrialAquaculture"))
                return new List<string> { "error=Industrial Aquaculture is not available" };
            Find.WindowStack.Add(new Dialog_PondStockingPlanner(proxy, component));
            return new List<string> { "opened=stocking planner", "pond=id:" + proxy.thingIDNumber + " cell:" + Cell(proxy.Position) };
        }

        private static void RequireSingle(CompFishTraits fish, List<string> names, string prefix, string label, List<string> issues)
        {
            int count = names.Count(name => name.StartsWith(prefix, StringComparison.Ordinal));
            if (count != 1) issues.Add("fish id:" + fish.parent.thingIDNumber + " has " + count + " " + label + " traits");
        }

        private static List<CompFishTraits> FishOnMap(Map map)
        {
            var result = new List<CompFishTraits>();
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                CompFishTraits fish = things[i].TryGetComp<CompFishTraits>();
                if (fish != null) result.Add(fish);
            }
            return result;
        }

        private static List<PondProxyThing> PondProxies(Map map) =>
            map.listerThings.AllThings.OfType<PondProxyThing>().OrderBy(proxy => proxy.thingIDNumber).ToList();

        private static PondProxyThing FindPond(Map map, FishPondMapComponent component, string argument)
        {
            if (int.TryParse(argument, out int id))
                return PondProxies(map).FirstOrDefault(proxy => proxy.thingIDNumber == id);
            string[] parts = (argument ?? string.Empty).Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int x) && int.TryParse(parts[1].Trim(), out int z))
                return component?.ProxyFor(new IntVec3(x, 0, z));
            return null;
        }

        private static bool Available(string research) => AquacultureProgression.IsAvailable(research);
        private static T Field<T>(object instance, string name)
        {
            object value = instance?.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance);
            return value is T typed ? typed : default(T);
        }
        private static string Cell(IntVec3 cell) => cell.x + "," + cell.z;
        private static string Clean(string text) => (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        private static List<string> NoMap() => new List<string> { "error=no active map" };
    }
}
