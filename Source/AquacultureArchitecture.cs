using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public enum AquacultureEventKind
    {
        FishingStarted,
        FishingPhase,
        FishHooked,
        FishCaught,
        FishStocked,
        FishEscaped,
        FishDied,
        FishEstablished,
        FishObserved,
        FishFed,
        FishBred,
        ColonyBorn,
        TraitObserved,
        PopulationSurveyed,
        EcologyWarning,
        EcologyRecovered,
        FishRecord
    }

    /// <summary>One bounded message between authoritative simulation systems and knowledge/UI consumers.</summary>
    public sealed class AquacultureEvent
    {
        public AquacultureEventKind kind;
        public Pawn pawn;
        public Map map;
        public IntVec3 cell;
        public ThingDef fishDef;
        public CompFishTraits fish;
        public CompFishTraits firstFish;
        public CompFishTraits secondFish;
        public FishingAttemptRecord attempt;
        public string phase;
        public string reason;
        public string source;
        public string logicalEventId;
        public float progress;
        public float value;
        public int generation;
        public bool success;
        public IReadOnlyList<string> traits;
    }

    /// <summary>Routes meaningful domain events; simulation systems do not know about journal or framework storage.</summary>
    public static class AquacultureEventRouter
    {
        private static readonly List<Action<AquacultureEvent>> Subscribers = new List<Action<AquacultureEvent>>();
        private static int sequence;

        public static int Revision { get; private set; }

        public static void Subscribe(Action<AquacultureEvent> handler)
        {
            if (handler != null && !Subscribers.Contains(handler)) Subscribers.Add(handler);
        }

        public static void Unsubscribe(Action<AquacultureEvent> handler)
        {
            if (handler != null) Subscribers.Remove(handler);
        }

        public static void Publish(AquacultureEvent value)
        {
            if (value == null) return;
            Revision++;
            AquacultureSnapshotCache.Invalidate();
            for (int i = 0; i < Subscribers.Count; i++)
            {
                try { Subscribers[i](value); }
                catch (Exception exception)
                {
                    Log.ErrorOnce("Aquaculture event consumer failed: " + exception,
                        GenText.StableStringHash("AquacultureEventRouter:" + Subscribers[i].Method.Name));
                }
            }
        }

        public static void FishingStarted(Pawn pawn, Map map, IntVec3 cell, string source)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishingStarted,
                pawn = pawn,
                map = map,
                cell = cell,
                source = source,
                logicalEventId = AquacultureKnowledgeContract.StableEventId("fishing-started",
                    pawn?.thingIDNumber.ToString(), CurrentTick(), StableMapIdentity(map), cell.ToString()),
                value = ++sequence
            });
        }

        public static void FishingPhase(Pawn pawn, Map map, IntVec3 cell, string phase, float progress)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishingPhase,
                pawn = pawn,
                map = map,
                cell = cell,
                phase = phase,
                progress = Mathf.Clamp01(progress),
                source = "fishing",
                logicalEventId = AquacultureKnowledgeContract.StableEventId("fishing-phase",
                    pawn?.thingIDNumber.ToString(), CurrentTick(), StableMapIdentity(map), phase)
            });
        }

        public static void FishHooked(FishingAttemptRecord attempt)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishHooked,
                pawn = attempt?.pawn,
                map = attempt?.pawn?.Map,
                cell = attempt?.waterCell ?? IntVec3.Invalid,
                fishDef = attempt?.FishDef,
                attempt = attempt,
                success = attempt?.fishBit == true,
                source = attempt?.framework,
                logicalEventId = AttemptEventId("fish-hooked", attempt, null)
            });
        }

        public static void FishCaught(FishingAttemptRecord attempt, CompFishTraits fish = null)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishCaught,
                pawn = attempt?.pawn,
                map = attempt?.pawn?.Map,
                cell = attempt?.waterCell ?? IntVec3.Invalid,
                fishDef = fish?.parent?.def ?? attempt?.FishDef,
                fish = fish,
                attempt = attempt,
                success = true,
                source = attempt?.framework,
                traits = fish?.traitDefNames?.ToList() ?? attempt?.hookedTraitNames?.ToList(),
                logicalEventId = AttemptEventId("fish-caught", attempt, fish)
            });
        }

        public static void FishStocked(Map map, IntVec3 cell, ThingDef fishDef, float amount, float resultingPopulation,
            string logicalEventId = null)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishStocked,
                map = map,
                cell = cell,
                fishDef = fishDef,
                progress = Mathf.Max(0f, amount),
                value = Mathf.Max(0f, resultingPopulation),
                source = "natural-population",
                logicalEventId = logicalEventId ?? AquacultureKnowledgeContract.StableEventId("fish-stocked",
                    fishDef?.defName, CurrentTick(), StableMapIdentity(map), cell.ToString() + ":" +
                    resultingPopulation.ToString("R", CultureInfo.InvariantCulture))
            });
        }

        public static void FishEscaped(FishingAttemptRecord attempt, string reason)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishEscaped,
                pawn = attempt?.pawn,
                map = attempt?.pawn?.Map,
                cell = attempt?.waterCell ?? IntVec3.Invalid,
                fishDef = attempt?.FishDef,
                attempt = attempt,
                reason = reason,
                source = attempt?.framework,
                logicalEventId = AttemptEventId("fish-escaped", attempt, null)
            });
        }

        public static void FishDied(CompFishTraits fish, string reason)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishDied,
                map = fish?.parent?.Map,
                cell = fish?.parent?.Position ?? IntVec3.Invalid,
                fishDef = fish?.parent?.def,
                fish = fish,
                reason = reason,
                source = "aquaculture",
                logicalEventId = FishEventId("fish-died", fish, null, reason)
            });
        }

        public static string CatchEventId(FishingAttemptRecord attempt)
        {
            return AttemptEventId("fish-caught", attempt, null);
        }

        public static void FishEstablished(CompFishTraits fish)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishEstablished,
                map = fish?.parent?.Map,
                cell = fish?.parent?.Position ?? IntVec3.Invalid,
                fishDef = fish?.parent?.def,
                fish = fish,
                source = "pond",
                logicalEventId = FishEventId("fish-established", fish, null, null)
            });
        }

        public static void ColonyBorn(CompFishTraits fish)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.ColonyBorn,
                map = fish?.parent?.Map,
                cell = fish?.parent?.Position ?? IntVec3.Invalid,
                fishDef = fish?.parent?.def,
                fish = fish,
                generation = fish?.breedGeneration ?? 0,
                source = "breeding",
                traits = fish?.traitDefNames?.ToList(),
                logicalEventId = FishEventId("colony-born", fish, null, fish?.breedGeneration.ToString())
            });
        }

        public static void FishBred(CompFishTraits first, CompFishTraits second, CompFishTraits child = null)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishBred,
                map = first?.parent?.Map,
                cell = first?.parent?.Position ?? IntVec3.Invalid,
                fishDef = child?.parent?.def ?? first?.parent?.def,
                fish = child,
                firstFish = first,
                secondFish = second,
                generation = child?.breedGeneration ?? Mathf.Max(first?.breedGeneration ?? 0, second?.breedGeneration ?? 0) + 1,
                source = "breeding",
                traits = child?.traitDefNames?.ToList(),
                logicalEventId = FishEventId("fish-bred", child ?? first, first, second?.parent?.thingIDNumber.ToString())
            });
        }

        public static void FishObserved(CompFishTraits fish, Pawn observer, string method, string reason = null)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.FishObserved,
                pawn = observer,
                map = fish?.parent?.Map,
                cell = fish?.parent?.Position ?? IntVec3.Invalid,
                fishDef = fish?.parent?.def,
                fish = fish,
                phase = method,
                reason = reason,
                source = "observation",
                traits = fish?.traitDefNames?.ToList(),
                logicalEventId = FishEventId("fish-observed", fish, null, method, true)
            });
        }

        public static void TraitObserved(CompFishTraits fish, Pawn observer, string trait)
        {
            Publish(new AquacultureEvent
            {
                kind = AquacultureEventKind.TraitObserved,
                pawn = observer,
                map = fish?.parent?.Map,
                cell = fish?.parent?.Position ?? IntVec3.Invalid,
                fishDef = fish?.parent?.def,
                fish = fish,
                reason = trait,
                source = "trait-observation",
                traits = fish?.traitDefNames?.ToList(),
                logicalEventId = FishEventId("trait-observed", fish, null, trait)
            });
        }

        public static void Ecology(AquacultureEventKind kind, Map map, IntVec3 cell, ThingDef fishDef, float value, string reason)
        {
            if (kind != AquacultureEventKind.EcologyWarning && kind != AquacultureEventKind.EcologyRecovered &&
                kind != AquacultureEventKind.PopulationSurveyed && kind != AquacultureEventKind.FishFed)
                kind = AquacultureEventKind.PopulationSurveyed;
            Publish(new AquacultureEvent
            {
                kind = kind,
                map = map,
                cell = cell,
                fishDef = fishDef,
                value = value,
                reason = reason,
                source = "ecology",
                logicalEventId = AquacultureKnowledgeContract.StableEventId(kind.ToString(), fishDef?.defName,
                    CurrentTick(), StableMapIdentity(map), cell + ":" + reason)
            });
        }

        private static int CurrentTick() => Find.TickManager?.TicksGame ?? 0;

        private static string AttemptEventId(string kind, FishingAttemptRecord attempt, CompFishTraits fish)
        {
            return AquacultureKnowledgeContract.StableEventId(kind,
                attempt?.pawn?.thingIDNumber.ToString() ?? fish?.parent?.thingIDNumber.ToString(),
                attempt?.startedTick ?? CurrentTick(), StableMapIdentity(attempt?.pawn?.Map),
                attempt?.waterCell.ToString() + ":" +
                (attempt?.FishDef?.defName ?? fish?.parent?.def?.defName));
        }

        private static string FishEventId(string kind, CompFishTraits fish, CompFishTraits other, string detail,
            bool includeCurrentTick = false)
        {
            return AquacultureKnowledgeContract.StableEventId(kind, fish?.parent?.thingIDNumber.ToString(),
                includeCurrentTick ? CurrentTick() : fish?.birthTick ?? 0, StableMapIdentity(fish?.parent?.Map),
                (other?.parent?.thingIDNumber.ToString() ?? string.Empty) + ":" + detail);
        }

        private static string StableMapIdentity(Map map)
        {
            return map?.Parent?.GetUniqueLoadID() ?? (map == null ? "map:none" : "tile:" + map.Tile);
        }
    }

    /// <summary>Consumes domain events and coordinates the legacy journal, V3 knowledge, and restrained notices.</summary>
    public static class AquacultureDiscoveryDirector
    {
        private static bool registered;
        private static readonly Dictionary<string, int> LastNotice = new Dictionary<string, int>();

        public static void Register()
        {
            if (registered) return;
            registered = true;
            AquacultureEventRouter.Subscribe(Handle);
        }

        private static void Handle(AquacultureEvent value)
        {
            AquacultureKnowledgeAdapter.Handle(value);
            AquacultureJournalComponent journal = AquacultureJournalComponent.Current;
            if (journal == null) return;
            switch (value.kind)
            {
                case AquacultureEventKind.FishCaught:
                    if (value.fish != null) journal.NotifyCaught(value.fish, value.pawn);
                    else journal.NotifyFishingCatch(value.fishDef, value.pawn);
                    break;
                case AquacultureEventKind.FishEstablished:
                    journal.NotifyEstablished(value.fish);
                    break;
                case AquacultureEventKind.ColonyBorn:
                    journal.NotifyColonyBorn(value.fish);
                    break;
                case AquacultureEventKind.TraitObserved:
                case AquacultureEventKind.FishObserved:
                    journal.NotifyFishRecord(value.fish);
                    break;
            }
            if (value.kind == AquacultureEventKind.EcologyWarning && AquacultureMod.Settings?.showPondAlerts == true)
                NotifyWarning(value);
        }

        private static void NotifyWarning(AquacultureEvent value)
        {
            string key = (value.map?.uniqueID.ToString() ?? "map") + ":" + value.cell + ":" + value.reason;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (LastNotice.TryGetValue(key, out int last) && now - last < 60000) return;
            LastNotice[key] = now;
            if (!value.reason.NullOrEmpty()) Messages.Message(value.reason, MessageTypeDefOf.CautionInput, false);
        }
    }

    public enum AquacultureRecommendationKind
    {
        Known,
        Inferred,
        Predicted
    }

    public sealed class AquacultureRecommendation
    {
        public string label;
        public string explanation;
        public AquacultureRecommendationKind kind;
        public float confidence;
        public ThingDef fishDef;
        public FishingTacklePartDef tackle;

        public string KindLabel => kind.ToString();
    }

    public static class AquacultureRecommendationService
    {
        public static AquacultureRecommendation BestKnownLure(Pawn pawn, IntVec3 cell)
        {
            if (pawn?.Map == null) return null;
            List<ThingDef> known = AquacultureKnowledgeAdapter.KnownSpeciesFor(pawn).ToList();
            if (known.Count == 0) return new AquacultureRecommendation
            {
                label = "No lure recommendation yet",
                explanation = "Catch or survey a local species before the journal recommends tackle.",
                kind = AquacultureRecommendationKind.Predicted,
                confidence = 0f
            };
            PondWaterKind water = AquaticSpeciesProfile.For(known[0]).waterKind;
            NaturalFishPopulationMapComponent populations = pawn.Map.GetComponent<NaturalFishPopulationMapComponent>();
            if (populations?.TryGetPreparedSummary(cell, out NaturalFishPopulationSummary summary) == true && summary.presentSpecies.Count > 0)
            {
                known = known.Where(summary.presentSpecies.Contains).ToList();
                if (known.Count > 0) water = AquaticSpeciesProfile.For(known[0]).waterKind;
            }
            FishingTacklePartDef best = DefDatabase<FishingTacklePartDef>.AllDefsListForReading
                .Where(part => part.slot == FishingTackleSlot.Lure && part.Available)
                .OrderByDescending(part => known.Sum(fish => AttractionFor(part, fish)))
                .ThenBy(part => part.defName).FirstOrDefault();
            if (best == null) return null;
            float confidence = Mathf.Clamp01(known.Count / 3f);
            return new AquacultureRecommendation
            {
                label = best.LabelCap.ToString(),
                explanation = "Known for " + string.Join(", ", known.Select(fish => fish.LabelCap.ToString()).ToArray()) +
                    ". The recommendation uses only recorded species evidence.",
                kind = AquacultureRecommendationKind.Known,
                confidence = confidence,
                tackle = best
            };
        }

        public static AquacultureRecommendation BestPawnForTrip(IEnumerable<Pawn> pawns, ThingDef fishDef)
        {
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (Pawn pawn in pawns ?? Enumerable.Empty<Pawn>())
            {
                if (pawn?.skills == null) continue;
                float score = pawn.skills.GetSkill(SkillDefOf.Animals).Level +
                    AquacultureKnowledgeAdapter.ExpertiseFor(pawn) * 4f +
                    AquacultureKnowledgeAdapter.SpeciesKnowledgeFor(pawn, fishDef) * 8f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = pawn;
            }
            if (best == null) return null;
            return new AquacultureRecommendation
            {
                label = best.LabelShortCap,
                explanation = "Animals skill and recorded fishing expertise favor this angler.",
                kind = AquacultureRecommendationKind.Inferred,
                confidence = Mathf.Clamp01(bestScore / 40f)
            };
        }

        private static float AttractionFor(FishingTacklePartDef lure, ThingDef fish)
        {
            if (lure == null || fish == null) return 0f;
            FishFishingExtension extension = fish.GetModExtension<FishFishingExtension>();
            PondWaterKind water = AquaticSpeciesProfile.For(fish).waterKind;
            float lureFactor = water == PondWaterKind.Saltwater ? lure.saltwaterAttraction : lure.freshwaterAttraction;
            float fishFactor = water == PondWaterKind.Saltwater ? extension?.saltwaterAttraction ?? 1f : extension?.freshwaterAttraction ?? 1f;
            return Mathf.Max(0.01f, lureFactor * fishFactor);
        }
    }

    public sealed class AquacultureSpeciesViewSnapshot
    {
        public readonly ThingDef fishDef;
        public readonly FishSpeciesJournalRecord record;
        public readonly string subjectId;
        public readonly string stageId;
        public readonly float knowledge;
        public readonly float confidence;
        public readonly bool identityKnown;
        public readonly IReadOnlyList<string> knownFacets;

        public AquacultureSpeciesViewSnapshot(ThingDef fishDef, FishSpeciesJournalRecord record, string subjectId,
            string stageId, float knowledge, float confidence, bool identityKnown, IEnumerable<string> knownFacets)
        {
            this.fishDef = fishDef;
            this.record = record;
            this.subjectId = subjectId;
            this.stageId = stageId;
            this.knowledge = knowledge;
            this.confidence = confidence;
            this.identityKnown = identityKnown;
            this.knownFacets = (knownFacets ?? Enumerable.Empty<string>()).Distinct().ToList();
        }
    }

    public sealed class AquacultureJournalViewSnapshot
    {
        public readonly IReadOnlyList<AquacultureSpeciesViewSnapshot> species;
        public readonly IReadOnlyList<FishBreedRecord> breeds;
        public readonly int discovered;
        public readonly int totalSpecies;
        public readonly int revision;

        public AquacultureJournalViewSnapshot(IEnumerable<AquacultureSpeciesViewSnapshot> species,
            IEnumerable<FishBreedRecord> breeds, int revision)
        {
            this.species = (species ?? Enumerable.Empty<AquacultureSpeciesViewSnapshot>()).ToList();
            this.breeds = (breeds ?? Enumerable.Empty<FishBreedRecord>()).ToList();
            this.discovered = this.species.Count(item => item.record?.discoveredTick >= 0 || item.identityKnown);
            totalSpecies = this.species.Count;
            this.revision = revision;
        }
    }

    public static class AquacultureSnapshotCache
    {
        private static readonly Dictionary<string, AquacultureJournalViewSnapshot> JournalByPawn =
            new Dictionary<string, AquacultureJournalViewSnapshot>();
        private static readonly Dictionary<Map, List<PondMenuSnapshot>> PondsByMap =
            new Dictionary<Map, List<PondMenuSnapshot>>();
        private static readonly Dictionary<Map, IReadOnlyList<NaturalWaterViewSnapshot>> WatersByMap =
            new Dictionary<Map, IReadOnlyList<NaturalWaterViewSnapshot>>();
        private static int invalidation = 1;

        public static int Revision => invalidation;

        public static void Invalidate()
        {
            invalidation++;
            JournalByPawn.Clear();
            PondsByMap.Clear();
            WatersByMap.Clear();
        }

        public static AquacultureJournalViewSnapshot Journal(Pawn pawn, bool colony)
        {
            string key = (colony ? "colony" : "pawn:") + (pawn?.thingIDNumber.ToString() ?? "none");
            if (JournalByPawn.TryGetValue(key, out AquacultureJournalViewSnapshot cached) && cached.revision == invalidation)
                return cached;
            AquacultureJournalComponent journal = AquacultureJournalComponent.Current;
            List<AquacultureSpeciesViewSnapshot> species = new List<AquacultureSpeciesViewSnapshot>();
            foreach (ThingDef fish in DefDatabase<ThingDef>.AllDefsListForReading.Where(FishUtility.IsFish).OrderBy(def => def.label).ThenBy(def => def.defName))
            {
                FishSpeciesJournalRecord record = journal?.RecordFor(fish, false);
                AquacultureKnowledgeView view = AquacultureKnowledgeAdapter.SpeciesView(fish, pawn, colony);
                species.Add(new AquacultureSpeciesViewSnapshot(fish, record, view.subjectId, view.stageId, view.knowledge,
                    view.confidence, view.identityKnown, view.knownFacets));
            }
            AquacultureJournalViewSnapshot result = new AquacultureJournalViewSnapshot(species, journal?.Breeds, invalidation);
            JournalByPawn[key] = result;
            return result;
        }

        public static IReadOnlyList<PondMenuSnapshot> Ponds(Map map)
        {
            if (map == null) return Array.Empty<PondMenuSnapshot>();
            if (PondsByMap.TryGetValue(map, out List<PondMenuSnapshot> cached)) return cached;
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            List<PondMenuSnapshot> result = new List<PondMenuSnapshot>();
            foreach (Thing proxy in map.listerThings.AllThings.Where(thing => thing is PondProxyThing))
            {
                PondMenuSnapshot snapshot = component?.MenuSnapshotAt(proxy.Position);
                if (snapshot != null) result.Add(snapshot);
            }
            PondsByMap[map] = result;
            return result;
        }

        public static IReadOnlyList<NaturalWaterViewSnapshot> Waters(Map map)
        {
            if (map == null) return Array.Empty<NaturalWaterViewSnapshot>();
            if (WatersByMap.TryGetValue(map, out IReadOnlyList<NaturalWaterViewSnapshot> cached)) return cached;
            IReadOnlyList<NaturalWaterViewSnapshot> result = map.GetComponent<NaturalFishPopulationMapComponent>()?.PreparedWaterSnapshots
                ?? Array.Empty<NaturalWaterViewSnapshot>();
            WatersByMap[map] = result;
            return result;
        }
    }

    public readonly struct AquacultureKnowledgeView
    {
        public readonly string subjectId;
        public readonly string stageId;
        public readonly float knowledge;
        public readonly float confidence;
        public readonly bool identityKnown;
        public readonly IReadOnlyList<string> knownFacets;

        public AquacultureKnowledgeView(string subjectId, string stageId, float knowledge, float confidence,
            bool identityKnown, IReadOnlyList<string> knownFacets)
        {
            this.subjectId = subjectId;
            this.stageId = stageId;
            this.knowledge = knowledge;
            this.confidence = confidence;
            this.identityKnown = identityKnown;
            this.knownFacets = knownFacets ?? Array.Empty<string>();
        }
    }

    /// <summary>Named service façade used by UI and diagnostics; implementations remain authoritative in existing components.</summary>
    public static class AquacultureFishingService
    {
        public static float SpeciesKnowledge(Pawn pawn, ThingDef fish) => AquacultureKnowledgeAdapter.SpeciesKnowledgeFor(pawn, fish);
        public static float Expertise(Pawn pawn) => AquacultureKnowledgeAdapter.ExpertiseFor(pawn);
    }

    public static class AquaculturePopulationService
    {
        public static NaturalFishPopulationSummary SummaryAt(Map map, IntVec3 cell)
        {
            return map?.GetComponent<NaturalFishPopulationMapComponent>()?.TryGetPreparedSummary(cell, out NaturalFishPopulationSummary summary) == true
                ? summary : null;
        }
    }

    public static class AquaculturePondEcologyService
    {
        public static PondMenuSnapshot SnapshotAt(Map map, IntVec3 cell) => map?.GetComponent<FishPondMapComponent>()?.MenuSnapshotAt(cell);
    }

    public static class AquacultureBreedingService
    {
        public static bool IsResearchAvailable => AquacultureProgression.IsAvailable("AF_SelectiveBreeding");
    }
}
