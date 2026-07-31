using System.Linq;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public static class AquacultureSharedKnowledgeIntegration
    {
        public static void Register()
        {
            KnowledgeProviderRegistry.Register("fishing", 20, EntryFor);
        }

        private static KnowledgeEntry EntryFor(Pawn pawn)
        {
            PawnFishingProgress progress = FishingProgressionComponent.Current?.ProgressFor(pawn, false);
            if (progress == null) return null;
            KnowledgeRank rank = progress.ExpertiseLevel;
            int known = progress.speciesKnowledge.Count(pair => pair.Value > 0f);
            string best = progress.speciesKnowledge.OrderByDescending(pair => pair.Value).Select(pair =>
                DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key)?.LabelCap.ToString()).FirstOrDefault(label => !label.NullOrEmpty());
            return new KnowledgeEntry
            {
                label = "Fishing",
                rank = rank,
                progress = progress.ExpertiseProgress,
                summary = known + " species" + (best.NullOrEmpty() ? string.Empty : " / " + best),
                tooltip = "Fishing - " + rank + "\n\nExpertise grows only from successful catches; each catch also teaches the paired species." +
                    "\n\nCurrent effects:" +
                    "\n- Wait and reel time: -" + FishingProgressionUtility.TimeReduction(rank).ToStringPercent() +
                    "\n- Escape chance: -" + ((int)rank * 0.05f).ToStringPercent() +
                    "\n- Bite chance: +" + ((int)rank * 0.04f).ToStringPercent() +
                    "\n\nKnown fish species: " + known +
                    "\nFishing XP: " + progress.expertiseExperience.ToString("0"),
                openDetails = () => MainTabWindow_AquacultureJournal.OpenExpertise(pawn)
            };
        }
    }
}
