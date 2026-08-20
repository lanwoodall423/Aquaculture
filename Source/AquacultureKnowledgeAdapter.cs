using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    /// <summary>
    /// The only Aquaculture source file that talks to Knowledge Framework. Simulation code
    /// publishes domain events and asks this adapter for read-only decisions.
    /// </summary>
    public static class AquacultureKnowledgeAdapter
    {
        public const string DomainId = "aquaculture";
        public const string GlobalSubjectId = "aquaculture.global";
        public const string SpeciesArchetype = "fish_species";
        public const string IndividualArchetype = "exceptional_fish";
        public const string BreedArchetype = "registered_breed";
        public const string WaterArchetype = "water_population";
        public const string PondArchetype = "managed_pond";
        public const string AnglerArchetype = "angler";

        private const string LegacyMigrationId = "aquaculture.legacy.fishing-progress";
        private const int LegacyMigrationVersion = 1;
        private const string ExpertiseTrackId = "fishing_expertise";
        private const string ContextGlobal = "global";
        private const string ContextBiome = "biome";
        private const string ContextMapRegion = "map_region";
        private const string ContextWaterBody = "water_body";
        private const string ContextFishingCell = "fishing_cell";
        private const string ContextPond = "managed_pond";
        private const string ContextPondType = "pond_type";
        private const string BalancedAggregationMigrationId = "aquaculture.knowledge.balanced-stage";
        private const int BalancedAggregationMigrationVersion = 1;

        private static bool registered;
        private static bool migrating;
        private static bool migrationChecked;
        private static bool registrationInProgress;
        private static string activeLogicalEventId;
        private static readonly HashSet<string> HandledLogicalEvents = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> SubmittedRelations = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> RegisteredDynamicSubjects = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<int, CompFishTraits> KnownIndividuals = new Dictionary<int, CompFishTraits>();

        private static readonly string[] FacetIds =
        {
            "identity", "habitat", "seasonality", "feeding", "behavior", "catching", "size", "traits",
            "health", "breeding", "pond_compatibility", "population"
        };

        private static readonly string[] ClaimIds =
        {
            "species_identity", "preferred_water", "preferred_temperature", "active_season", "bite_time",
            "preferred_lures", "average_mass", "maximum_observed_mass", "diet", "schooling_behavior",
            "population_estimate", "breeding_season", "maturity_time", "compatibility", "trait_expression",
            "food_demand", "stress_tolerance", "health_status", "breeding_readiness", "generation",
            "lineage_summary"
        };

        public static bool IsRegistered => registered;
        public static bool IsFrameworkReady => FrameworkReady;

        /// <summary>
        /// Re-attempts registration when the Knowledge Framework game component becomes
        /// available. Startup can run before that component exists, and a stale static
        /// registration flag must not hide a new game's empty schema.
        /// </summary>
        public static void EnsureRegistration()
        {
            if (registered)
            {
                if (V3ContractReady() && KnowledgeRegistry.Schema(DomainId) != null) return;
                registered = false;
            }
            Register();
        }

        public static void Register()
        {
            if (!V3ContractReady())
            {
                registered = false;
                return;
            }
            if (!registered && !registrationInProgress)
            {
                registrationInProgress = true;
                try
                {
                    bool domainAccepted = KnowledgeRegistry.RegisterDomain(new KnowledgeDomainRegistration
                    {
                        id = DomainId,
                        label = "Aquaculture",
                        description = "Evidence-based knowledge of fish, waters, ponds, anglers, and cultivated lines.",
                        enableUncertainty = true,
                        enableFamiliarity = true,
                        sharingModel = KnowledgeSharingModel.Reportable,
                        stageAggregationMode = KnowledgeStageAggregationMode.Balanced,
                        sortOrder = 20,
                        provenanceLimit = 24,
                        evidenceAggregateLimit = 128,
                        facets = BuildFacets(),
                        stages = BuildStages(),
                        expertiseTracks = new[]
                        {
                            new KnowledgeExpertiseTrackDef
                            {
                                defName = ExpertiseTrackId,
                                stableId = ExpertiseTrackId,
                                label = "Fishing Expertise",
                                adept = 100f,
                                expert = 300f,
                                master = 700f
                            }
                        },
                        observations = BuildObservations(),
                        claims = BuildClaims(),
                        archetypes = BuildArchetypes(),
                        milestoneTracks = BuildMilestoneTracks(),
                        expertiseNamespaces = new[]
                        {
                            new KnowledgeExpertiseNamespaceDef
                            {
                                defName = "aquaculture_fishing_expertise",
                                stableId = "aquaculture_fishing_expertise",
                                label = "Colony fishing expertise",
                                adept = 100f,
                                expert = 300f,
                                master = 700f
                            }
                        },
                        subjectResolver = ResolveSubject,
                        subjectSource = SourceSubjects,
                        source = "lan.aquaculture.fishing.v3"
                    }, new KnowledgeRegistrationOptions
                    {
                        source = "lan.aquaculture.fishing.v3",
                        priority = 100,
                        conflict = KnowledgeRegistrationConflict.ReplaceIfHigherPriority
                    });
                    if (!domainAccepted && KnowledgeRegistry.Schema(DomainId) == null)
                        throw new InvalidOperationException("Knowledge Framework rejected the Aquaculture domain registration.");
                    if (!RegisterContexts() || !RegisterRelations())
                        throw new InvalidOperationException("Knowledge Framework rejected an Aquaculture context or relation registration.");
                    KnowledgeProviderRegistry.Register(DomainId, 20, EntryFor);
                    if (!KnowledgeV3Ui.Register(new AquacultureKnowledgeUi(), true))
                        throw new InvalidOperationException("Knowledge Framework rejected the Aquaculture UI registration.");
                    registered = true;
                }
                catch (Exception exception)
                {
                    registered = false;
                    Log.ErrorOnce("[Aquaculture - Fishing] Knowledge Framework registration deferred: " +
                        exception.GetBaseException().Message, GenText.StableStringHash("AquacultureKnowledgeRegistration"));
                }
                finally { registrationInProgress = false; }
            }
            if (!registered) return;
            AquacultureDiscoveryDirector.Register();
            TryCommitBalancedAggregationMigration();
            TryMigrateLegacy();
        }

        public static void Handle(AquacultureEvent value)
        {
            if (value == null) return;
            Register();
            if (!FrameworkReady || !TryBeginLogicalEvent(value)) return;
            string previousEventId = activeLogicalEventId;
            activeLogicalEventId = value.logicalEventId;
            try
            {
            switch (value.kind)
            {
                case AquacultureEventKind.FishHooked:
                    ObserveHooked(value);
                    break;
                case AquacultureEventKind.FishCaught:
                    ObserveCatch(value);
                    break;
                case AquacultureEventKind.FishEscaped:
                    ObserveEscape(value);
                    break;
                case AquacultureEventKind.FishDied:
                    ObserveDeath(value);
                    break;
                case AquacultureEventKind.FishStocked:
                    ObserveStocking(value);
                    break;
                case AquacultureEventKind.FishEstablished:
                    ObserveEstablished(value);
                    break;
                case AquacultureEventKind.FishObserved:
                    ObserveFish(value, value.phase.NullOrEmpty() ? "observe_pond" : value.phase, 3f, 0f);
                    break;
                case AquacultureEventKind.FishFed:
                    ObserveFish(value, value.phase.NullOrEmpty() ? "feed" : value.phase, 2f, 0f);
                    break;
                case AquacultureEventKind.FishBred:
                    ObserveBreed(value);
                    break;
                case AquacultureEventKind.ColonyBorn:
                    ObserveBirth(value);
                    break;
                case AquacultureEventKind.TraitObserved:
                    ObserveTrait(value);
                    break;
                case AquacultureEventKind.PopulationSurveyed:
                    ObservePopulation(value);
                    break;
                case AquacultureEventKind.EcologyWarning:
                    ObserveEcology(value, false);
                    break;
                case AquacultureEventKind.EcologyRecovered:
                    ObserveEcology(value, true);
                    break;
            }
            }
            catch (Exception exception)
            {
                HandledLogicalEvents.Remove(value.logicalEventId);
                Log.ErrorOnce("[Aquaculture - Fishing] Knowledge event deferred for retry: " +
                    exception.GetBaseException().Message, GenText.StableStringHash("AquacultureKnowledgeEventRetry"));
            }
            finally { activeLogicalEventId = previousEventId; }
        }

        public static float SpeciesKnowledgeFor(Pawn pawn, ThingDef fishDef)
        {
            if (fishDef == null) return 0f;
            TryMigrateLegacy();
            if (!FrameworkReady) return LegacySpeciesKnowledge(pawn, fishDef);
            KnowledgeFacetSnapshotV2 snapshot = KnowledgeQuery.Facet(DomainId, SpeciesSubjectId(fishDef), "identity", pawn,
                KnowledgeScope.Personal, true, false);
            return Mathf.Clamp01(snapshot.amount / 100f);
        }

        public static float ColonySpeciesKnowledgeFor(ThingDef fishDef)
        {
            if (fishDef == null || !FrameworkReady) return 0f;
            KnowledgeFacetSnapshotV2 snapshot = KnowledgeQuery.Facet(DomainId, SpeciesSubjectId(fishDef), "identity", null,
                KnowledgeScope.Colony, true, false);
            return Mathf.Clamp01(snapshot.amount / 100f);
        }

        public static IEnumerable<ThingDef> KnownSpeciesFor(Pawn pawn)
        {
            if (pawn == null) return Enumerable.Empty<ThingDef>();
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish)
                .Where(def => SpeciesKnowledgeFor(pawn, def) >= 0.02f).ToList();
        }

        public static float ExpertiseFor(Pawn pawn)
        {
            if (pawn == null) return 0f;
            TryMigrateLegacy();
            if (!FrameworkReady) return FishingProgressionComponent.Current?.ProgressFor(pawn, false)?.expertiseExperience ?? 0f;
            return KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrackId).amount;
        }

        public static KnowledgeRank ExpertiseRankFor(Pawn pawn)
        {
            if (!FrameworkReady)
            {
                float legacy = FishingProgressionComponent.Current?.ProgressFor(pawn, false)?.expertiseExperience ?? 0f;
                return FishingProgressionUtility.LevelFor(legacy);
            }
            return KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrackId).rank;
        }

        public static float ExpertiseProgressFor(Pawn pawn)
        {
            if (!FrameworkReady) return FishingProgressionComponent.Current?.ProgressFor(pawn, false)?.ExpertiseProgress ?? 0f;
            return KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrackId).progress;
        }

        public static void Invalidate(Pawn pawn)
        {
            if (FrameworkReady) KnowledgeProviderRegistry.Invalidate(pawn);
        }

        public static AquacultureKnowledgeView SpeciesView(ThingDef fishDef, Pawn pawn, bool colony)
        {
            if (fishDef == null) return new AquacultureKnowledgeView(null, null, 0f, 0f, false, null);
            EnsureSpeciesSubject(fishDef);
            if (!FrameworkReady)
            {
                float legacy = colony ? 0f : LegacySpeciesKnowledge(pawn, fishDef);
                return new AquacultureKnowledgeView(SpeciesSubjectId(fishDef), legacy > 0f ? "Identified" : "Unknown",
                    legacy, legacy, legacy > 0f, legacy > 0f ? new[] { "identity" } : Array.Empty<string>());
            }
            KnowledgeScope scope = colony ? KnowledgeScope.Colony : KnowledgeScope.Personal;
            KnowledgeSubjectSnapshotV2 subject = KnowledgeQuery.Subject(DomainId, SpeciesSubjectId(fishDef), colony ? null : pawn, scope);
            KnowledgeStageSnapshot stage = KnowledgeDiscovery.StageSnapshot(DomainId, SpeciesSubjectId(fishDef),
                colony ? null : pawn, scope, KnowledgeContextKey.Empty, KnowledgeContextFallbackMode.ParentThenGlobal);
            List<string> known = new List<string>();
            foreach (string facet in FacetIds)
            {
                KnowledgeFacetSnapshotV2 value = KnowledgeQuery.Facet(DomainId, SpeciesSubjectId(fishDef), facet,
                    colony ? null : pawn, scope, true, false);
                if (value.amount > 0f) known.Add(facet);
            }
            KnowledgeClaimSnapshot identity = Claim(SpeciesSubjectId(fishDef), "identity", "species_identity", pawn, colony,
                KnowledgeContextKey.Empty);
            string stageId = stage?.stageId ?? subject?.stageId ?? "Unknown";
            return new AquacultureKnowledgeView(SpeciesSubjectId(fishDef), stageId, StageKnowledge(stageId),
                Mathf.Clamp01(identity?.effectiveConfidence ?? 0f), identity?.value != null && identity.effectiveConfidence > 0f, known);
        }

        private static float StageKnowledge(string stageId)
        {
            switch (stageId)
            {
                case "hooked": return 0.20f;
                case "identified": return 0.40f;
                case "studied": return 0.60f;
                case "cultivated": return 0.80f;
                case "documented": return 1f;
                default: return 0f;
            }
        }

        public static KnowledgeClaimSnapshot Claim(string subjectId, string facetId, string claimId, Pawn pawn, bool colony,
            KnowledgeContextKey context)
        {
            if (!FrameworkReady) return null;
            return KnowledgeClaimService.Snapshot(DomainId, subjectId, facetId, claimId, colony ? null : pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, context, KnowledgeContextFallbackMode.ParentThenGlobal);
        }

        public static bool IsClaimKnown(string subjectId, string facetId, string claimId, Pawn pawn, bool colony,
            KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            KnowledgeClaimSnapshot value = Claim(subjectId, facetId, claimId, pawn, colony, context);
            return value?.value != null && value.effectiveConfidence > 0f;
        }

        public static KnowledgeContextKey ContextForFishing(Map map, IntVec3 cell)
        {
            return map == null || !cell.IsValid ? KnowledgeContextKey.Empty : new KnowledgeContextKey(ContextFishingCell,
                AquacultureKnowledgeContract.StableContextId(ContextFishingCell, map.uniqueID,
                    cell.x + "," + cell.z, "topology:fishing", null));
        }

        public static KnowledgeContextKey ContextForPond(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid) return KnowledgeContextKey.Empty;
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            PondProxyThing proxy = component?.ProxyFor(cell);
            IntVec3 anchor = proxy?.Position ?? cell;
            PondWaterKind water = component?.WaterKindAt(cell) ?? PondWaterKind.Freshwater;
            return new KnowledgeContextKey(ContextPond, AquacultureKnowledgeContract.StableContextId(ContextPond,
                map.uniqueID, anchor.x + "," + anchor.z,
                "topology:" + (proxy?.thingIDNumber.ToString() ?? "cell"), "water:" + water));
        }

        public static KnowledgeContextKey ContextForFish(CompFishTraits fish)
        {
            if (fish?.parent?.Spawned != true) return KnowledgeContextKey.Empty;
            return fish.IsInPond ? ContextForPond(fish.parent.Map, fish.parent.Position) :
                ContextForFishing(fish.parent.Map, fish.parent.Position);
        }

        public static string SpeciesSubjectId(ThingDef fishDef) => "species:" + fishDef?.defName;
        public static string IndividualSubjectId(Thing fish) => "individual:" + (fish?.thingIDNumber.ToString() ?? "unknown");
        public static string BreedSubjectId(string breedId) => "breed:" + breedId;
        public static string PondSubjectId(Map map, IntVec3 cell) => "pond:" + map?.uniqueID + ":" + cell.x + "," + cell.z;
        public static string WaterSubjectId(Map map, IntVec3 cell) => "water:" + map?.uniqueID + ":" + cell.x + "," + cell.z;

        public static bool DocumentSpecies(Pawn pawn, ThingDef fishDef)
        {
            if (!FrameworkReady || pawn == null || fishDef == null) return false;
            EnsureSpeciesSubject(fishDef);
            return KnowledgeTransmission.Document(DomainId, SpeciesSubjectId(fishDef), pawn, "Aquaculture Field Journal");
        }

        private static bool FrameworkReady => registered && V3ContractReady() && GameComponent_KnowledgeFramework.Current != null &&
            KnowledgeRegistry.Schema(DomainId) != null;

        private static bool V3ContractReady()
        {
            // Domain schemas, contexts, relations, and UI providers are static framework
            // registrations and are valid before a game component is attached. The game
            // component is intentionally checked later by FrameworkReady for stateful I/O.
            return AquacultureKnowledgeContract.SupportsV3(KnowledgeFrameworkApi.ApiVersion,
                    (version, capability) => KnowledgeFrameworkApi.Supports(version, capability),
                    KnowledgeFrameworkApi.CapabilityVersion);
        }

        private static bool TryBeginLogicalEvent(AquacultureEvent value)
        {
            string id = value.logicalEventId;
            if (id.NullOrEmpty())
            {
                id = AquacultureKnowledgeContract.StableEventId(value.kind.ToString(), value.fishDef?.defName,
                    Find.TickManager?.TicksGame ?? 0, value.map?.uniqueID.ToString(), value.cell.ToString());
                value.logicalEventId = id;
            }
            if (!HandledLogicalEvents.Add(id)) return false;
            if (HandledLogicalEvents.Count > 2048)
            {
                string oldest = HandledLogicalEvents.FirstOrDefault();
                if (!oldest.NullOrEmpty()) HandledLogicalEvents.Remove(oldest);
            }
            return true;
        }

        private static void ObserveHooked(AquacultureEvent value)
        {
            if (value.fishDef == null || value.pawn == null) return;
            EnsureSpeciesSubject(value.fishDef);
            SubmitBoth(value.pawn, SpeciesSubjectId(value.fishDef), "default", "hooked", 1.5f, 0f, 0.5f,
                ContextForFishing(value.map, value.cell), null, value.source ?? "fishing");
            ReportMilestone(SpeciesSubjectId(value.fishDef), "discovery", "hooked", value.pawn, ContextForFishing(value.map, value.cell));
        }

        private static void ObserveCatch(AquacultureEvent value)
        {
            if (value.fishDef == null || value.pawn == null) return;
            EnsureSpeciesSubject(value.fishDef);
            EnsureIndividualSubject(value.fish);
            float mass = value.attempt?.HookedFishMass ?? FishingRodUtility.BaseFishMass(value.fishDef);
            List<string> visualTraits = (value.traits ?? Array.Empty<string>()).Where(IsPlayerReadableTrait).Distinct().ToList();
            KnowledgeContextKey context = ContextForFishing(value.map, value.cell);
            Func<bool, List<KnowledgeMeasurement>> measurements = colony => new List<KnowledgeMeasurement>
            {
                Measurement(SpeciesSubjectId(value.fishDef), "identity", "species_identity", KnowledgeClaimValue.Text(KnowledgeClaimValueType.StringId, value.fishDef.defName), context, colony,
                    "A successful catch identified the species."),
                Measurement(SpeciesSubjectId(value.fishDef), "size", "average_mass", KnowledgeClaimValue.Float(mass), context, colony,
                    "Mass recorded at the landing station."),
                Measurement(SpeciesSubjectId(value.fishDef), "size", "maximum_observed_mass", KnowledgeClaimValue.Float(mass), context, colony,
                    "Personal and colony record comparison."),
                Measurement(SpeciesSubjectId(value.fishDef), "traits", "trait_expression", KnowledgeClaimValue.Set(visualTraits), context, colony,
                    visualTraits.Count == 0 ? "No unusual visible trait was confirmed." : "Visible phenotype observed on a landed specimen.")
            };
            SubmitBoth(value.pawn, SpeciesSubjectId(value.fishDef), "identity", "catch", 8f,
                8f + (1f - SpeciesKnowledgeFor(value.pawn, value.fishDef)) * 4f, 1f, context,
                measurements(false), value.source ?? "fishing");
            ReportMilestone(SpeciesSubjectId(value.fishDef), "discovery", "identified", value.pawn, context);
            if (visualTraits.Count > 0) ReportMilestone(SpeciesSubjectId(value.fishDef), "discovery", "studied", value.pawn, context);
        }

        private static void ObserveEscape(AquacultureEvent value)
        {
            if (value.fishDef == null || value.pawn == null || !FrameworkReady) return;
            EnsureSpeciesSubject(value.fishDef);
            Submit(value.pawn, SpeciesSubjectId(value.fishDef), "catching", "escape", 0.5f, 0f, 0.25f,
                ContextForFishing(value.map, value.cell), null, value.source ?? "fishing");
        }

        private static void ObserveDeath(AquacultureEvent value)
        {
            if (value.fishDef == null) return;
            EnsureSpeciesSubject(value.fishDef);
            KnowledgeContextKey context = ContextForPond(value.map, value.cell);
            SubmitColony(SpeciesSubjectId(value.fishDef), "health", "death", 0.5f, 0.5f, context,
                new[]
                {
                    Measurement(SpeciesSubjectId(value.fishDef), "health", "health_status",
                        KnowledgeClaimValue.Text(KnowledgeClaimValueType.EnumId, "dead"), context, true,
                        value.reason ?? "A fish death was recorded.")
                }, "fish-death");
        }

        private static void ObserveEstablished(AquacultureEvent value)
        {
            if (value.fishDef == null || value.fish == null) return;
            EnsureSpeciesSubject(value.fishDef);
            EnsurePondSubject(value.map, value.cell);
            KnowledgeContextKey context = ContextForPond(value.map, value.cell);
            List<KnowledgeMeasurement> measurements = new List<KnowledgeMeasurement>
            {
                Measurement(SpeciesSubjectId(value.fishDef), "pond_compatibility", "preferred_water",
                    KnowledgeClaimValue.Text(KnowledgeClaimValueType.EnumId, value.fish.WaterKind.ToString()), context, false,
                    "A living specimen survived introduction to this pond water."),
                Measurement(SpeciesSubjectId(value.fishDef), "feeding", "diet",
                    KnowledgeClaimValue.Text(KnowledgeClaimValueType.EnumId, value.fish.Diet.ToString()), context, false,
                    "Pond observation recorded feeding behavior.")
            };
            SubmitBoth(value.pawn, SpeciesSubjectId(value.fishDef), "pond_compatibility", "established", 6f, 0f, 2f,
                context, measurements, "pond-establishment");
            ReportMilestone(SpeciesSubjectId(value.fishDef), "discovery", "cultivated", value.pawn, context);
        }

        private static void ObserveFish(AquacultureEvent value, string method, float knowledge, float expertise)
        {
            if (value.fishDef == null || value.fish == null || value.pawn == null) return;
            EnsureSpeciesSubject(value.fishDef);
            KnowledgeContextKey context = value.fish.IsInPond ? ContextForPond(value.map, value.cell) : ContextForFishing(value.map, value.cell);
            List<KnowledgeMeasurement> measurements = new List<KnowledgeMeasurement>
            {
                Measurement(SpeciesSubjectId(value.fishDef), "behavior", "schooling_behavior",
                    KnowledgeClaimValue.Text(KnowledgeClaimValueType.EnumId, value.fish.Solitary ? "solitary" : "schooling"), context, false,
                    "Movement and social behavior observed in the water."),
                Measurement(SpeciesSubjectId(value.fishDef), "feeding", "food_demand",
                    KnowledgeClaimValue.Float(AquaticSpeciesProfile.For(value.fishDef).hourlyDemand), context, false,
                    "Food demand estimated from pond feeding."),
                Measurement(SpeciesSubjectId(value.fishDef), "traits", "trait_expression",
                    KnowledgeClaimValue.Set((value.traits ?? value.fish.traitDefNames ?? new List<string>()).Where(IsPlayerReadableTrait)), context, false,
                    "Visible traits confirmed during a close observation.")
            };
            Submit(value.pawn, SpeciesSubjectId(value.fishDef), "behavior", method, knowledge, expertise, 1f, context, measurements, value.source ?? method);
            if (value.fish.IsInPond) ReportMilestone(SpeciesSubjectId(value.fishDef), "discovery", "studied", value.pawn, context);
        }

        private static void ObserveBreed(AquacultureEvent value)
        {
            if (value.firstFish == null || value.secondFish == null) return;
            ThingDef species = value.fishDef ?? value.firstFish.parent?.def;
            if (species == null) return;
            EnsureSpeciesSubject(species);
            string breedId = value.firstFish.breedId;
            if (!breedId.NullOrEmpty()) EnsureBreedSubject(AquacultureJournalComponent.Current?.BreedById(breedId));
            KnowledgeContextKey context = ContextForPond(value.map, value.cell);
            string subjectId = SpeciesSubjectId(species);
            SubmitBoth(value.firstFish.FindLikelyDiscovererForKnowledge(), subjectId, "breeding", "breed", 5f, 0f, 1f,
                context, new[]
                {
                    Measurement(subjectId, "breeding", "breeding_readiness", KnowledgeClaimValue.Percentage(100f), context, false,
                        "Compatible adults produced offspring in this pond."),
                    Measurement(subjectId, "breeding", "generation", KnowledgeClaimValue.Integer(value.generation), context, false,
                        "Generation observed from colony breeding.")
                }, "breeding");
            ReportMilestone(subjectId, "husbandry", "first_hatch", value.firstFish.FindLikelyDiscovererForKnowledge(), context);
        }

        private static void ObserveBirth(AquacultureEvent value)
        {
            if (value.fishDef == null || value.fish == null) return;
            EnsureSpeciesSubject(value.fishDef);
            EnsureIndividualSubject(value.fish);
            KnowledgeContextKey context = ContextForPond(value.map, value.cell);
            SubmitColony(SpeciesSubjectId(value.fishDef), "breeding", "hatch", 4f, 1f, context,
                new[]
                {
                    Measurement(SpeciesSubjectId(value.fishDef), "breeding", "generation", KnowledgeClaimValue.Integer(value.generation), context, true,
                        "Colony-born fish established a new generation."),
                    Measurement(SpeciesSubjectId(value.fishDef), "traits", "trait_expression",
                        KnowledgeClaimValue.Set((value.traits ?? Array.Empty<string>()).Where(IsPlayerReadableTrait)), context, true,
                        "Inherited phenotype recorded at hatching.")
                }, "breeding");
            if (!value.fish.breedId.NullOrEmpty())
            {
                FishBreedRecord breed = AquacultureJournalComponent.Current?.BreedById(value.fish.breedId);
                EnsureBreedSubject(breed);
                ReportMilestone(BreedSubjectId(value.fish.breedId), "husbandry", "breed_registered", null, context);
            }
            AddParentRelations(value);
        }

        private static void ObserveTrait(AquacultureEvent value)
        {
            if (value.fishDef == null || value.fish == null || value.pawn == null) return;
            EnsureSpeciesSubject(value.fishDef);
            string trait = value.reason;
            if (trait.NullOrEmpty()) return;
            KnowledgeContextKey context = ContextForFish(value.fish);
            Submit(value.pawn, SpeciesSubjectId(value.fishDef), "traits", "trait_observed", 8f, 0f, 1f, context,
                new[]
                {
                    Measurement(SpeciesSubjectId(value.fishDef), "traits", "trait_expression", KnowledgeClaimValue.Set(new[] { trait }), context, false,
                        "A visible trait was deliberately studied.")
                }, "trait-observation");
            ReportMilestone(SpeciesSubjectId(value.fishDef), "discovery", "studied", value.pawn, context);
        }

        private static void ObservePopulation(AquacultureEvent value)
        {
            if (value.map == null || value.fishDef == null || value.pawn == null) return;
            EnsureSpeciesSubject(value.fishDef);
            KnowledgeContextKey context = ContextForFishing(value.map, value.cell);
            Submit(value.pawn, SpeciesSubjectId(value.fishDef), "population", "survey", 5f, 0f, 1f, context,
                new[]
                {
                    Measurement(SpeciesSubjectId(value.fishDef), "population", "population_estimate",
                        KnowledgeClaimValue.Range(Mathf.Max(0f, value.value * 0.7f), Mathf.Max(0f, value.value * 1.3f)), context, false,
                        "Survey estimate; exact fish counts are not known.")
                }, "population-survey");
        }

        private static void ObserveEcology(AquacultureEvent value, bool recovered)
        {
            if (value.fishDef == null || value.map == null) return;
            EnsureSpeciesSubject(value.fishDef);
            KnowledgeContextKey context = ContextForPond(value.map, value.cell);
            SubmitColony(SpeciesSubjectId(value.fishDef), "health", recovered ? "health_recovered" : "health_event", 1.5f,
                0.5f, context, new[]
                {
                    Measurement(SpeciesSubjectId(value.fishDef), "health", "health_status",
                        KnowledgeClaimValue.Text(KnowledgeClaimValueType.EnumId, recovered ? "recovering" : "stressed"), context, true,
                        value.reason)
                }, "pond-ecology");
        }

        private static void SubmitBoth(Pawn pawn, string subjectId, string facetId, string reason, float knowledge, float expertise,
            float familiarity, KnowledgeContextKey context, IEnumerable<KnowledgeMeasurement> measurements, string source)
        {
            if (pawn == null) return;
            string eventId = CurrentLogicalEventId(source, subjectId, reason);
            KnowledgeTransaction transaction = new KnowledgeTransaction
            {
                source = source,
                transactionId = TransactionId(eventId)
            };
            transaction.Add(BuildObservation(pawn, subjectId, facetId, reason, knowledge, expertise, familiarity,
                context, measurements, source, false, eventId));
            transaction.Add(BuildObservation(null, subjectId, facetId, reason, knowledge * 0.7f, 0f, familiarity * 0.7f,
                context, measurements, source, true, eventId));
            SubmitTransaction(transaction);
        }

        private static bool Submit(Pawn pawn, string subjectId, string facetId, string reason, float knowledge, float expertise,
            float familiarity, KnowledgeContextKey context, IEnumerable<KnowledgeMeasurement> measurements, string source)
        {
            if (!FrameworkReady || pawn == null || subjectId.NullOrEmpty()) return false;
            string eventId = CurrentLogicalEventId(source, subjectId, reason);
            return SubmitTransaction(new KnowledgeTransaction
            {
                source = source,
                transactionId = TransactionId(eventId)
            }.Add(BuildObservation(pawn, subjectId, facetId, reason, knowledge, expertise, familiarity,
                context, measurements, source, false, eventId)));
        }

        private static bool SubmitColony(string subjectId, string facetId, string reason, float knowledge, float familiarity,
            KnowledgeContextKey context, IEnumerable<KnowledgeMeasurement> measurements, string source)
        {
            if (!FrameworkReady || subjectId.NullOrEmpty()) return false;
            string eventId = CurrentLogicalEventId(source, subjectId, reason);
            return SubmitTransaction(new KnowledgeTransaction
            {
                source = source,
                transactionId = TransactionId(eventId)
            }.Add(BuildObservation(null, subjectId, facetId, reason, knowledge, 0f, familiarity,
                context, measurements, source, true, eventId)));
        }

        private static KnowledgeObservation BuildObservation(Pawn pawn, string subjectId, string facetId, string reason,
            float knowledge, float expertise, float familiarity, KnowledgeContextKey context,
            IEnumerable<KnowledgeMeasurement> measurements, string source, bool colony, string eventId)
        {
            return new KnowledgeObservation
            {
                observer = colony ? null : pawn,
                domainId = DomainId,
                subjectId = subjectId,
                facetId = facetId,
                observationId = ObservationId(eventId, reason, subjectId, facetId, colony),
                logicalEventId = eventId,
                methodId = reason,
                targetColony = colony,
                directKnowledge = SafeKnowledge(knowledge),
                directExpertise = SafeKnowledge(expertise),
                directFamiliarity = SafeKnowledge(familiarity),
                expertiseTrackId = colony ? null : ExpertiseTrackId,
                success = true,
                quality = 1f,
                novelty = 1f,
                repetition = 1f,
                sourceReliability = 1f,
                reasonId = reason,
                source = source,
                sourceInstanceId = eventId + (colony ? ":colony" : ":pawn:" + (pawn?.thingIDNumber.ToString() ?? "0")),
                context = context,
                summary = source,
                claimMeasurements = measurements?.Select(item => CloneMeasurement(item, colony)).Where(item => item != null).ToList(),
                witnessDistribution = colony ? null : WitnessDistribution(pawn),
                suppressConfiguredKnowledge = true
                };
        }

        private static void ObserveStocking(AquacultureEvent value)
        {
            if (value.map == null || value.fishDef == null) return;
            EnsureSpeciesSubject(value.fishDef);
            KnowledgeContextKey context = ContextForFishing(value.map, value.cell);
            SubmitColony(SpeciesSubjectId(value.fishDef), "population", "stocking", 1f, 0.5f, context,
                new[]
                {
                    Measurement(SpeciesSubjectId(value.fishDef), "population", "population_estimate",
                        KnowledgeClaimValue.Range(Mathf.Max(0f, value.value * 0.7f),
                            Mathf.Max(0f, value.value * 1.3f)), context, true,
                        "A stocking event changed the approximate population.")
                }, "fish-stocking");
        }

        private static bool SubmitTransaction(KnowledgeTransaction transaction)
        {
            if (!FrameworkReady || transaction == null || transaction.Observations.Count == 0) return false;
            return KnowledgeEngine.Submit(transaction).success;
        }

        private static float SafeKnowledge(float value)
        {
            return AquacultureKnowledgeContract.IsFiniteNonNegative(value) ? value : 0f;
        }

        private static string CurrentLogicalEventId(string source, string subjectId, string reason)
        {
            return activeLogicalEventId.NullOrEmpty() ? AquacultureKnowledgeContract.StableEventId(source, subjectId,
                Find.TickManager?.TicksGame ?? 0, DomainId, reason) : activeLogicalEventId;
        }

        private static string TransactionId(string eventId) => "aquaculture:event:" + (eventId ?? "unknown");

        private static KnowledgeMeasurement Measurement(string subjectId, string facetId, string claimId,
            KnowledgeClaimValue value, KnowledgeContextKey context, bool colony, string summary)
        {
            return new KnowledgeMeasurement
            {
                domainId = DomainId,
                subjectId = subjectId,
                facetId = facetId,
                claimId = claimId,
                observer = null,
                scope = colony ? KnowledgeScope.Colony : KnowledgeScope.Personal,
                value = value,
                context = context,
                quality = 1f,
                evidenceWeight = 1f,
                confidenceFactor = 1f,
                disposition = KnowledgeEvidenceDisposition.Supporting,
                source = "Aquaculture",
                sourceInstanceId = MeasurementSourceId(subjectId, facetId, claimId, colony),
                methodId = "field_observation",
                reasonId = "field_observation",
                summary = summary,
                tick = Find.TickManager?.TicksGame ?? 0,
                revealed = true
            };
        }

        private static KnowledgeMeasurement CloneMeasurement(KnowledgeMeasurement value, bool colony)
        {
            KnowledgeMeasurement clone = value?.Clone();
            if (clone == null) return null;
            clone.scope = colony ? KnowledgeScope.Colony : KnowledgeScope.Personal;
            clone.observer = null;
            clone.sourceInstanceId = MeasurementSourceId(clone.subjectId, clone.facetId, clone.claimId, colony);
            return clone;
        }

        private static string MeasurementSourceId(string subjectId, string facetId, string claimId, bool colony)
        {
            return (activeLogicalEventId.NullOrEmpty() ? "aquaculture:unscoped" : activeLogicalEventId) + ":measurement:" +
                (colony ? "colony" : "personal") + ":" + subjectId + ":" + facetId + ":" + claimId;
        }

        private static KnowledgeWitnessDistribution WitnessDistribution(Pawn observer)
        {
            if (observer?.Map?.mapPawns?.FreeColonistsSpawned == null) return new KnowledgeWitnessDistribution();
            List<Pawn> witnesses = observer.Map.mapPawns.FreeColonistsSpawned.Where(pawn => pawn != observer &&
                pawn.Position.DistanceToSquared(observer.Position) <= 144f).Take(8).ToList();
            return new KnowledgeWitnessDistribution
            {
                policy = KnowledgeWitnessDistributionPolicy.WitnessesReduced,
                efficiency = 0.25f,
                expertiseEfficiency = 0f,
                confidenceEfficiency = 0.5f,
                includeObserver = false,
                maximumRecipients = witnesses.Count
            };
        }

        private static string ObservationId(string eventId, string reason, string subjectId, string facetId, bool colony)
        {
            return AquacultureKnowledgeContract.StableEventId("knowledge-observation", eventId ?? "unknown",
                0, reason ?? "field", (subjectId ?? string.Empty) + ":" + (facetId ?? string.Empty) + ":" +
                (colony ? "colony" : "personal"));
        }

        public static bool IsPlayerReadableTrait(string name)
        {
            return !name.NullOrEmpty() && (name.StartsWith("AF_Color_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Pattern_", StringComparison.Ordinal) || name.StartsWith("AF_Scale_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Size_", StringComparison.Ordinal) || name.StartsWith("AF_Body_", StringComparison.Ordinal) ||
                name.StartsWith("AF_Finish_", StringComparison.Ordinal) || name.StartsWith("AF_Effect_", StringComparison.Ordinal));
        }

        private static void ReportMilestone(string subjectId, string trackId, string milestoneId, Pawn pawn, KnowledgeContextKey context)
        {
            if (!FrameworkReady || subjectId.NullOrEmpty()) return;
            KnowledgeMilestoneService.ReportCondition(new KnowledgeMilestoneConditionSample
            {
                domainId = DomainId,
                subjectId = subjectId,
                trackId = trackId,
                milestoneId = milestoneId,
                pawn = pawn,
                context = context,
                conditionMet = true,
                elapsedTicks = int.MaxValue,
                completingPawn = pawn
            });
        }

        private static void AddParentRelations(AquacultureEvent value)
        {
            if (!FrameworkReady || value.fish == null) return;
            string child = IndividualSubjectId(value.fish.parent);
            EnsureIndividualSubject(value.fish);
            if (value.firstFish != null || value.secondFish != null)
            {
                AddRelation(value.firstFish, child, "parent_offspring");
                AddRelation(value.secondFish, child, "parent_offspring");
            }
            else
            {
                AddParentRelationById(value.fish.parentOneThingId, child);
                AddParentRelationById(value.fish.parentTwoThingId, child);
            }
            if (!value.fish.breedId.NullOrEmpty())
            {
                EnsureBreedSubject(AquacultureJournalComponent.Current?.BreedById(value.fish.breedId));
                AddBreedRelation(value.fish, BreedSubjectId(value.fish.breedId), "breed_descendant");
            }
        }

        private static void AddRelation(CompFishTraits parent, string childId, string type)
        {
            if (parent?.parent == null || childId.NullOrEmpty()) return;
            AddRelationByIds(IndividualSubjectId(parent.parent), childId, type);
        }

        private static void AddRelationByIds(string fromId, string childId, string type)
        {
            if (fromId.NullOrEmpty() || childId.NullOrEmpty() || type.NullOrEmpty()) return;
            string relationKey = DomainId + "|" + fromId + "|" + childId + "|" + type;
            if (!SubmittedRelations.Add(relationKey)) return;
            if (!KnowledgeRelationService.Add(new KnowledgeSubjectRelation
            {
                domainId = DomainId,
                fromSubjectId = fromId,
                toDomainId = DomainId,
                toSubjectId = childId,
                relationTypeId = type,
                confidence = 1f,
                source = "Aquaculture breeding",
                tick = Find.TickManager?.TicksGame ?? 0
            })) SubmittedRelations.Remove(relationKey);
        }

        private static void AddBreedRelation(CompFishTraits fish, string targetId, string type)
        {
            if (fish?.parent == null || targetId.NullOrEmpty()) return;
            AddRelationByIds(IndividualSubjectId(fish.parent), targetId, type);
        }

        private static void AddParentRelationById(int parentId, string childId)
        {
            if (parentId <= 0 || childId.NullOrEmpty()) return;
            if (KnownIndividuals.TryGetValue(parentId, out CompFishTraits parent) && parent?.parent != null)
                AddRelationByIds(IndividualSubjectId(parent.parent), childId, "parent_offspring");
        }

        private static KnowledgeEntry EntryFor(Pawn pawn)
        {
            if (pawn == null || !FrameworkReady) return null;
            KnowledgeExpertiseSnapshotV2 expertise = KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrackId);
            int known = KnownSpeciesFor(pawn).Count();
            if (known == 0 && expertise.amount <= 0f) return null;
            return new KnowledgeEntry
            {
                label = "Aquaculture",
                rank = expertise.rank,
                progress = expertise.progress,
                summary = known + " known species",
                tooltip = "Fishing expertise is personal. Species observations and colony reports are stored separately.",
                openDetails = () => MainTabWindow_AquacultureJournal.OpenExpertise(pawn)
            };
        }

        private static void EnsureSpeciesSubject(ThingDef fishDef)
        {
            if (fishDef == null || !FrameworkReady) return;
            RegisterSubject(new KnowledgeSubjectRegistration
            {
                id = SpeciesSubjectId(fishDef),
                label = fishDef.LabelCap.ToString(),
                description = "A fish species with universal biology and context-specific field evidence.",
                unidentifiedLabel = "Unknown aquatic species",
                unidentifiedDescription = "The colony has not yet identified this fish.",
                sourceDef = fishDef,
                archetypeId = SpeciesArchetype,
                applicableFacetIds = FacetIds,
                applicableClaimIds = ClaimIds,
                source = "Aquaculture species catalog"
            });
        }

        private static void EnsureIndividualSubject(CompFishTraits fish)
        {
            if (fish?.parent == null || !FrameworkReady) return;
            KnownIndividuals[fish.parent.thingIDNumber] = fish;
            RegisterSubject(new KnowledgeSubjectRegistration
            {
                id = IndividualSubjectId(fish.parent),
                label = fish.parent.LabelCap.ToString(),
                description = "An individual fish whose exceptional records and lineage are tracked separately from its species.",
                sourceDef = fish.parent.def,
                archetypeId = IndividualArchetype,
                applicableFacetIds = new[] { "identity", "size", "traits", "health", "breeding" },
                applicableClaimIds = new[] { "average_mass", "maximum_observed_mass", "trait_expression", "health_status", "generation", "lineage_summary" },
                source = "Aquaculture specimen registry"
            });
        }

        public static void ForgetIndividual(CompFishTraits fish)
        {
            int id = fish?.parent?.thingIDNumber ?? 0;
            if (id > 0) KnownIndividuals.Remove(id);
        }

        public static void TrackIndividual(CompFishTraits fish)
        {
            int id = fish?.parent?.thingIDNumber ?? 0;
            if (id <= 0) return;
            KnownIndividuals[id] = fish;
            if (FrameworkReady) EnsureIndividualSubject(fish);
        }

        private static void EnsurePondSubject(Map map, IntVec3 cell)
        {
            if (map == null || !FrameworkReady) return;
            RegisterSubject(new KnowledgeSubjectRegistration
            {
                id = PondSubjectId(map, cell),
                label = "Managed pond at " + cell.x + "," + cell.z,
                description = "A contextual managed pond population.",
                archetypeId = PondArchetype,
                applicableFacetIds = new[] { "habitat", "feeding", "health", "breeding", "population", "pond_compatibility" },
                applicableClaimIds = ClaimIds,
                source = "Aquaculture pond registry"
            });
        }

        private static void EnsureWaterSubject(Map map, IntVec3 cell)
        {
            if (map == null || !FrameworkReady) return;
            RegisterSubject(new KnowledgeSubjectRegistration
            {
                id = WaterSubjectId(map, cell),
                label = "Natural water at " + cell.x + "," + cell.z,
                description = "An estimated natural population in one connected water body.",
                archetypeId = WaterArchetype,
                applicableFacetIds = new[] { "habitat", "seasonality", "population", "catching" },
                applicableClaimIds = new[] { "preferred_water", "preferred_temperature", "active_season", "population_estimate", "preferred_lures", "bite_time" },
                source = "Aquaculture water survey"
            });
        }

        private static void EnsureBreedSubject(FishBreedRecord breed)
        {
            if (breed == null || breed.id.NullOrEmpty() || !FrameworkReady) return;
            RegisterSubject(new KnowledgeSubjectRegistration
            {
                id = BreedSubjectId(breed.id),
                label = breed.name,
                description = "A registered breeding line with observed inheritance stability.",
                sourceDef = breed.FishDef,
                archetypeId = BreedArchetype,
                applicableFacetIds = new[] { "identity", "traits", "size", "breeding", "health" },
                applicableClaimIds = new[] { "trait_expression", "average_mass", "maximum_observed_mass", "generation", "lineage_summary", "breeding_readiness" },
                source = "Aquaculture breed registry"
            });
        }

        private static void RegisterSubject(KnowledgeSubjectRegistration subject)
        {
            if (subject == null || !FrameworkReady) return;
            if (!subject.id.NullOrEmpty() && !RegisteredDynamicSubjects.Contains(subject.id))
            {
                if (!AquacultureKnowledgeContract.DynamicSubjectCountAllowed(RegisteredDynamicSubjects.Count + 1))
                {
                    Log.ErrorOnce("[Aquaculture - Fishing] Knowledge subject limit reached; dropping new subject " + subject.id,
                        GenText.StableStringHash("AquacultureKnowledgeSubjectLimit"));
                    return;
                }
                RegisteredDynamicSubjects.Add(subject.id);
            }
            bool accepted = KnowledgeRegistry.RegisterSubject(DomainId, subject, new KnowledgeRegistrationOptions
            {
                source = "lan.aquaculture.fishing.v3",
                priority = 100,
                conflict = KnowledgeRegistrationConflict.Replace
            });
            if (!accepted) RegisteredDynamicSubjects.Remove(subject.id);
        }

        private static KnowledgeSubjectRegistration ResolveSubject(string id)
        {
            if (id == GlobalSubjectId) return new KnowledgeSubjectRegistration
            {
                id = id,
                label = "Aquaculture",
                description = "Colony-wide aquaculture knowledge.",
                archetypeId = AnglerArchetype,
                applicableFacetIds = FacetIds,
                applicableClaimIds = ClaimIds,
                source = "Aquaculture"
            };
            if (id.StartsWith("species:", StringComparison.Ordinal))
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(id.Substring("species:".Length));
                return def == null ? null : new KnowledgeSubjectRegistration
                {
                    id = id,
                    label = def.LabelCap.ToString(),
                    description = "A fish species with universal biology and contextual field evidence.",
                    unidentifiedLabel = "Unknown aquatic species",
                    sourceDef = def,
                    archetypeId = SpeciesArchetype,
                    applicableFacetIds = FacetIds,
                    applicableClaimIds = ClaimIds,
                    source = "Aquaculture species catalog"
                };
            }
            if (id.StartsWith("breed:", StringComparison.Ordinal))
            {
                FishBreedRecord breed = AquacultureJournalComponent.Current?.BreedById(id.Substring("breed:".Length));
                return breed == null ? null : new KnowledgeSubjectRegistration
                {
                    id = id,
                    label = breed.name,
                    description = "A registered breeding line.",
                    sourceDef = breed.FishDef,
                    archetypeId = BreedArchetype,
                    applicableFacetIds = new[] { "identity", "traits", "size", "breeding", "health" },
                    applicableClaimIds = ClaimIds,
                    source = "Aquaculture breed registry"
                };
            }
            if (id.StartsWith("individual:", StringComparison.Ordinal))
            {
                if (!int.TryParse(id.Substring("individual:".Length), out int number)) return null;
                if (!KnownIndividuals.TryGetValue(number, out CompFishTraits fish) || fish?.parent == null) return null;
                Thing thing = fish.parent;
                return new KnowledgeSubjectRegistration
                {
                    id = id,
                    label = thing.LabelCap.ToString(),
                    description = "An exceptional individual fish.",
                    sourceDef = thing.def,
                    archetypeId = IndividualArchetype,
                    applicableFacetIds = new[] { "identity", "size", "traits", "health", "breeding" },
                    applicableClaimIds = ClaimIds,
                    source = "Aquaculture specimen registry"
                };
            }
            if (id.StartsWith("pond:", StringComparison.Ordinal)) return ContextSubject(id, "Managed pond", PondArchetype);
            if (id.StartsWith("water:", StringComparison.Ordinal)) return ContextSubject(id, "Natural water body", WaterArchetype);
            return null;
        }

        private static KnowledgeSubjectRegistration ContextSubject(string id, string label, string archetype)
        {
            return new KnowledgeSubjectRegistration
            {
                id = id,
                label = label,
                description = "A contextual Aquaculture population subject.",
                archetypeId = archetype,
                applicableFacetIds = FacetIds,
                applicableClaimIds = ClaimIds,
                source = "Aquaculture context registry"
            };
        }

        private static IEnumerable<KnowledgeSubjectRegistration> SourceSubjects()
        {
            yield return ResolveSubject(GlobalSubjectId);
            foreach (ThingDef fish in DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish))
                yield return ResolveSubject(SpeciesSubjectId(fish));
        }

        private static bool RegisterContexts()
        {
            bool types = KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextGlobal", stableId = ContextGlobal }, true)
                && KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextBiome", stableId = ContextBiome, parentTypeId = ContextGlobal }, true)
                && KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextMapRegion", stableId = ContextMapRegion, parentTypeId = ContextBiome }, true)
                && KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextWaterBody", stableId = ContextWaterBody, parentTypeId = ContextMapRegion }, true)
                && KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextFishingCell", stableId = ContextFishingCell, parentTypeId = ContextWaterBody }, true)
                && KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextPondType", stableId = ContextPondType, parentTypeId = ContextBiome }, true)
                && KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextPond", stableId = ContextPond, parentTypeId = ContextPondType }, true);
            bool resolvers = KnowledgeContextRegistry.RegisterResolver(ContextGlobal, new GlobalContextResolver(), true)
                && KnowledgeContextRegistry.RegisterResolver(ContextFishingCell, new FishingCellContextResolver(), true)
                && KnowledgeContextRegistry.RegisterResolver(ContextWaterBody, new WaterBodyContextResolver(), true)
                && KnowledgeContextRegistry.RegisterResolver(ContextMapRegion, new MapRegionContextResolver(), true)
                && KnowledgeContextRegistry.RegisterResolver(ContextPond, new PondContextResolver(), true)
                && KnowledgeContextRegistry.RegisterResolver(ContextPondType, new PondTypeContextResolver(), true);
            return types && resolvers;
        }

        private static bool RegisterRelations()
        {
            bool parent = KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
            {
                defName = "AF_RelationParentOffspring",
                stableId = "parent_offspring",
                label = "Parent / offspring",
                parentage = true,
                symmetric = false,
                inverseTypeId = "parent_offspring",
                metadataLimit = 8
            }, true);
            bool breed = KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
            {
                defName = "AF_RelationBreedDescendant",
                stableId = "breed_descendant",
                label = "Breed / descendant",
                parentage = false,
                symmetric = false,
                metadataLimit = 8
            }, true);
            bool trait = KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
            {
                defName = "AF_RelationTraitOrigin",
                stableId = "trait_origin",
                label = "Trait origin",
                parentage = false,
                symmetric = false,
                metadataLimit = 8
            }, true);
            return parent && breed && trait;
        }

        private static IReadOnlyList<KnowledgeFacetDef> BuildFacets()
        {
            return new[]
            {
                Facet("identity", "Identity", 12f), Facet("habitat", "Habitat", 10f), Facet("seasonality", "Seasonality", 8f),
                Facet("feeding", "Feeding", 10f), Facet("behavior", "Behavior", 10f), Facet("catching", "Catching", 10f),
                Facet("size", "Size", 10f), Facet("traits", "Traits", 12f), Facet("health", "Health", 8f),
                Facet("breeding", "Breeding", 12f), Facet("pond_compatibility", "Pond compatibility", 10f),
                Facet("population", "Population", 10f)
            };
        }

        private static KnowledgeFacetDef Facet(string id, string label, float completeness)
        {
            return new KnowledgeFacetDef
            {
                defName = "AF_Facet_" + id,
                stableId = id,
                label = label,
                description = "Field evidence for " + label.ToLowerInvariant() + ".",
                completenessAmount = completeness,
                personallyKnowable = true,
                documentable = true,
                shareable = true,
                approximateWhenUncertain = true
            };
        }

        private static IReadOnlyList<KnowledgeClaimDef> BuildClaims()
        {
            return new[]
            {
                Claim("species_identity", "identity", KnowledgeClaimValueType.StringId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("preferred_water", "habitat", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("preferred_temperature", "habitat", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.ObservedRange, KnowledgeClaimStalenessPolicy.Seasonal),
                Claim("active_season", "seasonality", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Seasonal),
                Claim("bite_time", "catching", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Seasonal),
                Claim("preferred_lures", "catching", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, KnowledgeClaimStalenessPolicy.SlowlyStale),
                Claim("average_mass", "size", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("maximum_observed_mass", "size", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Highest, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("diet", "feeding", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("schooling_behavior", "behavior", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("population_estimate", "population", KnowledgeClaimValueType.NumericRange, KnowledgeClaimAggregation.Latest, KnowledgeClaimStalenessPolicy.ConsumerManaged),
                Claim("breeding_season", "breeding", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Seasonal),
                Claim("maturity_time", "breeding", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("compatibility", "pond_compatibility", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.ConsumerManaged),
                Claim("trait_expression", "traits", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("food_demand", "feeding", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.SlowlyStale),
                Claim("stress_tolerance", "health", KnowledgeClaimValueType.Percentage, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("health_status", "health", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.Latest, KnowledgeClaimStalenessPolicy.ConsumerManaged),
                Claim("breeding_readiness", "breeding", KnowledgeClaimValueType.Percentage, KnowledgeClaimAggregation.Latest, KnowledgeClaimStalenessPolicy.ConsumerManaged),
                Claim("generation", "breeding", KnowledgeClaimValueType.Integer, KnowledgeClaimAggregation.Highest, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("lineage_summary", "breeding", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, KnowledgeClaimStalenessPolicy.Permanent)
            };
        }

        private static KnowledgeClaimDef Claim(string id, string facet, KnowledgeClaimValueType type,
            KnowledgeClaimAggregation aggregation, KnowledgeClaimStalenessPolicy staleness)
        {
            return new KnowledgeClaimDef
            {
                defName = "AF_Claim_" + id,
                stableId = id,
                domainId = DomainId,
                facetId = facet,
                valueType = type,
                aggregation = aggregation,
                stalenessPolicy = staleness,
                halfLifeTicks = staleness == KnowledgeClaimStalenessPolicy.Seasonal ? 900000f : 600000f,
                provisionalConfidence = 0.35f,
                revealedByDefault = false,
                documentable = true,
                provenanceLimit = 12,
                measurementHistoryLimit = 48
            };
        }

        private static IReadOnlyList<KnowledgeSubjectArchetypeDef> BuildArchetypes()
        {
            return new[]
            {
                Archetype(SpeciesArchetype, "Fish species", new[] { "identity", "habitat", "seasonality", "feeding", "behavior", "catching", "size", "traits", "health", "breeding", "pond_compatibility", "population" }),
                Archetype(IndividualArchetype, "Exceptional fish", new[] { "identity", "size", "traits", "health", "breeding" }),
                Archetype(BreedArchetype, "Registered breed", new[] { "identity", "traits", "size", "health", "breeding" }),
                Archetype(WaterArchetype, "Natural water population", new[] { "habitat", "seasonality", "catching", "population" }),
                Archetype(PondArchetype, "Managed pond population", new[] { "habitat", "feeding", "health", "breeding", "pond_compatibility", "population" }),
                Archetype(AnglerArchetype, "Aquaculture archive", FacetIds)
            };
        }

        private static KnowledgeSubjectArchetypeDef Archetype(string id, string label, IEnumerable<string> facets)
        {
            return new KnowledgeSubjectArchetypeDef
            {
                defName = "AF_Archetype_" + id,
                stableId = id,
                applicableFacetIds = facets.ToList(),
                applicableClaimIds = ClaimIds.ToList()
            };
        }

        private static IReadOnlyList<KnowledgeObservationDef> BuildObservations()
        {
            return new[] { "hooked", "catch", "escape", "established", "pond", "feed", "breed", "hatch", "survey", "trait_observed", "health_event", "health_recovered", "field" }
                .Select(id => new KnowledgeObservationDef
                {
                    defName = "AF_Observation_" + id,
                    stableId = "observe_" + id,
                    baseKnowledge = 0f,
                    baseExpertise = 0f,
                    baseFamiliarity = 0f,
                    facetIds = FacetIds.ToList(),
                    retainProvenance = true,
                    shareable = true,
                    accrualPolicy = new KnowledgeAccrualPolicy
                    {
                        uniquePerPawnAndSourceInstance = true,
                        diminishingReturns = 0.15f,
                        firstObservationBonus = 1f,
                        differentSpecimenBonus = 1.15f,
                        differentContextBonus = 1.1f,
                        stateLimit = 256
                    }
                }).ToList();
        }

        private static IReadOnlyList<KnowledgeStageDef> BuildStages()
        {
            return new[]
            {
                Stage("unknown", 0, "Unknown", null),
                Stage("hooked", 1, "Hooked", Group(KnowledgeRequirementGroupMode.All, Requirement(KnowledgeRequirementKind.EventCount, eventId: "hooked", minimum: 1f))),
                Stage("identified", 2, "Identified", Group(KnowledgeRequirementGroupMode.All, Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "identity", claimId: "species_identity"))),
                Stage("studied", 3, "Studied", Group(KnowledgeRequirementGroupMode.AtLeast, 3,
                    Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "size", claimId: "average_mass"),
                    Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "feeding", claimId: "diet"),
                    Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "behavior", claimId: "schooling_behavior"),
                    Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "traits", claimId: "trait_expression"),
                    Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "population", claimId: "population_estimate"))),
                Stage("cultivated", 4, "Cultivated", Group(KnowledgeRequirementGroupMode.All,
                    Requirement(KnowledgeRequirementKind.EventCount, eventId: "established", minimum: 1f),
                    Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "pond_compatibility", claimId: "preferred_water"))),
                Stage("documented", 5, "Documented", Group(KnowledgeRequirementGroupMode.All,
                    Requirement(KnowledgeRequirementKind.Documentation),
                    Requirement(KnowledgeRequirementKind.ClaimExists, facetId: "identity", claimId: "species_identity")))
            };
        }

        private static KnowledgeStageDef Stage(string id, int order, string label, KnowledgeRequirementGroup requirements)
        {
            return new KnowledgeStageDef
            {
                defName = id,
                label = label,
                description = label,
                order = order,
                minimumKnowledge = 0f,
                minimumConfidence = 0f,
                requirementGroup = requirements,
                contextSensitive = id == "cultivated"
            };
        }

        private static KnowledgeRequirement Requirement(KnowledgeRequirementKind kind, string facetId = null,
            string claimId = null, string eventId = null, float minimum = 0f)
        {
            return new KnowledgeRequirement
            {
                domainId = DomainId,
                kind = kind,
                facetId = facetId,
                claimId = claimId,
                eventId = eventId,
                minimum = minimum,
                label = claimId ?? eventId ?? kind.ToString()
            };
        }

        private static KnowledgeRequirementGroup Group(KnowledgeRequirementGroupMode mode, params KnowledgeRequirement[] requirements)
        {
            return new KnowledgeRequirementGroup { mode = mode, minimumCount = mode == KnowledgeRequirementGroupMode.AtLeast ? 1 : 0, requirements = requirements.ToList() };
        }

        private static KnowledgeRequirementGroup Group(KnowledgeRequirementGroupMode mode, int minimumCount, params KnowledgeRequirement[] requirements)
        {
            return new KnowledgeRequirementGroup { mode = mode, minimumCount = minimumCount, requirements = requirements.ToList() };
        }

        private static IReadOnlyList<KnowledgeMilestoneTrackDef> BuildMilestoneTracks()
        {
            return new[]
            {
                new KnowledgeMilestoneTrackDef
                {
                    defName = "AF_DiscoveryMilestones",
                    stableId = "discovery",
                    label = "Field discovery",
                    ordered = true,
                    milestones = new List<KnowledgeMilestoneDef>
                    {
                        Milestone("hooked", "Hooked", 0), Milestone("identified", "Identified", 1), Milestone("studied", "Studied", 2),
                        Milestone("cultivated", "Cultivated", 3), Milestone("documented", "Documented", 4)
                    }
                },
                new KnowledgeMilestoneTrackDef
                {
                    defName = "AF_HusbandryMilestones",
                    stableId = "husbandry",
                    label = "Husbandry milestones",
                    ordered = false,
                    milestones = new List<KnowledgeMilestoneDef>
                    {
                        Milestone("first_hatch", "First colony hatch", 0), Milestone("stable_population", "Stable population", 1),
                        Milestone("breed_registered", "Breed registered", 2), Milestone("breed_mastered", "Breed breeding true", 3),
                        Milestone("exceptional_lineage", "Multi-generation lineage", 4)
                    }
                }
            };
        }

        private static KnowledgeMilestoneDef Milestone(string id, string label, int order)
        {
            return new KnowledgeMilestoneDef
            {
                stableId = id,
                label = label,
                description = label,
                order = order,
                pauseBehavior = KnowledgeMilestonePauseBehavior.Pause,
                resetBehavior = KnowledgeMilestoneResetBehavior.Never,
                permanent = true
            };
        }

        private static void TryMigrateLegacy()
        {
            if (migrationChecked || migrating || !FrameworkReady) return;
            if (KnowledgeMigrationService.IsCommitted(LegacyMigrationId, LegacyMigrationVersion))
            {
                migrationChecked = true;
                return;
            }
            FishingProgressionComponent legacy = FishingProgressionComponent.Current;
            AquacultureJournalComponent journal = AquacultureJournalComponent.Current;
            if (legacy == null) return;
            migrating = true;
            try
            {
                List<bool> imports = new List<bool>();
                foreach (PawnFishingProgress progress in (legacy.PawnProgress ?? new List<PawnFishingProgress>()).Where(item => item?.pawn != null))
                {
                    foreach (KeyValuePair<string, float> pair in (progress.speciesKnowledge ?? new Dictionary<string, float>()).ToList())
                    {
                        ThingDef fish = DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                        if (fish == null) continue;
                        EnsureSpeciesSubject(fish);
                        float amount = FiniteLegacyKnowledge(pair.Value) * 100f;
                        imports.Add(KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                        {
                            consumerId = LegacyMigrationId + "/species/" + progress.pawn.thingIDNumber + "/" + fish.defName,
                            version = LegacyMigrationVersion,
                            domainId = DomainId,
                            subjectId = SpeciesSubjectId(fish),
                            pawn = progress.pawn,
                            personalKnowledge = amount,
                            colonyKnowledge = amount,
                            expertise = 0f
                        }));
                    }
                    imports.Add(KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                    {
                        consumerId = LegacyMigrationId + "/expertise/" + progress.pawn.thingIDNumber,
                        version = LegacyMigrationVersion,
                        domainId = DomainId,
                        subjectId = GlobalSubjectId,
                        pawn = progress.pawn,
                        expertise = FiniteLegacyKnowledge(progress.expertiseExperience)
                    }));
                }
                List<KnowledgeMilestoneConditionSample> milestones = new List<KnowledgeMilestoneConditionSample>();
                foreach (FishSpeciesJournalRecord record in journal?.SpeciesRecords ?? Enumerable.Empty<FishSpeciesJournalRecord>())
                {
                    ThingDef fish = record.FishDef;
                    if (fish == null) continue;
                    EnsureSpeciesSubject(fish);
                    AddLegacyMilestone(milestones, SpeciesSubjectId(fish), "hooked", record.discoveredTick >= 0);
                    AddLegacyMilestone(milestones, SpeciesSubjectId(fish), "identified", record.discoveredTick >= 0);
                    AddLegacyMilestone(milestones, SpeciesSubjectId(fish), "cultivated", record.establishedTick >= 0);
                    AddLegacyMilestone(milestones, SpeciesSubjectId(fish), "first_hatch", record.bredTick >= 0);
                    AddLegacyMilestone(milestones, SpeciesSubjectId(fish), "stable_population", record.stableTick >= 0);
                }
                List<KnowledgeSubjectRegistration> subjects = new List<KnowledgeSubjectRegistration>();
                List<KnowledgeSubjectRelation> relations = new List<KnowledgeSubjectRelation>();
                foreach (FishBreedRecord breed in journal?.Breeds ?? Enumerable.Empty<FishBreedRecord>())
                {
                    EnsureBreedSubject(breed);
                    KnowledgeSubjectRegistration subject = ResolveSubject(BreedSubjectId(breed.id));
                    if (subject != null) subjects.Add(subject);
                    ThingDef fish = breed.FishDef;
                    if (fish != null)
                    {
                        EnsureSpeciesSubject(fish);
                        relations.Add(new KnowledgeSubjectRelation
                        {
                            domainId = DomainId,
                            fromSubjectId = BreedSubjectId(breed.id),
                            toDomainId = DomainId,
                            toSubjectId = SpeciesSubjectId(fish),
                            relationTypeId = "breed_descendant",
                            confidence = 1f,
                            source = "legacy breed migration"
                        });
                    }
                }
                imports.Add(KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = LegacyMigrationId,
                    version = LegacyMigrationVersion,
                    domainId = DomainId,
                    subjectId = GlobalSubjectId,
                    milestones = milestones,
                    subjects = subjects,
                    relations = relations
                }));
                if (!AquacultureKnowledgeContract.CanFinalizeMigration(imports,
                    KnowledgeMigrationService.IsCommitted(LegacyMigrationId, LegacyMigrationVersion)))
                    throw new InvalidOperationException("Knowledge Framework did not durably commit every legacy import.");
                migrationChecked = true;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[Aquaculture - Fishing] V3 legacy migration deferred: " +
                    exception.GetBaseException().Message, GenText.StableStringHash("AquacultureLegacyMigration"));
            }
            finally { migrating = false; }
        }

        private static void TryCommitBalancedAggregationMigration()
        {
            if (!FrameworkReady || KnowledgeMigrationService.IsCommitted(BalancedAggregationMigrationId,
                BalancedAggregationMigrationVersion)) return;
            bool imported = KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
            {
                consumerId = BalancedAggregationMigrationId,
                version = BalancedAggregationMigrationVersion,
                domainId = DomainId,
                subjectId = GlobalSubjectId
            });
            if (!imported || !KnowledgeMigrationService.IsCommitted(BalancedAggregationMigrationId,
                BalancedAggregationMigrationVersion))
                Log.ErrorOnce("[Aquaculture - Fishing] Balanced knowledge-stage migration will retry.",
                    GenText.StableStringHash("AquacultureBalancedStageMigration"));
        }

        private static float FiniteLegacyKnowledge(float value)
        {
            return AquacultureKnowledgeContract.IsFiniteNonNegative(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static void AddLegacyMilestone(List<KnowledgeMilestoneConditionSample> target, string subjectId, string milestoneId, bool met)
        {
            if (!met) return;
            string track = milestoneId == "first_hatch" || milestoneId == "stable_population" ? "husbandry" : "discovery";
            target.Add(new KnowledgeMilestoneConditionSample
            {
                domainId = DomainId,
                subjectId = subjectId,
                trackId = track,
                milestoneId = milestoneId,
                conditionMet = true,
                elapsedTicks = int.MaxValue
            });
        }

        private static float LegacySpeciesKnowledge(Pawn pawn, ThingDef fishDef)
        {
            PawnFishingProgress progress = FishingProgressionComponent.Current?.ProgressFor(pawn, false);
            return progress != null && fishDef != null && progress.speciesKnowledge.TryGetValue(fishDef.defName, out float value)
                ? Mathf.Clamp01(value) : 0f;
        }

        private sealed class FishingCellContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context)
            {
                if (!TryMapCell(context, out Map map, out IntVec3 cell)) return KnowledgeContextKey.Empty;
                NaturalFishPopulationMapComponent populations = map.GetComponent<NaturalFishPopulationMapComponent>();
                NaturalWaterPopulation population = populations?.PopulationAt(cell);
                IntVec3 anchor = population?.anchor ?? cell;
                return new KnowledgeContextKey(ContextWaterBody, map.uniqueID + ":" + anchor.x + "," + anchor.z);
            }
        }

        private sealed class GlobalContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context) => KnowledgeContextKey.Empty;
        }

        private sealed class WaterBodyContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context)
            {
                string mapId = context.stableId?.Split(':').FirstOrDefault();
                return mapId.NullOrEmpty() ? KnowledgeContextKey.Empty : new KnowledgeContextKey(ContextMapRegion, mapId);
            }
        }

        private sealed class MapRegionContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context)
            {
                if (!int.TryParse(context.stableId, out int mapId)) return KnowledgeContextKey.Empty;
                Map map = MapById(mapId);
                return map?.Biome == null ? KnowledgeContextKey.Empty : new KnowledgeContextKey(ContextBiome, map.Biome.defName);
            }
        }

        private sealed class PondContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context)
            {
                if (!TryMapCell(context, out Map map, out IntVec3 cell)) return KnowledgeContextKey.Empty;
                PondWaterKind water = map.GetComponent<FishPondMapComponent>()?.WaterKindAt(cell) ?? PondWaterKind.Freshwater;
                return new KnowledgeContextKey(ContextPondType, map.uniqueID + ":" + water);
            }
        }

        private sealed class PondTypeContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context)
            {
                string mapPart = (context.stableId ?? string.Empty).Split(':').FirstOrDefault();
                if (!int.TryParse(mapPart, out int mapId)) return KnowledgeContextKey.Empty;
                Map map = MapById(mapId);
                return map?.Biome == null ? KnowledgeContextKey.Empty : new KnowledgeContextKey(ContextBiome, map.Biome.defName);
            }
        }

        private static bool TryMapCell(KnowledgeContextKey context, out Map map, out IntVec3 cell)
        {
            map = null;
            cell = IntVec3.Invalid;
            string[] pieces = (context.stableId ?? string.Empty).Split(':');
            if (pieces.Length < 2 || !int.TryParse(pieces[0], out int mapId)) return false;
            string[] coordinates = pieces[1].Split(',');
            if (coordinates.Length != 2 || !int.TryParse(coordinates[0], out int x) || !int.TryParse(coordinates[1], out int z)) return false;
            map = MapById(mapId);
            cell = new IntVec3(x, 0, z);
            return map != null && cell.InBounds(map);
        }

        private static Map MapById(int id) => Find.Maps?.FirstOrDefault(map => map.uniqueID == id);

        private sealed class AquacultureKnowledgeUi : IKnowledgeDomainUiV3, IKnowledgeDomainUiV2
        {
            public string DomainId => AquacultureKnowledgeAdapter.DomainId;
            public int Priority => 100;
            public IEnumerable<string> ListBadges(KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope)
            {
                if (row == null) yield break;
                yield return row.lastStage ?? "Unknown";
                if (row.confidence > 0f) yield return "Confidence " + row.confidence.ToStringPercent();
            }

            public IEnumerable<string> ListColumns(KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope)
            {
                if (row == null) yield break;
                yield return row.evidenceCount + " evidence";
                if (row.usedContextFallback) yield return "Context inherited";
            }

            public void DrawDetailPanels(Rect rect, KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope)
            {
                if (row == null) return;
                GUI.color = new Color(0.70f, 0.84f, 0.90f);
                Widgets.Label(rect, "Aquatic dossier: evidence is separated from universal species data and pond observations.");
                GUI.color = Color.white;
            }

            public IEnumerable<FloatMenuOption> SubjectActions(KnowledgeSubjectSnapshot subject, Pawn pawn, KnowledgeScope scope)
            {
                if (subject?.id?.StartsWith("species:", StringComparison.Ordinal) != true) yield break;
                ThingDef fish = DefDatabase<ThingDef>.GetNamedSilentFail(subject.id.Substring("species:".Length));
                if (fish != null && pawn != null) yield return new FloatMenuOption("Document species", () => DocumentSpecies(pawn, fish));
                yield return new FloatMenuOption("Open Aquaculture Journal", () => MainTabWindow_AquacultureJournal.OpenSpecies(fish));
            }

            public void DrawSubjectDetails(Rect rect, KnowledgeSubjectSnapshot subject, Pawn pawn, KnowledgeScope scope)
            {
                if (subject == null) return;
                GUI.color = Color.gray;
                Widgets.Label(rect, "Use the Aquaculture Field Journal for pond, water, breed, and record views.");
                GUI.color = Color.white;
            }
        }
    }
}
