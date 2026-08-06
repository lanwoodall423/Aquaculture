using System;
using System.Collections.Generic;

namespace AquacultureFishing
{
    /// <summary>Pure compatibility and identity rules shared by the runtime adapter and executable tests.</summary>
    public static class AquacultureKnowledgeContract
    {
        public const int MinimumApiVersion = 3;
        public const int MinimumCapabilityVersion = 3;
        public const int MaximumDynamicSubjects = 2048;
        public const string MinimumSemanticRelease = "3.0.0-beta.1";

        public static readonly string[] RequiredV3Capabilities =
        {
            "domains", "colony-knowledge", "pawn-knowledge", "expertise", "domain-ui",
            "evidence-transactions", "facets", "confidence", "discovery-stages", "relationships",
            "claims", "typed-measurements", "subject-archetypes", "requirement-stages",
            "observation-recipes", "contextual-knowledge", "milestones", "structural-relations",
            "subject-lifecycle", "accrual-policies", "claim-staleness", "transmission"
        };

        public static bool SupportsV3(int apiVersion, Func<int, string, bool> supports,
            Func<string, int> capabilityVersion)
        {
            if (supports == null || capabilityVersion == null || apiVersion < MinimumApiVersion) return false;
            for (int i = 0; i < RequiredV3Capabilities.Length; i++)
            {
                string capability = RequiredV3Capabilities[i];
                if (!supports(MinimumApiVersion, capability) || capabilityVersion(capability) < MinimumCapabilityVersion)
                    return false;
            }
            return true;
        }

        public static string StableEventId(string kind, string primaryId, int tick, string contextId, string detail = null)
        {
            return Join(kind, primaryId, tick.ToString(), contextId, detail);
        }

        public static string StableMigrationConsumerId(string migrationId, string scope)
        {
            return Join(migrationId, scope);
        }

        public static string StableContextId(string contextType, int mapId, string anchorId, string topologyId,
            string waterKind)
        {
            string id = mapId + ":" + (anchorId ?? string.Empty) + ":" + (topologyId ?? string.Empty);
            return string.IsNullOrEmpty(waterKind) ? id : id + ":" + waterKind;
        }

        public static bool RegistrationComplete(params bool[] phases)
        {
            if (phases == null || phases.Length == 0) return false;
            for (int i = 0; i < phases.Length; i++) if (!phases[i]) return false;
            return true;
        }

        public static bool DynamicSubjectCountAllowed(int count)
        {
            return count >= 0 && count <= MaximumDynamicSubjects;
        }

        public static bool CanFinalizeMigration(IList<bool> imports, bool committed)
        {
            if (!committed || imports == null || imports.Count == 0) return false;
            for (int i = 0; i < imports.Count; i++) if (!imports[i]) return false;
            return true;
        }

        public static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static string Join(params string[] parts)
        {
            List<string> normalized = new List<string>();
            for (int i = 0; i < parts.Length; i++)
                normalized.Add((parts[i] ?? string.Empty).Replace("|", "/"));
            return string.Join("|", normalized.ToArray());
        }
    }
}
