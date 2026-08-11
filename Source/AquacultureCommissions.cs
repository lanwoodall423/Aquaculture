using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class AquacultureCommissionRecord : IExposable
    {
        public string id;
        public string breedId;
        public string fishDefName;
        public string traitDefName;
        public float minimumStability;
        public int minimumGeneration = 1;
        public float minimumSizeFactor = 1f;
        public int offeredTick = -1;
        public int deadlineTick = -1;
        public int rewardSilver;
        public string improvementKind;

        public ThingDef FishDef => fishDefName.NullOrEmpty() ? null
            : DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);
        public FishTraitDef Trait => traitDefName.NullOrEmpty() ? null
            : DefDatabase<FishTraitDef>.GetNamedSilentFail(traitDefName);
        public FishBreedRecord Breed => AquacultureJournalComponent.Current?.BreedById(breedId);

        public int DaysRemaining(int now)
        {
            if (deadlineTick < 0) return 0;
            return Mathf.Max(0, Mathf.CeilToInt((deadlineTick - now) / 60000f));
        }

        public bool IsExpired(int now) => deadlineTick >= 0 && now >= deadlineTick;

        public string RequirementsText
        {
            get
            {
                return "AquacultureFishing.CommissionRequirements".Translate(
                    FishDef?.LabelCap ?? "AquacultureFishing.CommissionUnknown".Translate(),
                    Breed?.name ?? "AquacultureFishing.CommissionUnknown".Translate(),
                    AquacultureCommissionGoalRules.NormalizeStability(minimumStability).ToStringPercent(), minimumGeneration,
                    Trait?.LabelCap ?? "AquacultureFishing.CommissionUnknown".Translate(),
                    AquacultureCommissionGoalRules.NormalizeSizeThreshold(minimumSizeFactor).ToStringPercent()).ToString();
            }
        }

        public bool Matches(CompFishTraits fish, bool requireSpawned)
        {
            FishBreedRecord breed = Breed;
            return AquacultureCommissionGoalRules.IsEligible(new AquacultureCommissionEligibilityInput
            {
                alive = fish?.IsAlive == true,
                spawned = requireSpawned ? fish?.parent?.Spawned == true : fish?.parent != null,
                correctSpecies = fish?.parent?.def == FishDef,
                correctBreed = fish?.breedId == breedId && breed != null,
                adult = fish?.IsAdult == true,
                sterile = fish?.sterilized == true,
                foodReserve = fish?.foodReserve ?? float.NaN,
                starvationProgress = fish?.starvationProgress ?? float.NaN,
                waterStress = fish?.waterStress ?? float.NaN,
                temperatureStress = fish?.temperatureStress ?? float.NaN,
                habitatStress = fish?.habitatStress ?? float.NaN,
                breedStability = breed?.Stability ?? float.NaN,
                breedGeneration = fish?.breedGeneration ?? -1,
                sizeFactor = fish?.SizeFactor ?? float.NaN,
                hasRequiredTrait = !traitDefName.NullOrEmpty() && fish?.traitDefNames?.Contains(traitDefName) == true
            }) && (fish?.breedGeneration ?? -1) >= minimumGeneration
                && AquacultureCommissionGoalRules.MeetsSize(fish?.SizeFactor ?? float.NaN, minimumSizeFactor)
                && (breed?.Stability ?? float.NaN) + AquacultureCommissionGoalRules.SizeComparisonEpsilon >=
                    AquacultureCommissionGoalRules.NormalizeStability(minimumStability);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref breedId, "breedId");
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Values.Look(ref traitDefName, "traitDefName");
            Scribe_Values.Look(ref minimumStability, "minimumStability", 0.70f);
            Scribe_Values.Look(ref minimumGeneration, "minimumGeneration", 1);
            Scribe_Values.Look(ref minimumSizeFactor, "minimumSizeFactor", 1f);
            Scribe_Values.Look(ref offeredTick, "offeredTick", -1);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", -1);
            Scribe_Values.Look(ref rewardSilver, "rewardSilver");
            Scribe_Values.Look(ref improvementKind, "improvementKind");
        }
    }

    public static class AquacultureCommissionRules
    {
        public const int OfferCooldownTicks = 30 * 60000;
        public const int EligibilityCheckIntervalTicks = 6000;

        public static float TraitDifficulty(FishTraitDef trait)
        {
            if (trait == null) return 1f;
            float rarity = Mathf.Clamp(1.50f - Mathf.Max(0f, trait.commonality) * 0.25f, 0.90f, 1.50f);
            float kindFactor = trait.kind == FishTraitKind.Body || trait.kind == FishTraitKind.Breeding
                || trait.kind == FishTraitKind.Hardiness || trait.kind == FishTraitKind.Effect ? 1.25f
                : trait.IsNumeric ? 1.15f : 1f;
            return Mathf.Clamp(rarity * kindFactor, 0.75f, 2f);
        }

        public static int DeadlineDays(float traitDifficulty, int generation)
        {
            return AquacultureCommissionGoalRules.DeadlineDays(traitDifficulty, generation);
        }

        public static int RewardSilver(int generation, float stability, float traitDifficulty, float sizeFactor, int deadlineDays)
        {
            return AquacultureCommissionGoalRules.RewardSilver(generation, stability, traitDifficulty,
                sizeFactor, 1f, deadlineDays);
        }

        public static int RewardSilver(int generation, float stability, float traitDifficulty,
            float sizeFactor, float rarityFactor, int deadlineDays)
        {
            return AquacultureCommissionGoalRules.RewardSilver(generation, stability, traitDifficulty,
                sizeFactor, rarityFactor, deadlineDays);
        }

        public static string RequestKey(string breedId, string traitDefName, int generation, float sizeFactor)
        {
            return (breedId ?? string.Empty) + "|" + (traitDefName ?? string.Empty) + "|" + generation + "|" +
                sizeFactor.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string CurrentRequestKey(AquacultureCommissionGoal goal)
        {
            return goal?.RequestKey ?? string.Empty;
        }
    }

    public sealed class AquacultureCommissionComponent : GameComponent
    {
        private AquacultureCommissionRecord activeCommission;
        private List<string> requestHistory = new List<string>();
        private readonly List<Thing> eligibleSpecimens = new List<Thing>();
        private readonly AquacultureCommissionFishIndex<CompFishTraits> fishIndex =
            new AquacultureCommissionFishIndex<CompFishTraits>();
        private string eligibleSpecimensCommissionId;
        private bool fishIndexInitialized;
        private bool fishIndexDirty = true;
        private bool eligibleSpecimensDirty = true;
        private int indexedMapCount = -1;
        private int nextOfferTick;
        private int nextEligibilityCheckTick;

        public AquacultureCommissionComponent(Game game)
        {
        }

        public static AquacultureCommissionComponent Current => Verse.Current.Game?.GetComponent<AquacultureCommissionComponent>();
        public AquacultureCommissionRecord ActiveCommission => activeCommission;

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref activeCommission, "activeCommission");
            Scribe_Collections.Look(ref requestHistory, "commissionRequestHistory", LookMode.Value);
            Scribe_Values.Look(ref nextOfferTick, "commissionNextOfferTick");
            Scribe_Values.Look(ref nextEligibilityCheckTick, "commissionNextEligibilityCheckTick");
            if (requestHistory == null) requestHistory = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                requestHistory.RemoveAll(key => key.NullOrEmpty());
                NormalizeLoadedCommission();
                eligibleSpecimens.Clear();
                eligibleSpecimensCommissionId = null;
                eligibleSpecimensDirty = true;
                ClearFishIndex();
            }
        }

        public override void GameComponentTick()
        {
            AquacultureInGameTestTickPatch.PollFromExistingComponent();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextEligibilityCheckTick) return;
            nextEligibilityCheckTick = now + AquacultureCommissionRules.EligibilityCheckIntervalTicks;

            if (activeCommission != null)
            {
                if (!activeCommission.IsExpired(now))
                {
                    RefreshEligibleSpecimens();
                    return;
                }
                ExpireActiveCommission(now);
                return;
            }

            if (now < nextOfferTick) return;
            foreach (FishBreedRecord breed in AquacultureJournalComponent.Current?.Breeds
                ?.Where(item => item != null).OrderBy(item => item.id, StringComparer.Ordinal)
                ?? Enumerable.Empty<FishBreedRecord>())
            {
                if (TryOfferForBreed(breed, now)) break;
            }
        }

        public bool TryOfferForBreed(FishBreedRecord breed, int now = -1)
        {
            now = now >= 0 ? now : Find.TickManager?.TicksGame ?? 0;
            if (activeCommission != null || breed?.id.NullOrEmpty() != false || now < nextOfferTick || breed.FishDef == null)
                return false;

            List<CompFishTraits> candidates = HealthyBreedFish(breed).ToList();
            if (candidates.Count == 0) return false;

            List<FishTraitDef> traits = breed.traitDefNames?.Select(DefDatabase<FishTraitDef>.GetNamedSilentFail)
                .Where(trait => trait != null && candidates.Any(fish => fish?.traitDefNames?.Contains(trait.defName) == true))
                .OrderByDescending(AquacultureCommissionRules.TraitDifficulty)
                .ThenBy(trait => trait.defName, StringComparer.Ordinal).ToList() ?? new List<FishTraitDef>();
            if (traits.Count == 0) return false;

            AquacultureCommissionGoal selectedGoal = null;
            FishTraitDef selectedTrait = null;
            for (int i = 0; i < traits.Count; i++)
            {
                AquacultureCommissionCapability capability = BuildCapability(breed, candidates, traits[i], now);
                AquacultureCommissionGoal goal = AquacultureCommissionGoalRules.SelectGoal(capability,
                    candidateGoal => candidates.Any(fish => MatchesGoal(fish, breed, candidateGoal)),
                    goal => HasRequestKey(goal, breed),
                    candidateGoal => AquacultureCommissionRules.DeadlineDays(
                        AquacultureCommissionRules.TraitDifficulty(traits[i]), candidateGoal.minimumGeneration));
                if (goal != null)
                {
                    selectedTrait = traits[i];
                    selectedGoal = goal;
                    break;
                }
            }
            if (selectedTrait == null || selectedGoal == null) return false;

            float difficulty = AquacultureCommissionRules.TraitDifficulty(selectedTrait);
            int deadlineDays = AquacultureCommissionRules.DeadlineDays(difficulty, selectedGoal.minimumGeneration);
            float rarityFactor = SpeciesRarityFactor(breed.FishDef);
            activeCommission = new AquacultureCommissionRecord
            {
                id = Guid.NewGuid().ToString("N"),
                breedId = breed.id,
                fishDefName = breed.fishDefName,
                traitDefName = selectedTrait.defName,
                minimumStability = selectedGoal.minimumStability,
                minimumGeneration = selectedGoal.minimumGeneration,
                minimumSizeFactor = selectedGoal.minimumSizeFactor,
                offeredTick = now,
                deadlineTick = now + deadlineDays * 60000,
                improvementKind = selectedGoal.improvement.ToString(),
                rewardSilver = AquacultureCommissionRules.RewardSilver(
                    selectedGoal.minimumGeneration, selectedGoal.minimumStability, difficulty,
                    selectedGoal.minimumSizeFactor, rarityFactor, deadlineDays)
            };
            requestHistory.Add(selectedGoal.RequestKey);
            if (requestHistory.Count > 64) requestHistory.RemoveAt(0);
            RefreshEligibleSpecimens();
            Find.LetterStack.ReceiveLetter(
                "AquacultureFishing.CommissionLetterLabel".Translate(),
                "AquacultureFishing.CommissionLetterText".Translate(
                    activeCommission.Breed?.name ?? breed.name, activeCommission.RequirementsText,
                    activeCommission.DaysRemaining(now), activeCommission.rewardSilver).ToString(),
                LetterDefOf.PositiveEvent, null, 0, true);
            return true;
        }

        public IEnumerable<Thing> EligibleSpecimens(AquacultureCommissionRecord commission = null)
        {
            commission = commission ?? activeCommission;
            if (commission == null) return Enumerable.Empty<Thing>();
            if (commission == activeCommission && (eligibleSpecimensCommissionId != commission.id || eligibleSpecimensDirty))
                RefreshEligibleSpecimens();
            return eligibleSpecimens.Where(thing => thing != null && !thing.Destroyed && thing.Spawned);
        }

        public int EligibleSpecimenCount
        {
            get
            {
                if (activeCommission != null && (eligibleSpecimensCommissionId != activeCommission.id || eligibleSpecimensDirty))
                    RefreshEligibleSpecimens();
                int count = 0;
                for (int i = 0; i < eligibleSpecimens.Count; i++)
                    if (eligibleSpecimens[i] != null && !eligibleSpecimens[i].Destroyed && eligibleSpecimens[i].Spawned) count++;
                return count;
            }
        }

        public void RefreshEligibleSpecimens()
        {
            eligibleSpecimens.Clear();
            eligibleSpecimensCommissionId = activeCommission?.id;
            eligibleSpecimensDirty = false;
            if (activeCommission == null) return;
            EnsureFishIndex();
            IReadOnlyCollection<CompFishTraits> candidates = GetBreedFish(activeCommission.breedId);
            foreach (CompFishTraits fish in candidates ?? new HashSet<CompFishTraits>())
            {
                if (activeCommission.Matches(fish, true)) eligibleSpecimens.Add(fish.parent);
            }
        }

        private void ClearEligibleSpecimens()
        {
            eligibleSpecimens.Clear();
            eligibleSpecimensCommissionId = null;
            eligibleSpecimensDirty = true;
        }

        public bool TryDeliver(Thing specimen, out string reason)
        {
            reason = null;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (activeCommission == null)
            {
                reason = "AquacultureFishing.CommissionNoActive".Translate().ToString();
                return false;
            }
            if (activeCommission.IsExpired(now))
            {
                ExpireActiveCommission(now);
                reason = "AquacultureFishing.CommissionExpiredShort".Translate().ToString();
                return false;
            }
            CompFishTraits fish = specimen?.TryGetComp<CompFishTraits>();
            if (!activeCommission.Matches(fish, true))
            {
                reason = "AquacultureFishing.CommissionSpecimenInvalid".Translate().ToString();
                return false;
            }

            Thing reward = ThingMaker.MakeThing(ThingDefOf.Silver);
            reward.stackCount = activeCommission.rewardSilver;
            if (!GenPlace.TryPlaceThing(reward, specimen.Position, specimen.Map, ThingPlaceMode.Near))
            {
                reward.Destroy(DestroyMode.Vanish);
                reason = "AquacultureFishing.CommissionRewardPlacementFailed".Translate().ToString();
                return false;
            }
            specimen.Destroy(DestroyMode.Vanish);
            if (!AquacultureCommissionDeliveryRules.Completes(true, true, true, specimen.Destroyed))
            {
                reward.Destroy(DestroyMode.Vanish);
                reason = "AquacultureFishing.CommissionSpecimenTransferFailed".Translate().ToString();
                return false;
            }

            FishBreedRecord breed = activeCommission.Breed;
            if (breed != null)
            {
                breed.commissionsCompleted++;
                breed.lastCommissionTick = now;
            }
            int rewardAmount = activeCommission.rewardSilver;
            string breedName = breed?.name ?? "AquacultureFishing.CommissionUnknown".Translate().ToString();
            activeCommission = null;
            ClearEligibleSpecimens();
            nextOfferTick = now + AquacultureCommissionRules.OfferCooldownTicks;
            Messages.Message("AquacultureFishing.CommissionCompleted".Translate(breedName, rewardAmount),
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public void NotifyFishSpawned(CompFishTraits fish)
        {
            if (fish?.parent?.Spawned != true) return;
            EnsureFishIndexContainer();
            AddToFishIndex(fish);
            MarkEligibilityDirty();
        }

        public void NotifyFishDespawned(CompFishTraits fish)
        {
            if (fish == null) return;
            RemoveFromFishIndex(fish);
            MarkEligibilityDirty();
        }

        public void NotifyFishChanged(CompFishTraits fish)
        {
            if (fish == null) return;
            if (fish.parent?.Spawned == true) AddToFishIndex(fish);
            else RemoveFromFishIndex(fish);
            MarkEligibilityDirty();
        }

        public void NotifyMapChanged()
        {
            fishIndexDirty = true;
            indexedMapCount = -1;
            MarkEligibilityDirty();
        }

        public void NotifyEligibilityChanged()
        {
            MarkEligibilityDirty();
        }

        public void NotifyBreedRegistered(FishBreedRecord breed)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            nextOfferTick = Math.Max(nextOfferTick, now + AquacultureCommissionRules.OfferCooldownTicks);
            MarkEligibilityDirty();
        }

        public bool HasHistoryFor(FishBreedRecord breed)
        {
            if (breed == null) return false;
            return requestHistory.Any(key => key.StartsWith(breed.id + "|", StringComparison.Ordinal));
        }

        private IEnumerable<CompFishTraits> HealthyBreedFish(FishBreedRecord breed)
        {
            if (breed?.FishDef == null) yield break;
            EnsureFishIndex();
            foreach (CompFishTraits fish in GetBreedFish(breed.id) ?? Array.Empty<CompFishTraits>())
            {
                if (IsHealthyBreedFish(fish, breed)) yield return fish;
            }
        }

        private AquacultureCommissionCapability BuildCapability(FishBreedRecord breed,
            List<CompFishTraits> candidates, FishTraitDef trait, int now)
        {
            float currentSize = candidates.Count == 0 ? 0f : candidates.Max(fish => fish.SizeFactor);
            int currentGeneration = candidates.Count == 0 ? 0 : candidates.Max(fish => fish.breedGeneration);
            bool hasPair = HasBreedingPair(candidates, now);
            return new AquacultureCommissionCapability
            {
                breedId = breed.id,
                fishDefName = breed.fishDefName,
                definingTraitName = trait?.defName,
                currentGeneration = currentGeneration,
                currentStability = breed.Stability,
                currentSizeFactor = currentSize,
                maximumReasonableSizeFactor = Mathf.Min(AquacultureCommissionGoalRules.MaximumSizeFactor,
                    currentSize + 0.10f),
                generationsReachableBeforeDeadline = hasPair ? 1 : 0,
                minimumBreedingWindowDays = Mathf.Max(1,
                    Mathf.RoundToInt(AquacultureMod.Settings?.breedingIntervalDays ?? 15f)),
                hasBreedingPair = hasPair,
                canImproveStability = hasPair && breed.Stability < AquacultureCommissionGoalRules.MaximumStability,
                canImproveSize = hasPair
            };
        }

        private static bool HasBreedingPair(List<CompFishTraits> candidates, int now)
        {
            for (int femaleIndex = 0; femaleIndex < candidates.Count; femaleIndex++)
            {
                CompFishTraits female = candidates[femaleIndex];
                Map map = female?.parent?.Map;
                if (female?.IsFemale != true || map == null || !PondBreedingRules.CanBreedIgnoringCooldown(female, map)) continue;
                List<CompFishTraits> pondFish = map.GetComponent<FishPondMapComponent>()?.FishInSamePond(female);
                for (int maleIndex = 0; maleIndex < candidates.Count; maleIndex++)
                {
                    CompFishTraits male = candidates[maleIndex];
                    if (male == null || male.IsFemale || male.parent?.Map != map || pondFish?.Contains(male) != true) continue;
                    if (PondBreedingRules.CanBreedIgnoringCooldown(male, map)) return true;
                }
            }
            return false;
        }

        private static bool MatchesGoal(CompFishTraits fish, FishBreedRecord breed,
            AquacultureCommissionGoal goal)
        {
            if (fish == null || breed == null || goal == null) return false;
            return AquacultureCommissionGoalRules.MeetsGoal(new AquacultureCommissionEligibilityInput
            {
                alive = fish.IsAlive,
                spawned = fish.parent?.Spawned == true,
                correctSpecies = fish.parent?.def == breed.FishDef,
                correctBreed = fish.breedId == breed.id,
                adult = fish.IsAdult,
                sterile = fish.sterilized,
                foodReserve = fish.foodReserve,
                starvationProgress = fish.starvationProgress,
                waterStress = fish.waterStress,
                temperatureStress = fish.temperatureStress,
                habitatStress = fish.habitatStress,
                breedStability = breed.Stability,
                breedGeneration = fish.breedGeneration,
                sizeFactor = fish.SizeFactor,
                hasRequiredTrait = goal.traitDefName.NullOrEmpty() == false
                    && fish.traitDefNames?.Contains(goal.traitDefName) == true
            }, goal);
        }

        private static bool IsHealthyBreedFish(CompFishTraits fish, FishBreedRecord breed)
        {
            return AquacultureCommissionGoalRules.IsEligible(new AquacultureCommissionEligibilityInput
            {
                alive = fish?.IsAlive == true,
                spawned = fish?.parent?.Spawned == true,
                correctSpecies = fish?.parent?.def == breed?.FishDef,
                correctBreed = fish?.breedId == breed?.id,
                adult = fish?.IsAdult == true,
                sterile = fish?.sterilized == true,
                foodReserve = fish?.foodReserve ?? float.NaN,
                starvationProgress = fish?.starvationProgress ?? float.NaN,
                waterStress = fish?.waterStress ?? float.NaN,
                temperatureStress = fish?.temperatureStress ?? float.NaN,
                habitatStress = fish?.habitatStress ?? float.NaN,
                breedStability = breed?.Stability ?? float.NaN,
                breedGeneration = fish?.breedGeneration ?? -1,
                sizeFactor = fish?.SizeFactor ?? float.NaN,
                hasRequiredTrait = breed?.traitDefNames?.Any(name => !name.NullOrEmpty()
                    && fish?.traitDefNames?.Contains(name) == true) == true
            });
        }

        private bool HasRequestKey(AquacultureCommissionGoal goal, FishBreedRecord breed)
        {
            if (goal == null || breed == null) return true;
            if (requestHistory.Contains(goal.RequestKey)) return true;
            string legacyKey = AquacultureCommissionRules.RequestKey(breed.id, goal.traitDefName,
                goal.minimumGeneration, goal.minimumSizeFactor);
            return requestHistory.Contains(legacyKey);
        }

        private static float SpeciesRarityFactor(ThingDef fishDef)
        {
            float commonality = AquaticSpeciesProfile.PopulationCommonality(fishDef);
            if (commonality < 0.20f) return 1.35f;
            if (commonality < 0.65f) return 1.15f;
            return 0.95f;
        }

        private void NormalizeLoadedCommission()
        {
            if (activeCommission == null) return;
            FishBreedRecord breed = activeCommission.Breed;
            FishTraitDef trait = activeCommission.traitDefName.NullOrEmpty() ? null : activeCommission.Trait;
            if (trait == null && breed?.traitDefNames != null)
            {
                string migratedTrait = breed.traitDefNames
                    .Where(name => !name.NullOrEmpty())
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .FirstOrDefault(name => DefDatabase<FishTraitDef>.GetNamedSilentFail(name) != null);
                if (!migratedTrait.NullOrEmpty())
                {
                    activeCommission.traitDefName = migratedTrait;
                    trait = activeCommission.Trait;
                }
            }
            if (activeCommission.rewardSilver <= 0)
                activeCommission.rewardSilver = AquacultureCommissionGoalRules.MinimumReward;
            if (activeCommission.deadlineTick < 0)
            {
                int baseTick = activeCommission.offeredTick >= 0
                    ? activeCommission.offeredTick : Find.TickManager?.TicksGame ?? 0;
                activeCommission.offeredTick = baseTick;
                activeCommission.deadlineTick = baseTick + 7 * 60000;
            }
            bool referencesValid = breed != null && activeCommission.FishDef != null && trait != null
                && breed.fishDefName == activeCommission.fishDefName
                && breed.traitDefNames?.Contains(activeCommission.traitDefName) == true;
            AquacultureCommissionSavedState state = new AquacultureCommissionSavedState
            {
                id = activeCommission.id,
                breedId = activeCommission.breedId,
                fishDefName = activeCommission.fishDefName,
                traitDefName = activeCommission.traitDefName,
                minimumStability = activeCommission.minimumStability,
                minimumGeneration = activeCommission.minimumGeneration,
                minimumSizeFactor = activeCommission.minimumSizeFactor,
                offeredTick = activeCommission.offeredTick,
                deadlineTick = activeCommission.deadlineTick,
                rewardSilver = activeCommission.rewardSilver
            };
            if (!AquacultureCommissionGoalRules.TryNormalizeSavedState(state, referencesValid))
            {
                activeCommission = null;
                return;
            }
            activeCommission.id = state.id;
            activeCommission.minimumStability = state.minimumStability;
            activeCommission.minimumSizeFactor = state.minimumSizeFactor;
            activeCommission.minimumGeneration = state.minimumGeneration;
        }

        private void RecordFailure(FishBreedRecord breed, int now)
        {
            if (breed == null) return;
            breed.commissionsFailed++;
            breed.lastCommissionFailureTick = now;
        }

        private void ExpireActiveCommission(int now)
        {
            AquacultureCommissionRecord expired = activeCommission;
            if (expired == null) return;
            RecordFailure(expired.Breed, now);
            activeCommission = null;
            ClearEligibleSpecimens();
            nextOfferTick = now + AquacultureCommissionRules.OfferCooldownTicks;
            Messages.Message("AquacultureFishing.CommissionExpired".Translate(
                expired.Breed?.name ?? "AquacultureFishing.CommissionUnknown".Translate()),
                MessageTypeDefOf.CautionInput, false);
        }

        private void EnsureFishIndex()
        {
            int mapCount = Find.Maps?.Count ?? 0;
            if (fishIndexInitialized && !fishIndexDirty && indexedMapCount == mapCount) return;
            fishIndex.Clear();
            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
            {
                foreach (Thing thing in map.listerThings.AllThings ?? Enumerable.Empty<Thing>())
                {
                    CompFishTraits fish = thing?.TryGetComp<CompFishTraits>();
                    if (fish?.parent?.Spawned == true && FishUtility.IsRuntimeFish(thing.def)) AddToFishIndex(fish);
                }
            }
            fishIndexInitialized = true;
            fishIndexDirty = false;
            indexedMapCount = mapCount;
        }

        private void EnsureFishIndexContainer()
        {
            if (fishIndexInitialized) return;
            fishIndexDirty = true;
        }

        private void AddToFishIndex(CompFishTraits fish)
        {
            if (fish?.parent?.Spawned != true || !FishUtility.IsRuntimeFish(fish.parent.def)) return;
            fishIndex.Register(fish, fish.breedId);
        }

        private void RemoveFromFishIndex(CompFishTraits fish)
        {
            if (fish == null) return;
            fishIndex.Unregister(fish);
        }

        private IReadOnlyCollection<CompFishTraits> GetBreedFish(string breedId)
        {
            return fishIndex.ForBreed(breedId);
        }

        private void ClearFishIndex()
        {
            fishIndex.Clear();
            fishIndexInitialized = false;
            fishIndexDirty = true;
            indexedMapCount = -1;
        }

        private void MarkEligibilityDirty()
        {
            eligibleSpecimensDirty = true;
        }
    }

    public static class AquacultureCommissionManager
    {
        public static AquacultureCommissionComponent Current => AquacultureCommissionComponent.Current;

        public static void NotifyBreedRegistered(FishBreedRecord breed)
        {
            Current?.NotifyBreedRegistered(breed);
        }

        public static void NotifyFishSpawned(CompFishTraits fish)
        {
            Current?.NotifyFishSpawned(fish);
        }

        public static void NotifyFishDespawned(CompFishTraits fish)
        {
            Current?.NotifyFishDespawned(fish);
        }

        public static void NotifyFishChanged(CompFishTraits fish)
        {
            Current?.NotifyFishChanged(fish);
        }

        public static void NotifyMapChanged()
        {
            Current?.NotifyMapChanged();
        }

        public static void NotifyEligibilityChanged()
        {
            Current?.NotifyEligibilityChanged();
        }
    }

    public sealed class Dialog_DeliverAquacultureCommission : Window
    {
        private readonly AquacultureCommissionComponent component;
        private readonly AquacultureCommissionRecord commission;
        private readonly List<Thing> specimens;
        private Vector2 scrollPosition;

        public Dialog_DeliverAquacultureCommission(AquacultureCommissionComponent component,
            AquacultureCommissionRecord commission)
        {
            this.component = component;
            this.commission = commission;
            component?.RefreshEligibleSpecimens();
            specimens = component?.EligibleSpecimens(commission)?.ToList() ?? new List<Thing>();
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnAccept = false;
            resizeable = false;
            draggable = true;
            layer = WindowLayer.Dialog;
        }

        public override Vector2 InitialSize => new Vector2(620f, Mathf.Clamp(180f + specimens.Count * 58f, 240f, 680f));

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                "AquacultureFishing.CommissionDeliverTitle".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 42f),
                "AquacultureFishing.CommissionDeliverInstruction".Translate(
                    commission?.FishDef?.LabelCap ?? "AquacultureFishing.CommissionUnknown".Translate()).ToString());
            Rect outRect = new Rect(inRect.x, inRect.y + 84f, inRect.width, inRect.height - 84f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, specimens.Count * 58f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, view);
            for (int i = 0; i < specimens.Count; i++)
            {
                Thing specimen = specimens[i];
                Rect row = new Rect(0f, i * 58f, view.width, 52f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.ThingIcon(new Rect(row.x + 4f, row.y + 4f, 44f, 44f), specimen);
                CompFishTraits fish = specimen.TryGetComp<CompFishTraits>();
                Widgets.Label(new Rect(row.x + 56f, row.y + 4f, view.width * 0.45f, 24f), specimen.LabelCap);
                Widgets.Label(new Rect(row.x + 56f, row.y + 28f, view.width * 0.45f, 22f),
                    "AquacultureFishing.CommissionSpecimenDetails".Translate(
                        fish?.breedGeneration ?? 0, fish?.SizeFactor.ToStringPercent() ?? "-").ToString());
                if (Widgets.ButtonText(new Rect(view.width - 150f, row.y + 9f, 140f, 34f),
                    "AquacultureFishing.CommissionDeliverButton".Translate().ToString()))
                {
                    if (component.TryDeliver(specimen, out string reason))
                    {
                        Close();
                        return;
                    }
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                }
            }
            if (specimens.Count == 0)
                Widgets.Label(new Rect(8f, 8f, view.width - 16f, 32f), "AquacultureFishing.CommissionNoEligible".Translate());
            Widgets.EndScrollView();
        }
    }

    public static class AquacultureCommissionUi
    {
        public static void DrawBreedSection(Rect view, ref float y, FishBreedRecord breed)
        {
            y += 12f;
            Widgets.DrawLineHorizontal(0f, y, view.width);
            y += 16f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 30f), "AquacultureFishing.CommissionTitle".Translate());
            Text.Font = GameFont.Small;
            y += 36f;

            AquacultureCommissionComponent component = AquacultureCommissionComponent.Current;
            AquacultureCommissionRecord active = component?.ActiveCommission;
            if (active?.breedId == breed?.id)
            {
                Widgets.Label(new Rect(0f, y, view.width, 24f), active.RequirementsText);
                y += 28f;
                int days = active.DaysRemaining(Find.TickManager?.TicksGame ?? 0);
                Widgets.Label(new Rect(0f, y, view.width, 24f),
                    "AquacultureFishing.CommissionPreview".Translate(days, active.rewardSilver).ToString());
                y += 30f;
                int candidates = component.EligibleSpecimenCount;
                if (candidates > 0 && Widgets.ButtonText(new Rect(0f, y, 220f, 34f),
                    "AquacultureFishing.CommissionDeliverButton".Translate().ToString()))
                    Find.WindowStack.Add(new Dialog_DeliverAquacultureCommission(component, active));
                else
                    Widgets.Label(new Rect(0f, y, view.width, 26f), "AquacultureFishing.CommissionNoEligible".Translate());
                y += 42f;
            }
            else if (component?.ActiveCommission != null)
            {
                Widgets.Label(new Rect(0f, y, view.width, 42f),
                    "AquacultureFishing.CommissionOtherActive".Translate().ToString());
                y += 48f;
            }
            else
            {
                Widgets.Label(new Rect(0f, y, view.width, 42f),
                    "AquacultureFishing.CommissionWaiting".Translate().ToString());
                y += 48f;
            }

            if (breed != null)
                Widgets.Label(new Rect(0f, y, view.width, 24f),
                    "AquacultureFishing.CommissionCompletedCount".Translate(breed.commissionsCompleted).ToString());
            y += 32f;
            if (breed != null)
                Widgets.Label(new Rect(0f, y, view.width, 24f),
                    "AquacultureFishing.CommissionFailedCount".Translate(breed.commissionsFailed).ToString());
            y += 32f;
        }
    }
}
