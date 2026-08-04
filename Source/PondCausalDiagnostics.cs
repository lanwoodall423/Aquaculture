using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public enum PondCausalPriority
    {
        Lethal,
        Starvation,
        SevereStress,
        Reproduction,
        Advice
    }

    public sealed class PondCausalIssue
    {
        public string key;
        public PondCausalPriority priority;
        public string text;
        public int affectedFish;
        public int detailPage = -1;
        public CompFishTraits sampleFish;

        public Texture2D Icon => priority == PondCausalPriority.Lethal ? TexButton.Stop
            : priority == PondCausalPriority.Starvation ? TexButton.Minus
            : priority == PondCausalPriority.SevereStress ? TexButton.Suspend
            : priority == PondCausalPriority.Reproduction ? TexButton.Info
            : TexButton.Plus;

        public Color Color => priority == PondCausalPriority.Lethal ? new Color(1f, 0.35f, 0.25f)
            : priority == PondCausalPriority.Starvation ? new Color(1f, 0.58f, 0.25f)
            : priority == PondCausalPriority.SevereStress ? new Color(1f, 0.78f, 0.28f)
            : priority == PondCausalPriority.Reproduction ? new Color(0.95f, 0.85f, 0.42f)
            : new Color(0.65f, 0.82f, 0.95f);
    }

    public sealed class PondCausalSummary
    {
        public string populationLine;
        public string foodLine;
        public string waterLine;
        public string temperatureLine;
        public string habitatLine;
        public string breedingLine;
        public string nextBreedingLine;
        public float hourlyDemand;
        public float feedHours;
        public float temperature;
        public int hungry;
        public int starving;
        public int wrongWater;
        public int temperatureStressed;
        public int waterStressed;
        public float maximumTemperatureStress;
        public readonly List<PondCausalIssue> issues = new List<PondCausalIssue>();

        public bool HasIssues => issues.Count > 0;
    }

    public static class PondCausalSummaryBuilder
    {
        public static PondCausalSummary Build(PondEcologyRecord ecology, PondHabitatSnapshot habitat,
            IList<CompFishTraits> fish, Map map, int population, int now)
        {
            var summary = new PondCausalSummary();
            var species = new Dictionary<ThingDef, List<CompFishTraits>>();
            for (int i = 0; i < fish.Count; i++)
            {
                CompFishTraits comp = fish[i];
                if (comp?.parent?.def == null) continue;
                if (!species.TryGetValue(comp.parent.def, out List<CompFishTraits> members))
                {
                    members = new List<CompFishTraits>();
                    species.Add(comp.parent.def, members);
                }
                members.Add(comp);
                if (!AquaticSpeciesProfile.WaterCompatible(comp.WaterKind, ecology.waterKind)) summary.wrongWater++;
                if (comp.foodReserve < 0.25f) summary.hungry++;
                if (comp.starvationProgress > 0f) summary.starving++;
                if (comp.temperatureStress > 0f)
                {
                    summary.temperatureStressed++;
                    summary.maximumTemperatureStress = Mathf.Max(summary.maximumTemperatureStress, comp.temperatureStress);
                }
                if (comp.waterStress >= 0.1f) summary.waterStressed++;
                summary.hourlyDemand += AquaticSpeciesProfile.For(comp.parent.def).hourlyDemand
                    * Mathf.Max(0.2f, comp.SizeFactor)
                    * (AquacultureMod.Settings?.foodDemandMultiplier ?? 1f);
            }

            summary.feedHours = summary.hourlyDemand > 0f ? Mathf.Max(0f, ecology.preparedFeed) / summary.hourlyDemand : 0f;
            float feedDays = summary.feedHours / 24f;
            summary.populationLine = "AquacultureFishing.PondCausalPopulation".Translate(
                population, habitat.physicalMaximum, habitat.sustainablePopulation).ToString();
            summary.foodLine = "AquacultureFishing.PondCausalFoodReserve".Translate(
                feedDays, summary.feedHours, summary.hungry + summary.starving).ToString();
            summary.waterLine = summary.wrongWater > 0
                ? "AquacultureFishing.PondCausalWaterMismatch".Translate(summary.wrongWater).ToString()
                : "AquacultureFishing.PondCausalWaterCompatible".Translate().ToString();
            float temperature = GenTemperature.GetTemperatureForCell(ecology.anchor, map);
            summary.temperature = temperature;
            summary.temperatureLine = "AquacultureFishing.PondCausalTemperature".Translate(
                temperature.ToString("0.#"), summary.temperatureStressed).ToString();
            summary.habitatLine = "AquacultureFishing.PondCausalHabitat".Translate(
                habitat.stressedFish, PondCapacityRules.ConstraintLabel(habitat.limitingConstraint)).ToString();

            AddCapacityIssues(summary, habitat, population);
            AddConditionIssues(summary, habitat, ecology.waterKind, fish);
            BuildBreeding(summary, ecology, habitat, species, fish.Count, map, now);
            summary.issues.Sort((left, right) =>
            {
                int priority = left.priority.CompareTo(right.priority);
                return priority != 0 ? priority : string.Compare(left.key, right.key, StringComparison.Ordinal);
            });
            return summary;
        }

        private static void AddCapacityIssues(PondCausalSummary summary, PondHabitatSnapshot habitat, int population)
        {
            if (population > habitat.physicalMaximum)
                AddIssue(summary, "physical", PondCausalPriority.Lethal,
                    "AquacultureFishing.PondCausalPhysicalOvercrowding".Translate(population - habitat.physicalMaximum).ToString(),
                    population - habitat.physicalMaximum, 1, null);
            if (population > habitat.industrialMaximum)
                AddIssue(summary, "industrial", PondCausalPriority.SevereStress,
                    "AquacultureFishing.PondCausalIndustrialOverage".Translate(population - habitat.industrialMaximum).ToString(),
                    population - habitat.industrialMaximum, 1, null);
            else if (population > habitat.sustainablePopulation)
                AddIssue(summary, "sustainable", PondCausalPriority.SevereStress,
                    "AquacultureFishing.PondCausalEcologicalSupport".Translate(
                        population - habitat.sustainablePopulation,
                        PondCapacityRules.ConstraintLabel(habitat.limitingConstraint)).ToString(),
                    population - habitat.sustainablePopulation, 1, null);
        }

        private static void AddConditionIssues(PondCausalSummary summary, PondHabitatSnapshot habitat,
            PondWaterKind waterKind, IList<CompFishTraits> fish)
        {
            CompFishTraits waterSample = null;
            CompFishTraits temperatureSample = null;
            CompFishTraits stressSample = null;
            for (int i = 0; i < fish.Count; i++)
            {
                CompFishTraits comp = fish[i];
                if (comp == null) continue;
                if (waterSample == null && !AquaticSpeciesProfile.WaterCompatible(comp.WaterKind, waterKind)) waterSample = comp;
                if (temperatureSample == null && comp.temperatureStress > 0f) temperatureSample = comp;
                if (stressSample == null && (comp.waterStress >= 0.1f || comp.habitatStress >= 0.75f)) stressSample = comp;
            }
            if (summary.wrongWater > 0)
                AddIssue(summary, "water", PondCausalPriority.Lethal,
                    "AquacultureFishing.PondCausalWaterRisk".Translate(summary.wrongWater).ToString(),
                    summary.wrongWater, 1, waterSample);
            if (summary.temperatureStressed > 0)
                AddIssue(summary, "temperature", summary.maximumTemperatureStress >= 0.75f
                        ? PondCausalPriority.Lethal : PondCausalPriority.SevereStress,
                    (summary.maximumTemperatureStress >= 0.75f
                        ? "AquacultureFishing.PondCausalTemperatureCritical"
                        : "AquacultureFishing.PondCausalTemperatureRisk").Translate(summary.temperatureStressed).ToString(),
                    summary.temperatureStressed, 1, temperatureSample);
            if (summary.starving > 0)
                AddIssue(summary, "starvation", PondCausalPriority.Starvation,
                    "AquacultureFishing.PondCausalStarvation".Translate(summary.starving).ToString(),
                    summary.starving, 1, stressSample);
            else if (summary.hungry > 0)
                AddIssue(summary, "hunger", PondCausalPriority.Starvation,
                    "AquacultureFishing.PondCausalHunger".Translate(summary.hungry).ToString(),
                    summary.hungry, 1, stressSample);
            if (summary.waterStressed > 0)
                AddIssue(summary, "waterStress", PondCausalPriority.SevereStress,
                    "AquacultureFishing.PondCausalWaterStress".Translate(summary.waterStressed).ToString(),
                    summary.waterStressed, 1, stressSample);
            if (habitat.stressedFish > 0)
                AddIssue(summary, "habitat", PondCausalPriority.SevereStress,
                    "AquacultureFishing.PondCausalHabitatDeficit".Translate(habitat.stressedFish).ToString(),
                    habitat.stressedFish, 1, stressSample);
        }

        private static void BuildBreeding(PondCausalSummary summary, PondEcologyRecord ecology,
            PondHabitatSnapshot habitat, Dictionary<ThingDef, List<CompFishTraits>> species, int fishCount,
            Map map, int now)
        {
            if (fishCount == 0)
            {
                summary.breedingLine = "AquacultureFishing.PondCausalBreedingEmpty".Translate().ToString();
                return;
            }
            if (AquacultureMod.Settings?.globalBreedingEnabled == false || ecology.breedingMode == PondBreedingMode.Paused)
            {
                summary.breedingLine = "AquacultureFishing.PondCausalBreedingDisabled".Translate().ToString();
                AddIssue(summary, "breedingDisabled", PondCausalPriority.Reproduction,
                    "AquacultureFishing.PondCausalBreedingDisabled".Translate().ToString(), fishCount, 2, null);
                return;
            }
            if (fishCount >= habitat.effectiveCapacity)
            {
                summary.breedingLine = "AquacultureFishing.PondCausalBreedingAtCapacity".Translate().ToString();
                AddIssue(summary, "breedingCapacity", PondCausalPriority.Reproduction,
                    "AquacultureFishing.PondCausalBreedingAtCapacity".Translate().ToString(), fishCount, 0, null);
                return;
            }

            int juvenileSpecies = 0;
            int juvenileFish = 0;
            int noFemaleSpecies = 0;
            int noFemaleFish = 0;
            int noMaleSpecies = 0;
            int noMaleFish = 0;
            int sterileFish = 0;
            int conditionBlockedFish = 0;
            int waitingSpecies = 0;
            int earliest = int.MaxValue;
            int readySpecies = 0;
            CompFishTraits sample = null;
            foreach (List<CompFishTraits> members in species.Values)
            {
                int adults = members.Count(fish => fish.IsAlive && fish.IsAdult);
                int females = members.Count(fish => fish.IsAlive && fish.IsAdult && fish.IsFemale);
                int males = members.Count(fish => fish.IsAlive && fish.IsAdult && !fish.IsFemale);
                if (adults == 0) { juvenileSpecies++; juvenileFish += members.Count; sample = sample ?? members.FirstOrDefault(); continue; }
                if (females == 0) { noFemaleSpecies++; noFemaleFish += members.Count; sample = sample ?? members.FirstOrDefault(); continue; }
                if (males == 0) { noMaleSpecies++; noMaleFish += members.Count; sample = sample ?? members.FirstOrDefault(); continue; }
                int sterile = members.Count(fish => fish.IsAlive && fish.IsAdult && fish.sterilized);
                if (sterile >= adults) { sterileFish += sterile; sample = sample ?? members.FirstOrDefault(); continue; }
                List<CompFishTraits> eligibleFemales = members.Where(fish => fish.IsFemale
                    && PondBreedingRules.CanBreedIgnoringCooldown(fish, map)).ToList();
                List<CompFishTraits> eligibleMales = members.Where(fish => !fish.IsFemale
                    && PondBreedingRules.CanBreedIgnoringCooldown(fish, map)).ToList();
                if (eligibleFemales.Count == 0 || eligibleMales.Count == 0)
                {
                    conditionBlockedFish += adults;
                    sample = sample ?? members.FirstOrDefault();
                    continue;
                }
                int pairTick = int.MaxValue;
                for (int femaleIndex = 0; femaleIndex < eligibleFemales.Count; femaleIndex++)
                    for (int maleIndex = 0; maleIndex < eligibleMales.Count; maleIndex++)
                        pairTick = Mathf.Min(pairTick, Mathf.Max(now,
                            Mathf.Max(eligibleFemales[femaleIndex].nextBreedTick, eligibleMales[maleIndex].nextBreedTick)));
                if (pairTick <= now) readySpecies++;
                else { waitingSpecies++; earliest = Mathf.Min(earliest, pairTick); }
            }

            if (readySpecies > 0)
            {
                summary.breedingLine = "AquacultureFishing.PondCausalBreedingPossible".Translate(readySpecies).ToString();
                summary.nextBreedingLine = "AquacultureFishing.PondCausalNextBreeding".Translate(
                    0.ToStringTicksToPeriod()).ToString();
            }
            else if (waitingSpecies > 0 && earliest < int.MaxValue)
            {
                summary.breedingLine = "AquacultureFishing.PondCausalBreedingWaiting".Translate(waitingSpecies).ToString();
                summary.nextBreedingLine = "AquacultureFishing.PondCausalNextBreeding".Translate(
                    Mathf.Max(0, earliest - now).ToStringTicksToPeriod()).ToString();
            }
            else
            {
                summary.breedingLine = "AquacultureFishing.PondCausalBreedingBlocked".Translate().ToString();
            }
            if (juvenileSpecies > 0 && noFemaleSpecies == 0 && noMaleSpecies == 0)
                AddIssue(summary, "juvenile", PondCausalPriority.Reproduction,
                    "AquacultureFishing.PondCausalJuvenileOnly".Translate(juvenileSpecies, juvenileFish).ToString(), juvenileFish, 2, sample);
            if (noFemaleSpecies > 0)
                AddIssue(summary, "adultFemale", PondCausalPriority.Reproduction,
                    "AquacultureFishing.PondCausalNoAdultFemale".Translate(noFemaleSpecies, noFemaleFish).ToString(), noFemaleFish, 2, sample);
            if (noMaleSpecies > 0)
                AddIssue(summary, "adultMale", PondCausalPriority.Reproduction,
                    "AquacultureFishing.PondCausalNoAdultMale".Translate(noMaleSpecies, noMaleFish).ToString(), noMaleFish, 2, sample);
            if (sterileFish > 0)
                AddIssue(summary, "sterile", PondCausalPriority.Reproduction,
                    "AquacultureFishing.PondCausalSterile".Translate(sterileFish).ToString(), sterileFish, 2, sample);
            if (conditionBlockedFish > 0)
                AddIssue(summary, "breedingConditions", PondCausalPriority.Reproduction,
                    "AquacultureFishing.PondCausalBreedingConditions".Translate(conditionBlockedFish).ToString(),
                    conditionBlockedFish, 2, sample);
        }

        private static void AddIssue(PondCausalSummary summary, string key, PondCausalPriority priority,
            string text, int affectedFish, int detailPage, CompFishTraits sample)
        {
            PondCausalIssue existing = summary.issues.FirstOrDefault(issue => issue.key == key);
            if (existing != null)
            {
                existing.affectedFish += Mathf.Max(0, affectedFish);
                if (existing.sampleFish == null) existing.sampleFish = sample;
                return;
            }
            summary.issues.Add(new PondCausalIssue
            {
                key = key,
                priority = priority,
                text = text,
                affectedFish = Mathf.Max(0, affectedFish),
                detailPage = detailPage,
                sampleFish = sample
            });
        }
    }

    public static class PondCausalUi
    {
        public static float DrawSummary(Rect rect, PondMenuSnapshot snapshot, Action<int> navigate)
        {
            PondCausalSummary summary = snapshot?.causalSummary;
            if (summary == null) return 0f;
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 28f),
                "AquacultureFishing.PondCausalTitle".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 37f, rect.width - 20f, 22f), summary.populationLine);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 59f, rect.width - 20f, 22f), summary.foodLine);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 81f, rect.width - 20f, 22f), summary.waterLine);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 103f, rect.width - 20f, 22f), summary.temperatureLine);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 125f, rect.width - 20f, 22f), summary.habitatLine);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 147f, rect.width - 20f, 22f), summary.breedingLine);
            if (!string.IsNullOrEmpty(summary.nextBreedingLine))
                Widgets.Label(new Rect(rect.x + 10f, rect.y + 169f, rect.width - 20f, 22f), summary.nextBreedingLine);
            float issueY = rect.y + (string.IsNullOrEmpty(summary.nextBreedingLine) ? 169f : 191f);
            if (summary.issues.Count == 0)
            {
                GUI.color = new Color(0.55f, 0.86f, 0.62f);
                Widgets.Label(new Rect(rect.x + 10f, issueY, rect.width - 20f, 22f),
                    "AquacultureFishing.PondCausalNoActiveRisks".Translate());
                GUI.color = Color.white;
                return issueY - rect.y + 28f;
            }
            int visible = Mathf.Min(3, summary.issues.Count);
            for (int i = 0; i < visible; i++)
            {
                PondCausalIssue issue = summary.issues[i];
                Rect row = new Rect(rect.x + 8f, issueY + i * 28f, rect.width - 16f, 26f);
                GUI.color = issue.Color;
                GUI.DrawTexture(new Rect(row.x, row.y + 3f, 20f, 20f), issue.Icon, ScaleMode.ScaleToFit);
                GUI.color = issue.Color;
                Widgets.Label(new Rect(row.x + 25f, row.y + 2f, row.width - 110f, 22f), issue.text);
                GUI.color = Color.white;
                if (issue.detailPage >= 0 && Widgets.ButtonText(new Rect(row.xMax - 78f, row.y, 78f, 24f),
                    "AquacultureFishing.PondCausalDetails".Translate()))
                {
                    navigate?.Invoke(issue.detailPage);
                    if (issue.sampleFish?.parent?.Spawned == true) Find.Selector.Select(issue.sampleFish.parent);
                }
                TooltipHandler.TipRegion(row, issue.text);
            }
            return issueY - rect.y + visible * 28f + 4f;
        }
    }
}
