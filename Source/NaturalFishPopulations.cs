using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace AquacultureFishing
{
    public enum NaturalWaterHabitat { Pond, Lake, River, Marsh, Coastal, Ocean }
    public enum FishPopulationRarity { Common, Uncommon, Rare }

    public sealed class FishHabitatPreference
    {
        public NaturalWaterHabitat habitat;
        public float suitability = 1f;
    }

    public sealed class FishPopulationHabitatDef : Def
    {
        public NaturalWaterHabitat habitat;
        public int pondMaximumCells;
        public int minimumCells;
        public float populationPerCell = 0.12f;
        public float carryingCapacityFactor = 1f;
        public int minimumPopulation = 1;
        public int maximumPopulation = 500;
        public SimpleCurve diversityCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f), new CurvePoint(25f, 1f), new CurvePoint(70f, 2f),
            new CurvePoint(150f, 3f), new CurvePoint(300f, 4.5f), new CurvePoint(500f, 6f)
        };
        public int maximumDiversity = 8;
        public float minimumViablePopulation = 2f;
        public float minimumBreedingPopulation = 2f;
        public float localVariation = 0.12f;
        public int updateIntervalTicks = 60000;
        public float breedingPerDay = 0.035f;
        public float naturalMortalityPerDay = 0.01f;
        public float migrationPerDay;
        public float maximumMigrationPerDay = 2f;
        public float rebalancePerDay = 0.05f;

        public int CarryingCapacity(int cellCount)
        {
            int capacity = Mathf.RoundToInt(Mathf.Max(0f, cellCount) * populationPerCell * carryingCapacityFactor);
            return Mathf.Clamp(capacity, minimumPopulation, Mathf.Max(minimumPopulation, maximumPopulation));
        }

        public int DiversityFor(int cellCount)
        {
            float target = diversityCurve?.Evaluate(Mathf.Max(0, cellCount)) ?? 0f;
            return Mathf.Clamp(Mathf.RoundToInt(target), 0, Mathf.Max(0, maximumDiversity));
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (populationPerCell <= 0f) yield return defName + " needs positive populationPerCell.";
            if (carryingCapacityFactor <= 0f) yield return defName + " needs positive carryingCapacityFactor.";
            if (minimumPopulation < 0 || maximumPopulation < minimumPopulation) yield return defName + " has invalid population bounds.";
            if (diversityCurve == null || diversityCurve.PointsCount < 2 || maximumDiversity < 0) yield return defName + " has invalid diversity scaling.";
            if (minimumViablePopulation < 0.5f || minimumBreedingPopulation < minimumViablePopulation) yield return defName + " has invalid viable population bounds.";
            if (localVariation < 0f || localVariation > 1f) yield return defName + " localVariation must be between zero and one.";
            if (updateIntervalTicks < 2500) yield return defName + " updateIntervalTicks must be at least 2500.";
            if (breedingPerDay < 0f || naturalMortalityPerDay < 0f || migrationPerDay < 0f || maximumMigrationPerDay < 0f || rebalancePerDay < 0f)
                yield return defName + " lifecycle rates cannot be negative.";
        }
    }

    public sealed class NaturalFishSpeciesPopulation : IExposable
    {
        public string fishDefName;
        public float population;
        public bool established;

        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);

        public void ExposeData()
        {
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref population, "population");
            Scribe_Values.Look(ref established, "established");
            population = Mathf.Max(0f, population);
        }
    }

    public sealed class NaturalWaterPopulation : IExposable
    {
        public const int CurrentGenerationVersion = 1;

        public IntVec3 anchor;
        public NaturalWaterHabitat habitat;
        public int cellCount;
        public int carryingCapacity;
        public int targetPopulation;
        public int lastBalanceTick;
        public bool odysseyRarityInitialized;
        public bool initialPopulationComplete;
        public int generationVersion;
        public string deferredRealityStableId;
        public List<NaturalFishSpeciesPopulation> species = new List<NaturalFishSpeciesPopulation>();

        public float TotalPopulation => species?.Sum(item => Mathf.Max(0f, item.population)) ?? 0f;
        public IEnumerable<ThingDef> PresentSpecies => species?.Where(item => item.population >= 0.5f && item.FishDef != null).Select(item => item.FishDef)
            ?? Enumerable.Empty<ThingDef>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref habitat, "habitat", NaturalWaterHabitat.Pond);
            Scribe_Values.Look(ref cellCount, "cellCount");
            Scribe_Values.Look(ref carryingCapacity, "carryingCapacity");
            Scribe_Values.Look(ref targetPopulation, "targetPopulation");
            Scribe_Values.Look(ref lastBalanceTick, "lastBalanceTick");
            Scribe_Values.Look(ref odysseyRarityInitialized, "odysseyRarityInitialized");
            Scribe_Values.Look(ref initialPopulationComplete, "initialPopulationComplete");
            Scribe_Values.Look(ref generationVersion, "generationVersion");
            Scribe_Values.Look(ref deferredRealityStableId, "deferredRealityStableId");
            Scribe_Collections.Look(ref species, "species", LookMode.Deep);
            if (species == null) species = new List<NaturalFishSpeciesPopulation>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Preserve missing Def names for Deferred Reality migration and orphan inspection.
                species.RemoveAll(item => item == null || string.IsNullOrEmpty(item.fishDefName));
                species = species.GroupBy(item => item.fishDefName).Select(group => new NaturalFishSpeciesPopulation
                {
                    fishDefName = group.Key,
                    population = group.Sum(item => Mathf.Max(0f, item.population)),
                    established = group.Any(item => item.established)
                }).ToList();
                if (species.Count > 0) initialPopulationComplete = true;
            }
        }

    }

    public sealed class NaturalFishPopulationSummary
    {
        public float totalPopulation;
        public int carryingCapacity;
        public readonly List<ThingDef> presentSpecies = new List<ThingDef>();
        public readonly Dictionary<ThingDef, int> estimatedPopulation = new Dictionary<ThingDef, int>();
        public readonly Dictionary<ThingDef, string> displayLabels = new Dictionary<ThingDef, string>();
        public string readoutText = string.Empty;

        public int EstimatedPopulation(ThingDef fish)
        {
            return fish != null && estimatedPopulation.TryGetValue(fish, out int value) ? value : 0;
        }

        public string DisplayLabel(ThingDef fish)
        {
            return fish != null && displayLabels.TryGetValue(fish, out string value) ? value : fish?.LabelCap.ToString() ?? string.Empty;
        }
    }

    /// <summary>Prepared, uncertainty-preserving water data for map overlays and journal pages.</summary>
    public sealed class NaturalWaterViewSnapshot
    {
        public IntVec3 anchor;
        public NaturalWaterHabitat habitat;
        public float totalPopulation;
        public int carryingCapacity;
        public readonly List<ThingDef> species = new List<ThingDef>();
        public readonly Dictionary<ThingDef, int> estimates = new Dictionary<ThingDef, int>();

        public string AbundanceLabel => "~" + Mathf.RoundToInt(totalPopulation) + " / " + carryingCapacity;
    }

    public sealed class NaturalFishPopulationMapComponent : MapComponent
    {
        private static readonly IntVec3[] CardinalDirections =
        {
            new IntVec3(1, 0, 0), new IntVec3(-1, 0, 0), new IntVec3(0, 0, 1), new IntVec3(0, 0, -1)
        };

        private List<NaturalWaterPopulation> populations = new List<NaturalWaterPopulation>();
        private readonly Dictionary<IntVec3, NaturalWaterPopulation> populationByCell = new Dictionary<IntVec3, NaturalWaterPopulation>();
        private readonly Dictionary<NaturalWaterPopulation, NaturalFishPopulationSummary> summaryByPopulation =
            new Dictionary<NaturalWaterPopulation, NaturalFishPopulationSummary>();
        private readonly List<NaturalWaterViewSnapshot> preparedViews = new List<NaturalWaterViewSnapshot>();
        private bool preparedViewsDirty = true;
        private int nextBalanceTick;
        private int topologyRebuildTick;
        private bool initializedAllBodies;

        public NaturalFishPopulationMapComponent(Map map) : base(map) { }

        // Read-only integration surface for optional Deferred Reality natural-water ownership.
        public Map ActiveMap => map;
        public IReadOnlyList<NaturalWaterPopulation> Populations => populations;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref populations, "aquacultureNaturalFishPopulations", LookMode.Deep);
            if (populations == null) populations = new List<NaturalWaterPopulation>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                populations.RemoveAll(record => record == null || !record.anchor.IsValid);
                populationByCell.Clear();
                summaryByPopulation.Clear();
                preparedViews.Clear();
                preparedViewsDirty = true;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            InitializeAllWaterBodies();
            ScheduleNextBalance(Find.TickManager?.TicksGame ?? 0);
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (tick < nextBalanceTick) return;
            if (!initializedAllBodies && tick >= topologyRebuildTick) InitializeAllWaterBodies();
            for (int i = 0; i < populations.Count; i++)
            {
                FishPopulationHabitatDef habitatDef = HabitatDef(populations[i].habitat);
                int interval = Mathf.Max(2500, habitatDef?.updateIntervalTicks ?? 60000);
                if (tick - populations[i].lastBalanceTick >= interval) GradualRebalance(populations[i], tick);
            }
            ScheduleNextBalance(tick);
        }

        private void ScheduleNextBalance(int tick)
        {
            int next = int.MaxValue;
            for (int i = 0; i < populations.Count; i++)
            {
                FishPopulationHabitatDef habitatDef = HabitatDef(populations[i].habitat);
                int interval = Mathf.Max(2500, habitatDef?.updateIntervalTicks ?? 60000);
                next = Mathf.Min(next, populations[i].lastBalanceTick + interval);
            }
            if (!initializedAllBodies) next = Mathf.Min(next, Mathf.Max(tick + 1, topologyRebuildTick));
            nextBalanceTick = next == int.MaxValue ? tick + 60000 : Mathf.Max(tick + 1, next);
        }

        private void InitializeAllWaterBodies()
        {
            if (initializedAllBodies) return;
            populationByCell.Clear();
            summaryByPopulation.Clear();
            populations.RemoveAll(record => record == null || !record.anchor.InBounds(map) || !IsNaturalWater(record.anchor));
            List<WaterBody> bodies = map.waterBodyTracker?.Bodies;
            if (bodies?.Count > 0)
            {
                for (int i = 0; i < bodies.Count; i++)
                {
                    WaterBody body = bodies[i];
                    PopulationAtOdyssey(body.rootCell, body);
                }
                initializedAllBodies = true;
                return;
            }
            for (int index = 0; index < map.cellIndices.NumGridCells; index++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (IsNaturalWater(cell) && !populationByCell.ContainsKey(cell)) PopulationAt(cell);
            }
            initializedAllBodies = true;
        }

        public NaturalWaterPopulation PopulationAt(IntVec3 cell, IEnumerable<ThingDef> establishedSpecies = null, float establishedPopulation = 0f)
        {
            if (populationByCell.TryGetValue(cell, out NaturalWaterPopulation cached))
            {
                if (establishedSpecies != null && ImportEstablished(cached, establishedSpecies, establishedPopulation))
                    RefreshSummary(cached);
                return cached;
            }
            if (!TryResolveWaterCell(cell, out cell)) return null;
            if (populationByCell.TryGetValue(cell, out cached)) return cached;
            List<IntVec3> cells = ConnectedCells(cell, out WaterBody body);
            if (cells.Count == 0) return null;
            IntVec3 anchor = StableAnchor(cells);
            NaturalWaterPopulation record = MergeConnectedRecords(anchor, cells);
            if (record == null)
            {
                record = CreatePopulation(anchor, cells, body, establishedSpecies, establishedPopulation);
                populations.Add(record);
            }
            else
            {
                record.cellCount = cells.Count;
                ReconcileExternalPopulation(record, body);
                ImportEstablished(record, establishedSpecies, establishedPopulation);
                UpgradePopulationGeneration(record, cells);
            }
            for (int i = 0; i < cells.Count; i++) populationByCell[cells[i]] = record;
            if (record.generationVersion < NaturalWaterPopulation.CurrentGenerationVersion)
                UpgradePopulationGeneration(record, cells);
            RefreshSummary(record);
            SynchronizeWaterBody(body, record);
            return record;
        }

        public void InvalidateTopology()
        {
            initializedAllBodies = false;
            int tick = Find.TickManager?.TicksGame ?? 0;
            topologyRebuildTick = tick + 60;
            nextBalanceTick = Mathf.Min(nextBalanceTick, topologyRebuildTick);
            preparedViewsDirty = true;
            AquacultureSnapshotCache.Invalidate();
        }

        public bool TryGetPreparedSummary(IntVec3 cell, out NaturalFishPopulationSummary summary)
        {
            summary = null;
            return populationByCell.TryGetValue(cell, out NaturalWaterPopulation record) &&
                summaryByPopulation.TryGetValue(record, out summary);
        }

        public IReadOnlyList<NaturalWaterViewSnapshot> PreparedWaterSnapshots
        {
            get
            {
                if (preparedViewsDirty)
                {
                    preparedViews.Clear();
                    for (int i = 0; i < populations.Count; i++)
                    {
                        NaturalWaterPopulation population = populations[i];
                        if (population == null || !summaryByPopulation.TryGetValue(population, out NaturalFishPopulationSummary summary)) continue;
                        var view = new NaturalWaterViewSnapshot
                        {
                            anchor = population.anchor,
                            habitat = population.habitat,
                            totalPopulation = summary.totalPopulation,
                            carryingCapacity = summary.carryingCapacity
                        };
                        view.species.AddRange(summary.presentSpecies);
                        foreach (KeyValuePair<ThingDef, int> pair in summary.estimatedPopulation) view.estimates[pair.Key] = pair.Value;
                        preparedViews.Add(view);
                    }
                    preparedViews.Sort((left, right) => left.anchor.z != right.anchor.z
                        ? left.anchor.z.CompareTo(right.anchor.z) : left.anchor.x.CompareTo(right.anchor.x));
                    preparedViewsDirty = false;
                }
                return preparedViews;
            }
        }

        private NaturalWaterPopulation PreparedRecordAt(IntVec3 cell)
        {
            populationByCell.TryGetValue(cell, out NaturalWaterPopulation record);
            return record;
        }

        public IEnumerable<ThingDef> SpeciesAt(IntVec3 cell, IEnumerable<ThingDef> establishedSpecies = null, float establishedPopulation = 0f)
        {
            if (establishedSpecies == null && TryGetPreparedSummary(cell, out NaturalFishPopulationSummary summary))
                return summary.presentSpecies;
            return PopulationAt(cell, establishedSpecies, establishedPopulation)?.PresentSpecies ?? Enumerable.Empty<ThingDef>();
        }

        public IEnumerable<ThingDef> SpeciesAtOdyssey(IntVec3 cell, WaterBody body)
        {
            if (TryGetPreparedSummary(cell, out NaturalFishPopulationSummary summary)) return summary.presentSpecies;
            return PopulationAtOdyssey(cell, body)?.PresentSpecies ?? Enumerable.Empty<ThingDef>();
        }

        public float LocalWeight(IntVec3 cell, ThingDef fishDef)
        {
            NaturalWaterPopulation record = PreparedRecordAt(cell) ?? PopulationAt(cell);
            NaturalFishSpeciesPopulation speciesRecord = record?.species.FirstOrDefault(item => item.FishDef == fishDef);
            if (speciesRecord == null || speciesRecord.population < 0.5f) return 0f;
            FishPopulationHabitatDef habitatDef = HabitatDef(record.habitat);
            float variation = habitatDef?.localVariation ?? 0.12f;
            float local = 1f + (UnitHash(StableHash(WorldSeed, map.uniqueID, record.anchor.x, record.anchor.z,
                cell.x, cell.z, StableStringHash(fishDef.defName), 7919)) * 2f - 1f) * variation;
            return Mathf.Max(0.01f, speciesRecord.population * local);
        }

        public bool Contains(IntVec3 cell, ThingDef fishDef)
        {
            NaturalWaterPopulation record = PreparedRecordAt(cell) ?? PopulationAt(cell);
            if (record == null || fishDef == null) return false;
            return record.species.Any(item => item.FishDef == fishDef && item.population >= 0.5f);
        }

        public void ConsumeCatch(IntVec3 cell, ThingDef fishDef, float amount = 1f)
        {
            NaturalWaterPopulation record = PreparedRecordAt(cell) ?? PopulationAt(cell);
            NaturalFishSpeciesPopulation speciesRecord = record?.species.FirstOrDefault(item => item.FishDef == fishDef);
            if (speciesRecord == null) return;
            speciesRecord.population = Mathf.Max(0f, speciesRecord.population - Mathf.Max(0f, amount));
            RefreshSummary(record);
            WaterBody body = map.waterBodyTracker?.WaterBodyAt(cell);
            SynchronizeWaterBody(body, record);
        }

        public bool IntroduceFish(IntVec3 cell, ThingDef fishDef, float amount = 1f)
        {
            NaturalWaterPopulation record = PreparedRecordAt(cell) ?? PopulationAt(cell);
            if (record == null || fishDef == null || !FishUtility.IsFish(fishDef) || amount <= 0f) return false;
            float temperature = GenTemperature.GetTemperatureForCell(record.anchor, map);
            Season season = NormalizeSeason(GenLocalDate.Season(map));
            if (!Suitable(fishDef, record.habitat, map.Biome, temperature, season)) return false;
            NaturalFishSpeciesPopulation item = record.species.FirstOrDefault(existing => existing.FishDef == fishDef);
            if (item == null)
            {
                item = new NaturalFishSpeciesPopulation { fishDefName = fishDef.defName, established = true };
                record.species.Add(item);
            }
            item.population += amount;
            item.established = true;
            RefreshSummary(record);
            SynchronizeWaterBody(map.waterBodyTracker?.WaterBodyAt(record.anchor), record);
            return true;
        }

        private bool TryResolveWaterCell(IntVec3 requested, out IntVec3 waterCell)
        {
            waterCell = requested;
            if (requested.InBounds(map) && IsNaturalWater(requested)) return true;
            if (!requested.InBounds(map)) return false;
            for (int radius = 1; radius <= 12; radius++)
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(requested, radius, true))
                    if (candidate.InBounds(map) && IsNaturalWater(candidate))
                    {
                        waterCell = candidate;
                        return true;
                    }
            return false;
        }

        private NaturalWaterPopulation CreatePopulation(IntVec3 anchor, List<IntVec3> cells, WaterBody body,
            IEnumerable<ThingDef> establishedSpecies, float establishedPopulation)
        {
            NaturalWaterHabitat habitat = ClassifyHabitat(cells, body);
            FishPopulationHabitatDef habitatDef = HabitatDef(habitat) ?? DefDatabase<FishPopulationHabitatDef>.AllDefsListForReading.FirstOrDefault();
            int capacity = habitatDef?.CarryingCapacity(cells.Count) ?? Mathf.Max(1, Mathf.RoundToInt(cells.Count * 0.12f));
            var record = new NaturalWaterPopulation
            {
                anchor = anchor,
                habitat = habitat,
                cellCount = cells.Count,
                carryingCapacity = capacity,
                targetPopulation = capacity,
                lastBalanceTick = Find.TickManager?.TicksGame ?? 0
            };
            ImportEstablished(record, establishedSpecies, establishedPopulation);
            PopulateCompatibleSpecies(record, habitatDef, cells);
            record.initialPopulationComplete = true;
            record.generationVersion = NaturalWaterPopulation.CurrentGenerationVersion;
            RefreshSummary(record);
            return record;
        }

        private void UpgradePopulationGeneration(NaturalWaterPopulation record, List<IntVec3> cells)
        {
            if (record == null || record.generationVersion >= NaturalWaterPopulation.CurrentGenerationVersion || cells.NullOrEmpty()) return;
            record.cellCount = cells.Count;
            record.habitat = ClassifyHabitat(cells, map.waterBodyTracker?.WaterBodyAt(record.anchor));
            FishPopulationHabitatDef habitatDef = HabitatDef(record.habitat);
            if (habitatDef != null)
            {
                record.carryingCapacity = habitatDef.CarryingCapacity(record.cellCount);
                record.targetPopulation = Mathf.Max(record.targetPopulation, record.carryingCapacity);
                PopulateCompatibleSpecies(record, habitatDef, cells);
            }
            record.generationVersion = NaturalWaterPopulation.CurrentGenerationVersion;
        }

        private NaturalWaterPopulation PopulationAtOdyssey(IntVec3 cell, WaterBody body)
        {
            List<ThingDef> common = body?.CommonFishIncludingExtras.Where(FishUtility.IsFish).Distinct().ToList()
                ?? new List<ThingDef>();
            List<ThingDef> uncommon = body?.UncommonFish.Where(FishUtility.IsFish).Distinct().ToList()
                ?? new List<ThingDef>();
            NaturalWaterPopulation record = PopulationAt(cell, common.Concat(uncommon), body?.Population ?? 0f);
            if (record == null || record.odysseyRarityInitialized) return record;
            List<NaturalFishSpeciesPopulation> imported = record.species
                .Where(item => common.Contains(item.FishDef) || uncommon.Contains(item.FishDef)).ToList();
            float importedTotal = imported.Sum(item => item.population);
            float weight = imported.Sum(item => common.Contains(item.FishDef) ? 1f : 0.25f);
            if (importedTotal > 0f && weight > 0f)
                for (int i = 0; i < imported.Count; i++)
                    imported[i].population = importedTotal * (common.Contains(imported[i].FishDef) ? 1f : 0.25f) / weight;
            record.odysseyRarityInitialized = true;
            RefreshSummary(record);
            return record;
        }

        private void PopulateCompatibleSpecies(NaturalWaterPopulation record, FishPopulationHabitatDef habitatDef, List<IntVec3> cells)
        {
            float viablePopulation = Mathf.Max(habitatDef?.minimumViablePopulation ?? 2f,
                habitatDef?.minimumBreedingPopulation ?? 2f);
            int diversity = Mathf.Min(habitatDef?.DiversityFor(record.cellCount) ?? 1,
                Mathf.Max(0, Mathf.FloorToInt(record.carryingCapacity / Mathf.Max(0.5f, viablePopulation))));
            List<ThingDef> candidates = CompatibleCandidates(record);
            diversity = Mathf.Min(diversity, candidates.Count);
            var selected = record.species.Where(item => item.FishDef != null && candidates.Contains(item.FishDef) &&
                item.population >= 0.5f).Take(diversity).ToList();
            foreach (ThingDef fish in candidates)
            {
                if (selected.Count >= diversity) break;
                NaturalFishSpeciesPopulation item = record.species.FirstOrDefault(existing => existing.FishDef == fish);
                if (item == null)
                {
                    item = new NaturalFishSpeciesPopulation { fishDefName = fish.defName };
                    record.species.Add(item);
                }
                if (!selected.Contains(item)) selected.Add(item);
            }
            if (selected.Count == 0) return;
            float capacity = Mathf.Min(record.targetPopulation, record.carryingCapacity);
            float allocation = Mathf.Max(capacity, selected.Sum(item => item.population));
            float reserved = viablePopulation * selected.Count;
            float totalWeight = selected.Sum(item => PopulationWeight(item.FishDef, record.habitat));
            float assigned = 0f;
            for (int i = 0; i < selected.Count; i++)
            {
                NaturalFishSpeciesPopulation item = selected[i];
                float target = viablePopulation + Mathf.Max(0f, allocation - reserved) *
                    PopulationWeight(item.FishDef, record.habitat) / Mathf.Max(0.01f, totalWeight);
                if (i == selected.Count - 1) target = Mathf.Max(viablePopulation, allocation - assigned);
                item.population = target;
                assigned += item.population;
            }
        }

        private bool ImportEstablished(NaturalWaterPopulation record, IEnumerable<ThingDef> establishedSpecies, float establishedPopulation)
        {
            if (record == null) return false;
            List<ThingDef> imported = establishedSpecies?.Where(FishUtility.IsFish).Distinct().ToList() ?? new List<ThingDef>();
            if (imported.Count == 0) return false;
            bool allowNewSpecies = !record.initialPopulationComplete;
            bool firstImport = !record.species.Any();
            bool changed = false;
            float totalWeight = imported.Sum(fish => PopulationWeight(fish, record.habitat));
            for (int i = 0; i < imported.Count; i++)
            {
                NaturalFishSpeciesPopulation item = record.species.FirstOrDefault(existing => existing.FishDef == imported[i]);
                if (item == null)
                {
                    if (!allowNewSpecies) continue;
                    item = new NaturalFishSpeciesPopulation { fishDefName = imported[i].defName };
                    record.species.Add(item);
                    item.population = firstImport && establishedPopulation > 0f
                        ? establishedPopulation * PopulationWeight(imported[i], record.habitat) / Mathf.Max(0.01f, totalWeight)
                        : 0.5f;
                    changed = true;
                }
                if (!item.established)
                {
                    item.established = true;
                    changed = true;
                }
            }
            return changed;
        }

        private void ReconcileExternalPopulation(NaturalWaterPopulation record, WaterBody body)
        {
            if (record == null || body == null || !record.species.Any()) return;
            float total = record.TotalPopulation;
            float expectedScalar = Mathf.Min(body.MaxPopulation, total);
            if (body.Population >= expectedScalar - 0.01f) return;
            float remaining = Mathf.Max(0f, total - (expectedScalar - body.Population));
            float scale = total > 0f ? remaining / total : 0f;
            for (int i = 0; i < record.species.Count; i++) record.species[i].population *= scale;
        }

        private NaturalWaterPopulation MergeConnectedRecords(IntVec3 anchor, List<IntVec3> cells)
        {
            var cellSet = new HashSet<IntVec3>(cells);
            List<NaturalWaterPopulation> connected = populations.Where(record => cellSet.Contains(record.anchor)).ToList();
            NaturalWaterPopulation primary = connected.FirstOrDefault(record => record.anchor == anchor) ?? connected.FirstOrDefault();
            if (primary == null) return null;
            primary.anchor = anchor;
            for (int i = 0; i < connected.Count; i++)
            {
                NaturalWaterPopulation source = connected[i];
                if (source == primary) continue;
                for (int j = 0; j < source.species.Count; j++)
                {
                    NaturalFishSpeciesPopulation incoming = source.species[j];
                    NaturalFishSpeciesPopulation target = primary.species.FirstOrDefault(item => item.fishDefName == incoming.fishDefName);
                    if (target == null)
                    {
                        primary.species.Add(incoming);
                        continue;
                    }
                    target.population += incoming.population;
                    target.established |= incoming.established;
                }
                populations.Remove(source);
            }
            return primary;
        }

        private void GradualRebalance(NaturalWaterPopulation record, int tick)
        {
            record.lastBalanceTick = tick;
            FishPopulationHabitatDef habitatDef = HabitatDef(record.habitat);
            if (habitatDef == null) return;
            record.carryingCapacity = habitatDef?.CarryingCapacity(record.cellCount) ?? record.carryingCapacity;
            record.targetPopulation = record.carryingCapacity;
            int diversity = Mathf.Min(habitatDef?.DiversityFor(record.cellCount) ?? 1,
                Mathf.Max(0, Mathf.FloorToInt(record.carryingCapacity /
                    Mathf.Max(habitatDef.minimumViablePopulation, habitatDef.minimumBreedingPopulation))));
            if (AllowsNaturalMigration(record.habitat)) ApplyMigration(record, habitatDef, diversity);
            ApplyBreeding(record, habitatDef);
            ApplyNaturalMortality(record, habitatDef);
            float total = record.TotalPopulation;
            if (total > record.carryingCapacity && record.species.Any())
            {
                float reduction = Mathf.Min(total - record.carryingCapacity,
                    Mathf.Max(0.25f, total * habitatDef.rebalancePerDay));
                float scale = Mathf.Max(0f, (total - reduction) / total);
                for (int i = 0; i < record.species.Count; i++) record.species[i].population *= scale;
            }
            if (!AllowsNaturalMigration(record.habitat))
                for (int i = 0; i < record.species.Count; i++)
                    if (record.species[i].population < 0.5f) record.species[i].population = 0f;
            RefreshSummary(record);
            WaterBody body = map.waterBodyTracker?.WaterBodyAt(record.anchor);
            SynchronizeWaterBody(body, record);
        }

        private void ApplyBreeding(NaturalWaterPopulation record, FishPopulationHabitatDef habitatDef)
        {
            float expectedMortality = record.TotalPopulation * habitatDef.naturalMortalityPerDay;
            float available = Mathf.Max(0f, record.carryingCapacity - record.TotalPopulation + expectedMortality);
            if (available <= 0f || habitatDef.breedingPerDay <= 0f) return;
            float temperature = GenTemperature.GetTemperatureForCell(record.anchor, map);
            Season season = NormalizeSeason(GenLocalDate.Season(map));
            List<NaturalFishSpeciesPopulation> breeders = record.species.Where(item =>
                item.population >= habitatDef.minimumBreedingPopulation && item.FishDef != null &&
                Suitable(item.FishDef, record.habitat, map.Biome, temperature, season)).ToList();
            float potential = breeders.Sum(item => item.population * habitatDef.breedingPerDay * Suitability(item.FishDef, record.habitat));
            float growth = Mathf.Min(available, potential);
            if (growth <= 0f) return;
            for (int i = 0; i < breeders.Count; i++)
            {
                NaturalFishSpeciesPopulation item = breeders[i];
                float weight = item.population * habitatDef.breedingPerDay * Suitability(item.FishDef, record.habitat);
                item.population += growth * weight / Mathf.Max(0.01f, potential);
            }
        }

        private static void ApplyNaturalMortality(NaturalWaterPopulation record, FishPopulationHabitatDef habitatDef)
        {
            if (habitatDef.naturalMortalityPerDay <= 0f) return;
            for (int i = 0; i < record.species.Count; i++)
                record.species[i].population = Mathf.Max(0f,
                    record.species[i].population * (1f - habitatDef.naturalMortalityPerDay));
        }

        private void ApplyMigration(NaturalWaterPopulation record, FishPopulationHabitatDef habitatDef, int diversity)
        {
            if (habitatDef.migrationPerDay <= 0f || habitatDef.maximumMigrationPerDay <= 0f) return;
            float populationPressure = record.TotalPopulation / Mathf.Max(1f, record.carryingCapacity);
            float budget = Mathf.Min(habitatDef.maximumMigrationPerDay,
                record.carryingCapacity * habitatDef.migrationPerDay);
            float outbound = budget * Mathf.Clamp01((populationPressure - 0.8f) / 0.2f);
            if (outbound > 0f && record.TotalPopulation > 0f)
            {
                float scale = Mathf.Max(0f, (record.TotalPopulation - outbound) / record.TotalPopulation);
                for (int i = 0; i < record.species.Count; i++) record.species[i].population *= scale;
            }
            float incoming = budget * Mathf.Clamp01(1f - populationPressure);
            if (incoming <= 0f || diversity <= 0) return;
            List<ThingDef> candidates = CompatibleCandidates(record).Take(diversity).ToList();
            int present = record.species.Count(item => item.population >= 0.5f && candidates.Contains(item.FishDef));
            if (present < diversity)
            {
                ThingDef addition = candidates.FirstOrDefault(def =>
                    record.species.All(item => item.FishDef != def || item.population < 0.5f));
                if (addition != null)
                {
                    NaturalFishSpeciesPopulation item = record.species.FirstOrDefault(existing => existing.FishDef == addition);
                    if (item == null)
                    {
                        item = new NaturalFishSpeciesPopulation { fishDefName = addition.defName };
                        record.species.Add(item);
                    }
                }
            }
            List<NaturalFishSpeciesPopulation> receivers = record.species.Where(item => item.FishDef != null &&
                candidates.Contains(item.FishDef)).ToList();
            float totalWeight = receivers.Sum(item => PopulationWeight(item.FishDef, record.habitat) *
                (0.25f + RegionalSpeciesPressure(record, item.FishDef)));
            for (int i = 0; i < receivers.Count; i++)
            {
                NaturalFishSpeciesPopulation item = receivers[i];
                float weight = PopulationWeight(item.FishDef, record.habitat) *
                    (0.25f + RegionalSpeciesPressure(record, item.FishDef));
                item.population += incoming * weight / Mathf.Max(0.01f, totalWeight);
            }
        }

        private float RegionalSpeciesPressure(NaturalWaterPopulation record, ThingDef fishDef)
        {
            IEnumerable<NaturalWaterPopulation> regional = populations.Where(other => other != record &&
                AllowsNaturalMigration(other.habitat) && MigrationRegionCompatible(record.habitat, other.habitat));
            float capacity = regional.Sum(other => Mathf.Max(1f, other.carryingCapacity));
            if (capacity <= 0f) return 0f;
            return Mathf.Clamp01(regional.Sum(other => other.species.FirstOrDefault(item => item.FishDef == fishDef)?.population ?? 0f) / capacity);
        }

        private static bool AllowsNaturalMigration(NaturalWaterHabitat habitat)
        {
            return habitat == NaturalWaterHabitat.River || habitat == NaturalWaterHabitat.Coastal ||
                habitat == NaturalWaterHabitat.Ocean;
        }

        private static bool MigrationRegionCompatible(NaturalWaterHabitat first, NaturalWaterHabitat second)
        {
            if (first == NaturalWaterHabitat.River || second == NaturalWaterHabitat.River) return first == second;
            return first == NaturalWaterHabitat.Ocean && second == NaturalWaterHabitat.Ocean;
        }

        private List<ThingDef> CompatibleCandidates(NaturalWaterPopulation record)
        {
            float temperature = GenTemperature.GetTemperatureForCell(record.anchor, map);
            Season season = NormalizeSeason(GenLocalDate.Season(map));
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish)
                .Where(def => Suitable(def, record.habitat, map.Biome, temperature, season))
                .OrderBy(def => DeterministicRank(def, record))
                .ThenBy(def => def.defName).ToList();
        }

        private List<IntVec3> ConnectedCells(IntVec3 root, out WaterBody body)
        {
            body = map.waterBodyTracker?.WaterBodyAt(root);
            if (body?.cells != null && body.cells.Count > 0) return body.cells.Where(IsNaturalWater).ToList();
            return FloodFill(root);
        }

        private List<IntVec3> FloodFill(IntVec3 root)
        {
            var result = new List<IntVec3>();
            var seen = new HashSet<IntVec3>();
            var queue = new Queue<IntVec3>();
            seen.Add(root);
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                if (!IsNaturalWater(cell)) continue;
                result.Add(cell);
                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    IntVec3 next = cell + CardinalDirections[i];
                    if (next.InBounds(map) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return result;
        }

        private bool IsNaturalWater(IntVec3 cell)
        {
            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            return terrain?.IsWater == true && terrain.defName != "AF_Pond";
        }

        private NaturalWaterHabitat ClassifyHabitat(List<IntVec3> cells, WaterBody body)
        {
            string terrain = string.Join(" ", cells.Select(cell => map.terrainGrid.TerrainAt(cell)?.defName).Distinct()).ToLowerInvariant();
            string biome = (map.Biome?.defName ?? "").ToLowerInvariant();
            if (body?.waterBodyType == WaterBodyType.Saltwater || terrain.Contains("ocean"))
                return cells.Count >= (HabitatDef(NaturalWaterHabitat.Ocean)?.minimumCells ?? 800)
                    ? NaturalWaterHabitat.Ocean : NaturalWaterHabitat.Coastal;
            if (terrain.Contains("river") || terrain.Contains("watermoving")) return NaturalWaterHabitat.River;
            if (terrain.Contains("marsh") || terrain.Contains("swamp") || biome.Contains("swamp") || biome.Contains("marsh")) return NaturalWaterHabitat.Marsh;
            int pondMaximum = HabitatDef(NaturalWaterHabitat.Pond)?.pondMaximumCells ?? 120;
            return cells.Count <= pondMaximum ? NaturalWaterHabitat.Pond : NaturalWaterHabitat.Lake;
        }

        private bool Suitable(ThingDef fish, NaturalWaterHabitat habitat, BiomeDef biome, float temperature, Season season)
        {
            AquaticSpeciesExtension extension = fish.GetModExtension<AquaticSpeciesExtension>();
            if (extension?.compatibleHabitats?.Count > 0 && !extension.compatibleHabitats.Contains(habitat)) return false;
            if (extension?.compatibleBiomes?.Count > 0 && !extension.compatibleBiomes.Contains(biome)) return false;
            if (extension?.excludedBiomes?.Contains(biome) == true) return false;
            if ((extension?.compatibleBiomes?.Count ?? 0) == 0 && !AquaticSpeciesProfile.PopulationBiomeCompatible(fish, biome)) return false;
            if (extension?.populationSeasons?.Count > 0 && !extension.populationSeasons.Contains(season)) return false;
            float minimum = extension != null && extension.populationMinimumTemperature > -900f
                ? extension.populationMinimumTemperature : AquaticSpeciesProfile.For(fish).minimumTemperature;
            float maximum = extension != null && extension.populationMaximumTemperature > -900f
                ? extension.populationMaximumTemperature : AquaticSpeciesProfile.For(fish).maximumTemperature;
            if (temperature < minimum || temperature > maximum) return false;
            PondWaterKind water = AquaticSpeciesProfile.For(fish).waterKind;
            bool salt = habitat == NaturalWaterHabitat.Coastal || habitat == NaturalWaterHabitat.Ocean;
            return salt ? water != PondWaterKind.Freshwater : water != PondWaterKind.Saltwater;
        }

        private static float Suitability(ThingDef fish, NaturalWaterHabitat habitat)
        {
            FishHabitatPreference preference = fish.GetModExtension<AquaticSpeciesExtension>()?.habitatPreferences?
                .FirstOrDefault(item => item.habitat == habitat);
            return Mathf.Max(0.01f, preference?.suitability ?? 1f);
        }

        private static float PopulationWeight(ThingDef fish, NaturalWaterHabitat habitat)
        {
            AquaticSpeciesExtension extension = fish?.GetModExtension<AquaticSpeciesExtension>();
            float dependencyCommonality = AquaticSpeciesProfile.PopulationCommonality(fish);
            FishPopulationRarity rarity = extension != null ? extension.populationRarity :
                dependencyCommonality < 0.2f ? FishPopulationRarity.Rare :
                dependencyCommonality < 0.65f ? FishPopulationRarity.Uncommon : FishPopulationRarity.Common;
            float density = extension != null ? extension.populationDensity : Mathf.Clamp(dependencyCommonality, 0.1f, 2f);
            return Mathf.Max(0.01f, RarityWeight(rarity) * Mathf.Max(0.01f, density) * Suitability(fish, habitat));
        }

        private static float RarityWeight(FishPopulationRarity rarity)
        {
            return rarity == FishPopulationRarity.Rare ? 0.08f : rarity == FishPopulationRarity.Uncommon ? 0.32f : 1f;
        }

        private double DeterministicRank(ThingDef fish, NaturalWaterPopulation record)
        {
            float weight = Mathf.Max(0.001f, PopulationWeight(fish, record.habitat));
            double unit = Math.Max(0.000001, UnitHash(StableHash(WorldSeed, map.uniqueID, record.anchor.x,
                record.anchor.z, StableStringHash(fish.defName), 3571)));
            return -Math.Log(unit) / weight;
        }

        private static IntVec3 StableAnchor(IEnumerable<IntVec3> cells)
        {
            bool found = false;
            IntVec3 anchor = IntVec3.Invalid;
            foreach (IntVec3 cell in cells)
            {
                if (!found || cell.z < anchor.z || cell.z == anchor.z && cell.x < anchor.x)
                {
                    anchor = cell;
                    found = true;
                }
            }
            return anchor;
        }

        private void RefreshSummary(NaturalWaterPopulation record)
        {
            if (record == null) return;
            if (!summaryByPopulation.TryGetValue(record, out NaturalFishPopulationSummary summary))
            {
                summary = new NaturalFishPopulationSummary();
                summaryByPopulation[record] = summary;
            }
            summary.totalPopulation = 0f;
            summary.carryingCapacity = record.carryingCapacity;
            summary.presentSpecies.Clear();
            summary.estimatedPopulation.Clear();
            summary.displayLabels.Clear();
            var lines = new List<string>();
            List<NaturalFishSpeciesPopulation> present = record.species.Where(item =>
                item.FishDef != null && item.population >= 0.5f).OrderByDescending(item => item.population)
                .ThenBy(item => item.FishDef.label).ToList();
            for (int i = 0; i < record.species.Count; i++) summary.totalPopulation += Mathf.Max(0f, record.species[i].population);
            lines.Add(("Fish".Translate().CapitalizeFirst() + $" ({summary.totalPopulation:F0}/{summary.carryingCapacity})").ToString());
            for (int i = 0; i < present.Count; i++)
            {
                ThingDef fish = present[i].FishDef;
                int estimate = Mathf.RoundToInt(present[i].population);
                summary.presentSpecies.Add(fish);
                summary.estimatedPopulation[fish] = estimate;
                string displayLabel = fish.LabelCap.ToString() + $": ~{estimate}";
                summary.displayLabels[fish] = displayLabel;
                lines.Add("  " + displayLabel);
            }
            summary.readoutText = string.Join("\n", lines);
            preparedViewsDirty = true;
            AquacultureSnapshotCache.Invalidate();
        }

        private static Season NormalizeSeason(Season season)
        {
            if (season == Season.PermanentSummer) return Season.Summer;
            if (season == Season.PermanentWinter) return Season.Winter;
            return season;
        }

        private static FishPopulationHabitatDef HabitatDef(NaturalWaterHabitat habitat)
        {
            return DefDatabase<FishPopulationHabitatDef>.AllDefsListForReading.FirstOrDefault(def => def.habitat == habitat);
        }

        private static void SynchronizeWaterBody(WaterBody body, NaturalWaterPopulation record)
        {
            if (body == null || record == null) return;
            body.Population = Mathf.Min(body.MaxPopulation, record.TotalPopulation);
        }

        private string WorldSeed => Find.World?.info?.seedString ?? "unknown-world";

        private static int StableStringHash(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < (value?.Length ?? 0); i++) hash = (hash ^ value[i]) * 16777619;
                return hash;
            }
        }

        private static int StableHash(string seed, params int[] values)
        {
            unchecked
            {
                int hash = StableStringHash(seed);
                for (int i = 0; i < values.Length; i++) hash = (hash ^ values[i]) * 16777619;
                return hash;
            }
        }

        private static float UnitHash(int hash)
        {
            unchecked
            {
                uint value = (uint)hash;
                value ^= value >> 16;
                value *= 0x7feb352d;
                value ^= value >> 15;
                value *= 0x846ca68b;
                value ^= value >> 16;
                return (float)((value + 1d) / (uint.MaxValue + 2d));
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain))]
    public static class NaturalFishPopulationTerrainPatch
    {
        public static void Prefix(TerrainGrid __instance, IntVec3 __0, Map ___map, out TerrainDef __state)
        {
            __state = ___map != null && __0.InBounds(___map) ? __instance.TerrainAt(__0) : null;
        }

        public static void Postfix(TerrainDef __1, Map ___map, TerrainDef __state)
        {
            if (___map == null || __state == __1 || (__state?.IsWater != true && __1?.IsWater != true)) return;
            ___map.GetComponent<NaturalFishPopulationMapComponent>()?.InvalidateTopology();
        }
    }

    [HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
    public static class NaturalFishPopulationMouseoverPatch
    {
        private const string VanillaFishFormat = "{0}: {1} ({2:F0}/{3:F0}){4}";

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> result = instructions.ToList();
            MethodInfo vanillaLabel = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label),
                new[] { typeof(Rect), typeof(string) });
            MethodInfo suppressLabel = AccessTools.Method(typeof(NaturalFishPopulationMouseoverPatch),
                nameof(SuppressVanillaFishLabel));
            MethodInfo colorWhite = AccessTools.PropertyGetter(typeof(Color), nameof(Color.white));
            MethodInfo drawReadout = AccessTools.Method(typeof(NaturalFishPopulationMouseoverPatch),
                nameof(DrawPopulationReadout));
            bool foundFormat = false;
            bool patched = false;
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].opcode == System.Reflection.Emit.OpCodes.Ldstr &&
                    Equals(result[i].operand, VanillaFishFormat)) foundFormat = true;
                if (!foundFormat || !result[i].Calls(vanillaLabel)) continue;
                result[i].operand = suppressLabel;
                patched = true;
                break;
            }
            int finalColor = result.FindLastIndex(instruction => instruction.Calls(colorWhite));
            if (finalColor >= 0)
            {
                result.InsertRange(finalColor, new[]
                {
                    new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloc_1),
                    new CodeInstruction(System.Reflection.Emit.OpCodes.Call, drawReadout)
                });
            }
            else patched = false;
            if (!patched) Log.Error("[Aquaculture - Fishing] Could not patch the natural-water fish mouseover label.");
            return result;
        }

        public static void SuppressVanillaFishLabel(Rect rect, string vanillaLabel) { }

        public static void DrawPopulationReadout(float yOffset)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map) || map.GetComponent<NaturalFishPopulationMapComponent>()?
                .TryGetPreparedSummary(cell, out NaturalFishPopulationSummary summary) != true || summary.presentSpecies.Count == 0) return;
            bool vanillaFishSlot = ModsConfig.OdysseyActive &&
                map.waterBodyTracker?.TryGetWaterBodyAt(cell, out WaterBody body) == true && body.HasFish;
            if (vanillaFishSlot) yOffset -= 19f;
            int lineCount = summary.presentSpecies.Count + 1;
            Rect rect = new Rect(15f, UI.screenHeight - 65f - yOffset - summary.presentSpecies.Count * 19f, 999f, lineCount * 19f);
            Widgets.Label(rect, summary.readoutText);
        }
    }

    [HarmonyPatch(typeof(ITab_Fishing), "FillTab")]
    public static class NaturalFishPopulationFishingTabPatch
    {
        private static readonly FieldInfo SizeField = AccessTools.Field(typeof(InspectTabBase), "size");
        private static NaturalFishPopulationSummary activeSummary;
        private static int sizedSpeciesCount = -1;

        public static NaturalFishPopulationSummary ActiveSummary => activeSummary;

        public static NaturalFishPopulationSummary PreparedSummaryFor(WaterBody body)
        {
            Map map = Find.CurrentMap;
            if (map != null && body != null && map.GetComponent<NaturalFishPopulationMapComponent>()?
                .TryGetPreparedSummary(body.rootCell, out NaturalFishPopulationSummary summary) == true) return summary;
            return null;
        }

        public static float PreparedPopulation(WaterBody body) => (activeSummary ?? PreparedSummaryFor(body))?.totalPopulation ?? body?.Population ?? 0f;
        public static float PreparedCapacity(WaterBody body) => (activeSummary ?? PreparedSummaryFor(body))?.carryingCapacity ?? body?.MaxPopulation ?? 0f;
        public static IEnumerable<ThingDef> PreparedCommonSpecies(WaterBody body) =>
            (activeSummary ?? PreparedSummaryFor(body))?.presentSpecies ?? body?.CommonFishIncludingExtras ?? Enumerable.Empty<ThingDef>();
        public static IEnumerable<ThingDef> PreparedUncommonSpecies(WaterBody body) =>
            (activeSummary ?? PreparedSummaryFor(body)) != null ? Enumerable.Empty<ThingDef>() : body?.UncommonFish ?? Enumerable.Empty<ThingDef>();

        public static void Prefix(ITab_Fishing __instance)
        {
            Zone_Fishing zone = __instance?.SelZone;
            WaterBody body = zone?.CellCount > 0 ? zone.Cells[0].GetWaterBody(zone.Map) : null;
            activeSummary = PreparedSummaryFor(body);
            int speciesCount = activeSummary?.presentSpecies.Count ?? -1;
            if (speciesCount == sizedSpeciesCount) return;
            sizedSpeciesCount = speciesCount;
            float requiredHeight = speciesCount < 0 ? 450f : 360f + speciesCount * (Text.LineHeight + 2f);
            SizeField?.SetValue(__instance, new Vector2(300f, Mathf.Max(450f, requiredHeight)));
        }

        public static void Postfix() => activeSummary = null;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo population = AccessTools.PropertyGetter(typeof(WaterBody), nameof(WaterBody.Population));
            MethodInfo capacity = AccessTools.PropertyGetter(typeof(WaterBody), nameof(WaterBody.MaxPopulation));
            MethodInfo common = AccessTools.PropertyGetter(typeof(WaterBody), nameof(WaterBody.CommonFishIncludingExtras));
            MethodInfo uncommon = AccessTools.PropertyGetter(typeof(WaterBody), nameof(WaterBody.UncommonFish));
            MethodInfo preparedPopulation = AccessTools.Method(typeof(NaturalFishPopulationFishingTabPatch), nameof(PreparedPopulation));
            MethodInfo preparedCapacity = AccessTools.Method(typeof(NaturalFishPopulationFishingTabPatch), nameof(PreparedCapacity));
            MethodInfo preparedCommon = AccessTools.Method(typeof(NaturalFishPopulationFishingTabPatch), nameof(PreparedCommonSpecies));
            MethodInfo preparedUncommon = AccessTools.Method(typeof(NaturalFishPopulationFishingTabPatch), nameof(PreparedUncommonSpecies));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(population)) { instruction.opcode = System.Reflection.Emit.OpCodes.Call; instruction.operand = preparedPopulation; replaced++; }
                else if (instruction.Calls(capacity)) { instruction.opcode = System.Reflection.Emit.OpCodes.Call; instruction.operand = preparedCapacity; replaced++; }
                else if (instruction.Calls(common)) { instruction.opcode = System.Reflection.Emit.OpCodes.Call; instruction.operand = preparedCommon; replaced++; }
                else if (instruction.Calls(uncommon)) { instruction.opcode = System.Reflection.Emit.OpCodes.Call; instruction.operand = preparedUncommon; replaced++; }
                yield return instruction;
            }
            if (replaced != 4) Log.Error($"[Aquaculture - Fishing] Fishing tab population patch replaced {replaced}/4 data reads.");
        }
    }

    [HarmonyPatch(typeof(ITab_Fishing), "ListFish")]
    public static class NaturalFishPopulationFishingRowPatch
    {
        public static bool Prefix(ITab_Fishing __instance, ThingDef fish, Listing_Standard listing, bool uncommon)
        {
            NaturalFishPopulationSummary summary = NaturalFishPopulationFishingTabPatch.ActiveSummary;
            if (summary == null)
            {
                Zone_Fishing zone = __instance?.SelZone;
                WaterBody body = zone?.CellCount > 0 ? zone.Cells[0].GetWaterBody(zone.Map) : null;
                summary = NaturalFishPopulationFishingTabPatch.PreparedSummaryFor(body);
            }
            if (summary == null) return true;
            Rect rect = listing.GetRect(Text.LineHeight);
            Rect infoRect = new Rect(rect.x, rect.y, Text.LineHeight, Text.LineHeight);
            Widgets.InfoCardButton(infoRect, fish);
            Rect iconRect = new Rect(infoRect.xMax + 4f, rect.y, rect.height, rect.height);
            Widgets.ThingIcon(iconRect, fish);
            Rect labelRect = new Rect(iconRect.xMax + 4f, rect.y, rect.xMax - iconRect.xMax - 4f, rect.height);
            string label = summary.DisplayLabel(fish);
            Widgets.Label(labelRect, label.Truncate(labelRect.width));
            listing.Gap(2f);
            return false;
        }
    }

    [HarmonyPatch(typeof(Zone_Fishing), "get_OverTargetPopulation")]
    public static class NaturalFishPopulationFishingThresholdPatch
    {
        public static bool Prefix(Zone_Fishing __instance, ref bool __result)
        {
            if (__instance == null || __instance.CellCount <= 0) return true;
            NaturalFishPopulationMapComponent component = __instance.Map?.GetComponent<NaturalFishPopulationMapComponent>();
            if (component?.TryGetPreparedSummary(__instance.Cells[0], out NaturalFishPopulationSummary summary) != true) return true;
            __result = summary.totalPopulation / Mathf.Max(1f, summary.carryingCapacity) > __instance.targetPopulationPct;
            return false;
        }
    }
}
