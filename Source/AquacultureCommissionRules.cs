using System;
using System.Collections.Generic;
using System.Globalization;

namespace AquacultureFishing
{
    public enum AquacultureCommissionImprovement
    {
        Generation,
        Stability,
        Size
    }

    public sealed class AquacultureCommissionEligibilityInput
    {
        public bool alive;
        public bool spawned;
        public bool correctSpecies;
        public bool correctBreed;
        public bool adult;
        public bool sterile;
        public float foodReserve;
        public float starvationProgress;
        public float waterStress;
        public float temperatureStress;
        public float habitatStress;
        public float breedStability;
        public int breedGeneration;
        public float sizeFactor;
        public bool hasRequiredTrait;
    }

    public sealed class AquacultureCommissionCapability
    {
        public string breedId;
        public string fishDefName;
        public string definingTraitName;
        public int currentGeneration;
        public float currentStability;
        public float currentSizeFactor;
        public float maximumReasonableSizeFactor;
        public int generationsReachableBeforeDeadline;
        public int minimumBreedingWindowDays;
        public bool hasBreedingPair;
        public bool canImproveStability;
        public bool canImproveSize;
    }

    public sealed class AquacultureCommissionGoal
    {
        public AquacultureCommissionImprovement improvement;
        public string breedId;
        public string fishDefName;
        public string traitDefName;
        public float minimumStability;
        public int minimumGeneration;
        public float minimumSizeFactor;

        public string RequestKey
        {
            get
            {
                return AquacultureCommissionGoalRules.RequestKey(breedId, traitDefName, improvement,
                    minimumStability, minimumGeneration, minimumSizeFactor);
            }
        }
    }

    public sealed class AquacultureCommissionSavedState
    {
        public string id;
        public string breedId;
        public string fishDefName;
        public string traitDefName;
        public float minimumStability;
        public int minimumGeneration;
        public float minimumSizeFactor;
        public int offeredTick;
        public int deadlineTick;
        public int rewardSilver;
    }

    public sealed class AquacultureCommissionFishIndex<T> where T : class
    {
        private readonly HashSet<T> all = new HashSet<T>();
        private readonly Dictionary<T, string> breedByFish = new Dictionary<T, string>();
        private readonly Dictionary<string, HashSet<T>> fishByBreed = new Dictionary<string, HashSet<T>>(StringComparer.Ordinal);

        public int Count => all.Count;

        public void Register(T fish, string breedId)
        {
            if (fish == null) return;
            Unregister(fish);
            all.Add(fish);
            if (string.IsNullOrEmpty(breedId)) return;
            if (!fishByBreed.TryGetValue(breedId, out HashSet<T> group))
                fishByBreed[breedId] = group = new HashSet<T>();
            group.Add(fish);
            breedByFish[fish] = breedId;
        }

        public void Unregister(T fish)
        {
            if (fish == null) return;
            all.Remove(fish);
            if (!breedByFish.TryGetValue(fish, out string breedId)) return;
            breedByFish.Remove(fish);
            if (!fishByBreed.TryGetValue(breedId, out HashSet<T> group)) return;
            group.Remove(fish);
            if (group.Count == 0) fishByBreed.Remove(breedId);
        }

        public IReadOnlyCollection<T> ForBreed(string breedId)
        {
            return !string.IsNullOrEmpty(breedId) && fishByBreed.TryGetValue(breedId, out HashSet<T> group)
                ? group : Array.Empty<T>();
        }

        public void Clear()
        {
            all.Clear();
            breedByFish.Clear();
            fishByBreed.Clear();
        }
    }

    public static class AquacultureCommissionDeliveryRules
    {
        public static bool Completes(bool active, bool specimenValid, bool rewardPlaced, bool specimenConsumed)
        {
            return active && specimenValid && rewardPlaced && specimenConsumed;
        }
    }

    public static class AquacultureCommissionGoalRules
    {
        public const float MinimumStability = 0f;
        public const float MaximumStability = 0.98f;
        public const float StabilityImprovementStep = 0.05f;
        public const float SizeImprovementStep = 0.05f;
        public const float MaximumSizeFactor = 2f;
        public const float SizeComparisonEpsilon = 0.0001f;
        public const int MinimumReward = 250;
        public const int MaximumReward = 2400;

        public static float NormalizeStability(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return MinimumStability;
            return Clamp(value, MinimumStability, MaximumStability);
        }

        public static float NormalizeSizeThreshold(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            float floored = (float)(Math.Floor(Math.Max(0d, value) * 100d + 0.000001d) / 100d);
            return Clamp(floored, 0f, MaximumSizeFactor);
        }

