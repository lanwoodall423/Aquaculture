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

        private static bool registered;
        private static bool migrating;
        private static bool migrationChecked;
        private static int eventSequence;

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

        public static void Register()
        {
            if (!registered)
            {
                KnowledgeRegistry.RegisterDomain(new KnowledgeDomainRegistration
                {
                    id = DomainId,
                    label = "Aquaculture",
                    description = "Evidence-based knowledge of fish, waters, ponds, anglers, and cultivated lines.",
                    enableUncertainty = true,
                    enableFamiliarity = true,
                    sharingModel = KnowledgeSharingModel.Reportable,
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
                RegisterContexts();
                RegisterRelations();
                KnowledgeProviderRegistry.Register(DomainId, 20, EntryFor);
                KnowledgeV3Ui.Register(new AquacultureKnowledgeUi(), true);
                registered = true;
            }
            AquacultureDiscoveryDirector.Register();
            TryMigrateLegacy();
        }

        public static void Handle(AquacultureEvent value)
        {
            if (value == null) return;
            Register();
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
            List<string> known = new List<string>();
            float knowledge = 0f;
            float confidence = 0f;
            foreach (string facet in FacetIds)
            {
                KnowledgeFacetSnapshotV2 value = KnowledgeQuery.Facet(DomainId, SpeciesSubjectId(fishDef), facet,
                    colony ? null : pawn, scope, true, false);
                if (value.amount > 0f) known.Add(facet);
                knowledge += value.amount;
                confidence = Mathf.Max(confidence, value.confidence);
            }
            KnowledgeClaimSnapshot identity = Claim(SpeciesSubjectId(fishDef), "identity", "species_identity", pawn, colony,
                KnowledgeContextKey.Empty);
            return new AquacultureKnowledgeView(SpeciesSubjectId(fishDef), subject.stageId ?? "Unknown", Mathf.Clamp01(knowledge / 100f),
                confidence, identity.value != null && identity.effectiveConfidence > 0f, known);
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
                map.uniqueID + ":" + cell.x + "," + cell.z);
        }

        public static KnowledgeContextKey ContextForPond(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid) return KnowledgeContextKey.Empty;
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            PondProxyThing proxy = component?.ProxyFor(cell);
            IntVec3 anchor = proxy?.Position ?? cell;
            return new KnowledgeContextKey(ContextPond, map.uniqueID + ":" + anchor.x + "," + anchor.z);
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

        private static bool FrameworkReady => registered && GameComponent_KnowledgeFramework.Current != null &&
            KnowledgeRegistry.Schema(DomainId) != null;

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
            Submit(value.pawn, SpeciesSubjectId(value.fishDef), "identity", "catch", 8f, 8f + (1f - SpeciesKnowledgeFor(value.pawn, value.fishDef)) * 4f,
                1f, context, measurements(false), value.source ?? "fishing");
            SubmitColony(SpeciesSubjectId(value.fishDef), "identity", "catch", 5f, 1f, context, measurements(true), value.source ?? "fishing");
            if (value.fish != null) ObserveFish(value, "survey", 2f, 0f);
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
            Submit(value.pawn, SpeciesSubjectId(value.fishDef), "pond_compatibility", "established", 6f, 0f, 2f,
                context, measurements, "pond-establishment");
            SubmitColony(SpeciesSubjectId(value.fishDef), "pond_compatibility", "established", 5f, 1f, context,
                measurements.Select(item => CloneMeasurement(item, true)).ToList(), "pond-establishment");
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
            Submit(pawn, subjectId, facetId, reason, knowledge, expertise, familiarity, context,
                measurements?.Select(item => CloneMeasurement(item, false)).ToList(), source);
            SubmitColony(subjectId, facetId, reason, knowledge * 0.7f, familiarity * 0.7f, context,
                measurements?.Select(item => CloneMeasurement(item, true)).ToList(), source);
        }

        private static bool Submit(Pawn pawn, string subjectId, string facetId, string reason, float knowledge, float expertise,
            float familiarity, KnowledgeContextKey context, IEnumerable<KnowledgeMeasurement> measurements, string source)
        {
            if (!FrameworkReady || pawn == null || subjectId.NullOrEmpty()) return false;
            KnowledgeObservation observation = new KnowledgeObservation
            {
                observer = pawn,
                domainId = DomainId,
                subjectId = subjectId,
                facetId = facetId,
                observationId = ObservationId(reason),
                methodId = reason,
                directKnowledge = Mathf.Max(0f, knowledge),
                directExpertise = Mathf.Max(0f, expertise),
                directFamiliarity = Mathf.Max(0f, familiarity),
                expertiseTrackId = ExpertiseTrackId,
                success = true,
                quality = 1f,
                novelty = 1f,
                repetition = 1f,
                sourceReliability = 1f,
                reasonId = reason,
                source = source,
                sourceInstanceId = source + ":" + pawn.thingIDNumber + ":" + (++eventSequence),
                context = context,
                summary = source,
                claimMeasurements = measurements?.ToList(),
                witnessDistribution = WitnessDistribution(pawn),
                suppressConfiguredKnowledge = true
            };
            return KnowledgeEngine.Submit(new KnowledgeTransaction
            {
                source = source,
                transactionId = "aquaculture:" + source + ":" + eventSequence
            }.Add(observation)).success;
        }

        private static bool SubmitColony(string subjectId, string facetId, string reason, float knowledge, float familiarity,
            KnowledgeContextKey context, IEnumerable<KnowledgeMeasurement> measurements, string source)
        {
            if (!FrameworkReady || subjectId.NullOrEmpty()) return false;
            KnowledgeObservation observation = new KnowledgeObservation
            {
                domainId = DomainId,
                subjectId = subjectId,
                facetId = facetId,
                observationId = ObservationId(reason),
                methodId = reason,
                targetColony = true,
                directKnowledge = Mathf.Max(0f, knowledge),
                directFamiliarity = Mathf.Max(0f, familiarity),
                success = true,
                quality = 1f,
                novelty = 1f,
                repetition = 1f,
                sourceReliability = 1f,
                reasonId = reason,
                source = source,
                sourceInstanceId = source + ":colony:" + (++eventSequence),
                context = context,
                summary = source,
                claimMeasurements = measurements?.Select(item => CloneMeasurement(item, true)).ToList(),
                suppressConfiguredKnowledge = true
            };
            return KnowledgeEngine.Submit(new KnowledgeTransaction
            {
                source = source,
                transactionId = "aquaculture:colony:" + source + ":" + eventSequence
            }.Add(observation)).success;
        }

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
                sourceInstanceId = "event:" + (++eventSequence),
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
            return clone;
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

        private static string ObservationId(string reason)
        {
            string id = "observe_" + (reason ?? "field").Replace('-', '_');
            return new[] { "observe_hooked", "observe_catch", "observe_escape", "observe_established", "observe_pond",
                "observe_feed", "observe_breed", "observe_hatch", "observe_survey", "observe_trait_observed",
                "observe_health_event", "observe_health_recovered" }.Contains(id) ? id : "observe_field";
        }

        private static bool IsPlayerReadableTrait(string name)
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
            KnowledgeRelationService.Add(new KnowledgeSubjectRelation
            {
                domainId = DomainId,
                fromSubjectId = IndividualSubjectId(parent.parent),
                toDomainId = DomainId,
                toSubjectId = childId,
                relationTypeId = type,
                confidence = 1f,
                source = "Aquaculture breeding",
                tick = Find.TickManager?.TicksGame ?? 0
            });
        }

        private static void AddBreedRelation(CompFishTraits fish, string targetId, string type)
        {
            if (fish?.parent == null || targetId.NullOrEmpty()) return;
            KnowledgeRelationService.Add(new KnowledgeSubjectRelation
            {
                domainId = DomainId,
                fromSubjectId = IndividualSubjectId(fish.parent),
                toDomainId = DomainId,
                toSubjectId = targetId,
                relationTypeId = type,
                confidence = 1f,
                source = "Aquaculture breeding",
                tick = Find.TickManager?.TicksGame ?? 0
            });
        }

        private static void AddParentRelationById(int parentId, string childId)
        {
            if (parentId <= 0 || childId.NullOrEmpty()) return;
            Thing parent = Find.Maps.SelectMany(map => map.listerThings.AllThings)
                .FirstOrDefault(thing => thing.thingIDNumber == parentId);
            if (parent == null) return;
            KnowledgeRelationService.Add(new KnowledgeSubjectRelation
            {
                domainId = DomainId,
                fromSubjectId = IndividualSubjectId(parent),
                toDomainId = DomainId,
                toSubjectId = childId,
                relationTypeId = "parent_offspring",
                confidence = 1f,
                source = "Aquaculture breeding",
                tick = Find.TickManager?.TicksGame ?? 0
            });
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
            KnowledgeRegistry.RegisterSubject(DomainId, subject, new KnowledgeRegistrationOptions
            {
                source = "lan.aquaculture.fishing.v3",
                priority = 100,
                conflict = KnowledgeRegistrationConflict.Replace
            });
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
                Thing thing = Find.Maps.SelectMany(map => map.listerThings.AllThings).FirstOrDefault(item => item.thingIDNumber == number);
                return thing == null ? null : new KnowledgeSubjectRegistration
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

        private static void RegisterContexts()
        {
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextGlobal", stableId = ContextGlobal }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextBiome", stableId = ContextBiome }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextMapRegion", stableId = ContextMapRegion, parentTypeId = ContextBiome }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextWaterBody", stableId = ContextWaterBody, parentTypeId = ContextMapRegion }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextFishingCell", stableId = ContextFishingCell, parentTypeId = ContextWaterBody }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextPond", stableId = ContextPond, parentTypeId = ContextPondType }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "AF_ContextPondType", stableId = ContextPondType, parentTypeId = ContextBiome }, true);
            KnowledgeContextRegistry.RegisterResolver(ContextFishingCell, new FishingCellContextResolver(), true);
            KnowledgeContextRegistry.RegisterResolver(ContextWaterBody, new WaterBodyContextResolver(), true);
            KnowledgeContextRegistry.RegisterResolver(ContextMapRegion, new MapRegionContextResolver(), true);
            KnowledgeContextRegistry.RegisterResolver(ContextPond, new PondContextResolver(), true);
            KnowledgeContextRegistry.RegisterResolver(ContextPondType, new PondTypeContextResolver(), true);
        }

        private static void RegisterRelations()
        {
            KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
            {
                defName = "AF_RelationParentOffspring",
                stableId = "parent_offspring",
                label = "Parent / offspring",
                parentage = true,
                symmetric = false,
                inverseTypeId = "parent_offspring",
                metadataLimit = 8
            }, true);
            KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
            {
                defName = "AF_RelationBreedDescendant",
                stableId = "breed_descendant",
                label = "Breed / descendant",
                parentage = false,
                symmetric = false,
                metadataLimit = 8
            }, true);
            KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
            {
                defName = "AF_RelationTraitOrigin",
                stableId = "trait_origin",
                label = "Trait origin",
                parentage = false,
                symmetric = false,
                metadataLimit = 8
            }, true);
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
                Claim("population_estimate", "population", KnowledgeClaimValueType.NumericRange, KnowledgeClaimAggregation.Latest, KnowledgeClaimStalenessPolicy.Contextual),
                Claim("breeding_season", "breeding", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Seasonal),
                Claim("maturity_time", "breeding", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("compatibility", "pond_compatibility", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Contextual),
                Claim("trait_expression", "traits", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("food_demand", "feeding", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.SlowlyStale),
                Claim("stress_tolerance", "health", KnowledgeClaimValueType.Percentage, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.Permanent),
                Claim("health_status", "health", KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.Latest, KnowledgeClaimStalenessPolicy.Contextual),
                Claim("breeding_readiness", "breeding", KnowledgeClaimValueType.Percentage, KnowledgeClaimAggregation.Latest, KnowledgeClaimStalenessPolicy.Contextual),
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
                categoryId = "aquaculture",
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
                foreach (PawnFishingProgress progress in legacy.PawnProgress.Where(item => item?.pawn != null))
                {
                    foreach (KeyValuePair<string, float> pair in progress.speciesKnowledge.ToList())
                    {
                        ThingDef fish = DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                        if (fish == null) continue;
                        EnsureSpeciesSubject(fish);
                        float amount = Mathf.Clamp01(pair.Value) * 100f;
                        KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                        {
                            consumerId = LegacyMigrationId + "/species/" + progress.pawn.thingIDNumber + "/" + fish.defName,
                            version = LegacyMigrationVersion,
                            domainId = DomainId,
                            subjectId = SpeciesSubjectId(fish),
                            pawn = progress.pawn,
                            personalKnowledge = amount,
                            colonyKnowledge = amount,
                            expertise = 0f
                        });
                    }
                    KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                    {
                        consumerId = LegacyMigrationId + "/expertise/" + progress.pawn.thingIDNumber,
                        version = LegacyMigrationVersion,
                        domainId = DomainId,
                        subjectId = GlobalSubjectId,
                        pawn = progress.pawn,
                        expertise = Mathf.Max(0f, progress.expertiseExperience)
                    });
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
                    subjects.Add(ResolveSubject(BreedSubjectId(breed.id)));
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
                KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = LegacyMigrationId,
                    version = LegacyMigrationVersion,
                    domainId = DomainId,
                    subjectId = GlobalSubjectId,
                    milestones = milestones,
                    subjects = subjects,
                    relations = relations
                });
                migrationChecked = true;
            }
            catch (Exception exception)
            {
                Log.Warning("[Aquaculture - Fishing] V3 legacy migration deferred: " + exception.GetBaseException().Message);
            }
            finally { migrating = false; }
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
                return new KnowledgeContextKey(ContextPondType, water.ToString());
            }
        }

        private sealed class PondTypeContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context)
            {
                Map map = Find.CurrentMap;
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
