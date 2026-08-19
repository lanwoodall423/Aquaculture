using System;
using System.Linq;
using InsightCanvas;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    /// <summary>
    /// Insight Canvas content embedded in the native breed-registration Window. The Window retains
    /// ownership of close behavior; registration itself remains AquacultureJournal authority.
    /// </summary>
    public sealed class AquacultureBreedRegistrationDocument
    {
        private readonly Dialog_RegisterFishBreed owner;
        private readonly InsightUiDocument document;
        private InsightUiTextField nameField;
        private InsightUiButton registerButton;

        public AquacultureBreedRegistrationDocument(Dialog_RegisterFishBreed owner)
        {
            this.owner = owner;
            document = new InsightUiDocument("aquaculture.breed.registration.v2", BuildRoot())
            {
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(document);
            AquacultureInsightPresentation.Apply(document);
        }

        public InsightUiHost Host { get; private set; }

        public void Draw(Rect rect)
        {
            owner.RefreshValidationForUi();
            if (registerButton != null) registerButton.Enabled = owner.CanAcceptForUi;
            Host.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            Host.PostClose();
        }

        private InsightUiElement BuildRoot()
        {
            nameField = InsightUi.TextField("breed-registration.name", string.Empty)
                .Bind(() => owner.BreedNameForUi, value =>
                {
                    owner.SetBreedNameForUi(value);
                    document.Invalidate();
                });
            registerButton = InsightUi.Button("breed-registration.register",
                L("AquacultureFishing.BreedRegistrationRegister"), owner.AcceptForUi);
            return InsightUi.Scroll("breed-registration.scroll",
                AquacultureUiComponents.Panel("breed-registration",
                    InsightUi.Column("breed-registration.content").SetGap(8f).Add(
                        InsightUi.SectionHeader("breed-registration.header",
                            L("AquacultureFishing.BreedRegistrationTitle"),
                            L("AquacultureFishing.BreedRegistrationSubtitle"), null, null, true),
                        InsightUi.Label("breed-registration.name-label",
                            L("AquacultureFishing.BreedRegistrationName"), InsightUiTextStyle.Label),
                        nameField,
                        InsightUi.Label("breed-registration.validation", string.Empty,
                            InsightUiTextStyle.Caption).SetTextProvider(() => owner.ValidationMessageForUi),
                        InsightUi.Row("breed-registration.actions").SetGap(8f).Add(
                            InsightUi.Spacer("breed-registration.action-space").SetFlex(1f),
                            InsightUi.Button("breed-registration.cancel",
                                L("AquacultureFishing.BreedRegistrationCancel"), owner.CancelForUi),
                            registerButton))));
        }

        private static string L(string key) => key.Translate().ToString();
    }
}