        public static bool MeetsSize(float actualSizeFactor, float minimumSizeFactor)
        {
            if (float.IsNaN(actualSizeFactor) || float.IsInfinity(actualSizeFactor)) return false;
            float threshold = NormalizeSizeThreshold(minimumSizeFactor);
            return actualSizeFactor + SizeComparisonEpsilon >= threshold;
        }

        public static bool IsEligible(AquacultureCommissionEligibilityInput input)
        {
            if (input == null || !input.alive || !input.spawned || !input.correctSpecies || !input.correctBreed
                || !input.adult || input.sterile || !input.hasRequiredTrait) return false;
            if (!Finite(input.foodReserve) || input.foodReserve < 0.35f) return false;
            if (!Finite(input.starvationProgress) || input.starvationProgress > 0f) return false;
            if (!Finite(input.waterStress) || input.waterStress >= 0.10f) return false;
            if (!Finite(input.temperatureStress) || input.temperatureStress >= 0.10f) return false;
            if (!Finite(input.habitatStress) || input.habitatStress >= 0.75f) return false;
            if (input.breedGeneration < 0) return false;
            if (!Finite(input.breedStability) || !Finite(input.sizeFactor)) return false;
            return true;
        }

        public static bool MeetsGoal(AquacultureCommissionEligibilityInput input, AquacultureCommissionGoal goal)
        {
            if (input == null || goal == null || !IsEligible(input)) return false;
            if (input.breedStability + SizeComparisonEpsilon < NormalizeStability(goal.minimumStability)) return false;
            if (input.breedGeneration < Math.Max(0, goal.minimumGeneration)) return false;
            return MeetsSize(input.sizeFactor, goal.minimumSizeFactor);
        }

        public static List<AquacultureCommissionGoal> BuildGoals(AquacultureCommissionCapability capability)
        {
            List<AquacultureCommissionGoal> goals = new List<AquacultureCommissionGoal>();
            if (capability == null || string.IsNullOrEmpty(capability.breedId) || string.IsNullOrEmpty(capability.fishDefName)
                || string.IsNullOrEmpty(capability.definingTraitName) || !capability.hasBreedingPair) return goals;

            int generation = Math.Max(0, capability.currentGeneration);
            float stability = NormalizeStability(capability.currentStability);
            float size = NormalizeSizeThreshold(capability.currentSizeFactor);
            if (capability.generationsReachableBeforeDeadline > 0)
            {
                goals.Add(CreateGoal(capability, AquacultureCommissionImprovement.Generation,
                    stability, generation + 1, size));
            }
            if (capability.canImproveStability && stability < MaximumStability)
            {
                goals.Add(CreateGoal(capability, AquacultureCommissionImprovement.Stability,
                    NormalizeStability(stability + StabilityImprovementStep), generation, size));
            }
            if (capability.canImproveSize)
            {
                float targetSize = NormalizeSizeThreshold(size + SizeImprovementStep);
                if (targetSize > size && targetSize <= NormalizeSizeThreshold(capability.maximumReasonableSizeFactor))
                {
                    goals.Add(CreateGoal(capability, AquacultureCommissionImprovement.Size,
                        stability, generation, targetSize));
                }
            }
            return goals;
        }

        public static bool IsAchievable(AquacultureCommissionCapability capability,
            AquacultureCommissionGoal goal, int deadlineDays)
        {
            if (capability == null || goal == null || !capability.hasBreedingPair || deadlineDays < 1
                || (capability.minimumBreedingWindowDays > 0 && deadlineDays < capability.minimumBreedingWindowDays)) return false;
            if (goal.minimumGeneration > capability.currentGeneration + capability.generationsReachableBeforeDeadline)
                return false;
            if (goal.minimumStability > NormalizeStability(capability.currentStability)
                && !capability.canImproveStability) return false;
            if (goal.minimumSizeFactor > NormalizeSizeThreshold(capability.maximumReasonableSizeFactor)) return false;
            return true;
        }

        public static bool TryNormalizeSavedState(AquacultureCommissionSavedState state, bool referencesValid)
        {
            if (state == null || !referencesValid || string.IsNullOrEmpty(state.breedId)
                || string.IsNullOrEmpty(state.fishDefName) || string.IsNullOrEmpty(state.traitDefName)
                || state.rewardSilver <= 0 || state.deadlineTick < 0
                || (state.offeredTick >= 0 && state.deadlineTick < state.offeredTick)) return false;
            if (string.IsNullOrEmpty(state.id))
                state.id = "legacy-" + state.breedId + "-" + state.offeredTick;
            state.minimumStability = NormalizeStability(state.minimumStability);
            state.minimumGeneration = Math.Max(0, state.minimumGeneration);
            state.minimumSizeFactor = NormalizeSizeThreshold(state.minimumSizeFactor);
            return true;
        }

