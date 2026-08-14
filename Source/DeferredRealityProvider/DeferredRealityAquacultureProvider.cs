using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AquacultureFishing;
using DeferredReality.API;
using DeferredReality.Simulation;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeferredReality.Aquaculture
{
    /// <summary>
    /// Deferred Reality owns latent natural-water populations. Aquaculture remains the owner of active maps,
    /// Things, player actions, and the legacy projection. This provider is deliberately conservative: an invalid
    /// identity or unavailable Def leaves the legacy record intact and pauses the corresponding DRF work.
    /// </summary>
    public sealed class AquacultureRealityProvider : IRealityProvider, IRegionDescriptorProvider,
        IRealityMapIdentityProvider, IAnchorProvider, IRealityProcessProvider, IPopulationProvider,
        IRealityDiagnosticsProvider, IRealityExactlyOnceProvider
    {
        public const string ProviderId = DeferredRealityProviderRules.ProviderId;
        public static AquacultureRealityProvider Current { get; private set; }

        private readonly RealityProviderRegistration registration;
        private DeferredRealityWorldComponent world;
        private bool compatible;
        private int mappedMaps;
        private int migratedPopulations;
        private int skippedPopulations;
        private int processedEvents;
        private int duplicateEvents;
        private string lastError;

        public AquacultureRealityProvider()
        {
            registration = new RealityProviderRegistration
            {
                providerId = ProviderId,
                semanticApiVersion = DeferredRealityProviderRules.SemanticApiVersion,
                order = 200,
                dependencies = new List<string> { "lan.deferredreality.framework" },
                capabilities = RealityProviderCapability.Regions | RealityProviderCapability.Populations |
                    RealityProviderCapability.Processes |
                    RealityProviderCapability.Diagnostics | RealityProviderCapability.Anchors,
                supportedFidelities = RealityFidelityMask.Statistical | RealityFidelityMask.Materialized,
                defaultFidelity = RealityFidelity.Statistical,
                operationRetentionTicks = -1,
                cancelledProcessRetentionTicks = -1,
                displayName = "Aquaculture natural-water populations"
            };
        }

        public RealityProviderRegistration Registration => registration;

        public IEnumerable<RealityRegionDescriptor> DescribeRegions(RealityProviderContext context)
        {
            foreach (Map map in Find.Maps?.Where(item => item != null).OrderBy(item => item.Tile).ThenBy(item => item.uniqueID)
                ?? Enumerable.Empty<Map>())
            {
                if (map.GetComponent<NaturalFishPopulationMapComponent>() == null || map.Parent == null) continue;
                RealityRegionId region = RegionIdentityFor(map);
                yield return new RealityRegionDescriptor
                {
                    regionId = region.ToString(),
                    label = "Aquaculture natural water " + map.Tile,
                    fidelity = RealityFidelity.Materialized,
                    authority = RealityRegionAuthority.LiveProjection,
                    stableSeed = RealityDeterminism.StableHash(region.ToString()),
                    createdTick = context?.Now ?? 0,
                    lastUpdateTick = context?.Now ?? 0,
                    projectionMapUniqueId = map.uniqueID,
                    lastKnownWorldTile = (int)map.Tile
                };
            }
        }

        public bool TryClaimMap(Map map, out RealityMapIdentityClaim claim)
        {
            claim = null;
            if (map == null || !map.Tile.Valid || map.Parent == null ||
                map.GetComponent<NaturalFishPopulationMapComponent>() == null) return false;
            RealityRegionId region = RegionIdentityFor(map);
            claim = new RealityMapIdentityClaim
            {
                providerId = ProviderId,
                regionId = region,
                identityKey = DeferredRealityProviderRules.MapAlias(ProviderId, map.uniqueID, (int)map.Tile)
            };
            return true;
        }

        public bool ValidateAnchor(RealityAnchorRecord anchor, IList<RealityVeto> vetoes)
        {
            bool valid = anchor != null && anchor.providerId == ProviderId && anchor.typeId == "natural-water-body" &&
                !string.IsNullOrEmpty(anchor.regionId) && !string.IsNullOrEmpty(anchor.anchorId) &&
                Enum.TryParse(anchor.providerPayload, true, out NaturalWaterHabitat habitat) &&
                anchor.lifecycle != RealityAnchorLifecycle.Retired;
            if (!valid) AddIssue(vetoes, "anchor.invalid", "Natural-water anchor identity is invalid.");
            return valid;
        }

        public void OnAnchorMaterialized(RealityProviderContext context, RealityAnchorRecord anchor)
        {
        }

        public void OnRegistered(RealityProviderContext context)
        {
            if (context?.World == null) return;
            world = context.World;
            compatible = true;
            context.DeclareExactlyOnceDomain("population-operation", DeferredRealityProviderRules.SequenceDomain,
                true, "Operation markers are retained durably; no cursor compaction is claimed.");
            if (!MigrateAllMaps())
            {
                compatible = false;
                lastError = lastError ?? "Natural-water provider initialization did not complete.";
                return;
            }
            Current = this;
            AquacultureEventRouter.Subscribe(HandleAquacultureEvent);
        }

        public RealityFidelityContract DescribeFidelity(RealityProviderContext context, RealityRegionSnapshot region)
        {
            return new RealityFidelityContract
            {
                currentFidelity = region?.fidelity ?? registration.defaultFidelity,
                supportedFidelities = registration.supportedFidelities,
                transitions = new List<RealityFidelityTransitionRule>
                {
                    new RealityFidelityTransitionRule
                    {
                        fromFidelity = RealityFidelity.Statistical,
                        toFidelity = RealityFidelity.Materialized,
                        mechanism = RealityFidelityTransitionMechanism.Materialization
                    },
                    new RealityFidelityTransitionRule
                    {
                        fromFidelity = RealityFidelity.Materialized,
                        toFidelity = RealityFidelity.Statistical,
                        mechanism = RealityFidelityTransitionMechanism.Compression
                    }
                }
            };
        }

        public RealityProcessFidelityPolicy DescribeProcessFidelity(RealityProcessRecord process,
            RealityRegionSnapshot region)
        {
            return new RealityProcessFidelityPolicy
            {
                legalFidelities = RealityFidelityMask.Statistical,
                runsWhileLiveProjection = false,
                mayRequestEscalation = false
            };
        }

        public bool CanTransitionFidelity(RealityFidelityTransitionRequest request, IList<RealityVeto> vetoes)
        {
            if (request == null || request.providerId != ProviderId)
            {
                AddIssue(vetoes, "fidelity.ownership", "The fidelity transition is not owned by Aquaculture.");
                return false;
            }

            if (request.fromFidelity == request.toFidelity) return true;

            // Map materialization and compression are transactional framework-owned operations. This
            // provider supplies the contract for those edges but does not mutate projection state here.
            AddIssue(vetoes, "fidelity.framework-transition",
                "Natural-water fidelity transitions are owned by the framework projection services.");
            return false;
        }

        public void OnFidelityChanged(RealityProviderContext context, RealityRegionSnapshot region,
            RealityFidelity previous, RealityFidelity current)
        {
            // Projection services synchronize the active map at their transaction boundary. No provider
            // mutation is required for the detached fidelity notification.
        }

        public bool TryDescribeExactlyOnceDomain(string kind, string domainId, out RealityExactlyOnceDomain domain)
        {
            domain = null;
            if (kind != "population-operation" || domainId != DeferredRealityProviderRules.SequenceDomain) return false;
            domain = new RealityExactlyOnceDomain
            {
                kind = kind,
                domainId = domainId,
                allowGaps = true,
                proof = "Stable operation IDs and durable applied-operation markers are retained without compaction."
            };
            return true;
        }

        private bool MigrateAllMaps()
        {
            if (!compatible || world == null || !UnityData.IsInMainThread) return false;
            List<Map> maps = Find.Maps?.Where(map => map != null).OrderBy(map => map.Tile).ThenBy(map => map.uniqueID).ToList()
                ?? new List<Map>();
            bool success = true;
            for (int i = 0; i < maps.Count; i++)
                success &= MigrateMap(maps[i]);
            success &= RegisterCompatibleTopology();
            return success;
        }

        private bool MigrateMap(Map map)
        {
            if (map == null || !map.Tile.Valid) return false;
            NaturalFishPopulationMapComponent natural = map.GetComponent<NaturalFishPopulationMapComponent>();
            if (natural == null) return true;
            if (!natural.IsInitializedForDeferredReality) return true;
            RealityRegionId region = world.RegisterMap(map);
            if (!region.IsValid)
            {
                lastError = "Natural-water map could not claim a stable Deferred Reality region.";
                return false;
            }

            List<NaturalWaterPopulation> records = (natural.Populations ?? Array.Empty<NaturalWaterPopulation>())
                .Where(record => record != null).OrderBy(record => record.anchor.x).ThenBy(record => record.anchor.z)
                .ThenBy(record => record.deferredRealityStableId, StringComparer.Ordinal).ToList();
            for (int i = 0; i < records.Count; i++)
            {
                if (!MigrateWater(map, region, records[i])) return false;
            }
            mappedMaps++;
            SyncMap(map, natural, region);
            return true;
        }

        private bool MigrateWater(Map map, RealityRegionId region, NaturalWaterPopulation water)
        {
            if (water == null || water.cellCount <= 0 || water.carryingCapacity < 0) return true;
            string waterId = water.deferredRealityStableId;
            if (string.IsNullOrEmpty(waterId))
            {
                waterId = DeferredRealityProviderRules.WaterBodyId(region.ToString(), water.habitat.ToString(),
                    water.anchor.x, water.anchor.z, water.cellCount);
                water.deferredRealityStableId = waterId;
            }
            string anchorId = DeferredRealityProviderRules.AnchorId(region.ToString(), waterId);
            if (!world.UpsertAnchor(new RealityAnchorRecord
            {
                anchorId = anchorId,
                providerId = ProviderId,
                typeId = "natural-water-body",
                regionId = region.ToString(),
                lastKnownTick = world.Now,
                lastKnownLocation = new RealityLocation { x = water.anchor.x, z = water.anchor.z, areaId = waterId },
                importance = 1,
                providerPayload = water.habitat.ToString()
            })) return false;

            List<NaturalFishSpeciesPopulation> species = (water.species ?? new List<NaturalFishSpeciesPopulation>())
                .Where(item => item != null && !string.IsNullOrEmpty(item.fishDefName))
                .OrderBy(item => item.fishDefName, StringComparer.Ordinal).ToList();
            for (int i = 0; i < species.Count; i++)
            {
                NaturalFishSpeciesPopulation item = species[i];
                ThingDef fish = DefDatabase<ThingDef>.GetNamedSilentFail(item.fishDefName);
                if (fish == null)
                {
                    skippedPopulations++;
                    continue;
                }
                string populationId = DeferredRealityProviderRules.PopulationId(region.ToString(), waterId, item.fishDefName);
                if (!world.TryGetPopulationRecord(populationId, out RealityPopulationRecord existing))
                {
                    if (world.PopulationSnapshots(providerId: ProviderId,
                        kind: DeferredRealityProviderRules.NaturalWaterKind).Count >=
                        DeferredRealityProviderRules.MaximumDynamicRecords)
                    {
                        lastError = "Natural-water population record limit reached.";
                        return false;
                    }
                    float amount = DeferredRealityProviderRules.NormalizeNonNegative(item.population, 0f, water.carryingCapacity);
                    float capacity = Math.Max(0f, water.carryingCapacity);
                    if (!world.UpsertPopulation(new RealityPopulationRecord
                    {
                        populationId = populationId,
                        providerId = ProviderId,
                        kind = DeferredRealityProviderRules.NaturalWaterKind,
                        subjectId = DeferredRealityProviderRules.PopulationSubject(waterId, item.fishDefName),
                        regionId = region.ToString(),
                        amount = amount,
                        uncertainty = Math.Max(0.5f, Mathf.Sqrt(Math.Max(0f, amount))),
                        carryingCapacity = capacity,
                        demographicPayload = PopulationPayload(water.habitat, BreedingFloor(water.habitat)),
                        habitatSuitability = 1f,
                        migrationAllowed = DeferredRealityProviderRules.IsOpenHabitat(water.habitat.ToString()),
                        established = item.established || amount > 0f,
                        extinct = amount <= 0f,
                        lastUpdateTick = world.Now,
                        anchoredMemberIds = new List<string> { anchorId }
                    })) return false;
                    migratedPopulations++;
                }
                else if (!DeferredRealityProviderRules.IsValidPopulation(existing.populationId, existing.providerId,
                    existing.regionId, existing.subjectId, existing.amount, existing.carryingCapacity) ||
                    existing.regionId != region.ToString() || existing.subjectId !=
                    DeferredRealityProviderRules.PopulationSubject(waterId, item.fishDefName))
                {
                    lastError = "Existing Deferred Reality population identity does not match the natural-water record.";
                    return false;
                }
                if (!EnsureProcess(region, waterId, item.fishDefName, water.habitat)) return false;
                if (!EnsureConstraint(region, populationId, water, item.fishDefName)) return false;
            }
            return true;
        }

        private bool EnsureConstraint(RealityRegionId region, string populationId, NaturalWaterPopulation water,
            string fishDefName)
        {
            return world.AddConstraint(new RealityConstraint
            {
                constraintId = DeferredRealityProviderRules.ConstraintId(region.ToString(), populationId,
                    "breeding-floor"),
                providerId = ProviderId,
                typeId = "natural-water-breeding-floor",
                regionId = region.ToString(),
                createdTick = world.Now,
                validFromTick = world.Now,
                certainty = 1f,
                source = ProviderId,
                priority = 20,
                conflictDomainKeys = new List<string> { "natural-water", fishDefName ?? string.Empty },
                affectedPopulationIds = new List<string> { populationId },
                payload = PopulationPayload(water.habitat, BreedingFloor(water.habitat))
            });
        }

        private bool RegisterCompatibleTopology()
        {
            IReadOnlyList<RealityAnchorSnapshot> anchors = world.AnchorSnapshots(providerId: ProviderId)
                .OrderBy(item => item?.record?.anchorId, StringComparer.Ordinal).ToList();
            for (int i = 0; i < anchors.Count; i++)
            {
                RealityAnchorRecord from = anchors[i]?.record;
                if (!TryHabitat(from, out NaturalWaterHabitat fromHabitat)) continue;
                for (int j = i + 1; j < anchors.Count; j++)
                {
                    RealityAnchorRecord to = anchors[j]?.record;
                    if (!TryHabitat(to, out NaturalWaterHabitat toHabitat) ||
                        !DeferredRealityProviderRules.AreCompatibleHabitats(fromHabitat.ToString(), toHabitat.ToString()) ||
                        string.Equals(from.regionId, to.regionId, StringComparison.Ordinal)) continue;
                    string linkBase = DeferredRealityProviderRules.Stable("connection", ProviderId,
                        from.anchorId, to.anchorId);
                    if (!RealityRegionId.TryParse(from.regionId, out RealityRegionId fromRegion) ||
                        !RealityRegionId.TryParse(to.regionId, out RealityRegionId toRegion)) continue;
                    if (!world.UpsertConnection(new RealityRegionConnection
                    {
                        connectionId = RealityRegionConnection.StableId(fromRegion, toRegion,
                            RealityRegionConnectionDirection.Directed, "natural-water-migration", ProviderId,
                            linkBase + ":forward"),
                        destinationRegionId = to.regionId,
                        sourceRegionId = from.regionId,
                        direction = RealityRegionConnectionDirection.Directed,
                        kind = "natural-water-migration",
                        traversalCost = 1f,
                        ownerNamespace = ProviderId,
                        identityKey = linkBase + ":forward",
                        providerPayload = fromHabitat.ToString()
                    }) || !world.UpsertConnection(new RealityRegionConnection
                    {
                        connectionId = RealityRegionConnection.StableId(toRegion, fromRegion,
                            RealityRegionConnectionDirection.Directed, "natural-water-migration", ProviderId,
                            linkBase + ":reverse"),
                        sourceRegionId = to.regionId,
                        destinationRegionId = from.regionId,
                        direction = RealityRegionConnectionDirection.Directed,
                        kind = "natural-water-migration",
                        traversalCost = 1f,
                        ownerNamespace = ProviderId,
                        identityKey = linkBase + ":reverse",
                        providerPayload = toHabitat.ToString()
                    })) return false;
                }
            }
            return true;
        }

        private static bool TryHabitat(RealityAnchorRecord anchor, out NaturalWaterHabitat habitat)
        {
            habitat = default(NaturalWaterHabitat);
            return anchor != null && Enum.TryParse(anchor.providerPayload, true, out habitat) &&
                Enum.IsDefined(typeof(NaturalWaterHabitat), habitat);
        }

        private bool EnsureProcess(RealityRegionId region, string waterId, string fishDefName, NaturalWaterHabitat habitat)
        {
            string populationId = DeferredRealityProviderRules.PopulationId(region.ToString(), waterId, fishDefName);
            string processId = DeferredRealityProviderRules.ProcessId(populationId);
            RealityProcessSnapshot existing = world.ProcessSnapshots().FirstOrDefault(item =>
                item?.record?.processId == processId);
            if (existing?.record != null && existing.record.providerId == ProviderId &&
                existing.record.kind == RealityProcessKind.ProviderDefined) return true;
            return world.ScheduleProcess(new RealityProcessRecord
            {
                processId = processId,
                providerId = ProviderId,
                kind = RealityProcessKind.ProviderDefined,
                regionId = region.ToString(),
                nextDueTick = world.Now + UpdateIntervalTicks(habitat),
                lastExecutionTick = world.Now,
                intervalTicks = UpdateIntervalTicks(habitat),
                priority = 20,
                payload = ProcessPayload(waterId, fishDefName, habitat)
            });
        }

        public bool CanExecute(RealityProcessRecord process, RealityProcessExecution execution, IList<RealityVeto> vetoes)
        {
            if (!compatible || process == null || execution == null || process.providerId != ProviderId ||
                process.kind != RealityProcessKind.ProviderDefined)
            {
                AddIssue(vetoes, "process.invalid", "Natural-water process identity is invalid.");
                return false;
            }
            if (!TryPopulationFromPayload(process, out string populationId, out _))
            {
                AddIssue(vetoes, "process.payload", "Natural-water process payload is invalid.");
                return false;
            }
            if (!world.TryGetPopulationRecord(populationId, out RealityPopulationRecord record) ||
                !DeferredRealityProviderRules.IsValidPopulation(record.populationId, record.providerId, record.regionId,
                    record.subjectId, record.amount, record.carryingCapacity))
            {
                AddIssue(vetoes, "population.invalid", "Natural-water population is missing or invalid.");
                return false;
            }
            return true;
        }

        public RealityProcessResult Execute(RealityProcessRecord process, RealityProcessExecution execution)
        {
            if (!TryPopulationFromPayload(process, out string populationId, out ProcessParts parts))
                return new RealityProcessResult { succeeded = false, pause = true, error = "Invalid process payload." };
            if (!world.TryGetPopulationRecord(populationId, out RealityPopulationRecord record))
                return new RealityProcessResult { succeeded = false, pause = true, error = "Population record is unavailable." };

            long elapsed = Math.Max(0L, Math.Min(execution.elapsedTicks, 30L * 60000L));
            float next = DeferredRealityProviderRules.AdvancePopulation(record.amount, record.carryingCapacity, elapsed,
                record.habitatSuitability);
            float variation = execution.random == null ? 1f : execution.random.NextFloat(0.995f, 1.005f);
            next = Mathf.Clamp(DeferredRealityProviderRules.NormalizeNonNegative(next * variation), 0f,
                Math.Max(0f, record.carryingCapacity));
            float delta = next - record.amount;
            string operationId = DeferredRealityProviderRules.OperationId("demography", process.regionId,
                parts.waterId, parts.fishDefName, process.processId + ":" + execution.executionCount.ToString(CultureInfo.InvariantCulture));
            RealityPopulationMutationResult result = RealityPopulationService.ReproduceOrDie(world, populationId,
                Math.Max(0f, delta), Math.Max(0f, -delta), operationId, execution.toTick, ProviderId);
            if (!result.succeeded)
                return new RealityProcessResult { succeeded = false, pause = true, error = result.error };
            TryRegionalMigration(execution.executionCount, process.processId);
            return new RealityProcessResult
            {
                succeeded = true,
                nextDelayTicks = UpdateIntervalTicks(parts.habitat),
                analyticalSteps = Math.Max(1, execution.boundedStepCount)
            };
        }

        public bool CanChangePopulation(RealityPopulationRecord population, string operation, IList<RealityVeto> vetoes)
        {
            if (!compatible || population == null || population.providerId != ProviderId ||
                population.kind != DeferredRealityProviderRules.NaturalWaterKind ||
                !DeferredRealityProviderRules.IsValidPopulation(population.populationId, population.providerId,
                    population.regionId, population.subjectId, population.amount, population.carryingCapacity))
            {
                AddIssue(vetoes, "population.ownership", "Population is not a valid Aquaculture-owned natural-water record.");
                return false;
            }
            if (operation == "transfer" && !population.migrationAllowed)
            {
                AddIssue(vetoes, "migration.closed", "Closed water cannot migrate populations.");
                return false;
            }
            return true;
        }

        public void ReconcileActiveMap(RealityProviderContext context, RealityPopulationRecord population, string payload)
        {
            // The framework has already committed the exact-once mutation. Projection is deliberately deferred to
            // the next map sync so a callback failure cannot roll back or duplicate an authoritative population change.
        }

        private void TryRegionalMigration(int executionCount, string processId)
        {
            IReadOnlyList<RealityPopulationSnapshot> populations = world.PopulationSnapshots(providerId: ProviderId,
                kind: DeferredRealityProviderRules.NaturalWaterKind);
            var processParts = world.ProcessSnapshots().Where(item => item?.record?.providerId == ProviderId &&
                item.record.kind == RealityProcessKind.ProviderDefined).Select(item =>
            {
                TryPopulationFromPayload(item.record, out string id, out ProcessParts parts);
                return new { id, parts };
            }).Where(item => !string.IsNullOrEmpty(item.id)).OrderBy(item => item.id, StringComparer.Ordinal)
                .ThenBy(item => item.parts.habitat).GroupBy(item => item.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().parts, StringComparer.Ordinal);

            foreach (RealityPopulationSnapshot destinationSnapshot in populations.OrderBy(item => item.record.populationId,
                StringComparer.Ordinal))
            {
                RealityPopulationRecord destination = destinationSnapshot?.record;
                if (destination == null || !processParts.TryGetValue(destination.populationId, out ProcessParts destinationParts) ||
                    !DeferredRealityProviderRules.IsOpenHabitat(destinationParts.habitat.ToString()) ||
                    !DeferredRealityProviderRules.IsFinite(destination.amount)) continue;
                float floor = BreedingFloor(destinationParts.habitat);
                string destinationSpecies = SpeciesFromSubject(destination.subjectId);
                if (string.IsNullOrEmpty(destinationSpecies) || destination.amount > floor) continue;

                RealityPopulationRecord source = populations.Select(item => item?.record).Where(item => item != null &&
                    item.populationId != destination.populationId &&
                    string.Equals(SpeciesFromSubject(item.subjectId), destinationSpecies, StringComparison.Ordinal) &&
                    item.amount > 0f && processParts.TryGetValue(item.populationId, out ProcessParts sourceParts) &&
                    DeferredRealityProviderRules.CanRecover(SpeciesFromSubject(item.subjectId),
                        destinationSpecies, sourceParts.habitat.ToString(),
                        destinationParts.habitat.ToString(), item.amount, destination.amount, floor)).OrderBy(item => item.populationId,
                            StringComparer.Ordinal).FirstOrDefault();
                if (source == null) continue;
                float amount = Math.Min(source.amount, Math.Max(0.01f,
                    Math.Min(source.amount * 0.05f, Math.Max(0.01f, floor - destination.amount))));
                if (amount <= 0f) continue;
                string operationId = DeferredRealityProviderRules.Stable("operation", ProviderId, "migration",
                    processId, source.populationId, destination.populationId,
                    executionCount.ToString(CultureInfo.InvariantCulture));
                RealityPopulationMutationResult result = RealityPopulationService.Transfer(world, source.populationId,
                    destination.populationId, amount, operationId, world.Now, ProviderId);
                processedEvents++;
                if (result.duplicate) duplicateEvents++;
                if (!result.succeeded) lastError = result.error;
                break;
            }
        }

        private static string SpeciesFromSubject(string subjectId)
        {
            if (string.IsNullOrEmpty(subjectId)) return null;
            int marker = subjectId.LastIndexOf(":fish:", StringComparison.Ordinal);
            return marker < 0 ? null : subjectId.Substring(marker + ":fish:".Length);
        }

        public IEnumerable<string> DiagnosticLines(RealityDiagnosticsContext context)
        {
            int populationCount = context?.World?.PopulationSnapshots(providerId: ProviderId).Count ?? 0;
            int processCount = context?.World?.ProcessSnapshots().Count(item => item?.record?.providerId == ProviderId) ?? 0;
            yield return "provider=" + ProviderId + " compatible=" + compatible + " schema=" +
                DeferredRealityProviderRules.SchemaVersion;
            yield return "maps=" + mappedMaps + " populations=" + populationCount + " processes=" + processCount;
            yield return "migrated=" + migratedPopulations + " skippedDefs=" + skippedPopulations +
                " events=" + processedEvents + " duplicates=" + duplicateEvents;
            yield return "operationMarkersRetained=true sequenceDomain=" + DeferredRealityProviderRules.SequenceDomain;
            yield return "lastError=" + (lastError ?? "none");
        }

        /// <summary>Returns bounded on-demand diagnostics without requiring the framework's internal context type.</summary>
        public string[] BridgeDiagnostics()
        {
            var rows = new List<string>
            {
                "provider=" + ProviderId,
                "available=" + (Current != null && ReferenceEquals(Current, this)),
                "compatible=" + compatible,
                "semanticApiVersion=" + DeferredRealityProviderRules.SemanticApiVersion,
                "providerSchema=" + DeferredRealityProviderRules.SchemaVersion,
                "minimumDeferredRealitySaveSchema=" + DeferredRealityProviderRules.MinimumDeferredRealitySchemaVersion,
                "capabilities=" + registration.capabilities,
                "registrationInProgress=false",
                "operationRetentionTicks=" + registration.operationRetentionTicks,
                "sequenceDomain=" + DeferredRealityProviderRules.SequenceDomain
            };
            if (world == null)
            {
                rows.Add("world=unavailable");
                rows.Add("regions=0 activeMaps=0 populations=0 processes=0 pausedProcesses=0 quarantined=0");
                rows.Add("pendingReconciliation=0 synchronous=true");
                rows.Add("exactOnceMarkers=0 duplicates=" + duplicateEvents + " recentOperations=none");
                rows.Add("migration=migrated:" + migratedPopulations + " skippedDefs:" + skippedPopulations);
                rows.Add("lastError=" + (lastError ?? "none"));
                return rows.ToArray();
            }

            int regions = world.RegionSnapshots().Count(item => item.id.ProviderNamespace == ProviderId);
            int activeMaps = world.RegionSnapshots().Count(item => item.id.ProviderNamespace == ProviderId &&
                item.projectionMapUniqueId >= 0);
            var populations = world.PopulationSnapshots(providerId: ProviderId,
                kind: DeferredRealityProviderRules.NaturalWaterKind);
            var processes = world.ProcessSnapshots().Where(item => item?.record?.providerId == ProviderId).ToList();
            int pausedProcesses = processes.Count(item => item.record != null && item.record.paused);
            int processFailures = processes.Count(item => !string.IsNullOrEmpty(item.record?.lastError));
            var quarantined = world.QuarantineSnapshots().Count(item => item?.providerId == ProviderId);
            var applied = world.AppliedOperationSnapshots(ProviderId).OrderByDescending(item => item.tick)
                .ThenBy(item => item.operationId, StringComparer.Ordinal).Take(8)
                .Select(item => item.operationId ?? "unknown").ToArray();
            rows.Add("world=available tick=" + world.Now);
            rows.Add("regions=" + regions + " activeMaps=" + activeMaps + " populations=" + populations.Count +
                " processes=" + processes.Count + " pausedProcesses=" + pausedProcesses +
                " processFailures=" + processFailures + " quarantined=" + quarantined);
            rows.Add("pendingReconciliation=0 synchronous=true");
            rows.Add("exactOnceMarkers=" + world.AppliedOperationSnapshots(ProviderId).Count +
                " duplicates=" + duplicateEvents + " recentOperations=" +
                (applied.Length == 0 ? "none" : string.Join(",", applied)));
            rows.Add("migration=migrated:" + migratedPopulations + " skippedDefs:" + skippedPopulations +
                " inProgress:false");
            rows.Add("lastError=" + (lastError ?? "none"));
            return rows.ToArray();
        }

        public bool Owns(Map map)
        {
            if (!compatible || world == null || map == null ||
                !world.TryRegionForProjectionMapId(map.uniqueID, out RealityRegionId region) ||
                region.ProviderNamespace != ProviderId ||
                !world.TryGetRegion(region, out RealityRegionSnapshot snapshot)) return false;
            return snapshot.projectionMapUniqueId == map.uniqueID &&
                snapshot.authority == RealityRegionAuthority.LiveProjection;
        }

        public void NotifyMapReady(Map map)
        {
            if (!compatible || world == null || map == null || !UnityData.IsInMainThread) return;
            if (MigrateMap(map)) RegisterCompatibleTopology();
        }

        public void NotifyMapDeinit(Map map)
        {
            if (!compatible || world == null || map == null || !UnityData.IsInMainThread) return;
            world.UnregisterMap(map);
        }

        public void NotifyStockingObserved(Map map, IntVec3 cell, ThingDef fishDef, float resultingPopulation,
            string logicalEventId)
        {
            if (!compatible || map == null || fishDef == null || !cell.InBounds(map) || !UnityData.IsInMainThread) return;
            NaturalFishPopulationMapComponent natural = map.GetComponent<NaturalFishPopulationMapComponent>();
            if (natural == null) return;
            NaturalWaterPopulation water = natural.PopulationAt(cell);
            NaturalFishSpeciesPopulation species = water?.species?.FirstOrDefault(item => item?.fishDefName == fishDef.defName);
            if (water == null || species == null) return;
            RealityRegionId region = RegionFor(map);
            string waterId = EnsureWaterId(region, water);
            string populationId = DeferredRealityProviderRules.PopulationId(region.ToString(), waterId, fishDef.defName);
            if (!world.TryGetPopulationRecord(populationId, out RealityPopulationRecord record)) return;
            float delta = DeferredRealityProviderRules.NormalizeNonNegative(species.population) - record.amount;
            string logicalId = logicalEventId ?? DeferredRealityProviderRules.Stable("stocking-event", region.ToString(), waterId,
                fishDef.defName, resultingPopulation.ToString("R", CultureInfo.InvariantCulture));
            RealityPopulationMutationResult result = RealityPopulationService.ReconcileActiveMap(world, populationId,
                delta, DeferredRealityProviderRules.OperationId("stocking-reconcile", record.regionId, waterId,
                    fishDef.defName, logicalId), world.Now, ProviderId);
            processedEvents++;
            if (result.duplicate) duplicateEvents++;
            if (!result.succeeded) lastError = result.error;
        }

        public static bool ShouldRunLegacyTick(NaturalFishPopulationMapComponent component)
        {
            if (component == null || Current == null || !Current.Owns(component.map)) return true;
            if (Find.TickManager != null && Find.TickManager.TicksGame % 60000 == 0)
                Current.NotifyMapReady(component.map);
            return false;
        }

        private void HandleAquacultureEvent(AquacultureEvent value)
        {
            if (value == null || !compatible || world == null || !UnityData.IsInMainThread) return;
            if (value.kind == AquacultureEventKind.FishStocked)
            {
                NotifyStockingObserved(value.map, value.cell, value.fishDef, value.value, value.logicalEventId);
                return;
            }
            if (value.kind != AquacultureEventKind.FishCaught || value.map == null || value.fishDef == null ||
                !value.cell.InBounds(value.map)) return;
            NaturalFishPopulationMapComponent natural = value.map.GetComponent<NaturalFishPopulationMapComponent>();
            if (natural == null) return;
            NaturalWaterPopulation water = natural.PopulationAt(value.cell);
            NaturalFishSpeciesPopulation species = water?.species?.FirstOrDefault(item => item?.fishDefName == value.fishDef.defName);
            if (water == null || species == null) return;
            string waterId = EnsureWaterId(RegionFor(value.map), water);
            string populationId = DeferredRealityProviderRules.PopulationId(RegionFor(value.map).ToString(), waterId,
                value.fishDef.defName);
            if (!world.TryGetPopulationRecord(populationId, out RealityPopulationRecord record)) return;
            float delta = DeferredRealityProviderRules.NormalizeNonNegative(species.population) - record.amount;
            string operation = DeferredRealityProviderRules.OperationId("catch-reconcile", record.regionId, waterId,
                value.fishDef.defName, value.logicalEventId);
            RealityPopulationMutationResult result = RealityPopulationService.ReconcileActiveMap(world, populationId, delta,
                operation, world.Now, ProviderId);
            processedEvents++;
            if (result.duplicate) duplicateEvents++;
            if (!result.succeeded) lastError = result.error;
        }

        private void SyncMap(Map map, NaturalFishPopulationMapComponent natural, RealityRegionId region)
        {
            if (map == null || natural == null) return;
            foreach (NaturalWaterPopulation water in (natural.Populations ?? Array.Empty<NaturalWaterPopulation>())
                .Where(item => item != null))
            {
                string waterId = EnsureWaterId(region, water);
                foreach (NaturalFishSpeciesPopulation species in (water.species ?? new List<NaturalFishSpeciesPopulation>())
                    .Where(item => item != null && !string.IsNullOrEmpty(item.fishDefName)))
                {
                    string id = DeferredRealityProviderRules.PopulationId(region.ToString(), waterId, species.fishDefName);
                    if (!world.TryGetPopulationRecord(id, out RealityPopulationRecord record)) continue;
                    species.population = DeferredRealityProviderRules.NormalizeNonNegative(record.amount, 0f, water.carryingCapacity);
                }
            }
            natural.NotifyDeferredRealityProjection();
        }

        private string EnsureWaterId(RealityRegionId region, NaturalWaterPopulation water)
        {
            if (string.IsNullOrEmpty(water.deferredRealityStableId))
                water.deferredRealityStableId = DeferredRealityProviderRules.WaterBodyId(region.ToString(),
                    water.habitat.ToString(), water.anchor.x, water.anchor.z, water.cellCount);
            return water.deferredRealityStableId;
        }

        private RealityRegionId RegionFor(Map map, DeferredRealityWorldComponent targetWorld = null)
        {
            DeferredRealityWorldComponent owner = targetWorld ?? world;
            if (owner != null)
            {
                RealityRegionId registered = owner.RegisterMap(map);
                if (registered.IsValid) return registered;
            }
            return RegionIdentityFor(map);
        }

        private static RealityRegionId RegionIdentityFor(Map map)
        {
            string stableInstance = map?.Parent?.GetUniqueLoadID();
            return new RealityRegionId((int)map.Tile, RealityLayer.Custom, "natural-water-map", stableInstance, null,
                ProviderId, "aquaculture-natural-water");
        }

        private static float BreedingFloor(NaturalWaterHabitat habitat)
        {
            FishPopulationHabitatDef def = DefDatabase<FishPopulationHabitatDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.habitat == habitat);
            return Math.Max(0.5f, def?.minimumBreedingPopulation ?? 2f);
        }

        private static int UpdateIntervalTicks(NaturalWaterHabitat habitat)
        {
            FishPopulationHabitatDef def = DefDatabase<FishPopulationHabitatDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.habitat == habitat);
            return Math.Max(2500, def?.updateIntervalTicks ?? 60000);
        }

        private static string PopulationPayload(NaturalWaterHabitat habitat, float floor)
        {
            return DeferredRealityProviderRules.Stable("population-payload", habitat.ToString(),
                floor.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string ProcessPayload(string waterId, string fishDefName, NaturalWaterHabitat habitat)
        {
            return string.Join("\n", waterId ?? string.Empty, fishDefName ?? string.Empty, habitat.ToString());
        }

        private static bool TryPopulationFromPayload(RealityProcessRecord process, out string populationId,
            out ProcessParts parts)
        {
            populationId = null;
            parts = default(ProcessParts);
            if (process == null || string.IsNullOrEmpty(process.payload)) return false;
            string[] values = process.payload.Split(new[] { '\n' }, StringSplitOptions.None);
            if (values.Length < 3 || string.IsNullOrEmpty(values[0]) || string.IsNullOrEmpty(values[1]) ||
                !Enum.TryParse(values[2], true, out NaturalWaterHabitat habitat)) return false;
            parts = new ProcessParts { waterId = values[0], fishDefName = values[1], habitat = habitat };
            populationId = DeferredRealityProviderRules.PopulationId(process.regionId, values[0], values[1]);
            return true;
        }

        private struct ProcessParts
        {
            public string waterId;
            public string fishDefName;
            public NaturalWaterHabitat habitat;
        }

        private static void AddIssue(IList<RealityVeto> issues, string code, string message)
        {
            if (issues != null) issues.Add(new RealityVeto(code, message, ProviderId));
        }
    }

    [StaticConstructorOnStartup]
    public static class AquacultureRealityIntegration
    {
        static AquacultureRealityIntegration()
        {
            try
            {
                AquacultureRealityProvider provider = new AquacultureRealityProvider();
                RealityProviderRegistry.Register(provider);
                var harmony = new Harmony(AquacultureRealityProvider.ProviderId);
                harmony.PatchAll(typeof(AquacultureRealityIntegration).Assembly);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("Aquaculture Deferred Reality integration failed closed: " + exception,
                    RealityDeterminism.StableHash("aquaculture-deferred-reality-registration"));
            }
        }
    }

    [HarmonyPatch(typeof(NaturalFishPopulationMapComponent), nameof(NaturalFishPopulationMapComponent.MapComponentTick))]
    internal static class AquacultureDeferredRealityNaturalTickPatch
    {
        private static bool Prefix(NaturalFishPopulationMapComponent __instance)
        {
            return AquacultureRealityProvider.ShouldRunLegacyTick(__instance);
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    internal static class AquacultureDeferredRealityMapReadyPatch
    {
        private static void Postfix(Map __instance)
        {
            AquacultureRealityProvider.Current?.NotifyMapReady(__instance);
        }
    }

    [HarmonyPatch(typeof(MapDeiniter), nameof(MapDeiniter.Deinit))]
    internal static class AquacultureDeferredRealityMapDeinitPatch
    {
        private static void Postfix(Map map)
        {
            AquacultureRealityProvider.Current?.NotifyMapDeinit(map);
        }
    }
}
