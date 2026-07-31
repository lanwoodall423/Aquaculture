using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class PondStockingTarget : IExposable
    {
        public string speciesDefName;
        public int targetCount;

        public PondStockingTarget() { }
        public PondStockingTarget(ThingDef species, int count)
        {
            speciesDefName = species?.defName;
            targetCount = Mathf.Max(0, count);
        }

        public ThingDef Species => DefDatabase<ThingDef>.GetNamedSilentFail(speciesDefName);

        public void ExposeData()
        {
            Scribe_Values.Look(ref speciesDefName, "speciesDefName");
            Scribe_Values.Look(ref targetCount, "targetCount");
            targetCount = Mathf.Max(0, targetCount);
        }
    }

    public sealed class AquaticSpeciesExtension : DefModExtension
    {
        public bool overrideDiet;
        public FishDiet diet = FishDiet.Omnivore;
        public bool overrideWater;
        public PondWaterKind waterKind = PondWaterKind.Freshwater;
        public float minimumTemperature = -999f;
        public float maximumTemperature = -999f;
        public float foodDemandFactor = 1f;
        public float foodValueFactor = 1f;
        public float cruiseSpeedFactor = 1f;
        public float maximumSpeedFactor = 1f;
        public float turnRateFactor = 1f;
        public float schoolingFactor = 1f;
        public float cohesionFactor = 1f;
        public float alignmentFactor = 1f;
        public float separationFactor = 1f;
        public float neighborRadiusFactor = 1f;
        public float restFrequencyFactor = 1f;
        public float lifespanFactor = 1f;
        public float breedingIntervalFactor = 1f;
        public float offspringFactor = 1f;
        public float meatYieldFactor = 1f;
        public float beautyOffset;
        public float plantCoverDemand = -1f;
        public float shelterDemand = -1f;
        public float substrateDemand = -1f;
        public float currentDemand = -1f;
        public float openWaterDemand = -1f;
        public List<NaturalWaterHabitat> compatibleHabitats;
        public List<FishHabitatPreference> habitatPreferences;
        public List<BiomeDef> compatibleBiomes;
        public List<BiomeDef> excludedBiomes;
        public List<Season> populationSeasons;
        public float populationMinimumTemperature = -999f;
        public float populationMaximumTemperature = -999f;
        public FishPopulationRarity populationRarity = FishPopulationRarity.Common;
        public float populationDensity = 1f;
    }

    public sealed class AquaticSpeciesProfile
    {
        private static readonly Dictionary<ThingDef, AquaticSpeciesProfile> Cache = new Dictionary<ThingDef, AquaticSpeciesProfile>();
        private static readonly Dictionary<ThingDef, PondWaterKind> DependencyWater = new Dictionary<ThingDef, PondWaterKind>();
        private static readonly Dictionary<ThingDef, float> DependencyCommonality = new Dictionary<ThingDef, float>();
        private static readonly Dictionary<ThingDef, HashSet<string>> DependencyBiomes = new Dictionary<ThingDef, HashSet<string>>();
        private static bool dependencyScanned;

        public FishDiet diet;
        public PondWaterKind waterKind;
        public float hourlyDemand;
        public float foodValue;
        public float minimumTemperature;
        public float maximumTemperature;
        public float lifespanFactor = 1f;
        public float breedingIntervalFactor = 1f;
        public float offspringFactor = 1f;
        public float meatYieldFactor = 1f;
        public float beautyOffset;

        public static AquaticSpeciesProfile For(ThingDef def)
        {
            if (def == null) return new AquaticSpeciesProfile { diet = FishDiet.Omnivore, waterKind = PondWaterKind.Freshwater, hourlyDemand = 0.0025f, foodValue = 0.04f, minimumTemperature = 4f, maximumTemperature = 30f };
            if (Cache.TryGetValue(def, out AquaticSpeciesProfile cached)) return cached;
            EnsureDependencyMetadata();
            string key = ((def.defName ?? "") + " " + (def.label ?? "")).ToLowerInvariant();
            FishDiet diet = ClassifyDiet(key);
            PondWaterKind water = DependencyWater.TryGetValue(def, out PondWaterKind known) ? known : ClassifyWater(key);
            float nutrition = 0.05f;
            try { nutrition = Mathf.Max(0.02f, def.GetStatValueAbstract(StatDefOf.Nutrition)); } catch { }
            TemperatureRange(key, out float minimumTemperature, out float maximumTemperature);
            AquaticSpeciesExtension extension = def.GetModExtension<AquaticSpeciesExtension>();
            if (extension?.overrideDiet == true) diet = extension.diet;
            if (extension?.overrideWater == true) water = extension.waterKind;
            if (extension != null && extension.minimumTemperature > -900f) minimumTemperature = extension.minimumTemperature;
            if (extension != null && extension.maximumTemperature > -900f) maximumTemperature = extension.maximumTemperature;
            cached = new AquaticSpeciesProfile
            {
                diet = diet,
                waterKind = water,
                hourlyDemand = 0.0025f * Mathf.Pow(Mathf.Max(0.4f, nutrition / 0.05f), 0.72f) * Mathf.Max(0.05f, extension?.foodDemandFactor ?? 1f),
                foodValue = Mathf.Max(0.025f, nutrition) * Mathf.Max(0.05f, extension?.foodValueFactor ?? 1f),
                minimumTemperature = minimumTemperature,
                maximumTemperature = maximumTemperature
            };
            ApplySpeciesEconomy(key, cached);
            if (extension != null)
            {
                cached.lifespanFactor *= Mathf.Max(0.1f, extension.lifespanFactor);
                cached.breedingIntervalFactor *= Mathf.Max(0.1f, extension.breedingIntervalFactor);
                cached.offspringFactor *= Mathf.Max(0.1f, extension.offspringFactor);
                cached.meatYieldFactor *= Mathf.Max(0.1f, extension.meatYieldFactor);
                cached.beautyOffset += extension.beautyOffset;
            }
            Cache.Add(def, cached);
            return cached;
        }

        public static bool WaterCompatible(PondWaterKind fish, PondWaterKind pond) => fish == PondWaterKind.Brackishwater || fish == pond;

        public static float PopulationCommonality(ThingDef fish)
        {
            EnsureDependencyMetadata();
            return fish != null && DependencyCommonality.TryGetValue(fish, out float value) ? Mathf.Max(0.01f, value) : 1f;
        }

        public static bool PopulationBiomeCompatible(ThingDef fish, BiomeDef biome)
        {
            EnsureDependencyMetadata();
            return fish == null || biome == null || !DependencyBiomes.TryGetValue(fish, out HashSet<string> biomes) ||
                biomes.Count == 0 || biomes.Contains(biome.defName);
        }

        private static void EnsureDependencyMetadata()
        {
            if (dependencyScanned) return;
            dependencyScanned = true;
            try
            {
                Type type = AccessTools.TypeByName("VCE_Fishing.FishDef");
                if (type == null) return;
                Type database = typeof(DefDatabase<>).MakeGenericType(type);
                IEnumerable defs = AccessTools.Property(database, "AllDefsListForReading")?.GetValue(null) as IEnumerable;
                FieldInfo thingField = AccessTools.Field(type, "thingDef");
                FieldInfo freshField = AccessTools.Field(type, "canBeFreshwater");
                FieldInfo saltField = AccessTools.Field(type, "canBeSaltwater");
                FieldInfo commonalityField = AccessTools.Field(type, "commonality");
                FieldInfo allowedBiomesField = AccessTools.Field(type, "allowedBiomes");
                FieldInfo anyBiomeField = AccessTools.Field(type, "anyBiomeAllowed");
                Dictionary<string, HashSet<string>> biomeGroups = DependencyBiomeGroups();
                if (defs == null || thingField == null) return;
                foreach (object fishDef in defs)
                {
                    ThingDef thing = thingField.GetValue(fishDef) as ThingDef;
                    if (thing == null) continue;
                    bool fresh = freshField != null && (bool)freshField.GetValue(fishDef);
                    bool salt = saltField != null && (bool)saltField.GetValue(fishDef);
                    DependencyWater[thing] = fresh && salt ? PondWaterKind.Brackishwater : salt ? PondWaterKind.Saltwater : PondWaterKind.Freshwater;
                    if (commonalityField?.GetValue(fishDef) is float commonality) DependencyCommonality[thing] = commonality;
                    bool anyBiome = anyBiomeField != null && (bool)anyBiomeField.GetValue(fishDef);
                    if (!anyBiome && allowedBiomesField?.GetValue(fishDef) is IEnumerable<string> allowed)
                    {
                        var resolved = new HashSet<string>();
                        foreach (string key in allowed.Where(value => !value.NullOrEmpty()))
                        {
                            if (DefDatabase<BiomeDef>.GetNamedSilentFail(key) != null) resolved.Add(key);
                            if (biomeGroups.TryGetValue(key, out HashSet<string> grouped)) resolved.UnionWith(grouped);
                        }
                        if (resolved.Count > 0) DependencyBiomes[thing] = resolved;
                    }
                }
            }
            catch (Exception exception) { Log.Warning("[Aquaculture - Fishing] Could not read fishing water metadata: " + exception.Message); }
        }

        private static Dictionary<string, HashSet<string>> DependencyBiomeGroups()
        {
            var result = new Dictionary<string, HashSet<string>>();
            Type type = AccessTools.TypeByName("VCE_Fishing.BiomeTempDef");
            if (type == null) return result;
            Type database = typeof(DefDatabase<>).MakeGenericType(type);
            IEnumerable defs = AccessTools.Property(database, "AllDefsListForReading")?.GetValue(null) as IEnumerable;
            FieldInfo biomesField = AccessTools.Field(type, "biomes");
            if (defs == null || biomesField == null) return result;
            foreach (object def in defs)
            {
                string key = (def as Def)?.defName;
                if (key.NullOrEmpty() || !(biomesField.GetValue(def) is IEnumerable<string> biomes)) continue;
                result[key] = new HashSet<string>(biomes.Where(value => !value.NullOrEmpty()));
            }
            return result;
        }

        private static FishDiet ClassifyDiet(string key)
        {
            if (Contains(key, "shrimp", "prawn", "crab", "lobster", "crayfish")) return FishDiet.Detritivore;
            if (Contains(key, "anchovy", "herring", "sprat", "sardine", "mussel", "oyster")) return FishDiet.FilterFeeder;
            if (Contains(key, "piranha", "shark", "tuna", "salmon", "trout", "bass", "eel", "swordfish", "marlin", "perch", "cod", "haddock", "halibut", "angler")) return FishDiet.Carnivore;
            if (Contains(key, "tilapia", "carp", "mullet")) return FishDiet.Herbivore;
            return FishDiet.Omnivore;
        }

        private static PondWaterKind ClassifyWater(string key)
        {
            if (Contains(key, "salmon", "eel", "mullet")) return PondWaterKind.Brackishwater;
            if (Contains(key, "tuna", "cod", "haddock", "halibut", "herring", "mackerel", "marlin", "swordfish", "anchovy", "sprat", "sardine", "shark")) return PondWaterKind.Saltwater;
            return PondWaterKind.Freshwater;
        }

        private static void TemperatureRange(string key, out float minimum, out float maximum)
        {
            minimum = 4f;
            maximum = 30f;
            if (Contains(key, "cod", "haddock", "halibut", "herring", "sprat")) { minimum = -2f; maximum = 20f; return; }
            if (Contains(key, "salmon", "trout")) { minimum = 0f; maximum = 24f; return; }
            if (Contains(key, "lobster", "crab", "mackerel", "perch")) { minimum = 1f; maximum = 27f; return; }
            if (Contains(key, "koi", "carp", "goldfish")) { minimum = 1f; maximum = 32f; return; }
            if (Contains(key, "guppy", "tilapia", "piranha", "puffer", "toxfish")) { minimum = 18f; maximum = 35f; return; }
            if (Contains(key, "tuna", "marlin", "swordfish", "anchovy", "sardine", "mullet")) { minimum = 10f; maximum = 31f; }
        }

        private static void ApplySpeciesEconomy(string key, AquaticSpeciesProfile profile)
        {
            if (Contains(key, "anchovy", "sprat", "sardine", "minnow", "guppy", "herring"))
            {
                profile.hourlyDemand *= 0.55f;
                profile.foodValue *= 0.5f;
                profile.lifespanFactor = 0.55f;
                profile.breedingIntervalFactor = 0.65f;
                profile.offspringFactor = 1.6f;
                profile.meatYieldFactor = 0.55f;
                return;
            }
            if (Contains(key, "tuna", "marlin", "swordfish", "shark", "halibut"))
            {
                profile.hourlyDemand *= 1.65f;
                profile.foodValue *= 1.8f;
                profile.lifespanFactor = 1.75f;
                profile.breedingIntervalFactor = 1.4f;
                profile.offspringFactor = 0.65f;
                profile.meatYieldFactor = 1.9f;
                profile.beautyOffset = 1f;
                return;
            }
            if (Contains(key, "lobster", "crab", "crayfish"))
            {
                profile.hourlyDemand *= 0.8f;
                profile.foodValue *= 1.15f;
                profile.lifespanFactor = 1.35f;
                profile.breedingIntervalFactor = 1.2f;
                profile.meatYieldFactor = 1.25f;
                return;
            }
            if (Contains(key, "koi", "goldfish", "angelfish", "clownfish"))
            {
                profile.lifespanFactor = 1.25f;
                profile.breedingIntervalFactor = 0.9f;
                profile.meatYieldFactor = 0.7f;
                profile.beautyOffset = 2f;
                return;
            }
            if (Contains(key, "salmon", "trout", "bass", "carp", "catfish"))
            {
                profile.hourlyDemand *= 1.1f;
                profile.foodValue *= 1.2f;
                profile.lifespanFactor = 1.1f;
                profile.meatYieldFactor = 1.25f;
            }
        }

        private static bool Contains(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++) if (value.Contains(terms[i])) return true;
            return false;
        }
    }

    public sealed class PondEcologyRecord : IExposable
    {
        public IntVec3 anchor;
        public PondWaterKind waterKind = PondWaterKind.Freshwater;
        public float algae = -1f;
        public float detritus;
        public float preparedFeed;
        public List<PondOrganismPopulation> organisms = new List<PondOrganismPopulation>();
        public float predationCredit;
        public bool breedingEnabled = true;
        public PondBreedingMode breedingMode = PondBreedingMode.Natural;
        public bool automaticFeeding = true;
        public float targetFeedDays = 1f;
        public bool predationEnabled = true;
        public int populationLimit;
        public int managementPopulationTarget;
        public bool automaticSurplusHarvest;
        public bool harvestingEnabled = true;
        public int minimumHarvestPopulation = 2;
        public bool harvestAdultsOnly = true;
        public bool protectBreedingFemales = true;
        public PondWaterKind blueprintWater = PondWaterKind.Freshwater;
        public List<PondStockingTarget> stockingTargets = new List<PondStockingTarget>();
        public int lastEcologyTick;
        public int nextEcologyTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref waterKind, "waterKind", PondWaterKind.Freshwater);
            Scribe_Values.Look(ref algae, "algae", -1f);
            Scribe_Values.Look(ref detritus, "detritus");
            Scribe_Values.Look(ref preparedFeed, "preparedFeed");
            Scribe_Collections.Look(ref organisms, "organisms", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                organisms = organisms?.Where(item => item?.Organism != null && item.biomass > 0f).ToList()
                    ?? new List<PondOrganismPopulation>();
            Scribe_Values.Look(ref predationCredit, "predationCredit");
            Scribe_Values.Look(ref breedingEnabled, "breedingEnabled", true);
            Scribe_Values.Look(ref breedingMode, "breedingMode", PondBreedingMode.Natural);
            Scribe_Values.Look(ref automaticFeeding, "automaticFeeding", true);
            Scribe_Values.Look(ref targetFeedDays, "targetFeedDays", 1f);
            Scribe_Values.Look(ref predationEnabled, "predationEnabled", true);
            Scribe_Values.Look(ref populationLimit, "populationLimit");
            Scribe_Values.Look(ref managementPopulationTarget, "managementPopulationTarget");
            Scribe_Values.Look(ref automaticSurplusHarvest, "automaticSurplusHarvest");
            Scribe_Values.Look(ref harvestingEnabled, "harvestingEnabled", true);
            Scribe_Values.Look(ref minimumHarvestPopulation, "minimumHarvestPopulation", 2);
            Scribe_Values.Look(ref harvestAdultsOnly, "harvestAdultsOnly", true);
            Scribe_Values.Look(ref protectBreedingFemales, "protectBreedingFemales", true);
            Scribe_Values.Look(ref blueprintWater, "blueprintWater", PondWaterKind.Freshwater);
            Scribe_Collections.Look(ref stockingTargets, "stockingTargets", LookMode.Deep);
            Scribe_Values.Look(ref lastEcologyTick, "lastEcologyTick");
            Scribe_Values.Look(ref nextEcologyTick, "nextEcologyTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!breedingEnabled && breedingMode == PondBreedingMode.Natural) breedingMode = PondBreedingMode.Paused;
                breedingEnabled = breedingMode != PondBreedingMode.Paused;
                targetFeedDays = Mathf.Clamp(targetFeedDays, 0.25f, 5f);
                minimumHarvestPopulation = Mathf.Max(0, minimumHarvestPopulation);
                managementPopulationTarget = Mathf.Max(0, managementPopulationTarget);
            }
            if (stockingTargets == null) stockingTargets = new List<PondStockingTarget>();
            stockingTargets.RemoveAll(target => target == null || target.targetCount <= 0 || target.Species == null);
        }
    }

    public sealed partial class FishPondMapComponent
    {
        private int dataVersion = 1;
        private List<PondEcologyRecord> ecologyRecords = new List<PondEcologyRecord>();
        private readonly List<CompFishTraits> ecologyDeaths = new List<CompFishTraits>();
        private readonly List<CompFishTraits> ecologyConsumed = new List<CompFishTraits>();
        private readonly List<CompFishTraits>[] preyBuckets = { new List<CompFishTraits>(), new List<CompFishTraits>(), new List<CompFishTraits>(), new List<CompFishTraits>() };
        private readonly float[] availablePrey = new float[4];

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "aquacultureDataVersion", 1);
            Scribe_Collections.Look(ref ecologyRecords, "pondEcology", LookMode.Deep);
            if (ecologyRecords == null) ecologyRecords = new List<PondEcologyRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dataVersion = 1;
                MarkPondTopologyDirty();
            }
        }

        public PondWaterKind WaterKindAt(IntVec3 cell)
        {
            EnsurePondState();
            return pondByCell.TryGetValue(cell, out PondState pond) ? pond.ecology.waterKind : PondWaterKind.Freshwater;
        }

        public void SetPondWater(IntVec3 cell, PondWaterKind kind)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return;
            if (pond.fish.Count > 0 || PondEggCount(pond) > 0)
            {
                Messages.Message("Remove all fish and eggs before refilling this pond with a different water type.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            int removed = pond.ecology.organisms?.RemoveAll(population => population.Organism?.Compatible(kind) != true) ?? 0;
            pond.ecology.waterKind = kind;
            pond.beautyDirty = true;
            pond.menuSnapshot = null;
            Messages.Message("The empty pond will be filled as " + WaterLabel(kind) +
                (removed > 0 ? "; " + removed + " incompatible pond population" + (removed == 1 ? " was" : "s were") + " lost." : "."),
                MessageTypeDefOf.TaskCompletion, false);
        }

        public void AddPreparedFeed(IntVec3 cell, float amount)
        {
            EnsurePondState();
            if (pondByCell.TryGetValue(cell, out PondState pond)) pond.ecology.preparedFeed = Mathf.Max(0f, pond.ecology.preparedFeed + amount);
        }

        public bool FishFitsWater(IntVec3 cell, CompFishTraits fish)
        {
            EnsurePondState();
            return fish != null && pondByCell.TryGetValue(cell, out PondState pond) && AquaticSpeciesProfile.WaterCompatible(fish.WaterKind, pond.ecology.waterKind);
        }

        private PondEcologyRecord RecordFor(PondMovementUtility.PondInfo info)
        {
            PondEcologyRecord record = ecologyRecords.FirstOrDefault(item => info.cellSet.Contains(item.anchor));
            if (record == null)
            {
                record = new PondEcologyRecord { anchor = info.anchor };
                ecologyRecords.Add(record);
            }
            record.anchor = info.anchor;
            float capacity = Mathf.Max(0.1f, info.cells.Count * 0.25f);
            if (record.algae < 0f) record.algae = capacity * 0.6f;
            int intervalTicks = Mathf.Max(625, Mathf.RoundToInt((AquacultureMod.Settings?.ecologyIntervalHours ?? 1f) * 2500f));
            if (record.nextEcologyTick <= 0) record.nextEcologyTick = (Find.TickManager?.TicksGame ?? 0) + 250 + PositiveMod(info.anchor.GetHashCode(), Mathf.Max(1, intervalTicks - 250));
            return record;
        }

        private void ProcessEcology(int now)
        {
            bool processedAny = false;
            for (int pondIndex = 0; pondIndex < pondStates.Count; pondIndex++)
            {
                PondState pond = pondStates[pondIndex];
                PondEcologyRecord record = pond.ecology;
                if (now < record.nextEcologyTick) continue;
                float hours = record.lastEcologyTick <= 0 ? 1f : Mathf.Clamp((now - record.lastEcologyTick) / 2500f, 0.1f, 24f);
                record.lastEcologyTick = now;
                record.nextEcologyTick = now + Mathf.Max(625, Mathf.RoundToInt((AquacultureMod.Settings?.ecologyIntervalHours ?? 1f) * 2500f));
                TransferAutomaticFeed(pond);
                SimulateEcology(pond, hours);
                processedAny = true;
            }
            if (processedAny)
                AquacultureJournalComponent.Current?.EvaluateStablePopulations(
                    pondStates.Select(pond => (IEnumerable<CompFishTraits>)pond.fish), now);
        }

        private void SimulateEcology(PondState pond, float hours)
        {
            PondEcologyRecord record = pond.ecology;
            UpdateHabitat(pond, hours);
            ApplyPondTraitEcology(pond, hours);
            float capacity = Mathf.Max(0.1f, pond.info.cells.Count * 0.25f);
            float algaeGrowth = AquacultureMod.Settings?.algaeGrowthMultiplier ?? 1f;
            PondHabitatSnapshot habitat = EnsureHabitat(pond);
            float plantedGrowth = 1f + Mathf.Min(0.25f, habitat.plantStructures * 0.03f);
            record.algae = Mathf.Clamp(record.algae + 0.018f * algaeGrowth * plantedGrowth *
                record.algae * (1f - record.algae / capacity) * hours, 0f, capacity);
            record.detritus = Mathf.Min(capacity * 0.6f,
                record.detritus + habitat.substrateStructures * 0.0015f * hours);
            float algaeRatio = Mathf.Clamp01(record.algae / capacity);
            float detritusRatio = Mathf.Clamp01(record.detritus / Mathf.Max(0.1f, capacity * 0.6f));
            SimulateOrganismGrowth(pond, hours, algaeRatio, detritusRatio, ref record.algae, ref record.detritus);
            for (int i = 0; i < preyBuckets.Length; i++) preyBuckets[i].Clear();
            ecologyDeaths.Clear();
            ecologyConsumed.Clear();

            float algaeDemand = 0f, detritusDemand = 0f, preyDemand = 0f, feedDemand = 0f;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                fish.ecologyDemand = AquaticSpeciesProfile.For(fish.parent.def).hourlyDemand * Mathf.Max(0.2f, fish.SizeFactor) * hours * (AquacultureMod.Settings?.foodDemandMultiplier ?? 1f);
                fish.ecologyReceived = 0f;
                fish.ecologyWaterCompatible = AquaticSpeciesProfile.WaterCompatible(fish.WaterKind, record.waterKind);
                if (!fish.ecologyWaterCompatible) continue;
                DietShares(fish.Diet, out float algaeShare, out float detritusShare, out float preyShare);
                algaeDemand += fish.ecologyDemand * algaeShare;
                detritusDemand += fish.ecologyDemand * detritusShare;
                preyDemand += fish.ecologyDemand * preyShare;
                feedDemand += fish.ecologyDemand;
                preyBuckets[SizeBucket(fish)].Add(fish);
            }

            AllocatePlantPool(pond, ref record.algae, algaeDemand, true);
            AllocatePlantPool(pond, ref record.detritus, detritusDemand, false);
            AllocateOrganismRole(pond, PondOrganismRole.Producer, false);
            AllocateOrganismRole(pond, PondOrganismRole.Detritivore, false);
            float zooplanktonConsumed = AllocateOrganismRole(pond, PondOrganismRole.Zooplankton, true) +
                AllocateOrganismRole(pond, PondOrganismRole.FilterFeeder, true);
            if (AquacultureMod.Settings?.predationEnabled != false)
                AllocatePredation(pond, Mathf.Max(0f, preyDemand - zooplanktonConsumed));
            float remainingDemand = 0f;
            for (int i = 0; i < pond.fish.Count; i++) if (pond.fish[i].ecologyWaterCompatible) remainingDemand += Mathf.Max(0f, pond.fish[i].ecologyDemand - pond.fish[i].ecologyReceived);
            float feedRatio = remainingDemand > 0f ? Mathf.Min(1f, record.preparedFeed / remainingDemand) : 0f;
            if (feedRatio > 0f)
            {
                for (int i = 0; i < pond.fish.Count; i++)
                {
                    CompFishTraits fish = pond.fish[i];
                    if (fish.ecologyWaterCompatible) fish.ecologyReceived += Mathf.Max(0f, fish.ecologyDemand - fish.ecologyReceived) * feedRatio;
                }
                record.preparedFeed -= remainingDemand * feedRatio;
            }

            float metabolicWaste = 0f;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                metabolicWaste += pond.fish[i].ecologyReceived * 0.12f;
                UpdateHunger(pond.fish[i], hours);
                UpdateWaterStress(pond.fish[i], hours);
                UpdateTemperature(pond.fish[i], hours);
            }
            record.detritus = Mathf.Min(capacity * 0.6f, record.detritus + metabolicWaste);
            for (int i = 0; i < ecologyConsumed.Count; i++)
            {
                ecologyDeaths.Remove(ecologyConsumed[i]);
                if (!ecologyConsumed[i].parent.Destroyed) ecologyConsumed[i].parent.Destroy(DestroyMode.Vanish);
            }
            for (int i = 0; i < ecologyDeaths.Count; i++) if (!ecologyDeaths[i].parent.Destroyed) ecologyDeaths[i].MarkDead();
            ApplyAutomaticHarvestPolicy(pond);
            pond.beautyDirty = true;
            pond.menuSnapshot = null;
        }

        private static void AllocatePlantPool(PondState pond, ref float pool, float totalDemand, bool algaePool)
        {
            if (totalDemand <= 0f || pool <= 0f) return;
            float ratio = Mathf.Min(1f, pool / totalDemand);
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                if (!fish.ecologyWaterCompatible) continue;
                DietShares(fish.Diet, out float algae, out float detritus, out _);
                fish.ecologyReceived += fish.ecologyDemand * (algaePool ? algae : detritus) * ratio;
            }
            pool = Mathf.Max(0f, pool - totalDemand * ratio);
        }

        private static void SimulateOrganismGrowth(PondState pond, float hours, float algaeRatio,
            float detritusRatio, ref float algae, ref float detritus)
        {
            if (pond.ecology.organisms == null) return;
            for (int i = pond.ecology.organisms.Count - 1; i >= 0; i--)
            {
                PondOrganismPopulation population = pond.ecology.organisms[i];
                PondOrganismDef def = population.Organism;
                if (def == null)
                {
                    pond.ecology.organisms.RemoveAt(i);
                    continue;
                }
                float capacity = Mathf.Max(0.05f, pond.info.cells.Count * def.capacityPerCell);
                float resource = def.role == PondOrganismRole.Producer ? 1f :
                    def.role == PondOrganismRole.Detritivore ? detritusRatio :
                    def.role == PondOrganismRole.FilterFeeder ? (algaeRatio + detritusRatio) * 0.5f : algaeRatio;
                float growth = def.growthPerHour * population.biomass *
                    Mathf.Max(0f, 1f - population.biomass / capacity) * resource * hours;
                float collapse = resource < 0.04f && def.role != PondOrganismRole.Producer
                    ? population.biomass * 0.018f * hours : 0f;
                population.biomass = Mathf.Clamp(population.biomass + growth - collapse, 0f, capacity);
                if (def.role == PondOrganismRole.Zooplankton || def.role == PondOrganismRole.FilterFeeder)
                    algae = Mathf.Max(0f, algae - growth * 0.25f);
                if (def.role == PondOrganismRole.Detritivore || def.role == PondOrganismRole.FilterFeeder)
                    detritus = Mathf.Max(0f, detritus - growth * 0.20f);
                if (population.biomass < 0.0005f) pond.ecology.organisms.RemoveAt(i);
            }
        }

        private static float AllocateOrganismRole(PondState pond, PondOrganismRole role, bool preyFood)
        {
            float pool = OrganismBiomass(pond, role);
            if (pool <= 0f) return 0f;
            float totalDemand = 0f;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                if (!fish.ecologyWaterCompatible) continue;
                DietShares(fish.Diet, out float algae, out float detritus, out float prey);
                float share = preyFood ? prey : role == PondOrganismRole.Producer ? algae : detritus;
                totalDemand += Mathf.Min(
                    Mathf.Max(0f, fish.ecologyDemand - fish.ecologyReceived),
                    fish.ecologyDemand * share);
            }
            if (totalDemand <= 0f) return 0f;
            float consumed = Mathf.Min(pool, totalDemand);
            float ratio = consumed / totalDemand;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                if (!fish.ecologyWaterCompatible) continue;
                DietShares(fish.Diet, out float algae, out float detritus, out float prey);
                float share = preyFood ? prey : role == PondOrganismRole.Producer ? algae : detritus;
                float availableDemand = Mathf.Min(
                    Mathf.Max(0f, fish.ecologyDemand - fish.ecologyReceived),
                    fish.ecologyDemand * share);
                fish.ecologyReceived += availableDemand * ratio;
            }
            float remaining = consumed;
            List<PondOrganismPopulation> populations = pond.ecology.organisms;
            for (int i = 0; i < populations.Count && remaining > 0f; i++)
            {
                if (populations[i].Organism?.role != role) continue;
                float taken = Mathf.Min(populations[i].biomass, consumed * populations[i].biomass / pool);
                populations[i].biomass -= taken;
                remaining -= taken;
            }
            if (remaining > 0.0001f)
                for (int i = 0; i < populations.Count && remaining > 0f; i++)
                {
                    if (populations[i].Organism?.role != role) continue;
                    float taken = Mathf.Min(populations[i].biomass, remaining);
                    populations[i].biomass -= taken;
                    remaining -= taken;
                }
            return consumed;
        }

        private void AllocatePredation(PondState pond, float totalDemand)
        {
            if (totalDemand <= 0f) return;
            Array.Clear(availablePrey, 0, availablePrey.Length);
            for (int bucket = 0; bucket < 4; bucket++)
                for (int i = 0; i < preyBuckets[bucket].Count; i++) availablePrey[bucket] += PreyValue(preyBuckets[bucket][i]);
            float consumed = 0f;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                if (!fish.ecologyWaterCompatible) continue;
                DietShares(fish.Diet, out _, out _, out float prey);
                float wanted = fish.ecologyDemand * prey;
                int predatorBucket = SizeBucket(fish);
                float received = 0f;
                for (int bucket = predatorBucket - 1; bucket >= 0 && received < wanted; bucket--)
                {
                    float taken = Mathf.Min(wanted - received, availablePrey[bucket]);
                    availablePrey[bucket] -= taken;
                    received += taken;
                }
                fish.ecologyReceived += received;
                consumed += received;
            }
            pond.ecology.predationCredit += consumed;
            for (int bucket = 0; bucket < 3 && pond.ecology.predationCredit > 0f; bucket++)
            {
                List<CompFishTraits> candidates = preyBuckets[bucket];
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    CompFishTraits prey = candidates[i];
                    float value = PreyValue(prey);
                    if (pond.ecology.predationCredit < value) continue;
                    bool young = prey.traitDefNames.Contains(FishTraitUtility.Fry) ||
                        prey.traitDefNames.Contains(FishTraitUtility.Juvenile);
                    PondHabitatSnapshot habitat = EnsureHabitat(pond);
                    if (young && habitat.plantDemand > 0f && Rand.Chance(habitat.PlantFit * 0.65f)) continue;
                    pond.ecology.predationCredit -= value;
                    pond.ecology.detritus += value * 0.12f;
                    ecologyConsumed.Add(prey);
                }
            }
        }

        private void UpdateHunger(CompFishTraits fish, float hours)
        {
            if (!fish.IsAlive || fish.ecologyDemand <= 0f) return;
            float satisfaction = fish.ecologyWaterCompatible ? Mathf.Clamp01(fish.ecologyReceived / fish.ecologyDemand) : 0f;
            if (satisfaction >= 0.999f)
            {
                fish.foodReserve = Mathf.Min(1f, fish.foodReserve + 0.08f * hours);
                fish.starvationProgress = Mathf.Max(0f, fish.starvationProgress - 0.04f * hours);
                return;
            }
            float deficit = 1f - satisfaction;
            float totalHours = Mathf.Max(6f, AquacultureMod.Settings?.starvationHours ?? 96f);
            float reserveHours = totalHours * 0.5f;
            float dyingHours = fish.ecologyWaterCompatible ? totalHours * 0.5f : Mathf.Min(18f, totalHours * 0.5f);
            fish.foodReserve = Mathf.Max(0f, fish.foodReserve - deficit * hours / reserveHours);
            if (fish.foodReserve <= 0f) fish.starvationProgress += deficit * hours / dyingHours;
            if (fish.starvationProgress >= 1f) ecologyDeaths.Add(fish);
        }

        private void UpdateTemperature(CompFishTraits fish, float hours)
        {
            if (!fish.IsAlive || fish.parent?.Spawned != true) return;
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(fish.parent.def);
            float minimum = profile.minimumTemperature - fish.TemperatureRangeOffset;
            float maximum = profile.maximumTemperature + fish.TemperatureRangeOffset;
            float temperature = GenTemperature.GetTemperatureForCell(fish.parent.Position, map);
            fish.lastPondTemperature = temperature;
            float departure = temperature < minimum
                ? minimum - temperature
                : temperature > maximum ? temperature - maximum : 0f;
            if (departure <= 0f)
            {
                fish.temperatureStress = Mathf.Max(0f, fish.temperatureStress - hours / 12f);
                return;
            }
            float severity = 1f + departure / 8f;
            fish.temperatureStress += hours * severity / Mathf.Max(1f, AquacultureMod.Settings?.temperatureStressHours ?? 48f);
            if (fish.temperatureStress >= 1f && !ecologyDeaths.Contains(fish)) ecologyDeaths.Add(fish);
        }

        private void UpdateWaterStress(CompFishTraits fish, float hours)
        {
            if (!fish.IsAlive) return;
            if (fish.ecologyWaterCompatible)
            {
                fish.waterStress = Mathf.Max(0f, fish.waterStress - hours / 6f);
                return;
            }
            fish.waterStress += hours / Mathf.Max(1f, AquacultureMod.Settings?.waterStressHours ?? 12f);
            if (fish.waterStress >= 1f && !ecologyDeaths.Contains(fish)) ecologyDeaths.Add(fish);
        }

        private static float PreyValue(CompFishTraits fish) => AquaticSpeciesProfile.For(fish.parent.def).foodValue * Mathf.Max(0.2f, fish.SizeFactor) * fish.MeatYield;
        private static int SizeBucket(CompFishTraits fish) => fish.SizeFactor < 0.45f ? 0 : fish.SizeFactor < 0.85f ? 1 : fish.SizeFactor < 1.4f ? 2 : 3;
        private static string WaterLabel(PondWaterKind kind) => kind == PondWaterKind.Brackishwater ? "brackishwater" : kind.ToString().ToLowerInvariant();

        private static void DietShares(FishDiet diet, out float algae, out float detritus, out float prey)
        {
            algae = detritus = prey = 0f;
            switch (diet)
            {
                case FishDiet.Herbivore: algae = 1f; break;
                case FishDiet.Carnivore: prey = 1f; break;
                case FishDiet.Detritivore: detritus = 0.9f; algae = 0.1f; break;
                case FishDiet.FilterFeeder: algae = 0.8f; detritus = 0.2f; break;
                default: algae = 0.55f; detritus = 0.15f; prey = 0.3f; break;
            }
        }
    }
}
