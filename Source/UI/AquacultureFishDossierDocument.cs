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
        private InsightUiCallout healthCallout;
        private InsightUiBadge healthBadge;
        private InsightUiMeter foodReserveMeter;
        private InsightUiMeter habitatFitMeter;

        public AquacultureFishDossierDocument()
        {
            document = new InsightUiDocument("aquaculture.fish.dossier.v2", BuildRoot())
            {
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(document);
            AquacultureInsightPresentation.Apply(document);
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
                UpdateHealthPresentation();
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
            int expectedMeatYield = FishProcessingYield.ExpectedMeatCount(comp);
            AquacultureDossierHealthState healthState = AquacultureDossierHealthRules.Classify(
                comp.IsAlive, comp.foodReserve, comp.habitatFit, comp.starvationProgress);
            snapshot = new AquacultureFishDossierSnapshot(
                AquacultureUiStableIds.For("fish-dossier", parent?.thingIDNumber.ToString() ?? "none"),
                parent?.def?.LabelCap.ToString() ?? L("AquacultureFishing.WorkspaceUnknownSpecies"),
                !comp.initialized ? L("AquacultureFishing.WorkspaceUnknown") : comp.IsFemale
                    ? L("AquacultureFishing.WorkspaceFemale") : L("AquacultureFishing.WorkspaceMale"),
                LifeStage(comp), comp.BreedName ?? L("AquacultureFishing.WorkspaceUnregistered"),
                comp.breedGeneration.ToString(), Condition(comp), Production(comp), traitRows,
                knowledge.identityKnown ? knowledge.stageId + "  •  " + knowledge.confidence.ToStringPercent() :
                    L("AquacultureFishing.WorkspaceKnowledgeHidden"),
                knowledge.identityKnown ? knowledge.subjectId : string.Empty,
                comp.foodReserve, comp.habitatFit, comp.starvationProgress, expectedMeatYield,
                comp.sterilized, healthState, comp.traitRevision);
            UpdateHealthPresentation();
            if (traitsList != null)
            {
                traitsList.ItemCount = traitRows.Count;
                traitsList.Refresh();
            }
            document.Invalidate();
        }

        private void UpdateHealthPresentation()
        {
            if (healthCallout == null || healthBadge == null || foodReserveMeter == null || habitatFitMeter == null)
                return;
            if (snapshot == null)
            {
                healthCallout.Severity = InsightUiCalloutSeverity.Info;
                healthCallout.Title = L("AquacultureFishing.WorkspaceDossierHealthUnknown");
                healthCallout.Body = L("AquacultureFishing.WorkspaceUnknown");
                healthBadge.Text = L("AquacultureFishing.WorkspaceDossierHealthUnknown");
                foodReserveMeter.Current = 0f;
                foodReserveMeter.Maximum = 1f;
                foodReserveMeter.SetValueText(L("AquacultureFishing.WorkspaceUnknown"));
                habitatFitMeter.Current = 0f;
                habitatFitMeter.Maximum = 1f;
                habitatFitMeter.SetValueText(L("AquacultureFishing.WorkspaceUnknown"));
                return;
            }

            healthCallout.Severity = HealthSeverity(snapshot.HealthState);
            healthCallout.Title = HealthLabel(snapshot.HealthState);
            healthCallout.Body = HealthBody(snapshot);
            healthBadge.Text = HealthLabel(snapshot.HealthState);
            foodReserveMeter.Current = snapshot.FoodReserve < 0f ? 0f : Mathf.Clamp01(snapshot.FoodReserve);
            foodReserveMeter.Maximum = 1f;
            foodReserveMeter.SetValueText(snapshot.FoodReserve < 0f
                ? L("AquacultureFishing.WorkspaceUnknown") : snapshot.FoodReserve.ToStringPercent());
            habitatFitMeter.Current = snapshot.HabitatFit < 0f ? 0f : Mathf.Clamp01(snapshot.HabitatFit);
            habitatFitMeter.Maximum = 1f;
            habitatFitMeter.SetValueText(snapshot.HabitatFit < 0f
                ? L("AquacultureFishing.WorkspaceUnknown") : snapshot.HabitatFit.ToStringPercent());
        }

        private static InsightUiCalloutSeverity HealthSeverity(AquacultureDossierHealthState state)
        {
            switch (state)
            {
                case AquacultureDossierHealthState.Healthy: return InsightUiCalloutSeverity.Success;
                case AquacultureDossierHealthState.Attention: return InsightUiCalloutSeverity.Warning;
                case AquacultureDossierHealthState.Critical:
                case AquacultureDossierHealthState.Dead: return InsightUiCalloutSeverity.Error;
                default: return InsightUiCalloutSeverity.Info;
            }
        }

        private static string HealthLabel(AquacultureDossierHealthState state)
        {
            switch (state)
            {
                case AquacultureDossierHealthState.Healthy: return L("AquacultureFishing.WorkspaceDossierHealthHealthy");
                case AquacultureDossierHealthState.Attention: return L("AquacultureFishing.WorkspaceDossierHealthAttention");
                case AquacultureDossierHealthState.Critical: return L("AquacultureFishing.WorkspaceDossierHealthCritical");
                case AquacultureDossierHealthState.Dead: return L("AquacultureFishing.WorkspaceDossierHealthDead");
                default: return L("AquacultureFishing.WorkspaceDossierHealthUnknown");
            }
        }

        private static string HealthBody(AquacultureFishDossierSnapshot current)
        {
            if (current.HealthState == AquacultureDossierHealthState.Dead)
                return L("AquacultureFishing.WorkspaceDossierHealthDeadBody");
            if (current.HealthState == AquacultureDossierHealthState.Unknown)
                return L("AquacultureFishing.WorkspaceDossierHealthUnknownBody");
            return LF("AquacultureFishing.WorkspaceDossierHealthSummary",
                current.FoodReserve.ToStringPercent(), current.HabitatFit.ToStringPercent(),
                current.StarvationProgress > 0f ? L("AquacultureFishing.WorkspaceStarvation") : string.Empty);
        }

        private InsightUiElement BuildRoot()
        {
            traitsList = InsightUi.VirtualList("dossier.traits.list", 0, 42f,
                index => InsightUi.Label(AquacultureUiStableIds.For("dossier.trait", index.ToString()), traitRows[index]));
            traitsList.Overscan = 2;
            traitsList.CacheLimit = 48;
            traitsList.SetHeight(InsightLength.Fixed(190f));
            healthCallout = InsightUi.Callout("dossier.health.callout", InsightUiCalloutSeverity.Info,
                L("AquacultureFishing.WorkspaceDossierHealthUnknown"), string.Empty);
            healthBadge = InsightUi.Badge("dossier.health.badge", L("AquacultureFishing.WorkspaceDossierHealthUnknown"));
            foodReserveMeter = InsightUi.Meter("dossier.health.food", 0f, 1f)
                .SetLabel(L("AquacultureFishing.WorkspaceFoodReserve"));
            habitatFitMeter = InsightUi.Meter("dossier.health.habitat", 0f, 1f)
                .SetLabel(L("AquacultureFishing.WorkspaceHabitatFit"));
            InsightUiStack content = InsightUi.Column("dossier.content").SetGap(7f).Add(
                InsightUi.SectionHeader("dossier.header", L("AquacultureFishing.WorkspaceFishDossier"),
                    L("AquacultureFishing.WorkspaceFishDossierSubtitle"), null, null, true),
                InsightUi.Row("dossier.species.header").SetGap(7f).SetAlignment(InsightAlignment.Start, InsightAlignment.Center).Add(
                    InsightUi.Label("dossier.species", string.Empty, InsightUiTextStyle.Heading)
                        .SetTextProvider(() => snapshot?.Species ?? string.Empty),
                    InsightUi.Spacer("dossier.species.space").SetFlex(1f), healthBadge),
                healthCallout, foodReserveMeter, habitatFitMeter,
                InsightUi.Expander("dossier.identity", L("AquacultureFishing.WorkspaceDossierIdentity"),
                    InsightUi.Column("dossier.identity.content").SetGap(4f).Add(
                        DynamicRow("dossier.sex", L("AquacultureFishing.WorkspaceSex"), () => snapshot?.Sex),
                        DynamicRow("dossier.stage", L("AquacultureFishing.WorkspaceLifeStage"), () => snapshot?.LifeStage),
                        DynamicRow("dossier.breed", L("AquacultureFishing.WorkspaceBreed"), () => snapshot?.Breed),
                        DynamicRow("dossier.generation", L("AquacultureFishing.WorkspaceGeneration"), () => snapshot?.Generation)), true),
                InsightUi.Expander("dossier.production.group", L("AquacultureFishing.WorkspaceDossierProduction"),
                    InsightUi.Column("dossier.production.content").SetGap(4f).Add(
                        DynamicRow("dossier.yield", L("AquacultureFishing.WorkspaceMeatYield"), () =>
                            snapshot == null || snapshot.ExpectedMeatYield < 0 ? L("AquacultureFishing.WorkspaceUnknown") :
                                snapshot.ExpectedMeatYield.ToString()),
                        DynamicRow("dossier.sterilized", L("AquacultureFishing.WorkspaceSterilized"), () =>
                            snapshot == null ? L("AquacultureFishing.WorkspaceUnknown") :
                                snapshot.Sterilized ? L("AquacultureFishing.WorkspaceYes") : L("AquacultureFishing.WorkspaceNo"))), true),
                InsightUi.SectionHeader("dossier.traits.header", L("AquacultureFishing.WorkspaceTraits"),
                    L("AquacultureFishing.WorkspaceTraitsSubtitle"), null, null, true), traitsList,
                InsightUi.Callout("dossier.knowledge", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceKnowledgeTitle"), string.Empty),
                InsightUi.Label("dossier.knowledge.state", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => string.IsNullOrEmpty(snapshot?.KnowledgeLink)
                        ? L("AquacultureFishing.WorkspaceKnowledgeHidden")
                        : L("AquacultureFishing.WorkspaceKnowledgeKnown")),
                InsightUi.Label("dossier.knowledge.body", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => snapshot?.Knowledge ?? string.Empty),
                InsightUi.Button("dossier.knowledge.open", L("AquacultureFishing.WorkspaceOpenKnowledge"), () =>
                {
                    ThingDef def = capturedComp?.parent?.def;
                    if (def != null) MainTabWindow_AquacultureJournal.OpenSpecies(def);
                }));
            return InsightUi.Scroll("dossier.scroll", AquacultureUiComponents.Panel("dossier", content));
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

        private static string LF(string key, params object[] args)
        {
            return string.Format(L(key), args);
        }
    }
}
