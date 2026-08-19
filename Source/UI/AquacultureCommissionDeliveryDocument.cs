using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using Verse;

namespace AquacultureFishing
{
    /// <summary>Copied, renderer-neutral row data for the commission delivery surface.</summary>
    public sealed class AquacultureCommissionDeliverySnapshot
    {
        public AquacultureCommissionDeliverySnapshot(string stableId, int thingId, string label, string details)
        {
            StableId = stableId;
            ThingId = thingId;
            Label = label ?? string.Empty;
            Details = details ?? string.Empty;
        }

        public string StableId { get; private set; }
        public int ThingId { get; private set; }
        public string Label { get; private set; }
        public string Details { get; private set; }
    }

    /// <summary>
    /// Insight Canvas content embedded in the native commission Window. The native owner keeps
    /// pause/close and delivery authority; this document owns only copied presentation state.
    /// </summary>
    public sealed class AquacultureCommissionDeliveryDocument
    {
        private readonly Dialog_DeliverAquacultureCommission owner;
        private readonly InsightUiDocument document;
        private readonly List<AquacultureCommissionDeliverySnapshot> rows =
            new List<AquacultureCommissionDeliverySnapshot>();
        private InsightUiVirtualList specimenList;

        public AquacultureCommissionDeliveryDocument(Dialog_DeliverAquacultureCommission owner)
        {
            this.owner = owner;
            document = new InsightUiDocument("aquaculture.commission.delivery.v2", BuildRoot())
            {
                TrackDuplicateIds = true,
                DrawBackground = true
            };
            Host = new InsightUiHost(document);
            AquacultureInsightPresentation.Apply(document);
        }

        public InsightUiHost Host { get; private set; }

        public void Draw(UnityEngine.Rect rect)
        {
            CaptureSnapshot();
            Host.Draw(rect, UnityEngine.Time.deltaTime);
        }

        public void PostClose()
        {
            Host.PostClose();
        }

        private void CaptureSnapshot()
        {
            IReadOnlyList<AquacultureCommissionDeliverySnapshot> next = owner.CaptureSpecimensForUi();
            bool changed = next.Count != rows.Count;
            if (!changed)
            {
                for (int i = 0; i < next.Count; i++)
                {
                    if (next[i].ThingId != rows[i].ThingId || next[i].Details != rows[i].Details ||
                        next[i].Label != rows[i].Label)
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return;
            rows.Clear();
            rows.AddRange(next);
            if (specimenList != null)
            {
                specimenList.ItemCount = rows.Count;
                specimenList.Refresh();
            }
            document.Invalidate();
        }

        private InsightUiElement BuildRoot()
        {
            specimenList = InsightUi.VirtualList("commission.delivery.specimens", 0, 58f,
                index => BuildSpecimenRow(rows[index]));
            specimenList.Overscan = 2;
            specimenList.CacheLimit = 48;
            specimenList.SetFlex(1f);

            return InsightUi.Scroll("commission.delivery.scroll",
                AquacultureUiComponents.Panel("commission.delivery",
                    InsightUi.Column("commission.delivery.content").SetGap(8f).Add(
                        InsightUi.SectionHeader("commission.delivery.header",
                            L("AquacultureFishing.CommissionDeliverTitle"),
                            L("AquacultureFishing.CommissionDeliverInstruction"), null, null, true),
                        InsightUi.Label("commission.delivery.requirements", string.Empty)
                            .SetTextProvider(() => owner.RequirementsForUi),
                        InsightUi.Label("commission.delivery.preview", string.Empty, InsightUiTextStyle.Caption)
                            .SetTextProvider(() => owner.PreviewForUi),
                        InsightUi.Label("commission.delivery.count", string.Empty, InsightUiTextStyle.Caption)
                            .SetTextProvider(() => owner.EligibleCountForUi),
                        InsightUi.Callout("commission.delivery.authority", InsightUiCalloutSeverity.Info,
                            L("AquacultureFishing.WorkspaceCommissionRules"),
                            L("AquacultureFishing.WorkspaceCommissionDisclosure")),
                        specimenList,
                        InsightUi.Label("commission.delivery.empty", string.Empty, InsightUiTextStyle.Caption)
                            .SetTextProvider(() => rows.Count == 0
                                ? L("AquacultureFishing.CommissionNoEligible") : string.Empty))));
        }

        private InsightUiElement BuildSpecimenRow(AquacultureCommissionDeliverySnapshot row)
        {
            string id = row.StableId;
            return InsightUi.Row(id).SetGap(8f).Add(
                InsightUi.Column(id + ".details").SetGap(2f).SetFlex(1f).Add(
                    InsightUi.Label(id + ".label", row.Label),
                    InsightUi.Label(id + ".meta", row.Details, InsightUiTextStyle.Caption)),
                InsightUi.Button(id + ".deliver", L("AquacultureFishing.CommissionDeliverButton"),
                    () => Deliver(row.ThingId)));
        }

        private void Deliver(int thingId)
        {
            if (owner.TryDeliverForUi(thingId, out string reason))
            {
                document.Toasts.Show(L("AquacultureFishing.CommissionDeliverySuccess"),
                    InsightToastSeverity.Success);
                owner.CloseForUi();
                return;
            }
            document.Toasts.Show(reason ?? L("AquacultureFishing.CommissionSpecimenInvalid"),
                InsightToastSeverity.Error, 4f);
            document.Invalidate();
        }

        private static string L(string key) => key.Translate().ToString();
    }
}
