using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
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
        public string id;
        public string name;
        public string fishDefName;
        public List<string> traitDefNames = new List<string>();
        public Dictionary<string, float> traitValues = new Dictionary<string, float>();
        public int registeredTick;
        public string registeredBy;
        public int founderCount;
        public int births;
        public int matchingBirths;
        public int highestGeneration;
        public bool masteryAnnounced;
        public List<string> lineage = new List<string>();
        public ThingDef FishDef => DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);
        public float Stability => Mathf.Clamp(0.70f + matchingBirths * 0.03f + Mathf.Max(0, highestGeneration - 1) * 0.02f, 0.70f, 0.98f);
        public float MarketValueFactor => 1.15f + Stability * 0.35f;
        public float BeautyBonus => Stability * 0.75f;
        public bool Mastered => Stability >= 0.90f;

        public void ExposeData()
        {
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
            Scribe_Values.Look(ref highestGeneration, "highestGeneration");
            Scribe_Values.Look(ref masteryAnnounced, "masteryAnnounced");
            Scribe_Collections.Look(ref lineage, "lineage", LookMode.Value);
            if (traitDefNames == null) traitDefNames = new List<string>();
            if (traitValues == null) traitValues = new Dictionary<string, float>();
            if (lineage == null) lineage = new List<string>();
        }
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
            FishBreedRecord breed = BreedById(fish.breedId);
            if (breed == null || fish.breedBirthRecorded) return;
            fish.breedBirthRecorded = true;
            breed.matchingBirths++;
            if (fish.breedGeneration > breed.highestGeneration)
            {
                breed.highestGeneration = fish.breedGeneration;
                breed.lineage.Add("Generation " + breed.highestGeneration + " established on " + DayLabel(CurrentTick) + ".");
            }
            if (breed.Mastered && !breed.masteryAnnounced)
            {
                breed.masteryAnnounced = true;
                Messages.Message("Breed mastered: " + breed.name + " now breeds true with high reliability.", MessageTypeDefOf.PositiveEvent, false);
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
                highestGeneration = Mathf.Max(1, cohort.Max(fish => fish.breedGeneration))
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
            return breedRecord;
        }

        public bool ApplyBreedInheritance(CompFishTraits first, CompFishTraits second, List<string> inheritedTraits,
            Dictionary<string, float> inheritedValues, out string inheritedBreedId, out int inheritedGeneration)
        {
            inheritedBreedId = null;
            inheritedGeneration = 0;
            if (first?.breedId.NullOrEmpty() != false || first.breedId != second?.breedId) return false;
            FishBreedRecord breed = BreedById(first.breedId);
            if (breed == null) return false;
            breed.births++;
            var usedGroups = new HashSet<string>(inheritedTraits.Select(name =>
                FishTraitUtility.ExclusiveGroup(DefDatabase<FishTraitDef>.GetNamedSilentFail(name))));
            for (int i = 0; i < breed.traitDefNames.Count; i++)
            {
                string traitName = breed.traitDefNames[i];
                FishTraitDef trait = DefDatabase<FishTraitDef>.GetNamedSilentFail(traitName);
                if (trait == null || !Rand.Chance(breed.Stability)) continue;
                string group = FishTraitUtility.ExclusiveGroup(trait);
                inheritedTraits.RemoveAll(name =>
                    FishTraitUtility.ExclusiveGroup(DefDatabase<FishTraitDef>.GetNamedSilentFail(name)) == group);
                usedGroups.Add(group);
                if (!inheritedTraits.Contains(traitName)) inheritedTraits.Add(traitName);
                if (breed.traitValues.TryGetValue(traitName, out float value)) inheritedValues[traitName] = value;
            }
            int cap = Mathf.Max(0, AquacultureMod.Settings?.maxMutations ?? 2);
            if (inheritedTraits.Count > cap)
            {
                HashSet<string> defining = new HashSet<string>(breed.traitDefNames);
                inheritedTraits.RemoveAll(name => inheritedTraits.Count > cap && !defining.Contains(name));
            }
            bool matches = breed.traitDefNames.All(inheritedTraits.Contains);
            if (!matches) return false;
            inheritedBreedId = breed.id;
            inheritedGeneration = Mathf.Max(first.breedGeneration, second.breedGeneration) + 1;
            return true;
        }

        public static List<string> InheritableTraits(CompFishTraits fish)
        {
            return fish?.traitDefNames?
                .Where(name => !name.StartsWith("AF_Age_") && !name.StartsWith("AF_Sex_")
                    && !name.StartsWith(FishTraitUtility.DietPrefix) && !name.StartsWith(FishTraitUtility.WaterPrefix))
                .OrderBy(name => name, StringComparer.Ordinal).ToList() ?? new List<string>();
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

    public sealed class Dialog_RegisterFishBreed : Window
    {
        private readonly CompFishTraits founder;
        private string breedName = string.Empty;
        private bool focusName = true;

        public override Vector2 InitialSize => new Vector2(520f, 240f);

        public Dialog_RegisterFishBreed(CompFishTraits founder)
        {
            this.founder = founder;
            doCloseX = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Register Fish Breed");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 42f, inRect.width, 48f),
                "Name this stable breeding population. The name begins blank and must be unique.");
            GUI.SetNextControlName("AquacultureBreedName");
            breedName = Widgets.TextField(new Rect(inRect.x, inRect.y + 98f, inRect.width, 34f), breedName ?? string.Empty);
            if (focusName)
            {
                UI.FocusControl("AquacultureBreedName", this);
                focusName = false;
            }
            bool valid = !breedName.NullOrEmpty() && !AquacultureJournalComponent.Current.Breeds
                .Any(breed => breed.name.Equals(breedName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (Widgets.ButtonText(new Rect(inRect.xMax - 220f, inRect.yMax - 42f, 100f, 36f), "Cancel")) Close();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 42f, 110f, 36f), "Register", active: valid))
                Accept();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                Event.current.Use();
                if (valid) Accept();
            }
        }

        private void Accept()
        {
            FishBreedRecord breed = AquacultureJournalComponent.Current?.RegisterBreed(founder, breedName);
            if (breed != null) Close();
        }
    }

    public sealed class MainTabWindow_AquacultureJournal : MainTabWindow
    {
        private enum JournalPage { Species, Waters, Ponds, Expertise, Breeds, Anglers, Records }

        private JournalPage page;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private string selectedSpecies;
        private string selectedBreed;
        private readonly KnowledgeMenuState expertiseState = new KnowledgeMenuState();

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1180f, UI.screenWidth * 0.96f), Mathf.Min(720f, UI.screenHeight * 0.90f));

        public static void OpenExpertise(Pawn pawn)
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("AF_AquacultureJournal");
            if (button == null) return;
            Find.MainTabsRoot.SetCurrentTab(button, true);
            if (button.TabWindow is MainTabWindow_AquacultureJournal journal)
            {
                journal.page = JournalPage.Expertise;
                journal.expertiseState.scope = KnowledgeMenuScope.Colonist;
                journal.expertiseState.selectedPawn = pawn;
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
            AquacultureJournalViewSnapshot viewSnapshot = AquacultureSnapshotCache.Journal(null, true);
            DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, 70f), journal, viewSnapshot);
            Rect content = new Rect(inRect.x, inRect.y + 78f, inRect.width, inRect.height - 78f);
            bool breedsAvailable = AquacultureProgression.IsAvailable("AF_SelectiveBreeding");
            if (!breedsAvailable && page == JournalPage.Breeds) page = JournalPage.Species;
            if (page == JournalPage.Expertise)
            {
                KnowledgeMenuUI.Draw(content, expertiseState, ExpertiseModelFor, ExpertiseFor);
                return;
            }
            if (page == JournalPage.Waters)
            {
                DrawWaters(content, viewSnapshot);
                return;
            }
            if (page == JournalPage.Ponds)
            {
                DrawPonds(content);
                return;
            }
            if (page == JournalPage.Anglers)
            {
                DrawAnglers(content);
                return;
            }
            if (page == JournalPage.Records)
            {
                DrawRecords(content, journal);
                return;
            }
            if (content.width < 760f)
            {
                float splitHeight = Mathf.Max(180f, (content.height - 12f) * 0.42f);
                Rect list = new Rect(content.x, content.y, content.width, splitHeight);
                Rect detail = new Rect(content.x, list.yMax + 12f, content.width, content.height - splitHeight - 12f);
                Widgets.DrawMenuSection(list);
                Widgets.DrawMenuSection(detail);
                if (page == JournalPage.Species)
                {
                    DrawSpeciesList(list.ContractedBy(10f), journal, viewSnapshot);
                    DrawSpeciesDetail(detail.ContractedBy(14f), journal, viewSnapshot);
                }
                else
                {
                    DrawBreedList(list.ContractedBy(10f), journal);
                    DrawBreedDetail(detail.ContractedBy(14f), journal);
                }
                return;
            }
            Rect left = new Rect(content.x, content.y, 380f, content.height);
            Rect right = new Rect(left.xMax + 12f, content.y, content.width - left.width - 12f, content.height);
            Widgets.DrawMenuSection(left);
            Widgets.DrawMenuSection(right);
            if (page == JournalPage.Species)
            {
                DrawSpeciesList(left.ContractedBy(10f), journal, viewSnapshot);
                DrawSpeciesDetail(right.ContractedBy(14f), journal, viewSnapshot);
            }
            else
            {
                DrawBreedList(left.ContractedBy(10f), journal);
                DrawBreedDetail(right.ContractedBy(14f), journal);
            }
        }

        private void DrawHeader(Rect rect, AquacultureJournalComponent journal, AquacultureJournalViewSnapshot viewSnapshot)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, 330f, 34f), "Aquaculture Field Journal");
            Text.Font = GameFont.Small;
            int total = viewSnapshot?.totalSpecies ?? 0;
            int discovered = viewSnapshot?.discovered ?? 0;
            bool breedsAvailable = AquacultureProgression.IsAvailable("AF_SelectiveBreeding");
            string summary = "Species " + discovered + " / " + total;
            if (breedsAvailable) summary += "   Breeds " + journal.Breeds.Count + "   Mastered " + journal.Breeds.Count(breed => breed.Mastered);
            Widgets.Label(new Rect(rect.x, rect.y + 36f, 500f, 28f), summary);
            string[] labels = breedsAvailable
                ? new[] { "Species", "Waters", "Ponds", "Expertise", "Breeds", "Anglers", "Records" }
                : new[] { "Species", "Waters", "Ponds", "Expertise", "Anglers", "Records" };
            JournalPage[] pages = breedsAvailable
                ? new[] { JournalPage.Species, JournalPage.Waters, JournalPage.Ponds, JournalPage.Expertise, JournalPage.Breeds, JournalPage.Anglers, JournalPage.Records }
                : new[] { JournalPage.Species, JournalPage.Waters, JournalPage.Ponds, JournalPage.Expertise, JournalPage.Anglers, JournalPage.Records };
            float tabWidth = Mathf.Min(124f, (rect.width - 12f - (labels.Length - 1) * 6f) / labels.Length);
            float tabStart = rect.xMax - tabWidth * labels.Length - 6f * (labels.Length - 1);
            for (int i = 0; i < labels.Length; i++)
                if (Widgets.ButtonText(new Rect(tabStart + (tabWidth + 6f) * i, rect.y + 12f, tabWidth, 40f), labels[i], active: page != pages[i]))
                    page = pages[i];
        }

        private KnowledgeMenuModel ExpertiseModelFor(Pawn pawn, bool colony)
        {
            FishingProgressionComponent component = FishingProgressionComponent.Current;
            if (colony)
            {
                List<PawnFishingProgress> colonyRecords = component?.PawnProgress
                    .Where(record => record?.pawn?.Faction?.def?.isPlayer == true).ToList()
                    ?? new List<PawnFishingProgress>();
                return FishingKnowledgeModel(colonyRecords, null, true);
            }
            PawnFishingProgress personal = component?.ProgressFor(pawn, false);
            return FishingKnowledgeModel(personal == null
                ? new List<PawnFishingProgress>() : new List<PawnFishingProgress> { personal }, pawn, false);
        }

        private KnowledgeMenuModel FishingKnowledgeModel(List<PawnFishingProgress> records, Pawn pawn, bool colony)
        {
            PawnFishingProgress personal = colony ? null : records.FirstOrDefault();
            KnowledgeRank level = personal?.ExpertiseLevel ?? KnowledgeRank.Novice;
            var section = new KnowledgeMenuSection
            {
                id = "fish",
                label = "Fish Knowledge",
                emptyText = "No fish species are available."
            };
            foreach (ThingDef fishDef in DefDatabase<ThingDef>.AllDefs.Where(FishUtility.IsFish))
            {
                float knowledge = Mathf.Clamp01(records.Sum(record => record.KnowledgeFor(fishDef)));
                KnowledgeRank required = AquacultureMod.Settings?.MinimumExpertiseFor(fishDef) ?? KnowledgeRank.Novice;
                bool locked = !colony && required > level;
                section.rows.Add(new KnowledgeMenuRow
                {
                    label = fishDef.LabelCap,
                    iconDef = fishDef,
                    rank = FishingProgressionUtility.LevelFor(knowledge * 700f),
                    progress = knowledge,
                    status = locked ? "Locked - requires " + required : "Available",
                    tooltip = fishDef.LabelCap + "\n\n" + (locked ? "Requires " + required + " fishing expertise." : "Available to catch.") +
                        "\nKnowledge: " + knowledge.ToStringPercent()
                });
            }
            if (colony)
            {
                return new KnowledgeMenuModel
                {
                    title = "Colony Fishing Knowledge",
                    sections = new List<KnowledgeMenuSection> { section }
                };
            }
            return new KnowledgeMenuModel
            {
                title = (pawn?.LabelShortCap ?? "Colonist") + " - Fishing",
                expertiseLabel = "Fishing expertise",
                expertiseRank = level,
                expertiseProgress = personal?.ExpertiseProgress ?? 0f,
                sections = new List<KnowledgeMenuSection> { section }
            };
        }

        private static KnowledgeRank ExpertiseFor(Pawn pawn) =>
            FishingProgressionComponent.Current?.ProgressFor(pawn, false)?.ExpertiseLevel ?? KnowledgeRank.Novice;

        private void DrawSpeciesList(Rect rect, AquacultureJournalComponent journal, AquacultureJournalViewSnapshot viewSnapshot)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Species Catalogue");
            Text.Font = GameFont.Small;
            List<AquacultureSpeciesViewSnapshot> records = (viewSnapshot?.species ?? Array.Empty<AquacultureSpeciesViewSnapshot>())
                .OrderByDescending(item => item.identityKnown)
                .ThenByDescending(item => item.record?.Milestones ?? 0)
                .ThenBy(item => item.identityKnown ? item.fishDef.label : "Unknown")
                .ThenBy(item => item.fishDef.defName).ToList();
            Rect outRect = new Rect(rect.x, rect.y + 38f, rect.width, rect.height - 38f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, records.Count * 58f + 42f));
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            float y = 0f;
            for (int i = 0; i < records.Count; i++)
            {
                AquacultureSpeciesViewSnapshot record = records[i];
                Rect row = new Rect(0f, y, view.width, 52f);
                if (record.fishDef.defName == selectedSpecies) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Rect iconRect = new Rect(row.x + 4f, row.y + 5f, 42f, 42f);
                if (record.identityKnown)
                {
                    Widgets.ThingIcon(iconRect, record.fishDef);
                }
                else
                {
                    GUI.color = Color.gray;
                    Widgets.Label(iconRect, "?");
                    GUI.color = Color.white;
                }
                Widgets.Label(new Rect(row.x + 54f, row.y + 5f, row.width - 60f, 24f),
                    record.identityKnown ? record.fishDef.LabelCap.ToString() : "Unknown aquatic species");
                int milestones = record.record?.Milestones ?? 0;
                GUI.color = milestones == 4 ? new Color(0.55f, 0.95f, 0.60f) : Color.gray;
                Widgets.Label(new Rect(row.x + 54f, row.y + 27f, row.width - 60f, 22f),
                    record.stageId + "   " + milestones + " / 4 milestones");
                GUI.color = Color.white;
                TooltipHandler.TipRegion(row, record.identityKnown
                    ? record.fishDef.LabelCap + "\nKnowledge stage: " + record.stageId + "\nConfidence: " + record.confidence.ToStringPercent()
                    : "The colony has not identified this species yet.");
                if (Widgets.ButtonInvisible(row)) selectedSpecies = record.fishDef.defName;
                y += 58f;
            }
            int unknown = records.Count(record => !record.identityKnown);
            GUI.color = Color.gray;
            Widgets.Label(new Rect(4f, y + 6f, view.width - 8f, 28f), unknown + " species remain unidentified.");
            GUI.color = Color.white;
            Widgets.EndScrollView();
            if (selectedSpecies.NullOrEmpty() && records.Count > 0) selectedSpecies = records[0].fishDef.defName;
        }

        private void DrawSpeciesDetail(Rect rect, AquacultureJournalComponent journal, AquacultureJournalViewSnapshot viewSnapshot)
        {
            AquacultureSpeciesViewSnapshot species = viewSnapshot?.species.FirstOrDefault(item => item.fishDef?.defName == selectedSpecies);
            if (species?.fishDef == null)
            {
                Widgets.Label(rect, "Select a species to inspect its field dossier.");
                return;
            }
            FishSpeciesJournalRecord record = species.record ?? new FishSpeciesJournalRecord { fishDefName = species.fishDef.defName };
            Rect outRect = rect;
            Rect view = new Rect(0f, 0f, outRect.width - 16f, species.identityKnown ? 650f : 330f);
            Widgets.BeginScrollView(outRect, ref detailScroll, view);
            if (species.identityKnown) Widgets.ThingIcon(new Rect(0f, 0f, 72f, 72f), species.fishDef);
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(18f, 12f, 48f, 48f), "?");
                GUI.color = Color.white;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(84f, 6f, view.width - 84f, 34f),
                species.identityKnown ? species.fishDef.LabelCap.ToString() : "Unknown aquatic species");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(84f, 42f, view.width - 84f, 28f),
                "Knowledge stage: " + species.stageId + "   Confidence: " + species.confidence.ToStringPercent());
            float y = 92f;
            DrawProgressBar(new Rect(0f, y, view.width, 30f), species.knowledge, "Recorded knowledge " + species.knowledge.ToStringPercent());
            y += 46f;
            if (!species.identityKnown)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, y, view.width, 48f),
                    "The colony has observations for this species, but not enough evidence to identify it.\nCatch, observe, or study a specimen to reveal its identity.");
                GUI.color = Color.white;
                y += 72f;
                DrawKnownFacets(view, ref y, species);
                Widgets.EndScrollView();
                return;
            }
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(species.fishDef);
            Widgets.Label(new Rect(84f, 68f, view.width - 84f, 24f),
                profile.waterKind + "   " + profile.diet + "   " +
                profile.minimumTemperature.ToString("0.#") + " to " + profile.maximumTemperature.ToString("0.#") + " C");
            y += 0f;
            DrawKnownFacets(view, ref y, species);
            AquacultureRecommendation recommendation = AquacultureRecommendationService.BestPawnForTrip(ColonistAnglers(), species.fishDef);
            if (recommendation != null)
            {
                DrawRecord(view, ref y, "Recommended angler", recommendation.label);
                DrawRecord(view, ref y, "Recommendation confidence", recommendation.confidence.ToStringPercent());
            }
            y += 10f;
            DrawMilestone(view, ref y, "Discovered", record.discoveredTick, record.discoveredBy.NullOrEmpty()
                ? "A living specimen entered colony knowledge." : "First recorded by " + record.discoveredBy + ".");
            DrawMilestone(view, ref y, "Established", record.establishedTick, "Successfully placed in a compatible colony pond.");
            DrawMilestone(view, ref y, "Bred", record.bredTick, "Produced colony-born offspring.");
            string stableText = record.stableStartTick >= 0 && record.stableTick < 0
                ? "Healthy breeding population maintained for " + (CurrentTick - record.stableStartTick).ToStringTicksToPeriod() + " of one quadrum."
                : "Maintained a healthy breeding population for one quadrum.";
            DrawMilestone(view, ref y, "Stable", record.stableTick, stableText);
            y += 10f;
            Widgets.DrawLineHorizontal(0f, y, view.width);
            y += 14f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 30f), "Colony Records");
            Text.Font = GameFont.Small;
            y += 38f;
            DrawRecord(view, ref y, "Largest size", record.largestSize.ToStringPercent());
            DrawRecord(view, ref y, "Highest beauty", record.highestBeauty.ToString("0.#"));
            DrawRecord(view, ref y, "Highest nutrition", record.highestNutrition.ToStringPercent());
            DrawRecord(view, ref y, "Rarest trait combination", record.rarestTraitScore.ToString("0.00") + " rarity");
            DrawRecord(view, ref y, "Longest lived", record.longestLivedTicks.ToStringTicksToPeriod());
            Widgets.EndScrollView();
        }

        private static void DrawKnownFacets(Rect view, ref float y, AquacultureSpeciesViewSnapshot species)
        {
            Widgets.Label(new Rect(0f, y, view.width, 26f), "Known facets: " +
                (species.knownFacets.Count == 0 ? "none" : string.Join(", ", species.knownFacets.ToArray())));
            y += 34f;
        }

        private void DrawWaters(Rect rect, AquacultureJournalViewSnapshot viewSnapshot)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Water Surveys");
            Text.Font = GameFont.Small;
            Map map = Find.CurrentMap;
            IReadOnlyList<NaturalWaterViewSnapshot> waters = AquacultureSnapshotCache.Waters(map);
            if (waters.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, rect.y + 42f, rect.width, 50f),
                    "No prepared water survey is available on the current map. Move near natural water and observe it to begin a survey.");
                GUI.color = Color.white;
                return;
            }
            HashSet<ThingDef> known = new HashSet<ThingDef>((viewSnapshot?.species ?? Array.Empty<AquacultureSpeciesViewSnapshot>())
                .Where(item => item.identityKnown).Select(item => item.fishDef));
            Rect outRect = new Rect(rect.x, rect.y + 38f, rect.width, rect.height - 38f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, waters.Count * 112f + 20f));
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            float y = 0f;
            for (int i = 0; i < waters.Count; i++)
            {
                NaturalWaterViewSnapshot water = waters[i];
                Rect row = new Rect(0f, y, view.width, 102f);
                Widgets.DrawHighlightIfMouseover(row);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(10f, y + 6f, view.width - 20f, 26f),
                    water.habitat + " survey at " + water.anchor.x + ", " + water.anchor.z);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(10f, y + 34f, view.width * 0.48f, 24f), "Abundance " + water.AbundanceLabel);
                Widgets.Label(new Rect(view.width * 0.50f, y + 34f, view.width * 0.48f, 24f),
                    "Capacity " + water.carryingCapacity);
                float ratio = water.carryingCapacity <= 0 ? 0f : water.totalPopulation / water.carryingCapacity;
                DrawProgressBar(new Rect(10f, y + 62f, view.width * 0.42f, 24f), ratio, "Estimated biomass");
                List<string> identified = water.species.Where(known.Contains).Select(fish => fish.LabelCap.ToString()).ToList();
                int hidden = water.species.Count - identified.Count;
                string evidence = identified.Count == 0 ? "No identified species" : string.Join(", ", identified.ToArray());
                if (hidden > 0) evidence += "   + " + hidden + " unidentified";
                GUI.color = Color.gray;
                Widgets.Label(new Rect(view.width * 0.45f, y + 62f, view.width * 0.53f, 24f), evidence);
                GUI.color = Color.white;
                Pawn angler = expertiseState.selectedPawn ?? ColonistAnglers().FirstOrDefault();
                AquacultureRecommendation lure = AquacultureRecommendationService.BestKnownLure(angler, water.anchor);
                TooltipHandler.TipRegion(row, "Prepared survey data preserves uncertainty and does not reveal exact fish counts." +
                    (lure?.tackle == null ? string.Empty : "\nRecommended lure: " + lure.label));
                y += 112f;
            }
            Widgets.EndScrollView();
        }

        private void DrawPonds(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Pond Ecology");
            Text.Font = GameFont.Small;
            IReadOnlyList<PondMenuSnapshot> ponds = AquacultureSnapshotCache.Ponds(Find.CurrentMap);
            if (ponds.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, rect.y + 42f, rect.width, 50f),
                    "No constructed ponds are available on the current map.");
                GUI.color = Color.white;
                return;
            }
            Rect outRect = new Rect(rect.x, rect.y + 38f, rect.width, rect.height - 38f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, ponds.Count * 150f + 20f));
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            float y = 0f;
            for (int i = 0; i < ponds.Count; i++)
            {
                PondMenuSnapshot pond = ponds[i];
                Rect row = new Rect(0f, y, view.width, 140f);
                Widgets.DrawHighlightIfMouseover(row);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(10f, y + 6f, view.width - 20f, 26f), "Pond " + (i + 1));
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(10f, y + 34f, view.width * 0.48f, 24f),
                    "Population " + pond.population + " / " + pond.capacity);
                Widgets.Label(new Rect(view.width * 0.50f, y + 34f, view.width * 0.48f, 24f),
                    "Temperature " + pond.temperature.ToString("0.#") + " C");
                DrawProgressBar(new Rect(10f, y + 62f, view.width * 0.42f, 24f),
                    pond.capacity <= 0 ? 0f : pond.population / (float)pond.capacity, "Stocking");
                string warning = pond.warnings.Count == 0 ? "No current ecology warnings." : string.Join("; ", pond.warnings.ToArray());
                GUI.color = pond.warnings.Count == 0 ? Color.gray : new Color(1f, 0.75f, 0.35f);
                Widgets.Label(new Rect(view.width * 0.45f, y + 62f, view.width * 0.53f, 48f), warning);
                GUI.color = Color.white;
                Widgets.Label(new Rect(10f, y + 96f, view.width - 20f, 24f),
                    "Algae " + pond.algaePercent.ToStringPercent() + "   Detritus " + pond.detritusPercent.ToStringPercent() +
                    "   Eggs " + pond.eggs + "   Eligible harvest " + pond.eligibleHarvest);
                y += 150f;
            }
            Widgets.EndScrollView();
        }

        private void DrawAnglers(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Anglers");
            Text.Font = GameFont.Small;
            List<Pawn> anglers = ColonistAnglers();
            anglers = anglers.OrderByDescending(AquacultureKnowledgeAdapter.ExpertiseFor).ThenBy(pawn => pawn.LabelShortCap).ToList();
            if (anglers.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, rect.y + 42f, rect.width, 40f), "No colonist anglers are available.");
                GUI.color = Color.white;
                return;
            }
            Rect outRect = new Rect(rect.x, rect.y + 38f, rect.width, rect.height - 38f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, anglers.Count * 82f + 20f));
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            float y = 0f;
            for (int i = 0; i < anglers.Count; i++)
            {
                Pawn pawn = anglers[i];
                Rect row = new Rect(0f, y, view.width, 72f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.ThingIcon(new Rect(6f, y + 8f, 52f, 52f), pawn);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(68f, y + 5f, view.width - 78f, 26f), pawn.LabelShortCap);
                Text.Font = GameFont.Small;
                KnowledgeRank rank = AquacultureKnowledgeAdapter.ExpertiseRankFor(pawn);
                Widgets.Label(new Rect(68f, y + 31f, view.width * 0.34f, 22f), "Expertise " + rank);
                DrawProgressBar(new Rect(view.width * 0.43f, y + 30f, view.width * 0.28f, 24f),
                    AquacultureKnowledgeAdapter.ExpertiseProgressFor(pawn), "Progress");
                List<string> known = AquacultureKnowledgeAdapter.KnownSpeciesFor(pawn).Take(3)
                    .Select(fish => fish.LabelCap.ToString()).ToList();
                GUI.color = Color.gray;
                Widgets.Label(new Rect(view.width * 0.73f, y + 31f, view.width * 0.25f, 32f),
                    known.Count == 0 ? "No species known" : string.Join(", ", known.ToArray()));
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row)) OpenExpertise(pawn);
                y += 82f;
            }
            Widgets.EndScrollView();
        }

        private void DrawRecords(Rect rect, AquacultureJournalComponent journal)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Catch Records");
            Text.Font = GameFont.Small;
            List<FishSpeciesJournalRecord> records = journal.SpeciesRecords.Where(record => record?.discoveredTick >= 0)
                .OrderByDescending(record => record.discoveredTick).ThenBy(record => record.FishDef?.label).ToList();
            Rect outRect = new Rect(rect.x, rect.y + 38f, rect.width, rect.height - 38f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, records.Count * 70f + 30f));
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            if (records.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(6f, 8f, view.width - 12f, 40f), "No catch records have been recorded yet.");
                GUI.color = Color.white;
            }
            float y = 0f;
            for (int i = 0; i < records.Count; i++)
            {
                FishSpeciesJournalRecord record = records[i];
                Rect row = new Rect(0f, y, view.width, 60f);
                Widgets.DrawHighlightIfMouseover(row);
                if (record.FishDef != null) Widgets.ThingIcon(new Rect(6f, y + 8f, 42f, 42f), record.FishDef);
                Widgets.Label(new Rect(58f, y + 4f, view.width * 0.32f, 24f), record.FishDef?.LabelCap ?? "Unknown species");
                Widgets.Label(new Rect(view.width * 0.38f, y + 4f, view.width * 0.26f, 24f), DayLabel(record.discoveredTick));
                Widgets.Label(new Rect(view.width * 0.66f, y + 4f, view.width * 0.32f, 24f),
                    record.discoveredBy.NullOrEmpty() ? "Recorded by colony" : "By " + record.discoveredBy);
                GUI.color = Color.gray;
                Widgets.Label(new Rect(58f, y + 30f, view.width - 68f, 22f),
                    record.Milestones + " / 4 milestones   Largest " + record.largestSize.ToStringPercent() +
                    "   Longest lived " + record.longestLivedTicks.ToStringTicksToPeriod());
                GUI.color = Color.white;
                y += 70f;
            }
            Widgets.EndScrollView();
        }

        private void DrawBreedList(Rect rect, AquacultureJournalComponent journal)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Registered Breeds");
            Text.Font = GameFont.Small;
            List<FishBreedRecord> records = journal.Breeds.OrderBy(breed => breed.FishDef?.label).ThenBy(breed => breed.name).ToList();
            Rect outRect = new Rect(rect.x, rect.y + 38f, rect.width, rect.height - 38f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, records.Count * 64f + 70f));
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            float y = 0f;
            for (int i = 0; i < records.Count; i++)
            {
                FishBreedRecord breed = records[i];
                Rect row = new Rect(0f, y, view.width, 58f);
                if (breed.id == selectedBreed) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                if (breed.FishDef != null) Widgets.ThingIcon(new Rect(4f, row.y + 8f, 42f, 42f), breed.FishDef);
                Widgets.Label(new Rect(54f, row.y + 5f, row.width - 60f, 24f), breed.name);
                GUI.color = breed.Mastered ? new Color(0.55f, 0.95f, 0.60f) : Color.gray;
                Widgets.Label(new Rect(54f, row.y + 29f, row.width - 60f, 22f),
                    (breed.FishDef?.LabelCap ?? "Unknown") + "   " + breed.Stability.ToStringPercent());
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row)) selectedBreed = breed.id;
                y += 64f;
            }
            if (records.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(4f, 8f, view.width - 8f, 60f),
                    "Research Selective Fish Breeding, then select a compatible adult breeding pair in a pond.");
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();
            if (selectedBreed.NullOrEmpty() && records.Count > 0) selectedBreed = records[0].id;
        }

        private void DrawBreedDetail(Rect rect, AquacultureJournalComponent journal)
        {
            FishBreedRecord breed = journal.BreedById(selectedBreed);
            if (breed == null)
            {
                Widgets.Label(rect, "Registered breeds preserve selected traits and become more reliable over successive generations.");
                return;
            }
            Rect view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, 570f + breed.lineage.Count * 28f));
            Widgets.BeginScrollView(rect, ref detailScroll, view);
            if (breed.FishDef != null) Widgets.ThingIcon(new Rect(0f, 0f, 72f, 72f), breed.FishDef);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(84f, 4f, view.width - 84f, 34f), breed.name);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(84f, 40f, view.width - 84f, 28f),
                (breed.FishDef?.LabelCap ?? "Unknown species") + "   Generation " + breed.highestGeneration);
            float y = 92f;
            DrawProgressBar(new Rect(0f, y, view.width, 30f), breed.Stability, "Inheritance stability " + breed.Stability.ToStringPercent());
            y += 44f;
            Widgets.Label(new Rect(0f, y, view.width, 28f),
                breed.Mastered ? "Mastered breed: offspring inherit the defining traits with high reliability."
                    : "Breed toward 90% stability to achieve mastery.");
            y += 40f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 30f), "Defining Traits");
            Text.Font = GameFont.Small;
            y += 36f;
            for (int i = 0; i < breed.traitDefNames.Count; i++)
            {
                FishTraitDef trait = DefDatabase<FishTraitDef>.GetNamedSilentFail(breed.traitDefNames[i]);
                if (trait == null) continue;
                float value = breed.traitValues.TryGetValue(trait.defName, out float numeric) ? numeric : 0f;
                string label = trait.IsNumeric
                    ? trait.label.Replace("(+%)", "(+" + Mathf.RoundToInt(value) + "%)")
                    : trait.LabelCap.ToString();
                Widgets.Label(new Rect(12f, y, view.width - 24f, 26f), label);
                TooltipHandler.TipRegion(new Rect(12f, y, view.width - 24f, 26f), trait.description);
                y += 28f;
            }
            y += 10f;
            Widgets.DrawLineHorizontal(0f, y, view.width);
            y += 16f;
            DrawRecord(view, ref y, "Founders", breed.founderCount.ToString());
            DrawRecord(view, ref y, "Breed offspring", breed.matchingBirths.ToString());
            DrawRecord(view, ref y, "Highest generation", breed.highestGeneration.ToString());
            DrawRecord(view, ref y, "Market value", breed.MarketValueFactor.ToStringPercent() + " of base");
            DrawRecord(view, ref y, "Pond beauty", "+" + breed.BeautyBonus.ToString("0.##") + " per pond fish");
            if (!breed.registeredBy.NullOrEmpty()) DrawRecord(view, ref y, "Registered by", breed.registeredBy);
            DrawRecord(view, ref y, "Registered", DayLabel(breed.registeredTick));
            y += 12f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 30f), "Lineage History");
            Text.Font = GameFont.Small;
            y += 36f;
            for (int i = 0; i < breed.lineage.Count; i++)
            {
                Widgets.Label(new Rect(12f, y, view.width - 24f, 26f), breed.lineage[i]);
                y += 28f;
            }
            Widgets.EndScrollView();
        }

        private static void DrawMilestone(Rect view, ref float y, string label, int tick, string description)
        {
            Rect row = new Rect(0f, y, view.width, 70f);
            Widgets.DrawHighlightIfMouseover(row);
            GUI.color = tick >= 0 ? new Color(0.55f, 0.95f, 0.60f) : Color.gray;
            Widgets.Label(new Rect(8f, y + 5f, 28f, 26f), tick >= 0 ? "OK" : "-");
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(44f, y + 2f, 220f, 30f), label);
            Text.Font = GameFont.Small;
            if (tick >= 0) Widgets.Label(new Rect(view.width - 190f, y + 6f, 180f, 24f), DayLabel(tick));
            GUI.color = Color.gray;
            Widgets.Label(new Rect(44f, y + 34f, view.width - 54f, 30f), description);
            GUI.color = Color.white;
            y += 76f;
        }

        private static void DrawRecord(Rect view, ref float y, string label, string value)
        {
            Widgets.Label(new Rect(10f, y, view.width * 0.48f, 28f), label);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(view.width * 0.48f, y, view.width * 0.50f - 10f, 28f), value);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 30f;
        }

        private static void DrawProgressBar(Rect rect, float value, string label)
        {
            Widgets.FillableBar(rect, Mathf.Clamp01(value));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static List<Pawn> ColonistAnglers()
        {
            return (Verse.Current.Game?.Maps ?? new List<Map>()).SelectMany(map =>
                map.mapPawns?.FreeColonists ?? Enumerable.Empty<Pawn>()).Distinct().ToList();
        }

        private static string DayLabel(int tick)
        {
            return tick < 0 ? "Not completed" : "Day " + (tick / 60000 + 1);
        }

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;
    }
}
