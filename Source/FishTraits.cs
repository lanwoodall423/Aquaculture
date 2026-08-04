using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public enum FishTraitKind { Color, Variegated, Scale, NumericSize, Nutritious, Age, Sex, Diet, Water, Finish, Body, Breeding, Behavior, Hardiness, Effect }
    public enum FishPattern { None, Spotted, Striped }
    public enum FishDiet { Herbivore, Omnivore, Carnivore, Detritivore, FilterFeeder }
    public enum PondWaterKind { Freshwater, Saltwater, Brackishwater }
    public enum PondBreedingMode { Natural, Paused, Encouraged, Intensive }

    public sealed class FishTraitDef : Def
    {
        public FishTraitKind kind;
        public Color tint = Color.white;
        public FishPattern pattern;
        public float drawScale = 1f;
        public float verticalScale = 1f;
        public float movementSpeed = 1f;
        public float turnRate = 1f;
        public float schooling = 1f;
        public float cohesion = 1f;
        public float alignment = 1f;
        public float separation = 1f;
        public float restFrequency = 1f;
        public float meatYield = 1f;
        public float beautyOffset;
        public float commonality = 1f;
        public bool mutationEligible = true;
        public int minPercent;
        public int maxPercent;
        public int percentStep = 5;
        public FishDiet diet;
        public PondWaterKind waterKind;
        public float opacity = 1f;
        public bool livebearer;
        public float breedingCooldownFactor = 1f;
        public Season breedingSeason = Season.Undefined;
        public bool solitary;
        public bool curious;
        public float temperatureRangeOffset;
        public FishTraitPondEffect pondEffect;

        public bool IsNumeric => kind == FishTraitKind.NumericSize || kind == FishTraitKind.Nutritious;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (commonality < 0f) yield return defName + " has a negative commonality.";
            if ((kind == FishTraitKind.Scale || kind == FishTraitKind.Age) && drawScale <= 0f) yield return defName + " must have a positive drawScale.";
            if (kind == FishTraitKind.Variegated && pattern == FishPattern.None) yield return defName + " needs a pattern.";
            if (IsNumeric && (minPercent <= 0 || maxPercent < minPercent || percentStep <= 0)) yield return defName + " has invalid percentage bounds.";
            if (opacity <= 0f || opacity > 1f) yield return defName + " opacity must be greater than zero and no more than one.";
            if (breedingCooldownFactor <= 0f) yield return defName + " must have a positive breedingCooldownFactor.";
            if (pondEffect != null)
                foreach (string error in pondEffect.ConfigErrors(defName)) yield return error;
        }
    }

    public sealed class FishTraitSetting : IExposable
    {
        public string traitDefName;
        public bool enabled = true;
        public float weight = 1f;

        public FishTraitSetting() { }
        public FishTraitSetting(FishTraitDef trait) { traitDefName = trait.defName; weight = Mathf.Max(0f, trait.commonality); }
        public void ExposeData()
        {
            Scribe_Values.Look(ref traitDefName, "traitDefName");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref weight, "weight", 1f);
        }
    }

    public static class FishTraitUtility
    {
        public const string Fry = "AF_Age_Fry";
        public const string Juvenile = "AF_Age_Juvenile";
        public const string Adult = "AF_Age_Adult";
        public const string Elder = "AF_Age_Elder";
        public const string Male = "AF_Sex_Male";
        public const string Female = "AF_Sex_Female";
        public const string DietPrefix = "AF_Diet_";
        public const string WaterPrefix = "AF_Water_";

        public static List<FishTraitDef> Resolve(IEnumerable<string> names)
        {
            if (names == null) return new List<FishTraitDef>();
            return names.Select(name => DefDatabase<FishTraitDef>.GetNamedSilentFail(name))
                .Where(def => def != null)
                .OrderBy(TraitListOrder)
                .ToList();
        }

        private static int TraitListOrder(FishTraitDef trait)
        {
            if (trait.kind == FishTraitKind.Age) return 0;
            if (trait.kind == FishTraitKind.Sex) return 1;
            if (trait.kind == FishTraitKind.Diet) return 2;
            if (trait.kind == FishTraitKind.Water) return 3;
            return 4;
        }

        public static List<FishTraitDef> SelectMutationTraits(int maximum)
        {
            AquacultureSettings settings = AquacultureMod.Settings;
            var result = new List<FishTraitDef>();
            if (settings == null || maximum <= 0 || !Rand.Chance(settings.globalMutationRate)) return result;
            int desired = Rand.RangeInclusive(1, maximum);
            List<FishTraitDef> candidates = DefDatabase<FishTraitDef>.AllDefsListForReading
                .Where(def => def.mutationEligible && settings.TraitEnabled(def) && settings.TraitWeight(def) > 0f).ToList();
            while (result.Count < desired && candidates.Count > 0)
            {
                float total = candidates.Sum(def => settings.TraitWeight(def));
                float roll = Rand.Value * total;
                FishTraitDef selected = candidates[candidates.Count - 1];
                foreach (FishTraitDef candidate in candidates)
                {
                    roll -= settings.TraitWeight(candidate);
                    if (roll <= 0f) { selected = candidate; break; }
                }
                result.Add(selected);
                candidates.RemoveAll(def => def == selected || ExclusiveGroup(def) == ExclusiveGroup(selected));
            }
            return result;
        }

        public static int RollPercent(FishTraitDef trait)
        {
            int steps = (trait.maxPercent - trait.minPercent) / trait.percentStep;
            int index = 0;
            while (index < steps && Rand.Chance(0.45f)) index++;
            return trait.minPercent + index * trait.percentStep;
        }

        public static string ExclusiveGroup(FishTraitDef trait)
        {
            if (trait == null) return "";
            if (trait.kind == FishTraitKind.Color) return "Color";
            if (trait.kind == FishTraitKind.Variegated) return "Pattern";
            if (trait.kind == FishTraitKind.Scale || trait.kind == FishTraitKind.NumericSize) return "Size";
            if (trait.kind == FishTraitKind.Body) return "Body";
            if (trait.kind == FishTraitKind.Age) return "Age";
            if (trait.kind == FishTraitKind.Sex) return "Sex";
            if (trait.kind == FishTraitKind.Diet) return "Diet";
            if (trait.kind == FishTraitKind.Water) return "Water";
            if (trait.breedingSeason != Season.Undefined) return "BreedingSeason";
            return trait.defName;
        }

        public static string DisplayLabel(FishTraitDef trait, CompFishTraits comp = null)
        {
            if (trait == null) return "Unknown trait";
            float value = comp?.TraitValue(trait.defName) ?? 0f;
            return trait.IsNumeric && value > 0f ? trait.label.Replace("(+%)", "(+" + Mathf.RoundToInt(value) + "%)") : trait.LabelCap.ToString();
        }

        public static string EffectLine(FishTraitDef trait, float value = 0f)
        {
            if (trait == null) return "No effect";
            string result = "No effect";
            if (trait.kind == FishTraitKind.Color) result = "Whole-fish tint: RGB " + Mathf.RoundToInt(trait.tint.r * 255f) + ", " + Mathf.RoundToInt(trait.tint.g * 255f) + ", " + Mathf.RoundToInt(trait.tint.b * 255f);
            else if (trait.kind == FishTraitKind.Variegated) result = trait.pattern + " pattern within the matching texture mask";
            else if (trait.kind == FishTraitKind.Scale || trait.kind == FishTraitKind.Age)
                result = "Visual size: " + trait.drawScale.ToStringPercent() + "; " + ProcessingYieldText(FishProcessingYield.SizeYieldFactor(trait.drawScale));
            else if (trait.kind == FishTraitKind.NumericSize)
                result = value > 0f
                    ? "Visual size: +" + Mathf.RoundToInt(value) + "%; " + ProcessingYieldText(FishProcessingYield.SizeYieldFactor(1f + value / 100f))
                    : "Random visual size: +" + trait.minPercent + "% to +" + trait.maxPercent + "%; " +
                      ProcessingYieldRange(1f + trait.minPercent / 100f, 1f + trait.maxPercent / 100f);
            else if (trait.kind == FishTraitKind.Nutritious) result = value > 0f ? "Nutrition: +" + Mathf.RoundToInt(value) + "%" : "Random nutrition: +" + trait.minPercent + "% to +" + trait.maxPercent + "%";
            else if (trait.kind == FishTraitKind.Sex) result = "Used for pond breeding";
            else if (trait.kind == FishTraitKind.Diet) result = "Feeds as a " + trait.diet.ToString().ToLowerInvariant().Replace("filterfeeder", "filter feeder");
            else if (trait.kind == FishTraitKind.Water) result = trait.waterKind == PondWaterKind.Brackishwater ? "Tolerates freshwater, saltwater, and brackishwater ponds" : "Requires a " + trait.waterKind.ToString().ToLowerInvariant() + " pond";
            else if (trait.kind == FishTraitKind.Finish && trait.opacity < 0.999f) result = "Translucent finish: " + trait.opacity.ToStringPercent() + " opacity";
            else if (trait.kind == FishTraitKind.Finish && trait.defName.Contains("Gilded")) result = "Reflective golden finish";
            else if (trait.kind == FishTraitKind.Finish) result = trait.label + " surface effect";
            else if (trait.kind == FishTraitKind.Body) result = "Pond speed: " + trait.movementSpeed.ToStringPercent() + "; meat yield: " + trait.meatYield.ToStringPercent();
            else if (trait.livebearer) result = "Produces live fry instead of eggs";
            else if (trait.breedingCooldownFactor < 0.999f) result = "Breeding cooldown: " + trait.breedingCooldownFactor.ToStringPercent();
            else if (trait.breedingSeason != Season.Undefined) result = "Breeds during " + trait.breedingSeason;
            else if (trait.solitary) result = "Does not align or cohere with a school";
            else if (trait.curious) result = "Approaches colonists watching the pond";
            else if (trait.temperatureRangeOffset > 0f) result = "Livable temperature range: " + trait.temperatureRangeOffset.ToString("0.#") + " C wider at both limits";
            else if (trait.pondEffect != null) result = trait.pondEffect.summary.NullOrEmpty() ? "Affects pawns swimming in its pond" : trait.pondEffect.summary;
            if (!Mathf.Approximately(trait.beautyOffset, 0f)) result += "; beauty: " + (trait.beautyOffset > 0f ? "+" : "") + trait.beautyOffset.ToString("0.#");
            return result;
        }

        private static string ProcessingYieldText(float factor)
        {
            return "Processing yield: " + factor.ToString("0.00") + "x of normal size yield";
        }

        private static string ProcessingYieldRange(float minimumSize, float maximumSize)
        {
            return "Processing yield: " + FishProcessingYield.SizeYieldFactor(minimumSize).ToString("0.00") +
                "x to " + FishProcessingYield.SizeYieldFactor(maximumSize).ToString("0.00") + "x of normal size yield";
        }
    }
}
