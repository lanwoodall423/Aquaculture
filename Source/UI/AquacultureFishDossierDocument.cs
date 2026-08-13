using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    /// <summary>Insight Canvas dossier embedded in the native ITab_FishTraits shell.</summary>
    public sealed class AquacultureFishDossierDocument
    {
        private readonly InsightUiDocument document;
        private readonly List<string> traitRows = new List<string>();
        private AquacultureFishDossierSnapshot snapshot;
        private CompFishTraits capturedComp;
        private InsightUiVirtualList traitsList;

        public AquacultureFishDossierDocument()
        {
            document = new InsightUiDocument("aquaculture.fish.dossier.v2", BuildRoot())
            {
                Theme = AquacultureInsightTheme.Create(),
                Density = InsightUiDensity.Compact,
                HighContrast = false,
                ReducedMotion = false,
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(document);
        }

        public InsightUiHost Host { get; private set; }
        public AquacultureFishDossierSnapshot Snapshot => snapshot;

        public void Draw(Rect rect, CompFishTraits comp)
        {
            Capture(comp);
            Host.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            Host.PostClose();
        }

        private void Capture(CompFishTraits comp)
        {
            if (comp == null)
            {
                if (capturedComp != null) PostClose();
                capturedComp = null;
                snapshot = null;
                traitRows.Clear();
                if (traitsList != null) traitsList.ItemCount = 0;
                return;
            }
            if (ReferenceEquals(comp, capturedComp) && snapshot != null &&
                snapshot.Revision == comp.traitRevision) return;

            if (capturedComp != null && !ReferenceEquals(comp, capturedComp)) PostClose();
            capturedComp = comp;
            Thing parent = comp.parent;
            AquacultureKnowledgeView knowledge = parent?.def == null
                ? new AquacultureKnowledgeView(string.Empty, string.Empty, 0f, 0f, false, new string[0])
                : AquacultureKnowledgeAdapter.SpeciesView(parent.def, null, true);
            List<string> traits = (comp.ActiveTraits ?? new List<FishTraitDef>())
                .Where(trait => trait != null)
                .OrderBy(trait => trait.label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(trait => trait.defName, StringComparer.Ordinal)
                .Select(trait => FormatTrait(comp, trait)).ToList();
            traitRows.Clear();
            traitRows.AddRange(traits);
            snapshot = new AquacultureFishDossierSnapshot(
                AquacultureUiStableIds.For("fish-dossier", parent?.thingIDNumber.ToString() ?? "none"),
                parent?.def?.LabelCap.ToString() ?? L("AquacultureFishing.WorkspaceUnknownSpecies"),
                !comp.initialized ? L("AquacultureFishing.WorkspaceUnknown") : comp.IsFemale
                    ? L("AquacultureFishing.WorkspaceFemale") : L("AquacultureFishing.WorkspaceMale"),
                LifeStage(comp), comp.BreedName ?? L("AquacultureFishing.WorkspaceUnregistered"),
                comp.breedGeneration.ToString(), Condition(comp), Production(comp), traitRows,
                knowledge.identityKnown ? knowledge.stageId + "  •  " + knowledge.confidence.ToStringPercent() :
                    L("AquacultureFishing.WorkspaceKnowledgeHidden"),
                knowledge.identityKnown ? knowledge.subjectId : string.Empty, comp.traitRevision);
            if (traitsList != null)
            {
                traitsList.ItemCount = traitRows.Count;
                traitsList.Refresh();
            }
            document.Invalidate();
        }

        private InsightUiElement BuildRoot()
        {
            traitsList = InsightUi.VirtualList("dossier.traits.list", 0, 42f,
                index => InsightUi.Label(AquacultureUiStableIds.For("dossier.trait", index.ToString()), traitRows[index]));
            traitsList.Overscan = 2;
            traitsList.CacheLimit = 48;
            traitsList.SetHeight(InsightLength.Fixed(190f));
            InsightUiStack content = InsightUi.Column("dossier.content").SetGap(7f).Add(
                InsightUi.SectionHeader("dossier.header", L("AquacultureFishing.WorkspaceFishDossier"),
                    L("AquacultureFishing.WorkspaceFishDossierSubtitle"), null, null, true),
                InsightUi.Label("dossier.species", string.Empty).SetTextProvider(() => snapshot?.Species ?? string.Empty),
                DynamicRow("dossier.sex", L("AquacultureFishing.WorkspaceSex"), () => snapshot?.Sex),
                DynamicRow("dossier.stage", L("AquacultureFishing.WorkspaceLifeStage"), () => snapshot?.LifeStage),
                DynamicRow("dossier.breed", L("AquacultureFishing.WorkspaceBreed"), () => snapshot?.Breed),
                DynamicRow("dossier.generation", L("AquacultureFishing.WorkspaceGeneration"), () => snapshot?.Generation),
                DynamicRow("dossier.condition", L("AquacultureFishing.WorkspaceCondition"), () => snapshot?.Condition),
                DynamicRow("dossier.production", L("AquacultureFishing.WorkspaceProduction"), () => snapshot?.Production),
                InsightUi.SectionHeader("dossier.traits.header", L("AquacultureFishing.WorkspaceTraits"),
                    L("AquacultureFishing.WorkspaceTraitsSubtitle"), null, null, true), traitsList,
                InsightUi.Callout("dossier.knowledge", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceKnowledgeTitle"), string.Empty),
                InsightUi.Label("dossier.knowledge.body", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => snapshot?.Knowledge ?? string.Empty),
                InsightUi.Button("dossier.knowledge.open", L("AquacultureFishing.WorkspaceOpenKnowledge"), () =>
                {
                    ThingDef def = capturedComp?.parent?.def;
                    if (def != null) MainTabWindow_AquacultureJournal.OpenSpecies(def);
                }));
            return InsightUi.Scroll("dossier.scroll", content);
        }

        private static InsightUiElement DynamicRow(string id, string label, Func<string> value)
        {
            return InsightUi.Row(id).SetGap(6f).Add(InsightUi.Label(id + ".label", label),
                InsightUi.Spacer(id + ".space").SetFlex(1f), InsightUi.Label(id + ".value", string.Empty)
                    .SetTextProvider(() => value() ?? string.Empty));
        }

        private static string LifeStage(CompFishTraits comp)
        {
            if (comp.traitDefNames?.Contains(FishTraitUtility.Fry) == true) return L("AquacultureFishing.WorkspaceLifeStageFry");
            if (comp.traitDefNames?.Contains(FishTraitUtility.Juvenile) == true) return L("AquacultureFishing.WorkspaceLifeStageJuvenile");
            if (comp.traitDefNames?.Contains(FishTraitUtility.Elder) == true) return L("AquacultureFishing.WorkspaceLifeStageElder");
            return comp.IsAdult ? L("AquacultureFishing.WorkspaceLifeStageAdult") : L("AquacultureFishing.WorkspaceUnknown");
        }

        private static string Condition(CompFishTraits comp)
        {
            if (!comp.IsAlive) return L("AquacultureFishing.WorkspaceDead");
            return L("AquacultureFishing.WorkspaceFoodReserve") + " " + comp.foodReserve.ToStringPercent() +
                "  •  " + L("AquacultureFishing.WorkspaceHabitatFit") + " " + comp.habitatFit.ToStringPercent() +
                (comp.starvationProgress > 0f ? "  •  " + L("AquacultureFishing.WorkspaceStarvation") : string.Empty);
        }

        private static string Production(CompFishTraits comp)
        {
            return L("AquacultureFishing.WorkspaceMeatYield") + " " +
                FishProcessingYield.ExpectedMeatCount(comp) + "  •  " +
                L("AquacultureFishing.WorkspaceSterilized") + ": " +
                (comp.sterilized ? L("AquacultureFishing.WorkspaceYes") : L("AquacultureFishing.WorkspaceNo"));
        }

        private static string FormatTrait(CompFishTraits comp, FishTraitDef trait)
        {
            float value = comp.TraitValue(trait.defName);
            string label = trait.IsNumeric
                ? trait.label.Replace("(+%)", "(+" + Mathf.RoundToInt(value) + "%)")
                : trait.LabelCap.ToString();
            return label + ": " + FishTraitUtility.EffectLine(trait, value);
        }

        private static string L(string key) => key.Translate().ToString();
    }
}
