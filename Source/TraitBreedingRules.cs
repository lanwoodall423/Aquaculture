using System;
using System.Collections.Generic;
using System.Linq;

namespace AquacultureFishing
{
    /// <summary>Pure probability and evidence rules shared by fish generation and the executable dev tests.</summary>
    public static class TraitBreedingRules
    {
        public const float DualParentInheritanceMultiplier = 1.5f;
        public const float BaseStability = 0.50f;
        public const float SuccessRateContribution = 0.40f;
        public const float MaximumGenerationContribution = 0.10f;
        public const float GenerationContributionPerGeneration = 0.02f;
        public const float MinimumStability = 0.50f;
        public const float MaximumStability = 0.98f;

        public sealed class Candidate
        {
            public string Id;
            public string CompatibilityGroup;
            public bool PresentInFirstParent;
            public bool PresentInSecondParent;
            public float Weight = 1f;
        }

        public sealed class LegacySettingMigration
        {
            public float WildExceptionalTraitChance;
            public int MaximumInheritedTraits;
        }

        public static LegacySettingMigration MigrateLegacySettings(int version, float legacyMutationRate,
            int legacyMaximumMutations, float currentWildChance, int currentInheritedCap)
        {
            if (version >= 1)
            {
                return new LegacySettingMigration
                {
                    WildExceptionalTraitChance = Clamp01(currentWildChance),
                    MaximumInheritedTraits = Math.Max(0, currentInheritedCap)
                };
            }
            return new LegacySettingMigration
            {
                WildExceptionalTraitChance = Clamp01(legacyMutationRate),
                MaximumInheritedTraits = Math.Max(0, legacyMaximumMutations)
            };
        }

        public static float ParentalInheritanceChance(float baseChance, bool presentInBothParents)
        {
            float clamped = Clamp01(baseChance);
            return Clamp01(presentInBothParents ? clamped * DualParentInheritanceMultiplier : clamped);
        }

