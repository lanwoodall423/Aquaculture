using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class ITab_FishTraits : ITab
    {
        private Vector2 scrollPosition;
        private CompFishTraits cachedComp;
        private int cachedRevision = -1;
        private float cachedWidth = -1f;
        private readonly List<FishTraitDef> cachedTraits = new List<FishTraitDef>();
        private readonly List<float> cachedHeights = new List<float>();
        private float cachedTotalHeight;

        public ITab_FishTraits()
        {
            size = new Vector2(520f, 484f);
            labelKey = "AF_TraitsTab";
        }

        private void RefreshSnapshot(float width)
        {
            CompFishTraits comp = SelThing?.TryGetComp<CompFishTraits>();
            if (comp == cachedComp && comp != null && cachedRevision == comp.traitRevision && Mathf.Approximately(cachedWidth, width)) return;
            cachedComp = comp;
            cachedRevision = comp?.traitRevision ?? -1;
            cachedWidth = width;
            cachedTraits.Clear();
            cachedHeights.Clear();
            cachedTotalHeight = 0f;
            if (comp == null) return;
            cachedTraits.AddRange(comp.ActiveTraits);
            for (int i = 0; i < cachedTraits.Count; i++)
            {
                float height = RowHeight(cachedTraits[i], comp.TraitValue(cachedTraits[i].defName), width);
                cachedHeights.Add(height);
                cachedTotalHeight += height;
            }
        }

        public override bool IsVisible => SelThing?.TryGetComp<CompFishTraits>() != null;

        protected override void FillTab()
        {
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
            CompFishTraits selected = SelThing?.TryGetComp<CompFishTraits>();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Fish Traits" + (selected?.sterilized == true ? " - Sterilized" : ""));
            Text.Font = GameFont.Small;

            if (selected != null)
            {
                AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(selected.parent.def);
                float minimum = profile.minimumTemperature - selected.TemperatureRangeOffset;
                float maximum = profile.maximumTemperature + selected.TemperatureRangeOffset;
                string temperature = "Livable Temperature: " + minimum.ToString("0.#") + " to " + maximum.ToString("0.#") + " C";
                if (selected.IsInPond) temperature += "   Current: " + GenTemperature.GetTemperatureForCell(selected.parent.Position, selected.parent.Map).ToString("0.#") + " C";
                if (selected.temperatureStress > 0f) temperature += "   Stress: " + selected.temperatureStress.ToStringPercent();
                Widgets.Label(new Rect(rect.x, rect.y + 38f, rect.width, 26f), temperature);
                if (selected.IsInPond)
                {
                    PondWaterKind pondWater = selected.parent.Map.GetComponent<FishPondMapComponent>()?.WaterKindAt(selected.parent.Position) ?? PondWaterKind.Freshwater;
                    bool compatible = AquaticSpeciesProfile.WaterCompatible(selected.WaterKind, pondWater);
                    string water = "Pond Water: " + pondWater + "   Required: " + selected.WaterKind;
                    if (!compatible) water += "   Osmotic Stress: " + selected.waterStress.ToStringPercent();
                    Widgets.Label(new Rect(rect.x, rect.y + 62f, rect.width, 26f), water);
                }
                Widgets.Label(new Rect(rect.x, rect.y + 86f, rect.width, 60f),
                    "Species Profile: " + profile.diet + " | " + profile.waterKind +
                    "   Lifespan: " + profile.lifespanFactor.ToString("0.00") + "x   Meat: " +
                    profile.meatYieldFactor.ToStringPercent() + "   Expected processing: " +
                    FishProcessingYield.ExpectedMeatCount(selected) + " fish meat   Breeding: " +
                    profile.breedingIntervalFactor.ToString("0.00") + "x");
                Widgets.Label(new Rect(rect.x, rect.y + 150f, rect.width, 26f),
                    selected.Breed == null
                        ? "Breed: Unregistered"
                        : "Breed: " + selected.Breed.name + "   Generation " + selected.breedGeneration +
                           "   " + "AquacultureFishing.ResultingStability".Translate(selected.Breed.Stability.ToStringPercent()).ToString());
                if (selected.IsInPond)
                {
                    Color old = GUI.color;
                    GUI.color = selected.habitatStress > 0.05f
                        ? new Color(1f, 0.72f, 0.42f)
                        : new Color(0.62f, 0.92f, 0.68f);
                    Widgets.Label(new Rect(rect.x, rect.y + 174f, rect.width, 26f),
                        "Habitat Fit: " + selected.habitatFit.ToStringPercent() +
                        (selected.habitatStress > 0.001f
                            ? "   Habitat Stress: " + selected.habitatStress.ToStringPercent()
                            : "   Comfortable"));
                    GUI.color = old;
                }
            }

            Rect panel = new Rect(rect.x, rect.y + 206f, rect.width, rect.height - 206f);
            Widgets.DrawMenuSection(panel);
            Rect outRect = new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, panel.height - 16f);
            float viewWidth = outRect.width - 16f;
            RefreshSnapshot(viewWidth);
            if (cachedTraits.Count == 0)
            {
                Widgets.Label(new Rect(outRect.x + 4f, outRect.y + 4f, outRect.width - 8f, 30f), "No traits");
                return;
            }
            Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(outRect.height, cachedTotalHeight));
            Widgets.BeginScrollView(outRect, ref scrollPosition, view);
            float y = 0f;
            for (int i = 0; i < cachedTraits.Count; i++)
            {
                DrawTrait(new Rect(0f, y, view.width, cachedHeights[i] - 4f), cachedTraits[i], cachedComp.TraitValue(cachedTraits[i].defName));
                y += cachedHeights[i];
            }
            Widgets.EndScrollView();
        }

        private static float RowHeight(FishTraitDef trait, float value, float width)
        {
            float label = Mathf.Max(22f, Text.CalcHeight(trait.LabelCap, width - 12f));
            float description = Text.CalcHeight(trait.description ?? string.Empty, width - 12f);
            float effect = Text.CalcHeight(FishTraitUtility.EffectLine(trait, value), width - 12f);
            return Mathf.Max(64f, 10f + label + description + effect + 8f);
        }

        private static void DrawTrait(Rect rect, FishTraitDef trait, float value)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            float width = rect.width - 12f;
            float labelHeight = Mathf.Max(22f, Text.CalcHeight(trait.LabelCap, width));
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, width, labelHeight), trait.IsNumeric ? trait.label.Replace("(+%)", "(+" + Mathf.RoundToInt(value) + "%)") : trait.LabelCap.ToString());
            float y = rect.y + 4f + labelHeight;
            Color old = GUI.color;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            float descriptionHeight = Text.CalcHeight(trait.description ?? string.Empty, width);
            Widgets.Label(new Rect(rect.x + 6f, y, width, descriptionHeight), trait.description);
            y += descriptionHeight + 3f;
            Widgets.Label(new Rect(rect.x + 6f, y, width, rect.yMax - y), FishTraitUtility.EffectLine(trait, value));
            GUI.color = old;
            TooltipHandler.TipRegion(rect, trait.description);
        }
    }
}
