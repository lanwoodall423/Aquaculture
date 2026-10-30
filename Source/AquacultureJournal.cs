using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class FishSpeciesJournalRecord : IExposable
    {
        public string fishDefName;
        public int discoveredTick = -1;
        public int establishedTick = -1;
        public int bredTick = -1;
        public int stableTick = -1;
        public int stableStartTick = -1;
        public string discoveredBy;
        public float largestSize;
        public float highestBeauty;
        public float highestNutrition = 1f;
        public float rarestTraitScore;
        public int longestLivedTicks;

        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);
        public int Milestones => (discoveredTick >= 0 ? 1 : 0) + (establishedTick >= 0 ? 1 : 0) +
            (bredTick >= 0 ? 1 : 0) + (stableTick >= 0 ? 1 : 0);

        public void ExposeData()
        {
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref discoveredTick, "discoveredTick", -1);
            Scribe_Values.Look(ref establishedTick, "establishedTick", -1);
            Scribe_Values.Look(ref bredTick, "bredTick", -1);
            Scribe_Values.Look(ref stableTick, "stableTick", -1);
            Scribe_Values.Look(ref stableStartTick, "stableStartTick", -1);
            Scribe_Values.Look(ref discoveredBy, "discoveredBy");
            Scribe_Values.Look(ref largestSize, "largestSize");
            Scribe_Values.Look(ref highestBeauty, "highestBeauty");
            Scribe_Values.Look(ref highestNutrition, "highestNutrition", 1f);
            Scribe_Values.Look(ref rarestTraitScore, "rarestTraitScore");
            Scribe_Values.Look(ref longestLivedTicks, "longestLivedTicks");
        }
    }

    public sealed class FishBreedRecord : IExposable
    {
        public const int CurrentStabilityModelVersion = 1;
        public string id;
        public string name;
        public string fishDefName;
        public List<string> traitDefNames = new List<string>();
        public Dictionary<string, float> traitValues = new Dictionary<string, float>();
        public int registeredTick;
        public string registeredBy;
        public int founderCount;
        // births remains serialized for older saves and mirrors qualifyingBirths in new saves.
        public int births;
        public int matchingBirths;
        public int qualifyingBirths;
        public int highestGeneration;
        public int commissionsCompleted;
        public int lastCommissionTick = -1;
        public int commissionsFailed;
        public int lastCommissionFailureTick = -1;
        public bool masteryAnnounced;
        public int stabilityModelVersion;
        public float legacyStabilityFloor;
        public List<string> lineage = new List<string>();
        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);
        public float SuccessRate => TraitBreedingRules.SuccessRate(matchingBirths, qualifyingBirths);
        public float GenerationContribution => TraitBreedingRules.GenerationContribution(highestGeneration);
        public float Stability => Mathf.Max(SafeLegacyFloor(),
            TraitBreedingRules.ResultingStability(matchingBirths, qualifyingBirths, highestGeneration));
        public float MarketValueFactor => 1.15f + Stability * 0.35f;
        public float BeautyBonus => Stability * 0.75f;
        public bool Mastered => Stability >= 0.90f;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) stabilityModelVersion = CurrentStabilityModelVersion;
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Collections.Look(ref traitDefNames, "traitDefNames", LookMode.Value);
            Scribe_Collections.Look(ref traitValues, "traitValues", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref registeredTick, "registeredTick");
            Scribe_Values.Look(ref registeredBy, "registeredBy");
            Scribe_Values.Look(ref founderCount, "founderCount");
            Scribe_Values.Look(ref births, "births");
            Scribe_Values.Look(ref matchingBirths, "matchingBirths");
            Scribe_Values.Look(ref qualifyingBirths, "qualifyingBirths");
            Scribe_Values.Look(ref highestGeneration, "highestGeneration");
            Scribe_Values.Look(ref commissionsCompleted, "commissionsCompleted");
            Scribe_Values.Look(ref lastCommissionTick, "lastCommissionTick", -1);
            Scribe_Values.Look(ref commissionsFailed, "commissionsFailed");
            Scribe_Values.Look(ref lastCommissionFailureTick, "lastCommissionFailureTick", -1);
            Scribe_Values.Look(ref masteryAnnounced, "masteryAnnounced");
            Scribe_Values.Look(ref stabilityModelVersion, "stabilityModelVersion");
            Scribe_Values.Look(ref legacyStabilityFloor, "legacyStabilityFloor");
            Scribe_Collections.Look(ref lineage, "lineage", LookMode.Value);
            if (traitDefNames == null) traitDefNames = new List<string>();
            if (traitValues == null) traitValues = new Dictionary<string, float>();
            if (lineage == null) lineage = new List<string>();
            if (float.IsNaN(legacyStabilityFloor) || float.IsInfinity(legacyStabilityFloor)) legacyStabilityFloor = 0f;
            legacyStabilityFloor = Mathf.Clamp(legacyStabilityFloor, 0f, TraitBreedingRules.MaximumStability);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && stabilityModelVersion < CurrentStabilityModelVersion)
            {
                qualifyingBirths = TraitBreedingRules.QualifyingBirthsFromLegacy(births, matchingBirths);
                legacyStabilityFloor = Mathf.Max(legacyStabilityFloor,
                    TraitBreedingRules.LegacyStability(matchingBirths, highestGeneration));
                stabilityModelVersion = CurrentStabilityModelVersion;
            }
            matchingBirths = Mathf.Max(0, matchingBirths);
            qualifyingBirths = Mathf.Max(matchingBirths, qualifyingBirths);
            births = Mathf.Max(births, qualifyingBirths);
            highestGeneration = Mathf.Max(0, highestGeneration);
        }

        public void RecordQualifyingBirth(bool matched, int generation)
        {
            qualifyingBirths = Mathf.Max(0, qualifyingBirths) + 1;
            births = qualifyingBirths;
            if (!matched) return;
            matchingBirths = Mathf.Min(qualifyingBirths, Mathf.Max(0, matchingBirths) + 1);
            if (generation <= highestGeneration) return;
            highestGeneration = generation;
            if (lineage == null) lineage = new List<string>();
            lineage.Add("AquacultureFishing.GenerationEstablished".Translate(highestGeneration, DayLabel(CurrentTick)).ToString());
        }

        private float SafeLegacyFloor()
        {
            return float.IsNaN(legacyStabilityFloor) || float.IsInfinity(legacyStabilityFloor)
                ? 0f : Mathf.Clamp(legacyStabilityFloor, 0f, TraitBreedingRules.MaximumStability);
        }

        private static string DayLabel(int tick)
        {
            return "Day " + (Mathf.Max(0, tick) / 60000 + 1);
        }

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;
    }

    public sealed class AquacultureJournalComponent : GameComponent
    {
        private const int StableDurationTicks = 15 * 60000;
        private List<FishSpeciesJournalRecord> speciesRecords = new List<FishSpeciesJournalRecord>();
        private List<FishBreedRecord> breeds = new List<FishBreedRecord>();

        public AquacultureJournalComponent(Game game)
        {
        }

        public static AquacultureJournalComponent Current => Verse.Current.Game?.GetComponent<AquacultureJournalComponent>();
        public IReadOnlyList<FishSpeciesJournalRecord> SpeciesRecords => speciesRecords;
        public IReadOnlyList<FishBreedRecord> Breeds => breeds;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref speciesRecords, "aquacultureSpeciesJournal", LookMode.Deep);
            Scribe_Collections.Look(ref breeds, "aquacultureBreeds", LookMode.Deep);
            if (speciesRecords == null) speciesRecords = new List<FishSpeciesJournalRecord>();
            if (breeds == null) breeds = new List<FishBreedRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                speciesRecords.RemoveAll(record => record?.FishDef == null);
                breeds.RemoveAll(breed => breed?.FishDef == null || breed.id.NullOrEmpty());
            }
        }

        public FishSpeciesJournalRecord RecordFor(ThingDef fishDef, bool create = true)
        {
            if (fishDef == null) return null;
            FishSpeciesJournalRecord record = speciesRecords.FirstOrDefault(item => item.fishDefName == fishDef.defName);
            if (record == null && create)
            {
                record = new FishSpeciesJournalRecord { fishDefName = fishDef.defName };
                speciesRecords.Add(record);
            }
            return record;
        }

        public FishBreedRecord BreedById(string id)
        {
            return id.NullOrEmpty() ? null : breeds.FirstOrDefault(breed => breed.id == id);
        }

        public string BreedName(string id)
        {
            return BreedById(id)?.name;
        }

        public void NotifyCaught(CompFishTraits fish, Pawn discoverer = null)
        {
            if (fish?.parent?.def == null) return;
            FishSpeciesJournalRecord record = RecordFor(fish.parent.def);
            UpdateRecords(record, fish);
            if (record.discoveredTick >= 0)
            {
                if (record.discoveredBy.NullOrEmpty() && discoverer != null) record.discoveredBy = discoverer.LabelShortCap;
                return;
            }
            record.discoveredTick = CurrentTick;
            record.discoveredBy = discoverer?.LabelShortCap;
            Messages.Message("New fish discovered: " + fish.parent.def.LabelCap + ".", MessageTypeDefOf.PositiveEvent, false);
        }

        public void NotifyFishingCatch(ThingDef fishDef, Pawn discoverer)
        {
            if (fishDef == null) return;
            FishSpeciesJournalRecord record = RecordFor(fishDef);
            if (record.discoveredTick >= 0)
            {
                if (record.discoveredBy.NullOrEmpty() && discoverer != null) record.discoveredBy = discoverer.LabelShortCap;
                return;
            }
            record.discoveredTick = CurrentTick;
            record.discoveredBy = discoverer?.LabelShortCap;
            Messages.Message("New fish discovered: " + fishDef.LabelCap + ".", MessageTypeDefOf.PositiveEvent, false);
        }

        public void NotifyEstablished(CompFishTraits fish)
        {
            if (fish?.parent?.def == null) return;
            FishSpeciesJournalRecord record = RecordFor(fish.parent.def);
            UpdateRecords(record, fish);
            if (record.discoveredTick < 0) record.discoveredTick = CurrentTick;
            if (record.establishedTick >= 0) return;
            record.establishedTick = CurrentTick;
            Messages.Message(fish.parent.def.LabelCap + " established in a colony pond.", MessageTypeDefOf.PositiveEvent, false);
        }

        public void NotifyColonyBorn(CompFishTraits fish)
        {
            if (fish?.parent?.def == null) return;
            FishSpeciesJournalRecord record = RecordFor(fish.parent.def);
            UpdateRecords(record, fish);
            if (record.discoveredTick < 0) record.discoveredTick = CurrentTick;
            if (record.establishedTick < 0) record.establishedTick = CurrentTick;
            if (record.bredTick < 0)
            {
                record.bredTick = CurrentTick;
                Messages.Message("First colony-bred " + fish.parent.def.label + " hatched.", MessageTypeDefOf.PositiveEvent, false);
            }
            FishBreedRecord breed = BreedById(fish.qualifyingBreedId) ?? BreedById(fish.breedId);
            if (breed == null || fish.qualifyingBirthRecorded) return;
            // Old matching children have already incremented matchingBirths. Do not count them again;
            // their legacy total is preserved by FishBreedRecord migration.
            if (fish.breedBirthRecorded && fish.qualifyingBreedId.NullOrEmpty())
            {
                fish.qualifyingBirthRecorded = true;
                return;
            }
            bool matched = fish.breedId == breed.id;
            fish.qualifyingBirthRecorded = true;
            if (matched) fish.breedBirthRecorded = true;
            breed.RecordQualifyingBirth(matched, matched ? fish.breedGeneration : fish.qualifyingBreedGeneration);
            if (breed.Mastered && !breed.masteryAnnounced)
            {
                breed.masteryAnnounced = true;
                Messages.Message("AquacultureFishing.BreedMastered".Translate(breed.name), MessageTypeDefOf.PositiveEvent, false);
            }
        }

        public void NotifyFishRecord(CompFishTraits fish)
        {
            if (fish?.parent?.def == null) return;
            UpdateRecords(RecordFor(fish.parent.def), fish);
        }

        public void EvaluateStablePopulations(IEnumerable<IEnumerable<CompFishTraits>> ponds, int now)
        {
            var stableSpecies = new HashSet<ThingDef>();
            foreach (IEnumerable<CompFishTraits> pondFish in ponds ?? Enumerable.Empty<IEnumerable<CompFishTraits>>())
            {
                foreach (IGrouping<ThingDef, CompFishTraits> group in pondFish
                    .Where(fish => fish?.IsAlive == true).GroupBy(fish => fish.parent.def))
                {
                    List<CompFishTraits> members = group.ToList();
                    bool healthy = members.Count >= 3
                        && members.Any(fish => fish.IsAdult && fish.IsFemale)
                        && members.Any(fish => fish.IsAdult && !fish.IsFemale)
                        && members.All(fish => fish.foodReserve >= 0.35f && fish.starvationProgress <= 0f
                            && fish.waterStress < 0.10f && fish.temperatureStress < 0.10f);
                    if (healthy) stableSpecies.Add(group.Key);
                }
            }

            foreach (FishSpeciesJournalRecord record in speciesRecords)
            {
                if (record.stableTick >= 0) continue;
                if (record.FishDef == null || !stableSpecies.Contains(record.FishDef))
                {
                    record.stableStartTick = -1;
                    continue;
                }
                if (record.stableStartTick < 0) record.stableStartTick = now;
                if (now - record.stableStartTick < StableDurationTicks) continue;
                record.stableTick = now;
                Messages.Message(record.FishDef.LabelCap + " has maintained a stable pond population for one quadrum.",
                    MessageTypeDefOf.PositiveEvent, false);
            }
        }

        public bool CanRegisterBreed(CompFishTraits founder, out string reason, out List<CompFishTraits> cohort)
        {
            cohort = new List<CompFishTraits>();
            if (!AquacultureProgression.IsAvailable("AF_SelectiveBreeding"))
            {
                reason = "Requires Selective Fish Breeding.";
                return false;
            }
            if (founder?.IsAlive != true || !founder.IsInPond || !founder.IsAdult)
            {
                reason = "Select a living adult fish in a pond.";
                return false;
            }
            if (!founder.breedId.NullOrEmpty())
            {
                reason = "This fish already belongs to the " + (BreedName(founder.breedId) ?? "registered") + " breed.";
                return false;
            }
            List<string> signature = InheritableTraits(founder);
            if (signature.Count == 0)
            {
                reason = "A breed needs at least one inheritable trait.";
                return false;
            }
            if (!HasCompatibleTraits(signature))
            {
                reason = "AquacultureFishing.BreedTraitsConflict".Translate().ToString();
                return false;
            }
            FishPondMapComponent component = founder.parent.Map.GetComponent<FishPondMapComponent>();
            cohort = component?.FishInSamePond(founder)
                .Where(fish => fish.IsAlive && fish.IsAdult && fish.parent.def == founder.parent.def
                    && fish.breedId.NullOrEmpty() && SameSignature(founder, fish))
                .ToList() ?? new List<CompFishTraits>();
            if (cohort.Count < 2 || !cohort.Any(fish => fish.IsFemale) || !cohort.Any(fish => !fish.IsFemale))
            {
                reason = "Requires an adult male and female of this species with the same inheritable traits in one pond.";
                return false;
            }
            if (!cohort.Any(fish => fish.colonyBorn))
            {
                reason = "Breed this population at least once before registering it.";
                return false;
            }
            reason = null;
            return true;
        }

        public FishBreedRecord RegisterBreed(CompFishTraits founder, string breedName, Pawn registrar = null)
        {
            if (breedName.NullOrEmpty() || !CanRegisterBreed(founder, out _, out List<CompFishTraits> cohort)) return null;
            breedName = breedName.Trim();
            if (breedName.Length > 40) breedName = breedName.Substring(0, 40);
            if (breeds.Any(breed => breed.name.Equals(breedName, StringComparison.OrdinalIgnoreCase))) return null;
            var breedRecord = new FishBreedRecord
            {
                id = Guid.NewGuid().ToString("N"),
                name = breedName,
                fishDefName = founder.parent.def.defName,
                traitDefNames = InheritableTraits(founder),
                registeredTick = CurrentTick,
                registeredBy = registrar?.LabelShortCap,
                founderCount = cohort.Count,
                highestGeneration = Mathf.Max(1, cohort.Max(fish => fish.breedGeneration)),
                stabilityModelVersion = FishBreedRecord.CurrentStabilityModelVersion
            };
            breedRecord.lineage.Add("Founding population registered on " + DayLabel(CurrentTick) + ".");
            foreach (string traitName in breedRecord.traitDefNames)
            {
                float value = founder.TraitValue(traitName);
                if (value > 0f) breedRecord.traitValues[traitName] = value;
            }
            breeds.Add(breedRecord);
            for (int i = 0; i < cohort.Count; i++)
            {
                cohort[i].breedId = breedRecord.id;
                cohort[i].breedGeneration = Mathf.Max(1, cohort[i].breedGeneration);
                cohort[i].NotifyTraitsChanged();
            }
            Messages.Message("Registered fish breed: " + breedRecord.name + ".", MessageTypeDefOf.PositiveEvent, false);
            AquacultureCommissionManager.NotifyBreedRegistered(breedRecord);
            return breedRecord;
        }

        public FishBreedRecord BreedForParents(CompFishTraits first, CompFishTraits second)
        {
            if (first?.breedId.NullOrEmpty() != false || first.breedId != second?.breedId) return null;
            return BreedById(first.breedId);
        }

        public bool ApplyBreedInheritance(CompFishTraits first, CompFishTraits second, List<string> inheritedTraits,
            Dictionary<string, float> inheritedValues, out string inheritedBreedId, out int inheritedGeneration,
            out string qualifyingBreedId, out int qualifyingBreedGeneration)
        {
            inheritedBreedId = null;
            inheritedGeneration = 0;
            qualifyingBreedId = null;
            qualifyingBreedGeneration = 0;
            if (inheritedTraits == null) inheritedTraits = new List<string>();
            if (inheritedValues == null) inheritedValues = new Dictionary<string, float>();
            FishBreedRecord breed = BreedForParents(first, second);
            if (breed == null) return false;
            if (breed.traitDefNames == null) breed.traitDefNames = new List<string>();
            if (breed.traitValues == null) breed.traitValues = new Dictionary<string, float>();
            qualifyingBreedId = breed.id;
            qualifyingBreedGeneration = Mathf.Max(first.breedGeneration, second.breedGeneration) + 1;
            var usedGroups = new HashSet<string>();
            for (int i = 0; i < breed.traitDefNames.Count; i++)
            {
                string traitName = breed.traitDefNames[i];
                FishTraitDef trait = DefDatabase<FishTraitDef>.GetNamedSilentFail(traitName);
                if (trait == null) continue;
                string group = FishTraitUtility.ExclusiveGroup(trait);
                float reliability = TraitBreedingRules.RegisteredBreedDefiningTraitReliability(
                    breed.Stability, AquacultureMod.Settings?.registeredBreedDefiningTraitReliability ??
                    AquacultureSettings.DefaultRegisteredBreedDefiningTraitReliability);
                if (!Rand.Chance(reliability) || !usedGroups.Add(group)) continue;
                for (int inheritedIndex = inheritedTraits.Count - 1; inheritedIndex >= 0; inheritedIndex--)
                {
                    string inheritedName = inheritedTraits[inheritedIndex];
                    if (FishTraitUtility.ExclusiveGroup(DefDatabase<FishTraitDef>.GetNamedSilentFail(inheritedName)) != group) continue;
                    inheritedTraits.RemoveAt(inheritedIndex);
                    inheritedValues.Remove(inheritedName);
                }
                if (!inheritedTraits.Contains(traitName)) inheritedTraits.Add(traitName);
                if (breed.traitValues.TryGetValue(traitName, out float value)) inheritedValues[traitName] = value;
            }
            bool matches = breed.traitDefNames.All(inheritedTraits.Contains);
            if (matches)
            {
                inheritedBreedId = breed.id;
                inheritedGeneration = qualifyingBreedGeneration;
            }
            return matches;
        }

        public static List<string> InheritableTraits(CompFishTraits fish)
        {
            return fish?.traitDefNames?
                .Where(FishTraitUtility.IsInheritableTraitName)
                .OrderBy(name => name, StringComparer.Ordinal).ToList() ?? new List<string>();
        }

        private static bool HasCompatibleTraits(IEnumerable<string> names)
        {
            var groups = new HashSet<string>();
            foreach (string name in names ?? Enumerable.Empty<string>())
            {
                FishTraitDef trait = DefDatabase<FishTraitDef>.GetNamedSilentFail(name);
                if (trait == null || !groups.Add(FishTraitUtility.ExclusiveGroup(trait))) return false;
            }
            return true;
        }

        private static bool SameSignature(CompFishTraits first, CompFishTraits second)
        {
            List<string> a = InheritableTraits(first);
            List<string> b = InheritableTraits(second);
            if (!a.SequenceEqual(b)) return false;
            for (int i = 0; i < a.Count; i++)
                if (!Mathf.Approximately(first.TraitValue(a[i]), second.TraitValue(a[i]))) return false;
            return true;
        }

        private static void UpdateRecords(FishSpeciesJournalRecord record, CompFishTraits fish)
        {
            if (record == null || fish == null) return;
            record.largestSize = Mathf.Max(record.largestSize, fish.SizeFactor);
            record.highestBeauty = Mathf.Max(record.highestBeauty, 1f + fish.BeautyOffset);
            record.highestNutrition = Mathf.Max(record.highestNutrition, fish.NutritionMultiplier);
            record.rarestTraitScore = Mathf.Max(record.rarestTraitScore, TraitRarityScore(fish));
            record.longestLivedTicks = Mathf.Max(record.longestLivedTicks,
                Mathf.Max(0, (Find.TickManager?.TicksGame ?? fish.birthTick) - fish.birthTick));
        }

        private static float TraitRarityScore(CompFishTraits fish)
        {
            AquacultureSettings settings = AquacultureMod.Settings;
            if (settings == null || fish == null) return 0f;
            List<FishTraitDef> candidates = DefDatabase<FishTraitDef>.AllDefsListForReading
                .Where(trait => trait.mutationEligible && settings.TraitEnabled(trait) && settings.TraitWeight(trait) > 0f).ToList();
            float totalWeight = candidates.Sum(settings.TraitWeight);
            if (totalWeight <= 0f) return 0f;
            float score = 0f;
            List<string> inherited = InheritableTraits(fish);
            for (int i = 0; i < inherited.Count; i++)
            {
                FishTraitDef trait = DefDatabase<FishTraitDef>.GetNamedSilentFail(inherited[i]);
                float weight = trait == null ? 0f : settings.TraitWeight(trait);
                if (weight > 0f) score += -Mathf.Log10(Mathf.Clamp(weight / totalWeight, 0.000001f, 1f));
            }
            return score;
        }

        private static string DayLabel(int tick)
        {
            return "Day " + (Mathf.Max(0, tick) / 60000 + 1);
        }

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;
    }

    /// <summary>
    /// Aquaculture-owned breed registration presentation boundary. The native
    /// window remains responsible for name validation, accepting and closing.
    /// </summary>
    public interface IAquacultureBreedRegistrationView
    {
        void Draw(Rect rect);
        void PostClose();
    }

    public sealed class Dialog_RegisterFishBreed : Window
    {
        private readonly CompFishTraits founder;
        private string breedName = string.Empty;
        private IAquacultureBreedRegistrationView insightDocument;
        private bool canAcceptForUi;
        private string validationMessageForUi = string.Empty;

        public override Vector2 InitialSize => new Vector2(520f, 240f);

        public Dialog_RegisterFishBreed(CompFishTraits founder)
        {
            this.founder = founder;
            doCloseX = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            insightDocument = AquacultureBreedRegistrationViewFactory.Create(this);
        }

        public override void DoWindowContents(Rect inRect)
        {
            insightDocument.Draw(inRect);
            if (canAcceptForUi && Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                Event.current.Use();
                AcceptForUi();
            }
        }

        public override void PostClose()
        {
            insightDocument?.PostClose();
            base.PostClose();
        }

        internal string BreedNameForUi => breedName ?? string.Empty;
        internal bool CanAcceptForUi => canAcceptForUi;
        internal string ValidationMessageForUi => validationMessageForUi ?? string.Empty;

        internal void SetBreedNameForUi(string value)
        {
            breedName = value ?? string.Empty;
        }

        internal void RefreshValidationForUi()
        {
            string trimmed = (breedName ?? string.Empty).Trim();
            if (trimmed.NullOrEmpty())
            {
                canAcceptForUi = false;
                validationMessageForUi = "AquacultureFishing.BreedRegistrationEnterName".Translate().ToString();
                return;
            }
            if (AquacultureJournalComponent.Current?.Breeds?.Any(breed =>
                string.Equals(breed.name, trimmed, StringComparison.OrdinalIgnoreCase)) == true)
            {
                canAcceptForUi = false;
                validationMessageForUi = "AquacultureFishing.BreedRegistrationDuplicate".Translate().ToString();
                return;
            }
            string reason = null;
            List<CompFishTraits> cohort;
            canAcceptForUi = AquacultureJournalComponent.Current != null &&
                AquacultureJournalComponent.Current.CanRegisterBreed(founder, out reason, out cohort);
            validationMessageForUi = canAcceptForUi
                ? "AquacultureFishing.BreedRegistrationReady".Translate().ToString()
                : (reason ?? "AquacultureFishing.BreedRegistrationUnavailable".Translate().ToString());
        }

        internal void AcceptForUi()
        {
            FishBreedRecord breed = AquacultureJournalComponent.Current?.RegisterBreed(founder, breedName);
            if (breed != null) Close();
        }

        internal void CancelForUi() => Close();
    }

    /// <summary>
    /// Aquaculture-owned Journal presentation boundary. The game window owns
    /// selection and lifecycle; the renderer owns only its local display state.
    /// No Insight Canvas types cross this contract.
    /// </summary>
    public interface IAquacultureJournalWorkspace
    {
        void Draw(Rect rect);
        void PostClose();
        void SelectPage(string page);
        void SelectSpecies(string defName);
    }

    /// <summary>
    /// First native Journal workspace prototype: overview and species only.
    /// Not selected by the current player factory. Uses colony-authoritative,
    /// knowledge-gated snapshots; no gameplay or journal record writes.
    /// </summary>
    public sealed class NativeAquacultureJournalWorkspaceView : IAquacultureJournalWorkspace
    {
        private string activePage = "overview";
        private string selectedSpeciesDefName;
        private string speciesSearch = string.Empty;
        private Vector2 scrollPosition;

        public void SelectPage(string page)
        {
            activePage = page == "species" ? "species" : "overview";
            speciesSearch = string.Empty;
            scrollPosition = Vector2.zero;
        }

        public void SelectSpecies(string defName)
        {
            selectedSpeciesDefName = defName.NullOrEmpty() ? null : defName;
            activePage = "species";
            speciesSearch = string.Empty;
            scrollPosition = Vector2.zero;
        }

        public void PostClose()
        {
            activePage = "overview";
            selectedSpeciesDefName = null;
            speciesSearch = string.Empty;
            scrollPosition = Vector2.zero;
        }

        public void Draw(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect padded = rect.ContractedBy(8f);
            if (padded.width <= 30f || padded.height <= 110f) return;

            AquacultureJournalViewSnapshot journal = AquacultureSnapshotCache.Journal(null, true);
            IReadOnlyList<AquacultureSpeciesViewSnapshot> species = journal?.species ??
                Array.Empty<AquacultureSpeciesViewSnapshot>();
            // Filter against the masked label, never hidden Def names or labels.
            List<AquacultureSpeciesViewSnapshot> filtered = species
                .Where(item => item != null &&
                    (string.IsNullOrEmpty(speciesSearch) ||
                     SafeSpeciesLabel(item).IndexOf(speciesSearch,
                         StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(item => item.identityKnown ? 0 : 1)
                .ThenBy(item => SafeSpeciesLabel(item), StringComparer.OrdinalIgnoreCase)
                .ToList();

            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(padded.x, padded.y, padded.width, 32f),
                    L("AquacultureFishing.WorkspaceTitle"));
                Text.Font = GameFont.Small;

                float navWidth = Mathf.Max(10f, (padded.width - 16f) / 3f);
                float navY = padded.y + 36f;
                if (Widgets.ButtonText(new Rect(padded.x, navY, navWidth, 32f),
                    L("AquacultureFishing.WorkspaceOverview")))
                {
                    SelectPage("overview");
                    return;
                }
                if (Widgets.ButtonText(new Rect(padded.x + navWidth + 8f, navY,
                    navWidth, 32f), L("AquacultureFishing.WorkspaceSpecies")))
                {
                    SelectPage("species");
                    return;
                }
                if (Widgets.ButtonText(new Rect(padded.x + 2f * (navWidth + 8f), navY,
                    navWidth, 32f), L("AquacultureFishing.WorkspaceOpenKnowledge")))
                {
                    MainTabWindow_AquacultureJournal.OpenExpertise(null);
                    return;
                }

                Rect viewport = new Rect(padded.x, padded.y + 76f, padded.width,
                    Mathf.Max(0f, padded.height - 76f));
                if (viewport.width <= 0f || viewport.height <= 0f) return;
                float viewWidth = Mathf.Max(20f, viewport.width - 18f);
                float contentHeight = activePage == "species"
                    ? SpeciesContentHeight(species, filtered, viewWidth)
                    : OverviewContentHeight(viewWidth);
                Rect view = new Rect(0f, 0f, viewWidth,
                    Mathf.Max(viewport.height, contentHeight));
                scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f,
                    Mathf.Max(0f, view.height - viewport.height));

                Widgets.BeginScrollView(viewport, ref scrollPosition, view);
                try
                {
                    float y = 8f;
                    if (activePage == "species")
                        DrawSpecies(ref y, viewWidth, viewport.height, species, filtered);
                    else
                        DrawOverview(ref y, viewWidth, species);
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        private static string SafeSpeciesLabel(AquacultureSpeciesViewSnapshot item)
        {
            return item != null && item.identityKnown && item.fishDef != null
                ? item.fishDef.LabelCap.ToString()
                : L("AquacultureFishing.WorkspaceUnknownSpecies");
        }

        private static float LabelHeight(string value, float width)
        {
            return Mathf.Max(24f, Text.CalcHeight(value ?? string.Empty, Mathf.Max(20f, width)));
        }

        private static void Line(ref float y, float width, string value)
        {
            float height = LabelHeight(value, width);
            Widgets.Label(new Rect(6f, y, Mathf.Max(20f, width), height), value ?? string.Empty);
            y += height + 8f;
        }

        private static void Heading(ref float y, float width, string value)
        {
            Text.Font = GameFont.Medium;
            float height = Mathf.Max(32f, Text.CalcHeight(value, Mathf.Max(20f, width)));
            Widgets.Label(new Rect(6f, y, Mathf.Max(20f, width), height), value);
            y += height + 10f;
            Text.Font = GameFont.Small;
        }

        private static float SpeciesRowHeight(AquacultureSpeciesViewSnapshot item, float width)
        {
            return Mathf.Max(58f, LabelHeight(SafeSpeciesLabel(item), width - 12f) + 28f);
        }

        private static float OverviewContentHeight(float width)
        {
            return 130f +
                LabelHeight(L("AquacultureFishing.WorkspaceOverviewSubtitle"), width) +
                LabelHeight(L("AquacultureFishing.WorkspaceKnowledgeBody"), width) +
                LabelHeight(L("AquacultureFishing.WorkspaceDiscovered"), width);
        }

        private AquacultureSpeciesViewSnapshot FindKnownSpecies(
            IReadOnlyList<AquacultureSpeciesViewSnapshot> species)
        {
            return selectedSpeciesDefName.NullOrEmpty() ? null : species.FirstOrDefault(
                item => item != null && item.identityKnown && item.fishDef != null &&
                    item.fishDef.defName == selectedSpeciesDefName);
        }

        private static string SelectedKnowledgeLine(AquacultureSpeciesViewSnapshot selected)
        {
            if (selected == null || !selected.identityKnown)
                return L("AquacultureFishing.WorkspaceKnowledgeHidden");
            return L("AquacultureFishing.WorkspaceKnowledgeStage") + ": " +
                (selected.stageId ?? string.Empty) + "  •  " +
                L("AquacultureFishing.WorkspaceConfidence") + ": " +
                selected.confidence.ToStringPercent();
        }

        private static string SelectedFacetsLine(AquacultureSpeciesViewSnapshot selected)
        {
            if (selected == null || !selected.identityKnown)
                return L("AquacultureFishing.WorkspaceKnowledgeHidden");
            if (selected.knownFacets == null || selected.knownFacets.Count == 0)
                return L("AquacultureFishing.WorkspaceNoKnownFacets");
            return L("AquacultureFishing.WorkspaceKnownFacets") + ": " +
                string.Join(", ", selected.knownFacets.ToArray());
        }

        private float SpeciesContentHeight(IReadOnlyList<AquacultureSpeciesViewSnapshot> species,
            List<AquacultureSpeciesViewSnapshot> filtered, float width)
        {
            AquacultureSpeciesViewSnapshot selected = FindKnownSpecies(species);
            return 260f + LabelHeight(SelectedKnowledgeLine(selected), width) +
                LabelHeight(SelectedFacetsLine(selected), width) +
                filtered.Sum(item => SpeciesRowHeight(item, width) + 6f);
        }

        private void DrawOverview(ref float y, float width,
            IReadOnlyList<AquacultureSpeciesViewSnapshot> species)
        {
            Heading(ref y, width, L("AquacultureFishing.WorkspaceOverview"));
            Line(ref y, width, L("AquacultureFishing.WorkspaceOverviewSubtitle"));
            int known = species.Count(item => item != null && item.identityKnown);
            Line(ref y, width, L("AquacultureFishing.WorkspaceDiscovered") + ": " + known);
            Heading(ref y, width, L("AquacultureFishing.WorkspaceKnowledgeTitle"));
            Line(ref y, width, L("AquacultureFishing.WorkspaceKnowledgeBody"));
        }

        private void DrawSpecies(ref float y, float width, float viewportHeight,
            IReadOnlyList<AquacultureSpeciesViewSnapshot> species,
            List<AquacultureSpeciesViewSnapshot> filtered)
        {
            Heading(ref y, width, L("AquacultureFishing.WorkspaceSpecies"));
            Line(ref y, width, L("AquacultureFishing.WorkspaceSpeciesSubtitle"));
            Line(ref y, width, L("AquacultureFishing.WorkspaceSearchSpecies"));
            string nextSearch = Widgets.TextField(new Rect(6f, y,
                Mathf.Max(20f, width - 12f), 30f), speciesSearch);
            if (!string.Equals(nextSearch, speciesSearch, StringComparison.Ordinal))
            {
                speciesSearch = nextSearch ?? string.Empty;
                scrollPosition = Vector2.zero;
            }
            y += 42f;

            if (filtered.Count == 0)
                Line(ref y, width, L("AquacultureFishing.WorkspaceNoMatchingSpecies"));
            foreach (AquacultureSpeciesViewSnapshot item in filtered)
            {
                float height = SpeciesRowHeight(item, width);
                if (y + height >= scrollPosition.y &&
                    y <= scrollPosition.y + viewportHeight)
                {
                    if (Widgets.ButtonText(new Rect(6f, y, Mathf.Max(20f, width - 12f),
                        height - 26f), SafeSpeciesLabel(item)))
                    {
                        // Never derive a player-facing selection from hidden identity.
                        selectedSpeciesDefName = item.identityKnown
                            ? item.fishDef?.defName : null;
                        scrollPosition = Vector2.zero;
                        return;
                    }
                    Widgets.Label(new Rect(6f, y + height - 25f,
                        Mathf.Max(20f, width - 12f), 24f),
                        L(item.identityKnown
                            ? "AquacultureFishing.WorkspaceIdentityKnown"
                            : "AquacultureFishing.WorkspaceKnowledgeHidden"));
                }
                y += height + 6f;
            }

            y += 10f;
            Heading(ref y, width, L("AquacultureFishing.WorkspaceSpeciesDetail"));
            AquacultureSpeciesViewSnapshot selected = FindKnownSpecies(species);
            if (selected == null)
            {
                Line(ref y, width, L("AquacultureFishing.WorkspaceSelectSpecies"));
            }
            else
            {
                Line(ref y, width, SafeSpeciesLabel(selected));
                Line(ref y, width, SelectedKnowledgeLine(selected));
                Line(ref y, width, SelectedFacetsLine(selected));
            }
        }

        private static string L(string key) => key.Translate().ToString();
    }

    public sealed class MainTabWindow_AquacultureJournal : MainTabWindow
    {
        private enum JournalPage { Species, Waters, Ponds, Expertise, Breeds, Anglers, Records }

        private JournalPage page;
        private string selectedSpecies;
        private readonly AquacultureKnowledgeAdapter.ExpertiseMenu expertiseMenu =
            new AquacultureKnowledgeAdapter.ExpertiseMenu();
        private readonly NativeExpertiseMenu nativeExpertiseMenu = new NativeExpertiseMenu();
        private IAquacultureJournalWorkspace insightWorkspaceDocument;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1180f, UI.screenWidth * 0.96f), Mathf.Min(720f, UI.screenHeight * 0.90f));

        public static void OpenExpertise(Pawn pawn)
        {
            AquacultureKnowledgeAdapter.EnsureRegistration();
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("AF_AquacultureJournal");
            if (button == null) return;
            Find.MainTabsRoot.SetCurrentTab(button, true);
            if (button.TabWindow is MainTabWindow_AquacultureJournal journal)
            {
                journal.page = JournalPage.Expertise;
                journal.insightWorkspaceDocument?.PostClose();
                journal.insightWorkspaceDocument = null;
                journal.expertiseMenu.SelectPawn(pawn);
                journal.nativeExpertiseMenu.SelectPawn(pawn);
            }
        }

        public static void OpenSpecies(ThingDef fishDef)
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("AF_AquacultureJournal");
            if (button == null) return;
            Find.MainTabsRoot.SetCurrentTab(button, true);
            if (button.TabWindow is MainTabWindow_AquacultureJournal journal)
            {
                journal.page = JournalPage.Species;
                journal.selectedSpecies = fishDef?.defName;
                journal.insightWorkspaceDocument?.SelectSpecies(fishDef?.defName);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            AquacultureJournalComponent journal = AquacultureJournalComponent.Current;
            if (journal == null)
            {
                Widgets.Label(inRect, "No aquaculture journal is available.");
                return;
            }

            // Knowledge expertise remains intentionally routed to the canonical Knowledge Framework
            // browser. The other Journal surfaces are owned by the document-scoped workspace.
            if (page != JournalPage.Expertise)
            {
                bool firstWorkspaceDraw = insightWorkspaceDocument == null;
                if (firstWorkspaceDraw) insightWorkspaceDocument = AquacultureJournalWorkspaceFactory.Create();
                if (firstWorkspaceDraw)
                {
                    if (!selectedSpecies.NullOrEmpty())
                        insightWorkspaceDocument.SelectSpecies(selectedSpecies);
                    else
                        insightWorkspaceDocument.SelectPage(page == JournalPage.Ponds ? "ponds" :
                            page == JournalPage.Waters ? "conservation" :
                            page == JournalPage.Breeds ? "breeds" : "overview");
                }
                insightWorkspaceDocument.Draw(inRect);
                return;
            }
            if (insightWorkspaceDocument != null)
            {
                insightWorkspaceDocument.PostClose();
                insightWorkspaceDocument = null;
            }
            AquacultureKnowledgeAdapter.EnsureRegistration();
            const float navigationHeight = 34f;
            Rect backRect = new Rect(inRect.x, inRect.y, Mathf.Min(220f, inRect.width), navigationHeight);
            if (Widgets.ButtonText(backRect, "AquacultureFishing.WorkspaceBackToJournal".Translate()))
            {
                page = JournalPage.Species;
                expertiseMenu.ClearSelection();
                nativeExpertiseMenu.ClearSelection();
                return;
            }
            Rect knowledgeRect = new Rect(inRect.x, inRect.y + navigationHeight + 8f, inRect.width,
                Mathf.Max(0f, inRect.height - navigationHeight - 8f));
            if (AquacultureKnowledgeAdapter.IsFrameworkReady)
                expertiseMenu.Draw(knowledgeRect);
            else
                nativeExpertiseMenu.Draw(knowledgeRect);
        }

        /// <summary>
        /// Read-only RimWorld-native expertise view used only when Knowledge V3
        /// is not ready. It reads legacy Aquaculture records directly and
        /// deliberately hides unidentified or expertise-locked species.
        /// </summary>
        private sealed class NativeExpertiseMenu
        {
            private Pawn selectedPawn;
            private bool colonyScope = true;
            private Vector2 scrollPosition;

            public void SelectPawn(Pawn pawn)
            {
                selectedPawn = pawn?.Faction?.def?.isPlayer == true ? pawn : null;
                colonyScope = selectedPawn == null;
                scrollPosition = Vector2.zero;
            }

            public void ClearSelection()
            {
                selectedPawn = null;
                colonyScope = true;
                scrollPosition = Vector2.zero;
            }

            private void ChooseAngler(List<PawnFishingProgress> records)
            {
                List<Pawn> pawns = records.Select(record => record.pawn).Distinct()
                    .OrderBy(pawn => pawn.LabelShortCap.ToString()).ToList();
                if (selectedPawn?.Faction?.def?.isPlayer == true && !pawns.Contains(selectedPawn))
                    pawns.Insert(0, selectedPawn);
                List<FloatMenuOption> options = pawns
                    .Select(pawn => new FloatMenuOption(pawn.LabelShortCap.ToString(), () => SelectPawn(pawn)))
                    .ToList();
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }

            public void Draw(Rect rect)
            {
                List<PawnFishingProgress> playerRecords = FishingProgressionComponent.Current?.PawnProgress
                    .Where(record => record?.pawn?.Faction?.def?.isPlayer == true).ToList()
                    ?? new List<PawnFishingProgress>();

                // A pawn changing factions must not expose its saved knowledge.
                if (!colonyScope && selectedPawn?.Faction?.def?.isPlayer != true)
                    ClearSelection();

                const float buttonHeight = 30f;
                Rect colonyButton = new Rect(rect.x, rect.y, Mathf.Min(172f, rect.width * 0.45f), buttonHeight);
                Rect anglerButton = new Rect(colonyButton.xMax + 8f, rect.y,
                    Mathf.Max(0f, Mathf.Min(290f, rect.xMax - colonyButton.xMax - 8f)), buttonHeight);
                if (Widgets.ButtonText(colonyButton, colonyScope ? "Colony knowledge (selected)" : "Colony knowledge"))
                {
                    colonyScope = true;
                    selectedPawn = null;
                    scrollPosition = Vector2.zero;
                }
                if (anglerButton.width > 0f && Widgets.ButtonText(anglerButton,
                    colonyScope ? "Choose angler" : "Angler: " + selectedPawn.LabelShortCap))
                    ChooseAngler(playerRecords);

                PawnFishingProgress personal = colonyScope ? null :
                    FishingProgressionComponent.Current?.ProgressFor(selectedPawn, false);
                float experience = NativeFishingKnowledgeRules.Experience(personal?.expertiseExperience ?? 0f);
                FishingRank rank = NativeFishingKnowledgeRules.RankForExperience(experience);
                float rankProgress = NativeFishingKnowledgeRules.ProgressForExperience(experience);

                float summaryY = rect.y + buttonHeight + 10f;
                Widgets.Label(new Rect(rect.x, summaryY, rect.width, 28f),
                    colonyScope ? "Colony fishing knowledge - recorded player anglers" :
                    selectedPawn.LabelShortCap + " - Fishing expertise: " + rank + " (" + experience.ToString("0") + " XP)");
                if (!colonyScope)
                    Widgets.FillableBar(new Rect(rect.x, summaryY + 28f, Mathf.Min(320f, rect.width), 12f),
                        rankProgress);

                float listY = summaryY + (colonyScope ? 38f : 50f);
                Rect scrollRect = new Rect(rect.x, listY, rect.width, Mathf.Max(0f, rect.yMax - listY));
                if (scrollRect.height <= 0f) return;

                // Never read the framework's query API in native mode, including
                // through PawnFishingProgress.KnowledgeFor or ExpertiseLevel.
                List<ThingDef> identified = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(FishUtility.IsFish)
                    .Where(fish =>
                    {
                        float knowledge = KnowledgeFor(fish, playerRecords, personal);
                        FishingRank required = AquacultureMod.Settings?.MinimumExpertiseFor(fish) ?? FishingRank.Novice;
                        return knowledge > 0f && (colonyScope || rank >= required);
                    })
                    .OrderBy(fish => fish.label).ThenBy(fish => fish.defName).ToList();

                const float rowHeight = 31f;
                float viewHeight = Mathf.Max(scrollRect.height, identified.Count * rowHeight + 30f);
                Rect viewRect = new Rect(0f, 0f, Mathf.Max(0f, scrollRect.width - 18f), viewHeight);
                Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
                if (identified.Count == 0)
                    Widgets.Label(new Rect(0f, 0f, viewRect.width, 28f),
                        "No identified fish species are available for this view.");
                for (int i = 0; i < identified.Count; i++)
                {
                    ThingDef fish = identified[i];
                    float amount = KnowledgeFor(fish, playerRecords, personal);
                    float y = i * rowHeight;
                    Widgets.Label(new Rect(4f, y, Mathf.Max(0f, viewRect.width - 185f), 27f), fish.LabelCap);
                    Widgets.FillableBar(new Rect(Mathf.Max(0f, viewRect.width - 172f), y + 3f, 104f, 17f),
                        amount);
                    Widgets.Label(new Rect(Mathf.Max(0f, viewRect.width - 61f), y, 60f, 27f),
                        amount.ToStringPercent());
                }
                Widgets.EndScrollView();
            }

            private float KnowledgeFor(ThingDef fish, List<PawnFishingProgress> records,
                PawnFishingProgress personal)
            {
                if (colonyScope)
                    return NativeFishingKnowledgeRules.ColonySpeciesKnowledge(records, fish?.defName,
                        record => record?.pawn?.Faction?.def?.isPlayer == true,
                        record => record?.speciesKnowledge);
                return NativeFishingKnowledgeRules.SpeciesKnowledge(personal?.speciesKnowledge, fish?.defName);
            }
        }

        public override void PostClose()
        {
            insightWorkspaceDocument?.PostClose();
            base.PostClose();
        }
    }

    /// <summary>
    /// Native Widgets-only breed registration presentation prototype. The
    /// owning dialog retains every registration and validation decision.
    /// The default renderer is still the existing Insight Canvas document.
    /// </summary>
    public sealed class NativeAquacultureBreedRegistrationView : IAquacultureBreedRegistrationView
    {
        private readonly Dialog_RegisterFishBreed owner;
        private Vector2 scrollPosition;

        public NativeAquacultureBreedRegistrationView(Dialog_RegisterFishBreed owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void PostClose()
        {
            scrollPosition = Vector2.zero;
        }

        public void Draw(Rect rect)
        {
            owner.RefreshValidationForUi();

            Widgets.DrawMenuSection(rect);
            Rect viewport = rect.ContractedBy(8f);
            float viewWidth = Mathf.Max(80f, viewport.width - 18f);
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Small;

            string subtitle = L("AquacultureFishing.BreedRegistrationSubtitle");
            float subtitleHeight = Mathf.Max(24f, Text.CalcHeight(subtitle, viewWidth - 12f));
            float validationHeight = Mathf.Max(26f,
                Text.CalcHeight(owner.ValidationMessageForUi, viewWidth - 12f));
            Rect view = new Rect(0f, 0f, viewWidth,
                Mathf.Max(viewport.height, 172f + subtitleHeight + validationHeight));

            Widgets.BeginScrollView(viewport, ref scrollPosition, view);
            try
            {
                float y = 8f;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(6f, y, viewWidth - 12f, 32f),
                    L("AquacultureFishing.BreedRegistrationTitle"));
                y += 38f;

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(6f, y, viewWidth - 12f, subtitleHeight), subtitle);
                y += subtitleHeight + 10f;
                Widgets.Label(new Rect(6f, y, viewWidth - 12f, 24f),
                    L("AquacultureFishing.BreedRegistrationName"));
                y += 28f;

                string originalName = owner.BreedNameForUi;
                string enteredName = Widgets.TextField(new Rect(6f, y, viewWidth - 12f, 30f),
                    originalName);
                if (!string.Equals(originalName, enteredName, StringComparison.Ordinal))
                {
                    owner.SetBreedNameForUi(enteredName);
                    owner.RefreshValidationForUi();
                }
                y += 38f;

                validationHeight = Mathf.Max(26f,
                    Text.CalcHeight(owner.ValidationMessageForUi, viewWidth - 12f));
                Widgets.Label(new Rect(6f, y, viewWidth - 12f, validationHeight),
                    owner.ValidationMessageForUi);
                y += validationHeight + 10f;

                float buttonWidth = Mathf.Min(150f, Mathf.Max(0f, (viewWidth - 24f) / 2f));
                if (Widgets.ButtonText(new Rect(6f, y, buttonWidth, 32f),
                    L("AquacultureFishing.BreedRegistrationCancel")))
                {
                    owner.CancelForUi();
                    return;
                }

                // Preserve disabled registration semantics without changing
                // the dialog's Enter/KeypadEnter or validation authority.
                bool previousEnabled = GUI.enabled;
                try
                {
                    GUI.enabled = previousEnabled && owner.CanAcceptForUi;
                    if (Widgets.ButtonText(new Rect(viewWidth - buttonWidth - 6f, y,
                        buttonWidth, 32f), L("AquacultureFishing.BreedRegistrationRegister")))
                        owner.AcceptForUi();
                }
                finally
                {
                    GUI.enabled = previousEnabled;
                }
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = previousFont;
            }
        }

        private static string L(string key) => key.Translate().ToString();
    }
}