        /// <summary>
        /// Rolls every candidate independently, then resolves incompatible successes and applies the cap.
        /// The supplied index selector must be uniform over [minimum, maximumExclusive).
        /// </summary>
        public static List<string> RollInheritedTraits(IEnumerable<Candidate> source, float baseChance, int maximum,
            Func<float> nextValue, Func<int, int, int> nextIndex)
        {
            var candidates = (source ?? Enumerable.Empty<Candidate>())
                .Where(candidate => candidate != null && !string.IsNullOrEmpty(candidate.Id))
                .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToList();
            var successes = new List<Candidate>();
            foreach (Candidate candidate in candidates)
            {
                bool bothParents = candidate.PresentInFirstParent && candidate.PresentInSecondParent;
                float chance = ParentalInheritanceChance(baseChance, bothParents);
                if (ChanceSucceeds((nextValue ?? (() => 1f))(), chance)) successes.Add(candidate);
            }

            var compatible = new List<Candidate>();
            foreach (IGrouping<string, Candidate> group in successes.GroupBy(GroupKey, StringComparer.Ordinal))
            {
                List<Candidate> options = group.ToList();
                int selectedIndex = SafeIndex(nextIndex, options.Count);
                compatible.Add(options[selectedIndex]);
            }

            int cap = Math.Max(0, maximum);
            while (compatible.Count > cap) compatible.RemoveAt(SafeIndex(nextIndex, compatible.Count));
            return compatible.Select(candidate => candidate.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        /// <summary>Rolls one mutation event and fills weighted, compatible slots without duplicating traits.</summary>
        public static List<string> RollNewMutationTraits(IEnumerable<Candidate> source, ISet<string> excludedIds,
            ISet<string> occupiedGroups, float mutationChance, int maximum, Func<float> nextValue,
            Func<int, int, int> nextIndex)
        {
            int cap = Math.Max(0, maximum);
            if (cap == 0 || Clamp01(mutationChance) <= 0f) return new List<string>();
            if (!ChanceSucceeds((nextValue ?? (() => 1f))(), Clamp01(mutationChance))) return new List<string>();

            var candidates = (source ?? Enumerable.Empty<Candidate>())
                .Where(candidate => candidate != null && !string.IsNullOrEmpty(candidate.Id))
                .Where(candidate => excludedIds == null || !excludedIds.Contains(candidate.Id))
                .Where(candidate => occupiedGroups == null || !occupiedGroups.Contains(GroupKey(candidate)))
                .Where(candidate => candidate.Weight > 0f && !float.IsNaN(candidate.Weight) && !float.IsInfinity(candidate.Weight))
                .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            var result = new List<string>();
            while (result.Count < cap && candidates.Count > 0)
            {
                // Individually finite float weights can overflow when summed in float.
                // Double intermediates preserve valid weighted pools and draw boundaries.
                double totalWeight = candidates.Sum(candidate => (double)candidate.Weight);
                if (totalWeight <= 0d || double.IsNaN(totalWeight) || double.IsInfinity(totalWeight)) break;
                double roll = Clamp01((nextValue ?? (() => 0f))()) * totalWeight;
                Candidate selected = candidates[candidates.Count - 1];
                foreach (Candidate candidate in candidates)
                {
                    roll -= candidate.Weight;
                    if (roll <= 0f)
                    {
                        selected = candidate;
                        break;
                    }
                }
                result.Add(selected.Id);
                candidates.RemoveAll(candidate => string.Equals(GroupKey(candidate), GroupKey(selected), StringComparison.Ordinal));
            }
            return result.OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        public static float RegisteredBreedDefiningTraitReliability(float stability, float reliabilityCeiling)
        {
            return Clamp01(stability) * Clamp01(reliabilityCeiling);
        }

        public static float SuccessRate(int matchingBirths, int qualifyingBirths)
        {
            int matching = Math.Max(0, matchingBirths);
            int total = Math.Max(matching, Math.Max(0, qualifyingBirths));
            return total == 0 ? 0f : Clamp01(matching / (float)total);
        }

        public static float GenerationContribution(int highestGeneration)
        {
            return Clamp(CompletedGenerationSteps(highestGeneration) * GenerationContributionPerGeneration,
                0f, MaximumGenerationContribution);
        }

        public static float ResultingStability(int matchingBirths, int qualifyingBirths, int highestGeneration)
        {
            float result = BaseStability + SuccessRate(matchingBirths, qualifyingBirths) * SuccessRateContribution +
                GenerationContribution(highestGeneration);
            return Clamp(result, MinimumStability, MaximumStability);
        }

        public static float LegacyStability(int matchingBirths, int highestGeneration)
        {
            return Clamp(0.70f + Math.Max(0, matchingBirths) * 0.03f +
                CompletedGenerationSteps(highestGeneration) * 0.02f, 0.70f, 0.98f);
        }

        public static int QualifyingBirthsFromLegacy(int legacyBirths, int matchingBirths)
        {
            return Math.Max(0, Math.Max(legacyBirths, matchingBirths));
        }

        private static int CompletedGenerationSteps(int highestGeneration)
        {
            // Guard before subtraction: int.MinValue - 1 would wrap in unchecked arithmetic.
            return highestGeneration <= 1 ? 0 : highestGeneration - 1;
        }

        private static string GroupKey(Candidate candidate)
        {
            return string.IsNullOrEmpty(candidate?.CompatibilityGroup) ? candidate?.Id ?? string.Empty : candidate.CompatibilityGroup;
        }

        private static int SafeIndex(Func<int, int, int> nextIndex, int count)
        {
            if (count <= 1) return 0;
            int selected = nextIndex == null ? 0 : nextIndex(0, count);
            return Math.Max(0, Math.Min(count - 1, selected));
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool ChanceSucceeds(float roll, float chance)
        {
            if (chance <= 0f || float.IsNaN(roll) || float.IsInfinity(roll)) return false;
            return chance >= 1f || roll < chance;
        }
    }

    /// <summary>Engine-independent age-trait and lifespan timing rules, using wide tick arithmetic.</summary>
    public static class FishAgeTimingRules
    {
        private static long ElapsedTicks(int now, int birthTick)
        {
            // RimWorld stores ticks as ints. The difference can exceed int.MaxValue.
            return (long)now - birthTick;
        }

        /// <summary>Oldest-first sort key for manual and automatic pond harvesting.</summary>
        public static long AgeTicksForOrdering(int now, int birthTick)
        {
            // Preserve the full signed age when saved birth ticks cross int boundaries.
            return ElapsedTicks(now, birthTick);
        }

        public static float FractionElapsed(int now, int birthTick, int lifespanTicks)
        {
            if (lifespanTicks <= 0) return 0f;
            double fraction = ElapsedTicks(now, birthTick) / (double)lifespanTicks;
            return (float)Math.Max(0d, Math.Min(1d, fraction));
        }

        public static bool HasExpired(int now, int birthTick, int lifespanTicks)
        {
            return lifespanTicks > 0 && ElapsedTicks(now, birthTick) >= lifespanTicks;
        }

        public static int RemainingTicks(int now, int birthTick, int lifespanTicks)
        {
            if (lifespanTicks <= 0) return 0;
            long elapsed = Math.Max(0L, ElapsedTicks(now, birthTick));
            return (int)Math.Max(0L, (long)lifespanTicks - elapsed);
        }

        /// <summary>Number of ticks until an egg deadline, suitable for the inspect label.</summary>
        public static int RemainingUntilTick(int now, int dueTick)
        {
            // The difference of two valid game ticks may exceed the signed int range.
            // Clamp only after subtracting in long so future eggs never display zero
            // and expired eggs never display a spurious positive countdown.
            long remaining = (long)dueTick - now;
            return (int)Math.Max(0L, Math.Min((long)int.MaxValue, remaining));
        }

        public static int NextCheckTick(int birthTick, int roundedThresholdTicks)
        {
            // Keep the caller's Unity threshold rounding; only guard tick addition.
            long due = (long)birthTick + Math.Max(0, roundedThresholdTicks) + 1L;
            return due >= int.MaxValue ? int.MaxValue : (int)due;
        }

        /// <summary>Schedules egg and breeding checks without 32-bit tick wraparound.</summary>
        public static int ScheduleAfterTicks(int now, int delayTicks)
        {
            // Preserve ordinary tick intervals while preventing malformed negative delays
            // or near-int.MaxValue game clocks from scheduling an event in the past.
            if (delayTicks <= 0) return now;
            long due = (long)now + delayTicks;
            return due >= int.MaxValue ? int.MaxValue : (int)due;
        }
    }

    /// <summary>Engine-independent update and interpolation rules for pond fish schools.</summary>
    public static class FishSchoolTimingRules
    {
        public static bool IsUpdateDue(int now, int lastUpdateTick, int interval)
        {
            // Uninitialized school timestamps may be negative. Subtract before comparing
            // in long so a valid high game tick does not make schools stop updating.
            return (long)now - lastUpdateTick >= interval;
        }

        public static int SimulationStepTicks(int now, int lastUpdateTick, int interval)
        {
            // Preserve the existing first-step interval for uninitialized schools.
            if (lastUpdateTick < 0) return interval;
            long elapsed = (long)now - lastUpdateTick;
            return (int)Math.Max(1L, Math.Min(30L, elapsed));
        }

        public static float InterpolationAlpha(int now, int lastUpdateTick, int updateInterval)
        {
            float elapsed = (long)now - lastUpdateTick;
            return Math.Max(0f, Math.Min(1f, elapsed / Math.Max(1, updateInterval)));
        }
    }

    /// <summary>Pure timing contract for the managed pond ecology simulation.</summary>
    public static class PondEcologyTimingRules
    {
        public static float ElapsedHours(int now, int lastEcologyTick)
        {
            // Uninitialized/legacy ecology records retain the existing one-hour default.
            if (lastEcologyTick <= 0) return 1f;
            // Saved or extreme ticks can make the difference exceed the signed int range.
            // Compute elapsed time before clamping without overflowing to a false full day.
            float hours = ((long)now - lastEcologyTick) / 2500f;
            return Math.Max(0.1f, Math.Min(24f, hours));
        }
    }
}