        public static AquacultureCommissionGoal SelectGoal(AquacultureCommissionCapability capability,
            Func<AquacultureCommissionGoal, bool> alreadySatisfied,
            Func<AquacultureCommissionGoal, bool> alreadyRequested,
            Func<AquacultureCommissionGoal, int> deadlineFor)
        {
            foreach (AquacultureCommissionGoal goal in BuildGoals(capability))
            {
                if (alreadySatisfied != null && alreadySatisfied(goal)) continue;
                if (alreadyRequested != null && alreadyRequested(goal)) continue;
                int deadlineDays = deadlineFor == null ? 0 : deadlineFor(goal);
                if (!IsAchievable(capability, goal, deadlineDays)) continue;
                return goal;
            }
            return null;
        }

        public static int DeadlineDays(float traitDifficulty, int generation)
        {
            if (float.IsNaN(traitDifficulty) || float.IsInfinity(traitDifficulty)) traitDifficulty = 1f;
            int days = 18 - (int)Math.Round(Clamp(traitDifficulty, 0.75f, 2f) * 2f)
                - Math.Max(0, generation - 1) / 2;
            return Math.Max(7, Math.Min(18, days));
        }

        public static float GenerationFactor(int generation)
        {
            return 1f + Math.Min(8, Math.Max(0, generation - 1)) * 0.10f;
        }

        public static float StabilityFactor(float stability)
        {
            return 1f + Clamp((NormalizeStability(stability) - 0.50f) / 0.48f, 0f, 1f) * 0.40f;
        }

        public static float TraitDifficultyFactor(float traitDifficulty)
        {
            if (float.IsNaN(traitDifficulty) || float.IsInfinity(traitDifficulty)) traitDifficulty = 1f;
            return Clamp(0.90f + (Clamp(traitDifficulty, 0.75f, 2f) - 0.75f) / 1.25f * 0.35f, 0.90f, 1.25f);
        }

        public static float SizeFactor(float sizeFactor)
        {
            return 1f + Clamp(NormalizeSizeThreshold(sizeFactor) - 0.75f, 0f, 1.25f) * 0.20f;
        }

        public static float RarityFactor(float rarityFactor)
        {
            if (float.IsNaN(rarityFactor) || float.IsInfinity(rarityFactor)) rarityFactor = 1f;
            return Clamp(rarityFactor, 0.90f, 1.35f);
        }

        public static float DeadlineFactor(int deadlineDays)
        {
            return Clamp(14f / Math.Max(1, deadlineDays), 0.80f, 1.50f);
        }

        public static int RewardSilver(int generation, float stability, float traitDifficulty,
            float sizeFactor, float rarityFactor, int deadlineDays)
        {
            float raw = 250f * GenerationFactor(generation) * StabilityFactor(stability)
                * TraitDifficultyFactor(traitDifficulty) * SizeFactor(sizeFactor)
                * RarityFactor(rarityFactor) * DeadlineFactor(deadlineDays);
            int rounded = (int)Math.Round(raw / 25f, MidpointRounding.AwayFromZero) * 25;
            return Math.Max(MinimumReward, Math.Min(MaximumReward, rounded));
        }

        public static string RequestKey(string breedId, string traitDefName,
            AquacultureCommissionImprovement improvement, float stability, int generation, float sizeFactor)
        {
            return (breedId ?? string.Empty) + "|" + (traitDefName ?? string.Empty) + "|" + improvement + "|"
                + NormalizeStability(stability).ToString("0.00", CultureInfo.InvariantCulture) + "|" + generation + "|"
                + NormalizeSizeThreshold(sizeFactor).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static AquacultureCommissionGoal CreateGoal(AquacultureCommissionCapability capability,
            AquacultureCommissionImprovement improvement, float stability, int generation, float size)
        {
            return new AquacultureCommissionGoal
            {
                improvement = improvement,
                breedId = capability.breedId,
                fishDefName = capability.fishDefName,
                traitDefName = capability.definingTraitName,
                minimumStability = NormalizeStability(stability),
                minimumGeneration = Math.Max(0, generation),
                minimumSizeFactor = NormalizeSizeThreshold(size)
            };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
