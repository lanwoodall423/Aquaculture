using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AquacultureFishing
{
    /// <summary>Pure cross-framework invariants used by the executable integration harness.</summary>
    public static class FrameworkIntegrationRules
    {
        public sealed class ExactlyOnceLedger
        {
            private readonly HashSet<string> committed = new HashSet<string>(StringComparer.Ordinal);

            public int Count => committed.Count;
            public IReadOnlyCollection<string> Committed => committed;

            public bool Apply(string operationId, Func<bool> mutation)
            {
                if (string.IsNullOrEmpty(operationId) || mutation == null) return false;
                if (committed.Contains(operationId)) return true;
                if (!mutation()) return false;
                committed.Add(operationId);
                return true;
            }

            public ExactlyOnceLedger Restore(IEnumerable<string> persistedIds)
            {
                committed.Clear();
                if (persistedIds != null)
                {
                    foreach (string id in persistedIds.Where(value => !string.IsNullOrEmpty(value)))
                        committed.Add(id);
                }
                return this;
            }
        }

        public sealed class IntegrationState
        {
            public float population;
            public int activeAdvances;
            public int latentAdvances;
            public bool activeMap;
            public bool providerAvailable;
            public int knowledgeGroups;
            public int expertiseAwards;
            public int journalEntries;
            public readonly ExactlyOnceLedger populationOperations = new ExactlyOnceLedger();
            public readonly ExactlyOnceLedger knowledgeOperations = new ExactlyOnceLedger();
            public readonly ExactlyOnceLedger expertiseOperations = new ExactlyOnceLedger();
            public readonly ExactlyOnceLedger journalOperations = new ExactlyOnceLedger();

            public IntegrationState Clone()
            {
                var result = new IntegrationState
                {
                    population = population,
                    activeAdvances = activeAdvances,
                    latentAdvances = latentAdvances,
                    activeMap = activeMap,
                    providerAvailable = providerAvailable,
                    knowledgeGroups = knowledgeGroups,
                    expertiseAwards = expertiseAwards,
                    journalEntries = journalEntries
                };
                result.populationOperations.Restore(populationOperations.Committed);
                result.knowledgeOperations.Restore(knowledgeOperations.Committed);
                result.expertiseOperations.Restore(expertiseOperations.Committed);
                result.journalOperations.Restore(journalOperations.Committed);
                return result;
            }
        }

        public static string CorrelationId(string kind, string pawn, int eventTick, string map,
            string cell, string subject)
        {
            return Join(kind, pawn, eventTick.ToString(CultureInfo.InvariantCulture), map, cell, subject);
        }

        public static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        public static bool ApplyPopulationMutation(IntegrationState state, string operationId,
            float delta, bool providerAvailable, bool failBeforeMutation = false)
        {
            if (state == null || !IsFiniteNonNegative(state.population) || !IsFiniteNonNegative(delta)) return false;
            state.providerAvailable = providerAvailable;
            if (!providerAvailable || failBeforeMutation) return false;
            return state.populationOperations.Apply(operationId, () =>
            {
                float next = state.population + delta;
                if (!IsFiniteNonNegative(next)) return false;
                state.population = next;
                return true;
            });
        }

        public static bool ApplyKnowledgeAfterPopulation(IntegrationState state, string correlationId,
            bool submissionSucceeds)
        {
            if (state == null || string.IsNullOrEmpty(correlationId)) return false;
            return state.knowledgeOperations.Apply(correlationId, () =>
            {
                if (!submissionSucceeds) return false;
                state.knowledgeGroups++;
                return true;
            });
        }

        public static bool ApplyExpertise(IntegrationState state, string correlationId, bool succeeds)
        {
            if (state == null || string.IsNullOrEmpty(correlationId)) return false;
            return state.expertiseOperations.Apply(correlationId, () =>
            {
                if (!succeeds) return false;
                state.expertiseAwards++;
                return true;
            });
        }

        public static bool ApplyJournal(IntegrationState state, string correlationId, bool succeeds)
        {
            if (state == null || string.IsNullOrEmpty(correlationId)) return false;
            return state.journalOperations.Apply(correlationId, () =>
            {
                if (!succeeds) return false;
                state.journalEntries++;
                return true;
            });
        }

        public static bool AdvanceExactlyOnce(IntegrationState state, string operationId, bool active,
            bool latent, bool succeeds = true)
        {
            if (state == null || active == latent || !succeeds) return false;
            if (active) state.activeAdvances++;
            else state.latentAdvances++;
            return state.populationOperations.Apply(operationId, () => true);
        }

        public static void RemoveMapPreservingLatent(IntegrationState state)
        {
            if (state == null) return;
            state.activeMap = false;
        }

        private static string Join(params string[] parts)
        {
            return string.Join("|", (parts ?? new string[0]).Select(part =>
                (part ?? string.Empty).Length.ToString(CultureInfo.InvariantCulture) + ":" + (part ?? string.Empty)));
        }
    }
}
