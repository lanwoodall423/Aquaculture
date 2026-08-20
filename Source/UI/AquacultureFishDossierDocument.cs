using System;
using System.Collections.Generic;
using System.Globalization;
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
        private AquacultureFishDossierDisclosure disclosure = HiddenDisclosure();
        private CompFishTraits capturedComp;
        private string capturedSignature;
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
                capturedSignature = null;
                snapshot = null;
                traitRows.Clear();
                bool disclosureChanged = !disclosure.Equals(HiddenDisclosure());
                disclosure = HiddenDisclosure();
                if (disclosureChanged) document.Root = BuildRoot();
                UpdateHealthPresentation();
                return;
            }

            Thing parent = comp.parent;
            AquacultureKnowledgeView knowledge = parent?.def == null
                ? new AquacultureKnowledgeView(string.Empty, string.Empty, 0f, 0f, false, new string[0])
                : AquacultureKnowledgeAdapter.SpeciesView(parent.def, null, true);
            AquacultureUiDisclosureSnapshot capabilities = AquacultureUiDisclosure.CaptureCapabilities();
            AquacultureFishDossierDisclosure nextDisclosure = AquacultureUiDisclosure.ForFish(capabilities,
                comp.initialized, knowledge.identityKnown, HasFacet(knowledge, "traits"),
                HasFacet(knowledge, "size"), HasFacet(knowledge, "health"),
                HasFacet(knowledge, "breeding"), !comp.BreedName.NullOrEmpty(), comp.sterilized);
            string nextSignature = CaptureSignature(comp, knowledge, nextDisclosure);
            if (ReferenceEquals(comp, capturedComp) && snapshot != null && nextSignature == capturedSignature)
                return;

            if (capturedComp != null && !ReferenceEquals(comp, capturedComp)) PostClose();
            capturedComp = comp;
            capturedSignature = nextSignature;
            disclosure = nextDisclosure;

            traitRows.Clear();
            if (disclosure.TraitsVisible)
            {
                traitRows.AddRange((comp.ActiveTraits ?? new List<FishTraitDef>())
                    .Where(trait => trait != null && AquacultureKnowledgeAdapter.IsPlayerReadableTrait(trait.defName))
                    .OrderBy(trait => trait.label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(trait => trait.defName, StringComparer.Ordinal)
                    .Select(FormatTrait));
            }

            AquacultureDossierHealthState healthState = disclosure.HealthClassificationVisible
                ? AquacultureDossierHealthRules.Classify(comp.IsAlive, comp.foodReserve,
                    comp.habitatFit, comp.starvationProgress)
                : AquacultureDossierHealthState.Unknown;
            int expectedMeatYield = disclosure.ExpectedMeatYieldVisible
                ? FishProcessingYield.ExpectedMeatCount(comp) : -1;
            snapshot = new AquacultureFishDossierSnapshot(
                AquacultureUiStableIds.For("fish-dossier", parent?.thingIDNumber.ToString() ?? "none"),
                disclosure.SpeciesIdentityVisible
                    ? parent?.def?.LabelCap.ToString() ?? L("AquacultureFishing.WorkspaceUnknownSpecies")
                    : L("AquacultureFishing.WorkspaceUnknownSpecies"),
                disclosure.SexVisible ? Sex(comp) : L("AquacultureFishing.WorkspaceUnknown"),
                disclosure.LifeStageVisible ? LifeStage(comp) : L("AquacultureFishing.WorkspaceUnknown"),
                disclosure.BreedVisible ? comp.BreedName : string.Empty,
                disclosure.GenerationVisible ? comp.breedGeneration.ToString() : string.Empty,
                Condition(comp, disclosure, healthState), Production(comp, disclosure, expectedMeatYield), traitRows,
                disclosure.KnowledgeVisible
                    ? knowledge.stageId + "  •  " + knowledge.confidence.ToStringPercent() : string.Empty,
                disclosure.KnowledgeVisible ? knowledge.subjectId : string.Empty,
                disclosure.FoodReserveVisible ? comp.foodReserve : -1f,
                disclosure.HabitatFitVisible ? comp.habitatFit : -1f,
                disclosure.StarvationStateVisible ? comp.starvationProgress : -1f,
                expectedMeatYield, disclosure.SterilizationVisible && comp.sterilized,
                healthState, comp.traitRevision);

            document.Root = BuildRoot();
            UpdateHealthPresentation();
            document.Invalidate();
        }

        private static string CaptureSignature(CompFishTraits comp, AquacultureKnowledgeView knowledge,
            AquacultureFishDossierDisclosure current)
        {
            string facets = knowledge.knownFacets == null ? string.Empty :
                string.Join(",", knowledge.knownFacets.OrderBy(item => item, StringComparer.Ordinal));
            return string.Join("|", new[]
            {
                comp.traitRevision.ToString(CultureInfo.InvariantCulture),
                comp.initialized.ToString(), comp.IsAlive.ToString(), comp.IsFemale.ToString(),
                comp.IsAdult.ToString(), comp.foodReserve.ToString("R", CultureInfo.InvariantCulture),
                comp.habitatFit.ToString("R", CultureInfo.InvariantCulture),
                comp.starvationProgress.ToString("R", CultureInfo.InvariantCulture),
                comp.BreedName ?? string.Empty, comp.breedGeneration.ToString(CultureInfo.InvariantCulture),
                comp.sterilized.ToString(), knowledge.subjectId ?? string.Empty, knowledge.stageId ?? string.Empty,
                knowledge.identityKnown.ToString(), knowledge.confidence.ToString("R", CultureInfo.InvariantCulture),
                facets, current.GetHashCode().ToString(CultureInfo.InvariantCulture)
            });
        }

        private void UpdateHealthPresentation()
        {
            if (healthCallout == null || healthBadge == null) return;
            if (snapshot == null)
            {
                healthCallout.Severity = InsightUiCalloutSeverity.Info;
                healthCallout.Title = L("AquacultureFishing.WorkspaceDossierHealthUnknown");
                healthCallout.Body = L("AquacultureFishing.WorkspaceDossierHealthUnknownBody");
                healthBadge.Text = L("AquacultureFishing.WorkspaceDossierHealthUnknown");
                return;
            }

            healthCallout.Severity = HealthSeverity(snapshot.HealthState);
            healthCallout.Title = HealthLabel(snapshot.HealthState);
            healthCallout.Body = HealthBody(snapshot);
            healthBadge.Text = HealthLabel(snapshot.HealthState);
            if (foodReserveMeter != null)
            {
                foodReserveMeter.Current = snapshot.FoodReserve < 0f ? 0f : Mathf.Clamp01(snapshot.FoodReserve);
                foodReserveMeter.Maximum = 1f;
                foodReserveMeter.SetValueText(snapshot.FoodReserve < 0f
                    ? L("AquacultureFishing.WorkspaceUnknown") : snapshot.FoodReserve.ToStringPercent());
            }
            if (habitatFitMeter != null)
            {
                habitatFitMeter.Current = snapshot.HabitatFit < 0f ? 0f : Mathf.Clamp01(snapshot.HabitatFit);
                habitatFitMeter.Maximum = 1f;
                habitatFitMeter.SetValueText(snapshot.HabitatFit < 0f
                    ? L("AquacultureFishing.WorkspaceUnknown") : snapshot.HabitatFit.ToStringPercent());
            }
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
            if (current.FoodReserve < 0f || current.HabitatFit < 0f)
                return LF("AquacultureFishing.WorkspaceDossierHealthObservedBody", HealthLabel(current.HealthState));
            return LF("AquacultureFishing.WorkspaceDossierHealthSummary",
                current.FoodReserve.ToStringPercent(), current.HabitatFit.ToStringPercent(),
                current.StarvationProgress > 0f ? L("AquacultureFishing.WorkspaceStarvation") : string.Empty);
        }

        private InsightUiElement BuildRoot()
        {
            healthCallout = InsightUi.Callout("dossier.health.callout", InsightUiCalloutSeverity.Info,
                L("AquacultureFishing.WorkspaceDossierHealthUnknown"), string.Empty);
            healthBadge = InsightUi.Badge("dossier.health.badge", L("AquacultureFishing.WorkspaceDossierHealthUnknown"));
            List<InsightUiElement> content = new List<InsightUiElement>
            {
                InsightUi.SectionHeader("dossier.header", L("AquacultureFishing.WorkspaceFishDossier"),
                    L("AquacultureFishing.WorkspaceFishDossierSubtitle"), null, null, true),
                InsightUi.Row("dossier.species.header").SetGap(AquacultureUiSpacing.Row).SetAlignment(InsightAlignment.Start, InsightAlignment.Center).Add(
                    InsightUi.Label("dossier.species", string.Empty, InsightUiTextStyle.Heading)
                        .SetTextProvider(() => snapshot?.Species ?? string.Empty),
                    InsightUi.Spacer("dossier.species.space").SetFlex(1f), healthBadge),
                healthCallout
            };
            if (disclosure.FoodReserveVisible)
            {
                foodReserveMeter = InsightUi.Meter("dossier.health.food", 0f, 1f)
                    .SetLabel(L("AquacultureFishing.WorkspaceFoodReserve"));
                content.Add(foodReserveMeter);
            }
            else foodReserveMeter = null;
            if (disclosure.HabitatFitVisible)
            {
                habitatFitMeter = InsightUi.Meter("dossier.health.habitat", 0f, 1f)
                    .SetLabel(L("AquacultureFishing.WorkspaceHabitatFit"));
                content.Add(habitatFitMeter);
            }
            else habitatFitMeter = null;

            List<InsightUiElement> identityRows = new List<InsightUiElement>();
            if (disclosure.SexVisible)
                identityRows.Add(DynamicRow("dossier.sex", L("AquacultureFishing.WorkspaceSex"), () => snapshot?.Sex));
            if (disclosure.LifeStageVisible)
                identityRows.Add(DynamicRow("dossier.stage", L("AquacultureFishing.WorkspaceLifeStage"), () => snapshot?.LifeStage));
            if (disclosure.BreedVisible)
                identityRows.Add(DynamicRow("dossier.breed", L("AquacultureFishing.WorkspaceBreed"), () => snapshot?.Breed));
            if (disclosure.GenerationVisible)
                identityRows.Add(DynamicRow("dossier.generation", L("AquacultureFishing.WorkspaceGeneration"), () => snapshot?.Generation));
            if (identityRows.Count > 0)
                content.Add(InsightUi.Expander("dossier.identity", L("AquacultureFishing.WorkspaceDossierIdentity"),
                    InsightUi.Column("dossier.identity.content").SetGap(AquacultureUiSpacing.Micro).Add(identityRows.ToArray()), true));

            if (disclosure.ProductionGroupVisible)
            {
                List<InsightUiElement> productionRows = new List<InsightUiElement>();
                if (disclosure.ExpectedMeatYieldVisible)
                    productionRows.Add(DynamicRow("dossier.yield", L("AquacultureFishing.WorkspaceMeatYield"), () =>
                        snapshot == null || snapshot.ExpectedMeatYield < 0 ? L("AquacultureFishing.WorkspaceUnknown") :
                            snapshot.ExpectedMeatYield.ToString()));
                if (disclosure.SterilizationVisible)
                    productionRows.Add(DynamicRow("dossier.sterilized", L("AquacultureFishing.WorkspaceSterilized"), () =>
                        snapshot == null ? L("AquacultureFishing.WorkspaceUnknown") :
                            snapshot.Sterilized ? L("AquacultureFishing.WorkspaceYes") : L("AquacultureFishing.WorkspaceNo")));
                content.Add(InsightUi.Expander("dossier.production.group", L("AquacultureFishing.WorkspaceDossierProduction"),
                    InsightUi.Column("dossier.production.content").SetGap(AquacultureUiSpacing.Micro).Add(productionRows.ToArray()), true));
            }

            if (disclosure.TraitsVisible)
            {
                content.Add(InsightUi.SectionHeader("dossier.traits.header", L("AquacultureFishing.WorkspaceTraits"),
                    L("AquacultureFishing.WorkspaceTraitsSubtitle"), null, null, true));
                InsightUiStack traits = InsightUi.Column("dossier.traits.list").SetGap(AquacultureUiSpacing.Micro);
                if (traitRows.Count == 0)
                {
                    traits.Add(InsightUi.Label("dossier.traits.empty", L("AquacultureFishing.PondNoTraits"),
                        InsightUiTextStyle.Caption));
                }
                else
                {
                    for (int i = 0; i < traitRows.Count; i++)
                    {
                        traits.Add(InsightUi.Label(AquacultureUiStableIds.For("dossier.trait", i.ToString()),
                            traitRows[i]));
                    }
                }
                content.Add(traits);
            }

            if (disclosure.KnowledgeVisible)
            {
                content.Add(InsightUi.Callout("dossier.knowledge", InsightUiCalloutSeverity.Info,
                    L("AquacultureFishing.WorkspaceKnowledgeTitle"), string.Empty));
                content.Add(InsightUi.Label("dossier.knowledge.state", L("AquacultureFishing.WorkspaceKnowledgeKnown"),
                    InsightUiTextStyle.Caption));
                content.Add(InsightUi.Label("dossier.knowledge.body", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => snapshot?.Knowledge ?? string.Empty));
                content.Add(InsightUi.Button("dossier.knowledge.open", L("AquacultureFishing.WorkspaceOpenKnowledge"), () =>
                    {
                        ThingDef def = capturedComp?.parent?.def;
                        if (def != null) MainTabWindow_AquacultureJournal.OpenSpecies(def);
                    }));
            }
            return InsightUi.Scroll("dossier.scroll", AquacultureUiComponents.Panel("dossier",
                InsightUi.Column("dossier.content").SetGap(AquacultureUiSpacing.Row).Add(content.ToArray())));
        }

        private static InsightUiElement DynamicRow(string id, string label, Func<string> value)
        {
            return InsightUi.Row(id).SetGap(AquacultureUiSpacing.Row).Add(InsightUi.Label(id + ".label", label),
                InsightUi.Spacer(id + ".space").SetFlex(1f), InsightUi.Label(id + ".value", string.Empty)
                    .SetTextProvider(() => value() ?? string.Empty));
        }

        private static AquacultureFishDossierDisclosure HiddenDisclosure()
        {
            return new AquacultureFishDossierDisclosure(false, false, false, false, false, false, false, false,
                false, false, false);
        }

        private static bool HasFacet(AquacultureKnowledgeView knowledge, string facet)
        {
            return knowledge.knownFacets != null && knowledge.knownFacets.Contains(facet, StringComparer.Ordinal);
        }

        private static string Sex(CompFishTraits comp)
        {
            return comp.IsFemale ? L("AquacultureFishing.WorkspaceFemale") : L("AquacultureFishing.WorkspaceMale");
        }

        private static string LifeStage(CompFishTraits comp)
        {
            if (comp.traitDefNames?.Contains(FishTraitUtility.Fry) == true) return L("AquacultureFishing.WorkspaceLifeStageFry");
            if (comp.traitDefNames?.Contains(FishTraitUtility.Juvenile) == true) return L("AquacultureFishing.WorkspaceLifeStageJuvenile");
            if (comp.traitDefNames?.Contains(FishTraitUtility.Elder) == true) return L("AquacultureFishing.WorkspaceLifeStageElder");
            return comp.IsAdult ? L("AquacultureFishing.WorkspaceLifeStageAdult") : L("AquacultureFishing.WorkspaceUnknown");
        }

        private static string Condition(CompFishTraits comp, AquacultureFishDossierDisclosure current,
            AquacultureDossierHealthState healthState)
        {
            if (!comp.IsAlive) return L("AquacultureFishing.WorkspaceDead");
            if (!current.ExactHealthMetricsVisible) return HealthLabel(healthState);
            return L("AquacultureFishing.WorkspaceFoodReserve") + " " + comp.foodReserve.ToStringPercent() +
                "  •  " + L("AquacultureFishing.WorkspaceHabitatFit") + " " + comp.habitatFit.ToStringPercent() +
                (comp.starvationProgress > 0f ? "  •  " + L("AquacultureFishing.WorkspaceStarvation") : string.Empty);
        }

        private static string Production(CompFishTraits comp, AquacultureFishDossierDisclosure current,
            int expectedMeatYield)
        {
            List<string> values = new List<string>();
            if (current.ExpectedMeatYieldVisible)
                values.Add(L("AquacultureFishing.WorkspaceMeatYield") + " " + expectedMeatYield);
            if (current.SterilizationVisible)
                values.Add(L("AquacultureFishing.WorkspaceSterilized") + ": " +
                    (comp.sterilized ? L("AquacultureFishing.WorkspaceYes") : L("AquacultureFishing.WorkspaceNo")));
            return string.Join("  •  ", values.ToArray());
        }

        private static string FormatTrait(FishTraitDef trait)
        {
            // Knowledge records visible trait identity, not the exact internal value. Keep the
            // dossier to an observable label instead of leaking CompFishTraits numerics.
            return trait.LabelCap.ToString();
        }

        private static string L(string key) => key.Translate().ToString();

        private static string LF(string key, params object[] args)
        {
            return string.Format(L(key), args);
        }
    }
}
