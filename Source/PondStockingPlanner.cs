using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class Dialog_PondStockingPlanner : Window
    {
        private readonly PondProxyThing pond;
        private readonly FishPondMapComponent component;
        private readonly List<ThingDef> species;
        private readonly Dictionary<ThingDef, int> plan = new Dictionary<ThingDef, int>();
        private Vector2 speciesScroll;
        private Vector2 planScroll;
        private Vector2 forecastScroll;
        private string search = string.Empty;
        private PondWaterKind plannedWater;

        public override Vector2 InitialSize => new Vector2(1120f, 740f);

        public Dialog_PondStockingPlanner(PondProxyThing pond, FishPondMapComponent component)
        {
            this.pond = pond;
            this.component = component;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = false;
            plannedWater = component?.StockingBlueprintWaterAt(pond.Position) ?? PondWaterKind.Freshwater;
            species = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(FishUtility.IsFish)
                .OrderBy(def => def.label)
                .ThenBy(def => def.defName)
                .ToList();
            if (!LoadBlueprint()) LoadCurrentStock();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (pond?.Spawned != true || component == null)
            {
                Widgets.Label(inRect, "The pond is no longer available.");
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 290f, 34f), "Pond Stocking Planner");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width - 290f, 28f),
                "Model a stable ecosystem before moving fish. Calculations update only when this window is open.");

            DrawWaterControl(new Rect(inRect.xMax - 275f, inRect.y, 275f, 64f));

            float top = inRect.y + 76f;
            float gap = 10f;
            float leftWidth = 340f;
            float middleWidth = 315f;
            Rect available = new Rect(inRect.x, top, leftWidth, inRect.height - top - 4f);
            Rect planned = new Rect(available.xMax + gap, top, middleWidth, available.height);
            Rect forecast = new Rect(planned.xMax + gap, top, inRect.xMax - planned.xMax - gap, available.height);
            DrawAvailableSpecies(available);
            DrawPlannedStock(planned);
            DrawForecast(forecast);
        }

        private void DrawWaterControl(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 20f), "PLANNED WATER");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(rect.x + 10f, rect.y + 27f, rect.width - 20f, 29f), WaterLabel(plannedWater)))
            {
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(PondWaterKind)).Cast<PondWaterKind>()
                    .Select(kind => new FloatMenuOption(WaterLabel(kind), () => plannedWater = kind)).ToList()));
            }
            TooltipHandler.TipRegion(rect, "This changes the forecast only. Empty ponds can be refilled from Basic management.");
        }

        private void DrawAvailableSpecies(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 30f), "Available Species");
            Text.Font = GameFont.Small;
            search = Widgets.TextField(new Rect(rect.x + 12f, rect.y + 46f, rect.width - 24f, 30f), search ?? string.Empty);

            List<ThingDef> filtered = species.Where(def => search.NullOrEmpty()
                || def.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || def.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Rect outRect = new Rect(rect.x + 8f, rect.y + 84f, rect.width - 16f, rect.height - 92f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f, Mathf.Max(outRect.height, filtered.Count * 48f));
            Widgets.BeginScrollView(outRect, ref speciesScroll, view);
            for (int i = 0; i < filtered.Count; i++)
            {
                ThingDef def = filtered[i];
                Rect row = new Rect(0f, i * 48f, view.width, 44f);
                if (i % 2 == 1) Widgets.DrawAltRect(row);
                DrawDefIcon(new Rect(row.x + 4f, row.y + 4f, 36f, 36f), def);
                Widgets.Label(new Rect(row.x + 47f, row.y + 3f, row.width - 102f, 24f), def.LabelCap);
                AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(row.x + 47f, row.y + 24f, row.width - 102f, 18f), profile.diet + " | " + WaterLabel(profile.waterKind));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(row.xMax - 46f, row.y + 7f, 40f, 30f), "+")) ChangeCount(def, 1);
                TooltipHandler.TipRegion(row, SpeciesTooltip(def, profile));
            }
            Widgets.EndScrollView();
        }

        private void DrawPlannedStock(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 30f), "Planned Stock");
            Text.Font = GameFont.Small;
            List<KeyValuePair<ThingDef, int>> entries = plan.Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key.label).ToList();
            int total = entries.Sum(pair => pair.Value);
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 42f, rect.width - 24f, 24f),
                entries.Count + " species | " + total + " fish");
            GUI.color = Color.white;

            Rect outRect = new Rect(rect.x + 8f, rect.y + 72f, rect.width - 16f, rect.height - 184f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f, Mathf.Max(outRect.height, entries.Count * 50f));
            Widgets.BeginScrollView(outRect, ref planScroll, view);
            for (int i = 0; i < entries.Count; i++)
            {
                ThingDef def = entries[i].Key;
                int count = entries[i].Value;
                Rect row = new Rect(0f, i * 50f, view.width, 46f);
                if (i % 2 == 1) Widgets.DrawAltRect(row);
                DrawDefIcon(new Rect(row.x + 4f, row.y + 5f, 34f, 34f), def);
                Widgets.Label(new Rect(row.x + 44f, row.y + 3f, row.width - 142f, 24f), def.LabelCap);
                Text.Font = GameFont.Tiny;
                GUI.color = CompatibilityColor(def);
                Widgets.Label(new Rect(row.x + 44f, row.y + 25f, row.width - 142f, 18f), CompatibilityLabel(def));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(row.xMax - 94f, row.y + 8f, 28f, 28f), "-")) ChangeCount(def, -1);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(row.xMax - 63f, row.y + 8f, 30f, 28f), count.ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonText(new Rect(row.xMax - 30f, row.y + 8f, 28f, 28f), "+")) ChangeCount(def, 1);
            }
            Widgets.EndScrollView();

            float buttonY = rect.yMax - 100f;
            float third = (rect.width - 28f) / 3f;
            if (Widgets.ButtonText(new Rect(rect.x + 10f, buttonY, third, 32f), "Current")) LoadCurrentStock();
            if (Widgets.ButtonText(new Rect(rect.x + 14f + third, buttonY, third, 32f), "Blueprint")) LoadBlueprint();
            if (Widgets.ButtonText(new Rect(rect.x + 18f + third * 2f, buttonY, third, 32f), "Clear")) plan.Clear();
            if (Widgets.ButtonText(new Rect(rect.x + 10f, buttonY + 40f, rect.width - 20f, 38f), "Save Living Blueprint"))
            {
                component.SetStockingBlueprint(pond.Position, plan, plannedWater);
                Messages.Message(total > 0
                        ? "Saved a living blueprint for " + total + " fish. Surplus harvesting will now respect species targets."
                        : "Cleared the pond blueprint.",
                    MessageTypeDefOf.TaskCompletion, false);
            }
            TooltipHandler.TipRegion(new Rect(rect.x + 10f, buttonY + 40f, rect.width - 20f, 38f),
                "Persist this species mix and water intent on the pond. The total becomes its population goal. If surplus harvesting is enabled, handlers remove only fish above species targets.");
        }

        private void DrawForecast(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Forecast data = CalculateForecast();
            Rect inner = rect.ContractedBy(12f);
            Color statusColor = data.danger ? new Color(1f, 0.48f, 0.34f)
                : data.warnings.Count > 0 ? new Color(1f, 0.78f, 0.28f)
                : new Color(0.45f, 0.92f, 0.55f);
            Text.Font = GameFont.Medium;
            GUI.color = statusColor;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 32f), data.danger ? "Unsafe Plan" : data.warnings.Count > 0 ? "Needs Support" : "Stable Plan");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float y = inner.y + 40f;
            DrawGauge(inner, ref y, "Population", data.totalFish, data.capacity, new Color(0.32f, 0.72f, 0.96f));
            DrawGauge(inner, ref y, "Daily Algae Demand", data.algaeDemand, Mathf.Max(0.001f, data.sustainableAlgae), new Color(0.38f, 0.82f, 0.44f));
            y += 4f;
            DrawForecastCard(inner, ref y, "Food Demand", data.dailyDemand.ToString("0.000") + " / day",
                "Natural algae growth: " + data.sustainableAlgae.ToString("0.000") + "/day\nEstimated prepared feed: " + data.feedNeeded.ToString("0.000") + "/day",
                TexCommand.DesirePower);
            DrawForecastCard(inner, ref y, "Diet Mix",
                "Plants " + data.plantEaters + " | Mixed " + data.omnivores + " | Predators " + data.predators,
                data.predationRisk ? "Predation risk is present." : "No modeled predation pressure.",
                TexCommand.Attack);
            DrawForecastCard(inner, ref y, "Expected Value",
                "Beauty " + data.beauty.ToString("0.#") + " | Meat index " + data.meatIndex.ToString("0.#"),
                data.breedingSpecies + " species have enough planned adults to establish breeding pairs.",
                TexCommand.SelectCarriedThing);

            Rect warningOut = new Rect(inner.x, y + 4f, inner.width, inner.yMax - y - 4f);
            List<string> messages = data.warnings.Count > 0 ? data.warnings : new List<string>
            {
                "Water, temperature, capacity, and baseline food pressure are compatible."
            };
            Rect warningView = new Rect(0f, 0f, warningOut.width - 18f, Mathf.Max(warningOut.height, messages.Count * 42f));
            Widgets.BeginScrollView(warningOut, ref forecastScroll, warningView);
            for (int i = 0; i < messages.Count; i++)
            {
                Rect row = new Rect(0f, i * 42f, warningView.width, 38f);
                GUI.color = data.warnings.Count > 0 ? new Color(1f, 0.78f, 0.35f) : new Color(0.60f, 0.90f, 0.65f);
                Widgets.Label(row, (data.warnings.Count > 0 ? "! " : "OK ") + messages[i]);
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();
        }

        private Forecast CalculateForecast()
        {
            PondMenuSnapshot snapshot = component.MenuSnapshotAt(pond.Position) ?? new PondMenuSnapshot();
            float demandMultiplier = AquacultureMod.Settings?.foodDemandMultiplier ?? 1f;
            float algaeMultiplier = AquacultureMod.Settings?.algaeGrowthMultiplier ?? 1f;
            PondMovementUtility.PondInfo pondInfo = PondMovementUtility.InfoAt(pond.Map, pond.Position);
            float algaeCapacity = Mathf.Max(0.1f, (pondInfo?.cells.Count ?? 1) * 0.25f);
            var data = new Forecast { capacity = snapshot.capacity };
            float temperature = snapshot.temperature;

            foreach (KeyValuePair<ThingDef, int> pair in plan)
            {
                if (pair.Value <= 0) continue;
                ThingDef def = pair.Key;
                int count = pair.Value;
                AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
                float daily = profile.hourlyDemand * 24f * demandMultiplier * count;
                DietShares(profile.diet, out float algae, out float detritus, out float prey);
                data.totalFish += count;
                data.dailyDemand += daily;
                data.algaeDemand += daily * algae;
                data.detritusDemand += daily * detritus;
                data.preyDemand += daily * prey;
                data.beauty += count * (1f + profile.beautyOffset);
                data.meatIndex += count * profile.meatYieldFactor;
                if (profile.diet == FishDiet.Carnivore) data.predators += count;
                else if (profile.diet == FishDiet.Omnivore) data.omnivores += count;
                else data.plantEaters += count;
                if (count >= 2) data.breedingSpecies++;
                if (!AquaticSpeciesProfile.WaterCompatible(profile.waterKind, plannedWater))
                    data.warnings.Add(def.LabelCap + " requires " + WaterLabel(profile.waterKind) + " conditions.");
                if (temperature < profile.minimumTemperature || temperature > profile.maximumTemperature)
                    data.warnings.Add(def.LabelCap + " is outside its " + profile.minimumTemperature.ToString("0.#") + " to " +
                        profile.maximumTemperature.ToString("0.#") + " C range.");
            }

            data.sustainableAlgae = 0.0045f * algaeCapacity * algaeMultiplier * 24f;
            float naturalSupport = Mathf.Min(data.algaeDemand, data.sustainableAlgae);
            data.feedNeeded = Mathf.Max(0f, data.dailyDemand - naturalSupport);
            data.predationRisk = data.predators + data.omnivores > 0 && plan.Count(pair => pair.Value > 0) > 1;
            if (data.totalFish > data.capacity)
                data.warnings.Insert(0, "Population exceeds carrying capacity by " + (data.totalFish - data.capacity) + " fish.");
            if (data.algaeDemand > data.sustainableAlgae * 1.1f)
                data.warnings.Add("Plant-food demand exceeds sustainable algae growth.");
            if (data.preyDemand > 0f && !data.predationRisk)
                data.warnings.Add("Predatory food demand has no varied prey population.");
            else if (data.predationRisk)
                data.warnings.Add("Predators may consume smaller pond fish; the forecast cannot guarantee species ratios.");
            if (data.totalFish == 0) data.warnings.Add("Add fish to create a stocking plan.");
            data.danger = data.totalFish > data.capacity || data.warnings.Any(message => message.Contains("requires") || message.Contains("outside"));
            return data;
        }

        private void DrawGauge(Rect rect, ref float y, string label, float value, float maximum, Color color)
        {
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), label + "  " + value.ToString("0.##") + " / " + maximum.ToString("0.##"));
            Rect bar = new Rect(rect.x, y + 25f, rect.width, 12f);
            Widgets.DrawBoxSolid(bar, new Color(0.12f, 0.13f, 0.14f));
            float fraction = maximum <= 0f ? (value > 0f ? 1f : 0f) : Mathf.Clamp01(value / maximum);
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), color);
            y += 48f;
        }

        private static void DrawForecastCard(Rect rect, ref float y, string title, string value, string details, Texture2D icon)
        {
            Rect card = new Rect(rect.x, y, rect.width, 88f);
            Widgets.DrawMenuSection(card);
            if (icon != null) GUI.DrawTexture(new Rect(card.x + 9f, card.y + 20f, 34f, 34f), icon, ScaleMode.ScaleToFit);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(card.x + 50f, card.y + 7f, card.width - 58f, 18f), title.ToUpperInvariant());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(card.x + 50f, card.y + 25f, card.width - 58f, 24f), value);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(card.x + 50f, card.y + 49f, card.width - 58f, 34f), details);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 96f;
        }

        private void LoadCurrentStock()
        {
            plan.Clear();
            PondMenuSnapshot snapshot = component?.MenuSnapshotAt(pond.Position);
            if (snapshot == null) return;
            for (int i = 0; i < snapshot.fish.Count; i++)
            {
                ThingDef def = snapshot.fish[i].fish?.parent?.def;
                if (def != null) plan[def] = Count(def) + 1;
            }
        }

        private bool LoadBlueprint()
        {
            Dictionary<ThingDef, int> saved = component?.StockingBlueprintAt(pond.Position);
            if (saved == null || saved.Count == 0) return false;
            plan.Clear();
            foreach (KeyValuePair<ThingDef, int> pair in saved) plan[pair.Key] = pair.Value;
            plannedWater = component.StockingBlueprintWaterAt(pond.Position);
            return true;
        }

        private void ChangeCount(ThingDef def, int delta)
        {
            int changed = Mathf.Clamp(Count(def) + delta, 0, 999);
            if (changed <= 0) plan.Remove(def);
            else plan[def] = changed;
        }

        private int Count(ThingDef def) => plan.TryGetValue(def, out int count) ? count : 0;

        private Color CompatibilityColor(ThingDef def)
        {
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
            if (!AquaticSpeciesProfile.WaterCompatible(profile.waterKind, plannedWater)) return new Color(1f, 0.45f, 0.35f);
            float temperature = component.MenuSnapshotAt(pond.Position)?.temperature ?? 21f;
            return temperature < profile.minimumTemperature || temperature > profile.maximumTemperature
                ? new Color(1f, 0.78f, 0.30f)
                : new Color(0.52f, 0.92f, 0.58f);
        }

        private string CompatibilityLabel(ThingDef def)
        {
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(def);
            if (!AquaticSpeciesProfile.WaterCompatible(profile.waterKind, plannedWater)) return "Wrong water";
            float temperature = component.MenuSnapshotAt(pond.Position)?.temperature ?? 21f;
            return temperature < profile.minimumTemperature || temperature > profile.maximumTemperature ? "Unsafe temperature" : "Compatible";
        }

        private static string SpeciesTooltip(ThingDef def, AquaticSpeciesProfile profile)
        {
            return def.LabelCap + "\nDiet: " + profile.diet + "\nWater: " + WaterLabel(profile.waterKind) +
                "\nTemperature: " + profile.minimumTemperature.ToString("0.#") + " to " + profile.maximumTemperature.ToString("0.#") +
                " C\nDaily food demand: " + (profile.hourlyDemand * 24f).ToString("0.000") +
                "\nBreeding interval: " + profile.breedingIntervalFactor.ToStringPercent() +
                "\nOffspring: " + profile.offspringFactor.ToStringPercent() +
                "\nMeat yield: " + profile.meatYieldFactor.ToStringPercent();
        }

        private static void DrawDefIcon(Rect rect, ThingDef def)
        {
            Texture2D icon = def?.uiIcon;
            if (icon != null) GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
        }

        private static string WaterLabel(PondWaterKind kind) =>
            kind == PondWaterKind.Brackishwater ? "Brackishwater" : kind.ToString();

        private static void DietShares(FishDiet diet, out float algae, out float detritus, out float prey)
        {
            algae = detritus = prey = 0f;
            switch (diet)
            {
                case FishDiet.Herbivore: algae = 1f; break;
                case FishDiet.Carnivore: prey = 1f; break;
                case FishDiet.Detritivore: detritus = 0.9f; algae = 0.1f; break;
                case FishDiet.FilterFeeder: algae = 0.8f; detritus = 0.2f; break;
                default: algae = 0.55f; detritus = 0.15f; prey = 0.3f; break;
            }
        }

        private sealed class Forecast
        {
            public int totalFish;
            public int capacity;
            public int plantEaters;
            public int omnivores;
            public int predators;
            public int breedingSpecies;
            public float dailyDemand;
            public float algaeDemand;
            public float detritusDemand;
            public float preyDemand;
            public float sustainableAlgae;
            public float feedNeeded;
            public float beauty;
            public float meatIndex;
            public bool predationRisk;
            public bool danger;
            public readonly List<string> warnings = new List<string>();
        }
    }
}
