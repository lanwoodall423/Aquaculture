using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class FishPatternMaskRecord : IExposable
    {
        public string textureKey;
        public FishPattern pattern;
        public int width;
        public int height;
        public byte[] pixels;

        public FishPatternMaskRecord() { }
        public FishPatternMaskRecord(string key, FishPattern fishPattern, int textureWidth, int textureHeight)
        {
            textureKey = key;
            pattern = fishPattern;
            width = textureWidth;
            height = textureHeight;
            pixels = new byte[width * height];
        }

        public bool HasPixels => pixels != null && pixels.Any(value => value != 0);

        public void ExposeData()
        {
            string data = Scribe.mode == LoadSaveMode.Saving && pixels != null ? Convert.ToBase64String(pixels) : null;
            Scribe_Values.Look(ref textureKey, "textureKey");
            Scribe_Values.Look(ref pattern, "pattern");
            Scribe_Values.Look(ref width, "width");
            Scribe_Values.Look(ref height, "height");
            Scribe_Values.Look(ref data, "pixels");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                try { pixels = data.NullOrEmpty() ? new byte[Mathf.Max(1, width * height)] : Convert.FromBase64String(data); }
                catch { pixels = new byte[Mathf.Max(1, width * height)]; }
                if (pixels.Length != width * height) pixels = new byte[Mathf.Max(1, width * height)];
            }
        }
    }

    public sealed class FishMaskRecord : IExposable
    {
        public string fishDefName;
        public List<FishPatternMaskRecord> patternMasks = new List<FishPatternMaskRecord>();

        public FishMaskRecord() { }
        public FishMaskRecord(string defName) { fishDefName = defName; }

        public FishPatternMaskRecord GetMask(string textureKey, FishPattern pattern, int width, int height, bool create = true)
        {
            FishPatternMaskRecord record = patternMasks.FirstOrDefault(mask => mask.textureKey == textureKey && mask.pattern == pattern);
            if (record != null && (record.width != width || record.height != height))
            {
                patternMasks.Remove(record);
                record = null;
            }
            if (record == null && create)
            {
                record = new FishPatternMaskRecord(textureKey, pattern, width, height);
                patternMasks.Add(record);
            }
            return record;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Collections.Look(ref patternMasks, "patternMasks", LookMode.Deep);
            if (patternMasks == null) patternMasks = new List<FishPatternMaskRecord>();
        }
    }

    public sealed class AquacultureSettings : ModSettings
    {
        public int dataVersion = 1;
        public bool enableResearchProgression = true;
        public float fishingDurationFactor = 0.30f;
        public bool showPondAlerts = true;
        public float globalMutationRate = 0.20f;
        public int maxMutations = 2;
        public bool globalBreedingEnabled = true;
        public float breedingIntervalDays = 15f;
        public float eggHatchDays = 3f;
        public int minimumOffspring = 1;
        public int maximumOffspring = 3;
        public int minimumLifespanDays = 48;
        public int maximumLifespanDays = 72;
        public float ecologyIntervalHours = 1f;
        public float foodDemandMultiplier = 1f;
        public float algaeGrowthMultiplier = 1f;
        public float starvationHours = 96f;
        public float waterStressHours = 12f;
        public float temperatureStressHours = 48f;
        public float fishCapacityPerCell = 2f;
        public bool predationEnabled = true;
        public float feedValuePerUnit = 0.05f;
        public float feederRange = 6f;
        public int animaFishPerTree = 20;
        public List<FishTraitSetting> traitSettings = new List<FishTraitSetting>();
        public List<FishMaskRecord> masks = new List<FishMaskRecord>();
        public int maskRevision;
        public bool enhancedPondVisuals = true;
        public bool waterTypeTint = true;
        public bool algaeTint = true;
        public bool depthShading = true;
        public bool shorelineVisuals = true;
        public bool pondDecorations = true;
        public bool fishShadows = true;
        public bool underwaterFishTint = true;
        public bool surfaceOverlay = true;
        public bool caustics = true;
        public bool pseudoDepth = true;
        public bool pondRipples = true;

        public FishTraitSetting GetTraitSetting(FishTraitDef trait, bool create = true)
        {
            FishTraitSetting record = traitSettings.FirstOrDefault(setting => setting.traitDefName == trait.defName);
            if (record == null && create)
            {
                record = new FishTraitSetting(trait);
                traitSettings.Add(record);
            }
            return record;
        }

        public bool TraitEnabled(FishTraitDef trait) => GetTraitSetting(trait, false)?.enabled ?? true;
        public float TraitWeight(FishTraitDef trait) => Mathf.Max(0f, GetTraitSetting(trait, false)?.weight ?? trait.commonality);

        public FishMaskRecord GetMask(string defName, bool create = true)
        {
            FishMaskRecord record = masks.FirstOrDefault(mask => mask.fishDefName == defName);
            if (record == null && create)
            {
                record = new FishMaskRecord(defName);
                masks.Add(record);
            }
            return record;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref dataVersion, "dataVersion", 1);
            Scribe_Values.Look(ref enableResearchProgression, "enableResearchProgression", true);
            Scribe_Values.Look(ref fishingDurationFactor, "fishingDurationFactor", 0.30f);
            Scribe_Values.Look(ref showPondAlerts, "showPondAlerts", true);
            Scribe_Values.Look(ref globalMutationRate, "globalMutationRate", 0.20f);
            Scribe_Values.Look(ref maxMutations, "maxMutations", 2);
            Scribe_Values.Look(ref globalBreedingEnabled, "globalBreedingEnabled", true);
            Scribe_Values.Look(ref breedingIntervalDays, "breedingIntervalDays", 15f);
            Scribe_Values.Look(ref eggHatchDays, "eggHatchDays", 3f);
            Scribe_Values.Look(ref minimumOffspring, "minimumOffspring", 1);
            Scribe_Values.Look(ref maximumOffspring, "maximumOffspring", 3);
            Scribe_Values.Look(ref minimumLifespanDays, "minimumLifespanDays", 48);
            Scribe_Values.Look(ref maximumLifespanDays, "maximumLifespanDays", 72);
            Scribe_Values.Look(ref ecologyIntervalHours, "ecologyIntervalHours", 1f);
            Scribe_Values.Look(ref foodDemandMultiplier, "foodDemandMultiplier", 1f);
            Scribe_Values.Look(ref algaeGrowthMultiplier, "algaeGrowthMultiplier", 1f);
            Scribe_Values.Look(ref starvationHours, "starvationHours", 96f);
            Scribe_Values.Look(ref waterStressHours, "waterStressHours", 12f);
            Scribe_Values.Look(ref temperatureStressHours, "temperatureStressHours", 48f);
            Scribe_Values.Look(ref fishCapacityPerCell, "fishCapacityPerCell", 2f);
            Scribe_Values.Look(ref predationEnabled, "predationEnabled", true);
            Scribe_Values.Look(ref feedValuePerUnit, "feedValuePerUnit", 0.05f);
            Scribe_Values.Look(ref feederRange, "feederRange", 6f);
            Scribe_Values.Look(ref animaFishPerTree, "animaFishPerTree", 20);
            Scribe_Collections.Look(ref traitSettings, "traitSettings", LookMode.Deep);
            Scribe_Collections.Look(ref masks, "fishMasks", LookMode.Deep);
            Scribe_Values.Look(ref enhancedPondVisuals, "enhancedPondVisuals", true);
            Scribe_Values.Look(ref waterTypeTint, "waterTypeTint", true);
            Scribe_Values.Look(ref algaeTint, "algaeTint", true);
            Scribe_Values.Look(ref depthShading, "depthShading", true);
            Scribe_Values.Look(ref shorelineVisuals, "shorelineVisuals", true);
            Scribe_Values.Look(ref pondDecorations, "pondDecorations", true);
            Scribe_Values.Look(ref fishShadows, "fishShadows", true);
            Scribe_Values.Look(ref underwaterFishTint, "underwaterFishTint", true);
            Scribe_Values.Look(ref surfaceOverlay, "surfaceOverlay", true);
            Scribe_Values.Look(ref caustics, "caustics", true);
            Scribe_Values.Look(ref pseudoDepth, "pseudoDepth", true);
            Scribe_Values.Look(ref pondRipples, "pondRipples", true);
            if (traitSettings == null) traitSettings = new List<FishTraitSetting>();
            if (masks == null) masks = new List<FishMaskRecord>();
            dataVersion = 1;
            fishingDurationFactor = Mathf.Clamp(fishingDurationFactor, 0.1f, 1f);
            globalMutationRate = Mathf.Clamp01(globalMutationRate);
            maxMutations = Mathf.Clamp(maxMutations, 0, 8);
            breedingIntervalDays = Mathf.Clamp(breedingIntervalDays, 1f, 60f);
            eggHatchDays = Mathf.Clamp(eggHatchDays, 0.25f, 30f);
            minimumOffspring = Mathf.Clamp(minimumOffspring, 1, 10);
            maximumOffspring = Mathf.Clamp(maximumOffspring, minimumOffspring, 12);
            minimumLifespanDays = Mathf.Clamp(minimumLifespanDays, 5, 600);
            maximumLifespanDays = Mathf.Clamp(maximumLifespanDays, minimumLifespanDays, 1200);
            ecologyIntervalHours = Mathf.Clamp(ecologyIntervalHours, 0.25f, 6f);
            foodDemandMultiplier = Mathf.Clamp(foodDemandMultiplier, 0.1f, 5f);
            algaeGrowthMultiplier = Mathf.Clamp(algaeGrowthMultiplier, 0f, 5f);
            starvationHours = Mathf.Clamp(starvationHours, 6f, 600f);
            waterStressHours = Mathf.Clamp(waterStressHours, 1f, 240f);
            temperatureStressHours = Mathf.Clamp(temperatureStressHours, 1f, 240f);
            fishCapacityPerCell = Mathf.Clamp(fishCapacityPerCell, 0.25f, 10f);
            feedValuePerUnit = Mathf.Clamp(feedValuePerUnit, 0.005f, 1f);
            feederRange = Mathf.Clamp(feederRange, 1f, 30f);
            animaFishPerTree = Mathf.Clamp(animaFishPerTree, 1, 100);
        }
    }

    public sealed class AquacultureMod : Mod
    {
        private enum SettingsPage { General, Traits, Ecology, Breeding, Visuals, Masks }
        public static readonly HarmonyLib.Harmony Harmony = new HarmonyLib.Harmony("lan.aquaculture.fishing");
        public static AquacultureSettings Settings;
        private SettingsPage page;
        private Vector2 traitScroll;

        public AquacultureMod(ModContentPack content) : base(content) { Settings = GetSettings<AquacultureSettings>(); }
        public override string SettingsCategory() => "Aquaculture - Fishing";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect nav = new Rect(inRect.x, inRect.y, 210f, inRect.height);
            Rect content = new Rect(nav.xMax + 16f, inRect.y, inRect.width - nav.width - 16f, inRect.height);
            Widgets.DrawMenuSection(nav);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(nav.x + 12f, nav.y + 10f, nav.width - 24f, 36f), "Aquaculture");
            Text.Font = GameFont.Small;
            DrawNavButton(new Rect(nav.x + 8f, nav.y + 56f, nav.width - 16f, 40f), "General", SettingsPage.General);
            DrawNavButton(new Rect(nav.x + 8f, nav.y + 100f, nav.width - 16f, 40f), "Traits", SettingsPage.Traits);
            DrawNavButton(new Rect(nav.x + 8f, nav.y + 144f, nav.width - 16f, 40f), "Ecology", SettingsPage.Ecology);
            DrawNavButton(new Rect(nav.x + 8f, nav.y + 188f, nav.width - 16f, 40f), "Breeding", SettingsPage.Breeding);
            DrawNavButton(new Rect(nav.x + 8f, nav.y + 232f, nav.width - 16f, 40f), "Visuals", SettingsPage.Visuals);
            DrawNavButton(new Rect(nav.x + 8f, nav.y + 276f, nav.width - 16f, 40f), "Masks", SettingsPage.Masks);
            Widgets.DrawMenuSection(content);
            Rect body = content.ContractedBy(14f);
            if (page == SettingsPage.General) DrawGeneralPage(body);
            else if (page == SettingsPage.Traits) DrawTraitsPage(body);
            else if (page == SettingsPage.Ecology) DrawEcologyPage(body);
            else if (page == SettingsPage.Breeding) DrawBreedingPage(body);
            else if (page == SettingsPage.Masks) DrawMasksPage(body);
            else DrawVisualsPage(body);
        }

        private void DrawNavButton(Rect rect, string label, SettingsPage target)
        {
            if (page == target) Widgets.DrawHighlightSelected(rect);
            if (Widgets.ButtonInvisible(rect)) page = target;
            Widgets.Label(rect.ContractedBy(10f), label);
        }

        private static void DrawGeneralPage(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "General");
            Text.Font = GameFont.Small;
            float y = rect.y + 42f;
            DrawSettingToggle(rect, ref y, "Research Progression", "Require aquaculture research for pond construction, management, diagnostics, breeding controls, and automation.", ref Settings.enableResearchProgression);
            DrawSettingToggle(rect, ref y, "Pond Alerts", "Show alerts when a pond has starving fish, incompatible water, dangerous temperatures, or overcrowding.", ref Settings.showPondAlerts);
            DrawSettingSlider(rect, ref y, "Fishing Duration", ref Settings.fishingDurationFactor, 0.1f, 1f, value => value.ToStringPercent());
            DrawSettingSlider(rect, ref y, "Anima Fish Per Tree", ref Settings.animaFishPerTree, 1, 100, value => Mathf.RoundToInt(value).ToString());
            Widgets.Label(new Rect(rect.x, y + 8f, rect.width, 66f), "Progression can be disabled for sandbox play. Fishing still produces one fish per completed catch. Existing ponds continue to simulate when progression is enabled later.");
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 44f, 170f, 34f), "Reset General"))
            {
                Settings.enableResearchProgression = true;
                Settings.showPondAlerts = true;
                Settings.fishingDurationFactor = 0.30f;
                Settings.animaFishPerTree = 20;
            }
        }

        private void DrawTraitsPage(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Traits");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y + 38f, 250f, 28f), "Global Mutation Rate");
            Settings.globalMutationRate = Widgets.HorizontalSlider(new Rect(rect.x + 250f, rect.y + 43f, rect.width - 330f, 22f), Settings.globalMutationRate, 0f, 1f, true);
            Widgets.Label(new Rect(rect.xMax - 70f, rect.y + 38f, 70f, 28f), Settings.globalMutationRate.ToStringPercent());
            Widgets.Label(new Rect(rect.x, rect.y + 72f, 250f, 28f), "Max Mutations");
            Settings.maxMutations = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(rect.x + 250f, rect.y + 77f, rect.width - 330f, 22f), Settings.maxMutations, 0f, 8f, true));
            Widgets.Label(new Rect(rect.xMax - 70f, rect.y + 72f, 70f, 28f), Settings.maxMutations.ToString());
            Widgets.Label(new Rect(rect.x, rect.y + 106f, rect.width, 42f), "A successful mutation rolls between one and Max Mutations. Age, Sex, Diet, and Water are required traits and do not count toward this limit.");

            List<FishTraitDef> traits = DefDatabase<FishTraitDef>.AllDefsListForReading.OrderBy(def => def.kind).ThenBy(def => def.label).ToList();
            traits = traits.Where(def => def.mutationEligible).ToList();
            Rect outRect = new Rect(rect.x, rect.y + 154f, rect.width, rect.height - 154f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, traits.Count * 74f);
            Widgets.BeginScrollView(outRect, ref traitScroll, view);
            for (int i = 0; i < traits.Count; i++) DrawTraitSetting(new Rect(0f, i * 74f, view.width, 68f), traits[i]);
            Widgets.EndScrollView();
        }

        private static void DrawEcologyPage(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Pond Ecology");
            Text.Font = GameFont.Small;
            float y = rect.y + 42f;
            DrawSettingSlider(rect, ref y, "Simulation Interval", ref Settings.ecologyIntervalHours, 0.25f, 6f, value => value.ToString("0.##") + " h");
            DrawSettingSlider(rect, ref y, "Food Demand", ref Settings.foodDemandMultiplier, 0.1f, 5f, value => value.ToString("0.00") + "x");
            DrawSettingSlider(rect, ref y, "Algae Growth", ref Settings.algaeGrowthMultiplier, 0f, 5f, value => value.ToString("0.00") + "x");
            DrawSettingSlider(rect, ref y, "Starvation Time", ref Settings.starvationHours, 6f, 600f, value => Mathf.RoundToInt(value) + " h");
            DrawSettingSlider(rect, ref y, "Wrong Water Survival", ref Settings.waterStressHours, 1f, 240f, value => Mathf.RoundToInt(value) + " h");
            DrawSettingSlider(rect, ref y, "Temperature Survival", ref Settings.temperatureStressHours, 1f, 240f, value => Mathf.RoundToInt(value) + " h");
            DrawSettingSlider(rect, ref y, "Capacity Per Pond Cell", ref Settings.fishCapacityPerCell, 0.25f, 10f, value => value.ToString("0.00"));
            DrawSettingSlider(rect, ref y, "Feed Value Per Unit", ref Settings.feedValuePerUnit, 0.005f, 0.25f, value => value.ToString("0.000"));
            DrawSettingSlider(rect, ref y, "Automatic Feeder Range", ref Settings.feederRange, 1f, 30f, value => Mathf.RoundToInt(value) + " cells");
            DrawSettingToggle(rect, ref y, "Predation", "Carnivorous fish can consume smaller pond fish when no other food satisfies them.", ref Settings.predationEnabled);
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 44f, 170f, 34f), "Reset Ecology"))
            {
                Settings.ecologyIntervalHours = 1f;
                Settings.foodDemandMultiplier = 1f;
                Settings.algaeGrowthMultiplier = 1f;
                Settings.starvationHours = 96f;
                Settings.waterStressHours = 12f;
                Settings.temperatureStressHours = 48f;
                Settings.fishCapacityPerCell = 2f;
                Settings.predationEnabled = true;
                Settings.feedValuePerUnit = 0.05f;
                Settings.feederRange = 6f;
            }
        }

        private static void DrawBreedingPage(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Breeding And Lifespan");
            Text.Font = GameFont.Small;
            float y = rect.y + 42f;
            DrawSettingToggle(rect, ref y, "Enable Breeding", "Allow compatible adult male and female fish to reproduce in ponds.", ref Settings.globalBreedingEnabled);
            DrawSettingSlider(rect, ref y, "Days Between Breeding", ref Settings.breedingIntervalDays, 1f, 60f, value => Mathf.RoundToInt(value) + " days");
            DrawSettingSlider(rect, ref y, "Egg Hatch Time", ref Settings.eggHatchDays, 0.25f, 30f, value => value.ToString("0.##") + " days");
            DrawSettingSlider(rect, ref y, "Minimum Offspring", ref Settings.minimumOffspring, 1, 10, value => Mathf.RoundToInt(value).ToString());
            DrawSettingSlider(rect, ref y, "Maximum Offspring", ref Settings.maximumOffspring, 1, 12, value => Mathf.RoundToInt(value).ToString());
            Settings.maximumOffspring = Mathf.Max(Settings.minimumOffspring, Settings.maximumOffspring);
            DrawSettingSlider(rect, ref y, "Minimum Lifespan", ref Settings.minimumLifespanDays, 5, 600, value => Mathf.RoundToInt(value) + " days");
            DrawSettingSlider(rect, ref y, "Maximum Lifespan", ref Settings.maximumLifespanDays, 5, 1200, value => Mathf.RoundToInt(value) + " days");
            Settings.maximumLifespanDays = Mathf.Max(Settings.minimumLifespanDays, Settings.maximumLifespanDays);
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 44f, 170f, 34f), "Reset Breeding"))
            {
                Settings.globalBreedingEnabled = true;
                Settings.breedingIntervalDays = 15f;
                Settings.eggHatchDays = 3f;
                Settings.minimumOffspring = 1;
                Settings.maximumOffspring = 3;
                Settings.minimumLifespanDays = 48;
                Settings.maximumLifespanDays = 72;
            }
        }

        private static void DrawSettingToggle(Rect rect, ref float y, string label, string description, ref bool value)
        {
            Rect row = new Rect(rect.x, y, rect.width, 46f);
            Widgets.CheckboxLabeled(new Rect(row.x, row.y, row.width * 0.42f, 30f), label, ref value);
            Color old = GUI.color;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(row.x + row.width * 0.44f, row.y, row.width * 0.56f, 42f), description);
            GUI.color = old;
            y += 48f;
        }

        private static void DrawSettingSlider(Rect rect, ref float y, string label, ref float value, float minimum, float maximum, Func<float, string> formatter)
        {
            Widgets.Label(new Rect(rect.x, y, 230f, 28f), label);
            value = Widgets.HorizontalSlider(new Rect(rect.x + 230f, y + 5f, rect.width - 315f, 20f), value, minimum, maximum, true);
            Widgets.Label(new Rect(rect.xMax - 80f, y, 80f, 28f), formatter(value));
            y += 38f;
        }

        private static void DrawSettingSlider(Rect rect, ref float y, string label, ref int value, int minimum, int maximum, Func<float, string> formatter)
        {
            float slider = value;
            DrawSettingSlider(rect, ref y, label, ref slider, minimum, maximum, formatter);
            value = Mathf.RoundToInt(slider);
        }

        private static void DrawTraitSetting(Rect rect, FishTraitDef trait)
        {
            FishTraitSetting setting = Settings.GetTraitSetting(trait);
            Widgets.DrawMenuSection(rect);
            Widgets.Checkbox(new Vector2(rect.x + 10f, rect.y + 11f), ref setting.enabled);
            Widgets.Label(new Rect(rect.x + 42f, rect.y + 7f, rect.width * 0.48f, 26f), trait.LabelCap);
            Color old = GUI.color;
            GUI.color = new Color(0.70f, 0.70f, 0.70f);
            Widgets.Label(new Rect(rect.x + 42f, rect.y + 34f, rect.width * 0.54f, 25f), FishTraitUtility.EffectLine(trait));
            GUI.color = old;
            Widgets.Label(new Rect(rect.x + rect.width * 0.61f, rect.y + 7f, 88f, 26f), "Weight " + setting.weight.ToString("0.00"));
            setting.weight = Widgets.HorizontalSlider(new Rect(rect.x + rect.width * 0.61f, rect.y + 38f, rect.width * 0.36f, 20f), setting.weight, 0f, 5f, true);
            TooltipHandler.TipRegion(rect, trait.description);
        }

        private static void DrawMasksPage(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Variegation Masks");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y + 40f, rect.width, 68f), "Paint separate Spotted and Striped masks for every texture used by each loaded fish, including single-item and stack graphics.");
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + 116f, 220f, 38f), "Open Mask Painter")) Find.WindowStack.Add(new Dialog_FishMaskPainter());
        }

        private static void DrawVisualsPage(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Pond Visuals");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y + 38f, rect.width, 42f), "These options affect rendering only and can be changed without rebuilding ponds.");
            Rect master = new Rect(rect.x, rect.y + 84f, rect.width, 34f);
            Widgets.CheckboxLabeled(master, "Enable Enhanced Pond Visuals", ref Settings.enhancedPondVisuals);
            if (!Settings.enhancedPondVisuals)
            {
                Color old = GUI.color;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, rect.y + 126f, rect.width, 48f), "RimWorld's underlying shallow-water terrain remains visible. Enable this option to configure additional pond rendering.");
                GUI.color = old;
                return;
            }

            float y = rect.y + 130f;
            DrawVisualToggle(rect, ref y, "Water Type Tint", "Distinct freshwater, saltwater, and brackishwater coloration.", ref Settings.waterTypeTint);
            DrawVisualToggle(rect, ref y, "Algae Tint", "Water becomes greener as the pond's algae biomass rises.", ref Settings.algaeTint);
            DrawVisualToggle(rect, ref y, "Depth Shading", "Darkens cells farther from the pond shoreline.", ref Settings.depthShading);
            DrawVisualToggle(rect, ref y, "Shoreline", "Adds a narrow natural rim and shallow-water highlight.", ref Settings.shorelineVisuals);
            DrawVisualToggle(rect, ref y, "Decorations", "Shows deterministic lily pads, reeds, and stones.", ref Settings.pondDecorations);
            DrawVisualToggle(rect, ref y, "Fish Shadows", "Shows soft submerged shadows beneath swimming fish.", ref Settings.fishShadows);
            DrawVisualToggle(rect, ref y, "Underwater Fish Tint", "Blends fish toward their pond's water color while preserving trait visuals.", ref Settings.underwaterFishTint);
            DrawVisualToggle(rect, ref y, "Surface Overlay", "Draws a translucent water surface above fish and below pawns.", ref Settings.surfaceOverlay);
            DrawVisualToggle(rect, ref y, "Caustics", "Adds restrained animated light bands across the water surface.", ref Settings.caustics);
            DrawVisualToggle(rect, ref y, "Pseudo-Depth", "Fish periodically move between shallow and deeper visual states.", ref Settings.pseudoDepth);
            DrawVisualToggle(rect, ref y, "Ripples", "Shows occasional surface ripples near shallow fish.", ref Settings.pondRipples);
        }

        private static void DrawVisualToggle(Rect rect, ref float y, string label, string description, ref bool value)
        {
            Rect row = new Rect(rect.x, y, rect.width, 40f);
            Widgets.CheckboxLabeled(new Rect(row.x, row.y, row.width * 0.42f, 30f), label, ref value);
            Color old = GUI.color;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(row.x + row.width * 0.44f, row.y, row.width * 0.56f, 36f), description);
            GUI.color = old;
            y += 42f;
        }

        public override void WriteSettings()
        {
            Settings.maskRevision++;
            FishVisualCache.Clear();
            if (Current.Game?.Maps != null)
                for (int i = 0; i < Current.Game.Maps.Count; i++) Current.Game.Maps[i].GetComponent<FishPondMapComponent>()?.InvalidatePondMenus();
            base.WriteSettings();
        }
    }

    public sealed class FishTextureVariation
    {
        public string key;
        public string label;
        public Texture2D texture;
        public Graphic graphic;
    }

    public static class FishGraphicUtility
    {
        private static readonly Dictionary<ThingDef, List<FishTextureVariation>> Cache = new Dictionary<ThingDef, List<FishTextureVariation>>();
        private static readonly System.Reflection.FieldInfo SubGraphicsField = AccessTools.Field(typeof(Graphic_Collection), "subGraphics");

        public static List<FishTextureVariation> Variations(ThingDef def)
        {
            if (def != null && Cache.TryGetValue(def, out List<FishTextureVariation> cached)) return cached;
            var result = new List<FishTextureVariation>();
            AddGraphic(result, def?.graphicData?.Graphic, "Single texture");
            if (result.Count == 0 && def?.uiIcon != null) AddTexture(result, def.uiIcon, null, "UI texture");
            if (def != null) Cache[def] = result;
            return result;
        }

        private static void AddGraphic(List<FishTextureVariation> result, Graphic graphic, string label)
        {
            Graphic[] children = graphic is Graphic_Collection ? SubGraphicsField?.GetValue(graphic) as Graphic[] : null;
            if (children != null && children.Length > 0)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    string childLabel;
                    if (graphic is Graphic_StackCount)
                        childLabel = i == 0 ? "Single item" : i == children.Length - 1 ? "Full stack" : "Stack level " + i;
                    else if (graphic is Graphic_Random)
                        childLabel = "Random variation " + (i + 1) + " of " + children.Length;
                    else childLabel = label + " " + (i + 1);
                    AddGraphic(result, children[i], childLabel);
                }
                return;
            }
            AddTexture(result, graphic?.MatSingle?.mainTexture as Texture2D, graphic, label);
        }

        private static void AddTexture(List<FishTextureVariation> result, Texture2D texture, Graphic graphic, string label)
        {
            if (texture == null || result.Any(item => item.texture.GetInstanceID() == texture.GetInstanceID())) return;
            result.Add(new FishTextureVariation { key = texture.name, label = label, texture = texture, graphic = graphic });
        }

        public static FishTextureVariation VariationFor(Thing thing, Graphic sourceGraphic = null)
        {
            if (thing == null) return null;
            Graphic graphic = sourceGraphic ?? thing.DefaultGraphic;
            if (graphic is Graphic_StackCount stack) graphic = stack.SubGraphicFor(thing);
            else if (graphic is Graphic_Random random) graphic = random.SubGraphicFor(thing);
            Texture texture = graphic?.MatAt(thing.Rotation, thing)?.mainTexture;
            return Variations(thing.def).FirstOrDefault(item => item.texture == texture || item.texture.name == texture?.name) ?? Variations(thing.def).FirstOrDefault();
        }
    }

    public sealed class Dialog_FishMaskPainter : Window
    {
        private ThingDef selected;
        private FishPattern selectedPattern = FishPattern.Spotted;
        private int variationIndex;
        private bool erase;
        private float brushSize = 3f;
        private Vector2 fishScroll;
        private string search = "";
        private FishPatternMaskRecord overlayMask;
        private Texture2D overlayTexture;
        private bool masksChanged;

        public override Vector2 InitialSize => new Vector2(1120f, 780f);
        protected override float Margin => 18f;
        public Dialog_FishMaskPainter() { doCloseX = true; closeOnAccept = false; absorbInputAroundWindow = true; }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 40f), "Fish Variegation Mask Painter");
            Text.Font = GameFont.Small;
            Rect left = new Rect(0f, 48f, 280f, rect.height - 48f);
            Rect right = new Rect(300f, 48f, rect.width - 300f, rect.height - 48f);
            DrawFishList(left);
            if (selected == null) Widgets.Label(right, "Select a loaded fish.");
            else DrawPainter(right);
        }

        private void DrawFishList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 25f), "Loaded Fish");
            search = Widgets.TextField(new Rect(rect.x + 10f, rect.y + 38f, rect.width - 20f, 30f), search);
            List<ThingDef> fish = DefDatabase<ThingDef>.AllDefs.Where(FishUtility.IsFish)
                .Where(def => search.NullOrEmpty() || def.LabelCap.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(def => def.label).ToList();
            Rect outRect = new Rect(rect.x + 8f, rect.y + 76f, rect.width - 16f, rect.height - 84f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, fish.Count * 34f);
            Widgets.BeginScrollView(outRect, ref fishScroll, view);
            for (int i = 0; i < fish.Count; i++)
            {
                Rect row = new Rect(0f, i * 34f, view.width, 32f);
                if (selected == fish[i]) Widgets.DrawHighlightSelected(row);
                if (Widgets.ButtonInvisible(row)) { selected = fish[i]; variationIndex = 0; }
                Widgets.Label(row.ContractedBy(6f), fish[i].LabelCap);
            }
            Widgets.EndScrollView();
        }

        private void DrawPainter(Rect rect)
        {
            List<FishTextureVariation> variations = FishGraphicUtility.Variations(selected);
            if (variations.Count == 0) { Widgets.Label(rect, "No usable textures were found for this fish."); return; }
            variationIndex = Mathf.Clamp(variationIndex, 0, variations.Count - 1);
            FishTextureVariation variation = variations[variationIndex];
            FishMaskRecord fishMask = AquacultureMod.Settings.GetMask(selected.defName);
            FishPatternMaskRecord mask = fishMask.GetMask(variation.key, selectedPattern, variation.texture.width, variation.texture.height);

            Widgets.DrawMenuSection(rect);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 8f, 250f, 30f), selected.LabelCap + " - " + selectedPattern);
            if (Widgets.ButtonText(new Rect(rect.x + 12f, rect.y + 44f, 130f, 34f), selectedPattern.ToString()))
                selectedPattern = selectedPattern == FishPattern.Spotted ? FishPattern.Striped : FishPattern.Spotted;
            if (Widgets.ButtonText(new Rect(rect.x + 154f, rect.y + 44f, 100f, 34f), erase ? "Erase" : "Paint")) erase = !erase;
            if (Widgets.ButtonText(new Rect(rect.x + 266f, rect.y + 44f, 62f, 34f), "Prev")) variationIndex = (variationIndex - 1 + variations.Count) % variations.Count;
            if (Widgets.ButtonText(new Rect(rect.x + 334f, rect.y + 44f, 62f, 34f), "Next")) variationIndex = (variationIndex + 1) % variations.Count;
            Widgets.Label(new Rect(rect.x + 408f, rect.y + 48f, 170f, 28f), variation.label + " (" + variation.texture.width + "x" + variation.texture.height + ")");
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 84f, 100f, 28f), "Brush: " + Mathf.RoundToInt(brushSize) + " px");
            brushSize = Widgets.HorizontalSlider(new Rect(rect.x + 112f, rect.y + 89f, 180f, 20f), brushSize, 1f, 24f, true);
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 142f, rect.y + 82f, 130f, 34f), "Clear Mask"))
            {
                Array.Clear(mask.pixels, 0, mask.pixels.Length);
                masksChanged = true;
                RebuildOverlay(mask);
            }

            float canvasSize = Mathf.Min(600f, rect.height - 146f);
            Rect canvas = new Rect(rect.x + 24f, rect.y + 126f, canvasSize, canvasSize);
            Widgets.DrawBoxSolid(canvas, new Color(0.10f, 0.12f, 0.12f));
            GUI.DrawTexture(canvas, variation.texture, ScaleMode.StretchToFill, true);
            DrawMaskOverlay(canvas, mask);
            HandlePainting(canvas, mask);
            Widgets.DrawBox(canvas);
        }

        private void DrawMaskOverlay(Rect canvas, FishPatternMaskRecord mask)
        {
            EnsureOverlay(mask);
            if (overlayTexture != null) GUI.DrawTexture(canvas, overlayTexture, ScaleMode.StretchToFill, true);
        }

        private void EnsureOverlay(FishPatternMaskRecord mask)
        {
            if (overlayMask == mask && overlayTexture != null) return;
            RebuildOverlay(mask);
        }

        private void RebuildOverlay(FishPatternMaskRecord mask)
        {
            if (overlayTexture != null) UnityEngine.Object.Destroy(overlayTexture);
            overlayMask = mask;
            if (mask == null) { overlayTexture = null; return; }
            overlayTexture = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false);
            overlayTexture.filterMode = FilterMode.Point;
            Color32[] colors = new Color32[mask.pixels.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = MaskOverlayColor(mask.pixels[i]);
            overlayTexture.SetPixels32(colors);
            overlayTexture.Apply(false, false);
        }

        private static Color32 MaskOverlayColor(byte value) => value == 0
            ? new Color32(0, 0, 0, 0)
            : new Color32(40, 220, 255, 125);

        private void HandlePainting(Rect canvas, FishPatternMaskRecord mask)
        {
            Event ev = Event.current;
            if (!(ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) || ev.button != 0 || !canvas.Contains(ev.mousePosition)) return;
            int cx = Mathf.Clamp(Mathf.FloorToInt((ev.mousePosition.x - canvas.x) / canvas.width * mask.width), 0, mask.width - 1);
            int cy = Mathf.Clamp(mask.height - 1 - Mathf.FloorToInt((ev.mousePosition.y - canvas.y) / canvas.height * mask.height), 0, mask.height - 1);
            int radius = Mathf.Max(0, Mathf.RoundToInt(brushSize) - 1);
            EnsureOverlay(mask);
            bool changed = false;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if (x >= 0 && y >= 0 && x < mask.width && y < mask.height && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                    {
                        int index = y * mask.width + x;
                        byte value = erase ? (byte)0 : (byte)255;
                        if (mask.pixels[index] == value) continue;
                        mask.pixels[index] = value;
                        overlayTexture.SetPixel(x, y, MaskOverlayColor(value));
                        changed = true;
                    }
            if (changed)
            {
                overlayTexture.Apply(false, false);
                masksChanged = true;
            }
            ev.Use();
        }

        public override void PostClose()
        {
            if (overlayTexture != null) UnityEngine.Object.Destroy(overlayTexture);
            if (masksChanged)
            {
                AquacultureMod.Settings.maskRevision++;
                FishVisualCache.Clear();
            }
            base.PostClose();
            AquacultureMod.Settings.Write();
        }
    }
}
