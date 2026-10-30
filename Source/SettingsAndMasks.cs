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
        public const int CurrentCapacityModelVersion = 2;
        public const int CurrentTraitBreedingSettingsVersion = 1;
        public const int CurrentPresentationSettingsVersion = 1;
        public const float DefaultWildExceptionalTraitChance = 0.20f;
        public const float DefaultParentalTraitInheritanceChance = 0.55f;
        public const int DefaultMaximumInheritedTraits = 2;
        public const float DefaultOffspringMutationChance = 0.05f;
        public const int DefaultMaximumOffspringMutations = 1;
        public const float DefaultRegisteredBreedDefiningTraitReliability = 0.95f;
        public int dataVersion = 1;
        public bool enableResearchProgression = true;
        public float fishingDurationFactor = 0.30f;
        public bool showPondAlerts = true;
        // These keys remain serialized so old settings files continue to load during migration.
        public float globalMutationRate = DefaultWildExceptionalTraitChance;
        public int maxMutations = DefaultMaximumInheritedTraits;
        public int traitBreedingSettingsVersion;
        public float wildExceptionalTraitChance = DefaultWildExceptionalTraitChance;
        public float parentalTraitInheritanceChance = DefaultParentalTraitInheritanceChance;
        public int maxInheritedTraits = DefaultMaximumInheritedTraits;
        public float offspringMutationChance = DefaultOffspringMutationChance;
        public int maxOffspringMutations = DefaultMaximumOffspringMutations;
        public float registeredBreedDefiningTraitReliability = DefaultRegisteredBreedDefiningTraitReliability;
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
        public float fishCapacityPerCell = PondCapacityRules.DefaultCapacityPerCell;
        public int capacityModelVersion = CurrentCapacityModelVersion;
        public bool capacityTransitionWarning;
        public bool predationEnabled = true;
        public float feedValuePerUnit = 0.05f;
        public float feederRange = 6f;
        public int animaFishPerTree = 20;
        public List<FishTraitSetting> traitSettings = new List<FishTraitSetting>();
        public List<FishExpertiseSetting> fishExpertiseSettings = new List<FishExpertiseSetting>();
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
        // Aquaculture-owned player presentation preferences. These are intentionally separate from
        // Insight Canvas global settings and do not change gameplay or balance semantics.
        public int presentationDensity = AquaculturePresentationPreferences.DefaultDensityIndex;
        public bool presentationHighContrast;
        public bool presentationReducedMotion;
        public int presentationSettingsVersion;

        public AquaculturePresentationPreferences PresentationPreferences =>
            AquaculturePresentationPreferences.FromSerializedValues(
                presentationDensity, presentationHighContrast, presentationReducedMotion);

        public void SetPresentationPreferences(AquaculturePresentationPreferences preferences)
        {
            presentationDensity = preferences.DensityIndex;
            presentationHighContrast = preferences.HighContrast;
            presentationReducedMotion = preferences.ReducedMotion;
        }

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

        public FishExpertiseSetting GetFishExpertiseSetting(ThingDef fishDef, bool create = true)
        {
            if (fishDef == null) return null;
            FishExpertiseSetting record = fishExpertiseSettings.FirstOrDefault(setting => setting.fishDefName == fishDef.defName);
            if (record == null && create)
            {
                record = new FishExpertiseSetting(fishDef);
                fishExpertiseSettings.Add(record);
            }
            return record;
        }

        public FishingRank MinimumExpertiseFor(ThingDef fishDef)
        {
            return GetFishExpertiseSetting(fishDef, false)?.minimumFishingExpertise ?? FishingRank.Novice;
        }

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
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                traitBreedingSettingsVersion = CurrentTraitBreedingSettingsVersion;
                presentationSettingsVersion = CurrentPresentationSettingsVersion;
            }
            Scribe_Values.Look(ref dataVersion, "dataVersion", 1);
            Scribe_Values.Look(ref enableResearchProgression, "enableResearchProgression", true);
            Scribe_Values.Look(ref fishingDurationFactor, "fishingDurationFactor", 0.30f);
            Scribe_Values.Look(ref showPondAlerts, "showPondAlerts", true);
            Scribe_Values.Look(ref globalMutationRate, "globalMutationRate", 0.20f);
            Scribe_Values.Look(ref maxMutations, "maxMutations", 2);
            Scribe_Values.Look(ref traitBreedingSettingsVersion, "traitBreedingSettingsVersion", 0);
            Scribe_Values.Look(ref wildExceptionalTraitChance, "wildExceptionalTraitChance", DefaultWildExceptionalTraitChance);
            Scribe_Values.Look(ref parentalTraitInheritanceChance, "parentalTraitInheritanceChance", DefaultParentalTraitInheritanceChance);
            Scribe_Values.Look(ref maxInheritedTraits, "maxInheritedTraits", DefaultMaximumInheritedTraits);
            Scribe_Values.Look(ref offspringMutationChance, "offspringMutationChance", DefaultOffspringMutationChance);
            Scribe_Values.Look(ref maxOffspringMutations, "maxOffspringMutations", DefaultMaximumOffspringMutations);
            Scribe_Values.Look(ref registeredBreedDefiningTraitReliability, "registeredBreedDefiningTraitReliability", DefaultRegisteredBreedDefiningTraitReliability);
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
            Scribe_Values.Look(ref fishCapacityPerCell, "fishCapacityPerCell", PondCapacityRules.DefaultCapacityPerCell);
            Scribe_Values.Look(ref capacityModelVersion, "capacityModelVersion", 0);
            Scribe_Values.Look(ref capacityTransitionWarning, "capacityTransitionWarning", false);
            Scribe_Values.Look(ref predationEnabled, "predationEnabled", true);
            Scribe_Values.Look(ref feedValuePerUnit, "feedValuePerUnit", 0.05f);
            Scribe_Values.Look(ref feederRange, "feederRange", 6f);
            Scribe_Values.Look(ref animaFishPerTree, "animaFishPerTree", 20);
            Scribe_Collections.Look(ref traitSettings, "traitSettings", LookMode.Deep);
            Scribe_Collections.Look(ref fishExpertiseSettings, "fishExpertiseSettings", LookMode.Deep);
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
            Scribe_Values.Look(ref presentationDensity, "presentationDensity",
                AquaculturePresentationPreferences.DefaultDensityIndex);
            Scribe_Values.Look(ref presentationHighContrast, "presentationHighContrast", false);
            Scribe_Values.Look(ref presentationReducedMotion, "presentationReducedMotion", false);
            Scribe_Values.Look(ref presentationSettingsVersion, "presentationSettingsVersion", 0);
            if (traitSettings == null) traitSettings = new List<FishTraitSetting>();
            if (fishExpertiseSettings == null) fishExpertiseSettings = new List<FishExpertiseSetting>();
            if (masks == null) masks = new List<FishMaskRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && traitBreedingSettingsVersion < CurrentTraitBreedingSettingsVersion)
            {
                // The old pair controlled one shared roll. Keep its player-selected values as the
                // least surprising starting point for the new wild chance and inherited cap.
                TraitBreedingRules.LegacySettingMigration migration = TraitBreedingRules.MigrateLegacySettings(
                    traitBreedingSettingsVersion, globalMutationRate, maxMutations,
                    wildExceptionalTraitChance, maxInheritedTraits);
                wildExceptionalTraitChance = migration.WildExceptionalTraitChance;
                maxInheritedTraits = migration.MaximumInheritedTraits;
                traitBreedingSettingsVersion = CurrentTraitBreedingSettingsVersion;
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && capacityModelVersion < CurrentCapacityModelVersion)
            {
                if (fishCapacityPerCell > PondCapacityRules.DefaultCapacityPerCell + 0.001f)
                    capacityTransitionWarning = true;
                capacityModelVersion = CurrentCapacityModelVersion;
            }
            else if (Scribe.mode == LoadSaveMode.Saving)
            {
                capacityModelVersion = CurrentCapacityModelVersion;
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                presentationSettingsVersion < CurrentPresentationSettingsVersion)
            {
                // There were no earlier serialized presentation keys. Missing values have already
                // received the safe defaults above; retain any valid values and advance the marker.
                presentationSettingsVersion = CurrentPresentationSettingsVersion;
            }
            dataVersion = 1;
            fishingDurationFactor = Mathf.Clamp(fishingDurationFactor, 0.1f, 1f);
            globalMutationRate = Mathf.Clamp01(globalMutationRate);
            maxMutations = Mathf.Clamp(maxMutations, 0, 8);
            wildExceptionalTraitChance = Mathf.Clamp01(wildExceptionalTraitChance);
            parentalTraitInheritanceChance = Mathf.Clamp01(parentalTraitInheritanceChance);
            maxInheritedTraits = Mathf.Clamp(maxInheritedTraits, 0, 8);
            offspringMutationChance = Mathf.Clamp01(offspringMutationChance);
            maxOffspringMutations = Mathf.Clamp(maxOffspringMutations, 0, 8);
            registeredBreedDefiningTraitReliability = Mathf.Clamp01(registeredBreedDefiningTraitReliability);
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
            presentationDensity = AquaculturePresentationPreferences.ClampDensityIndex(presentationDensity);
            if (Scribe.mode == LoadSaveMode.Saving)
                presentationSettingsVersion = CurrentPresentationSettingsVersion;
        }
    }

    /// <summary>
    /// Aquaculture-owned settings renderer contract. The Mod owns saved settings,
    /// while the selected view owns only its presentation and redraw lifecycle.
    /// </summary>
    public interface IAquacultureSettingsView
    {
        void Draw(Rect rect);
        void OnSettingsWritten();
    }

    public sealed class AquacultureMod : Mod
    {
        public static readonly HarmonyLib.Harmony Harmony = new HarmonyLib.Harmony("lan.aquaculture.fishing");
        public static AquacultureSettings Settings;
        private IAquacultureSettingsView insightSettingsDocument;

        public AquacultureMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AquacultureSettings>();
        }
        public override string SettingsCategory() => "Aquaculture - Fishing";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (insightSettingsDocument == null)
                insightSettingsDocument = AquacultureSettingsViewFactory.Create(Settings);
            insightSettingsDocument.Draw(inRect.ContractedBy(8f));
        }

        public override void WriteSettings()
        {
            Settings.maskRevision++;
            FishVisualCache.Clear();
            if (Current.Game?.Maps != null)
                for (int i = 0; i < Current.Game.Maps.Count; i++) Current.Game.Maps[i].GetComponent<FishPondMapComponent>()?.InvalidatePondMenus();
            insightSettingsDocument?.OnSettingsWritten();
            base.WriteSettings();
        }
    }


    /// <summary>
    /// Source-only native settings prototype for six settings pages.
    /// The existing Canvas factory remains the player default pending actual
    /// RimWorld validation; all field ownership stays in AquacultureSettings.
    /// </summary>
    public sealed class NativeAquacultureSettingsView : IAquacultureSettingsView
    {
        private readonly AquacultureSettings settings;
        private readonly List<ThingDef> fishCatalog;
        private readonly List<FishTraitDef> traitCatalog;
        private Vector2 scrollPosition;
        private float visibleHeight;
        private string page = "gameplay";
        private string fishSearch = string.Empty;
        private string traitSearch = string.Empty;

        public NativeAquacultureSettingsView(AquacultureSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            // Exactly the same editable catalog visibility as the Canvas
            // settings document. Never create settings records on paint.
            fishCatalog = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def != null && FishUtility.IsFish(def))
                .OrderBy(def => def.label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(def => def.defName, StringComparer.Ordinal).ToList();
            traitCatalog = DefDatabase<FishTraitDef>.AllDefsListForReading
                .Where(def => def != null && def.mutationEligible &&
                    !FishTraitUtility.IsDemographicOrEcology(def.defName))
                .OrderBy(def => def.kind)
                .ThenBy(def => def.label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(def => def.defName, StringComparer.Ordinal).ToList();
        }

        public void OnSettingsWritten()
        {
            scrollPosition = Vector2.zero;
            fishSearch = string.Empty;
            traitSearch = string.Empty;
        }

        public void Draw(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect padded = rect.ContractedBy(8f);
            if (padded.width <= 0f || padded.height <= 0f) return;
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(padded.x, padded.y, padded.width, 32f),
                    L("AquacultureFishing.SettingsTitle"));
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(padded.x, padded.y + 36f,
                    padded.width, 34f), PageTitle()))
                {
                    ChoosePage();
                    return;
                }

                Rect viewport = new Rect(padded.x, padded.y + 78f, padded.width,
                    Mathf.Max(0f, padded.height - 78f));
                if (viewport.width <= 0f || viewport.height <= 0f) return;
                float viewWidth = Mathf.Max(80f, viewport.width - 18f);

                // Catalog pages compute their scroll height from visible
                // rows. General-page sizing stays unchanged from B5A.
                float pageHeight = page == "fishing" ? FishingContentHeight(viewWidth) :
                    page == "advanced" ? AdvancedContentHeight(viewWidth) :
                    page == "ecology" ? 1100f : page == "appearance" ? 800f :
                    page == "breeding" ? 720f : 500f;
                Rect content = new Rect(0f, 0f, viewWidth,
                    Mathf.Max(viewport.height, pageHeight));
                scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f,
                    Mathf.Max(0f, content.height - viewport.height));
                visibleHeight = viewport.height;
                Widgets.BeginScrollView(viewport, ref scrollPosition, content);
                try
                {
                    float y = 8f;
                    switch (page)
                    {
                        case "fishing": DrawFishing(ref y, viewWidth); break;
                        case "ecology": DrawEcology(ref y, viewWidth); break;
                        case "breeding": DrawBreeding(ref y, viewWidth); break;
                        case "appearance": DrawAppearance(ref y, viewWidth); break;
                        case "advanced": DrawAdvanced(ref y, viewWidth); break;
                        default: DrawGameplay(ref y, viewWidth); break;
                    }
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        private string PageTitle()
        {
            switch (page)
            {
                case "fishing": return L("AquacultureFishing.SettingsFishing");
                case "ecology": return L("AquacultureFishing.SettingsEcology");
                case "breeding": return L("AquacultureFishing.SettingsBreeding");
                case "appearance": return L("AquacultureFishing.SettingsAppearance");
                case "advanced": return L("AquacultureFishing.SettingsAdvanced");
                default: return L("AquacultureFishing.SettingsGameplay");
            }
        }

        private void ChoosePage()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption(L("AquacultureFishing.SettingsGameplay"), () => SelectPage("gameplay")),
                new FloatMenuOption(L("AquacultureFishing.SettingsFishing"), () => SelectPage("fishing")),
                new FloatMenuOption(L("AquacultureFishing.SettingsEcology"), () => SelectPage("ecology")),
                new FloatMenuOption(L("AquacultureFishing.SettingsBreeding"), () => SelectPage("breeding")),
                new FloatMenuOption(L("AquacultureFishing.SettingsAppearance"), () => SelectPage("appearance")),
                new FloatMenuOption(L("AquacultureFishing.SettingsAdvanced"), () => SelectPage("advanced"))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SelectPage(string selected)
        {
            page = selected;
            scrollPosition = Vector2.zero;
        }

        private void DrawGameplay(ref float y, float width)
        {
            Section(ref y, width, "AquacultureFishing.SettingsGameplay",
                "AquacultureFishing.SettingsGameplaySubtitle");
            Toggle(ref y, width, "AquacultureFishing.SettingsResearchProgression",
                "AquacultureFishing.SettingsResearchProgressionTip",
                ref settings.enableResearchProgression);
            Toggle(ref y, width, "AquacultureFishing.SettingsPondAlerts",
                "AquacultureFishing.SettingsPondAlertsTip", ref settings.showPondAlerts);
            Slider(ref y, width, "AquacultureFishing.SettingsFishingDuration",
                "AquacultureFishing.SettingsFishingDurationTip",
                ref settings.fishingDurationFactor, 0.1f, 1f);
            IntSlider(ref y, width, "AquacultureFishing.SettingsAnimaFishPerTree",
                "AquacultureFishing.SettingsAnimaFishPerTreeTip",
                ref settings.animaFishPerTree, 1, 100);
            Note(ref y, width, L("AquacultureFishing.SettingsGameplayScopeBody"));
            ResetButton(ref y, width, "AquacultureFishing.SettingsResetGameplay", ResetGameplay);
        }

        // Native Fishing and Advanced page helpers follow.
        private List<ThingDef> FilterFishCatalog()
        {
            return fishCatalog.Where(def => def != null &&
                (string.IsNullOrEmpty(fishSearch) ||
                def.label.IndexOf(fishSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                def.defName.IndexOf(fishSearch, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }

        private List<FishTraitDef> FilterTraitCatalog()
        {
            return traitCatalog.Where(trait => trait != null &&
                (string.IsNullOrEmpty(traitSearch) ||
                trait.label.IndexOf(traitSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                trait.defName.IndexOf(traitSearch, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }

        private static float FishRowHeight(string label, float width)
        {
            return Mathf.Max(78f, Text.CalcHeight(label ?? string.Empty,
                Mathf.Max(20f, width - 12f)) + 46f);
        }

        private static float TraitRowHeight(string label, float width)
        {
            return Mathf.Max(136f, Text.CalcHeight(label ?? string.Empty,
                Mathf.Max(20f, width - 12f)) + 104f);
        }

        private float FishingContentHeight(float width)
        {
            return 260f + FilterFishCatalog().Sum(def =>
                FishRowHeight(def.LabelCap.ToString(), width) + 6f);
        }

        private float AdvancedContentHeight(float width)
        {
            return 1400f + FilterTraitCatalog().Sum(trait =>
                TraitRowHeight(trait.LabelCap.ToString(), width) + 6f);
        }

        private bool IsVisible(float y, float height)
        {
            return y + height >= scrollPosition.y && y <= scrollPosition.y + visibleHeight;
        }

        private static void SearchField(ref float y, float width, string labelKey,
            ref string filter, ref Vector2 scroll)
        {
            Widgets.Label(new Rect(6f, y, Mathf.Max(20f, width - 12f), 24f), L(labelKey));
            y += 26f;
            string updated = Widgets.TextField(
                new Rect(6f, y, Mathf.Max(20f, width - 12f), 30f), filter);
            if (!string.Equals(updated, filter, StringComparison.Ordinal))
            {
                filter = updated ?? string.Empty;
                scroll = Vector2.zero;
            }
            y += 42f;
        }

        private void DrawFishing(ref float y, float width)
        {
            Section(ref y, width, "AquacultureFishing.SettingsFishing",
                "AquacultureFishing.SettingsFishingExplanation");
            SearchField(ref y, width, "AquacultureFishing.SettingsSearchFish",
                ref fishSearch, ref scrollPosition);
            List<ThingDef> fish = FilterFishCatalog();
            if (fish.Count == 0)
                Note(ref y, width, L("AquacultureFishing.PondPlannerNoAvailableSpecies"));
            foreach (ThingDef definition in fish)
            {
                float height = FishRowHeight(definition.LabelCap.ToString(), width);
                if (IsVisible(y, height))
                {
                    Widgets.Label(new Rect(6f, y + 3f, Mathf.Max(20f, width - 12f),
                        height - 44f), definition.LabelCap);
                    Rect button = new Rect(6f, y + height - 38f,
                        Mathf.Max(20f, width - 12f), 32f);
                    if (Widgets.ButtonText(button,
                        L("AquacultureFishing.SettingsExpertise") + ": " +
                        settings.MinimumExpertiseFor(definition)))
                    {
                        ChooseFishingRank(definition);
                        return;
                    }
                    TooltipHandler.TipRegion(button, L("AquacultureFishing.SettingsExpertiseTip"));
                }
                y += height + 6f;
            }
        }

        private void ChooseFishingRank(ThingDef fish)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (FishingRank rank in (FishingRank[])Enum.GetValues(typeof(FishingRank)))
            {
                FishingRank selected = rank;
                options.Add(new FloatMenuOption(selected.ToString(), () =>
                    settings.GetFishExpertiseSetting(fish).minimumFishingExpertise = selected));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawEcology(ref float y, float width)
        {
            Section(ref y, width, "AquacultureFishing.SettingsEcology",
                "AquacultureFishing.SettingsEcologySubtitle");
            Slider(ref y, width, "AquacultureFishing.SettingsSimulationInterval",
                "AquacultureFishing.SettingsSimulationIntervalTip",
                ref settings.ecologyIntervalHours, 0.25f, 6f);
            Slider(ref y, width, "AquacultureFishing.SettingsFoodDemand",
                "AquacultureFishing.SettingsFoodDemandTip",
                ref settings.foodDemandMultiplier, 0.1f, 5f);
            Slider(ref y, width, "AquacultureFishing.SettingsAlgaeGrowth",
                "AquacultureFishing.SettingsAlgaeGrowthTip",
                ref settings.algaeGrowthMultiplier, 0f, 5f);
            Slider(ref y, width, "AquacultureFishing.SettingsStarvationTime",
                "AquacultureFishing.SettingsStarvationTimeTip",
                ref settings.starvationHours, 6f, 600f);
            Slider(ref y, width, "AquacultureFishing.SettingsWaterStress",
                "AquacultureFishing.SettingsWaterStressTip",
                ref settings.waterStressHours, 1f, 240f);
            Slider(ref y, width, "AquacultureFishing.SettingsTemperatureStress",
                "AquacultureFishing.SettingsTemperatureStressTip",
                ref settings.temperatureStressHours, 1f, 240f);
            Slider(ref y, width, "AquacultureFishing.PondBaseCapacitySetting",
                "AquacultureFishing.PondCapacityBaselineTooltip",
                ref settings.fishCapacityPerCell, 0.25f, 10f);
            if (settings.capacityTransitionWarning)
            {
                Note(ref y, width, "AquacultureFishing.PondCapacityTransitionWarning".Translate(
                    settings.fishCapacityPerCell.ToString("0.##"),
                    PondCapacityRules.DefaultCapacityPerCell.ToString("0.##")).ToString());
                float buttonWidth = Mathf.Max(40f, (width - 18f) / 2f);
                if (Widgets.ButtonText(new Rect(6f, y, buttonWidth, 34f),
                    L("AquacultureFishing.PondCapacityUseNewBaseline")))
                {
                    settings.fishCapacityPerCell = PondCapacityRules.DefaultCapacityPerCell;
                    settings.capacityTransitionWarning = false;
                }
                if (Widgets.ButtonText(new Rect(12f + buttonWidth, y, buttonWidth, 34f),
                    L("AquacultureFishing.PondCapacityKeepExisting")))
                    settings.capacityTransitionWarning = false;
                y += 40f;
            }
            Slider(ref y, width, "AquacultureFishing.SettingsFeedValue",
                "AquacultureFishing.SettingsFeedValueTip",
                ref settings.feedValuePerUnit, 0.005f, 0.25f);
            Slider(ref y, width, "AquacultureFishing.SettingsFeederRange",
                "AquacultureFishing.SettingsFeederRangeTip",
                ref settings.feederRange, 1f, 30f);
            Toggle(ref y, width, "AquacultureFishing.SettingsGlobalPredation",
                "AquacultureFishing.SettingsGlobalPredationTip",
                ref settings.predationEnabled);
            ResetButton(ref y, width, "AquacultureFishing.SettingsResetEcology", ResetEcology);
        }

        private void DrawBreeding(ref float y, float width)
        {
            Section(ref y, width, "AquacultureFishing.SettingsBreeding",
                "AquacultureFishing.SettingsBreedingSubtitle");
            Toggle(ref y, width, "AquacultureFishing.SettingsEnableBreeding",
                "AquacultureFishing.SettingsEnableBreedingTip",
                ref settings.globalBreedingEnabled);
            Slider(ref y, width, "AquacultureFishing.SettingsBreedingInterval",
                "AquacultureFishing.SettingsBreedingIntervalTip",
                ref settings.breedingIntervalDays, 1f, 60f);
            Slider(ref y, width, "AquacultureFishing.SettingsEggHatch",
                "AquacultureFishing.SettingsEggHatchTip",
                ref settings.eggHatchDays, 0.25f, 30f);

            int previousMinimum = settings.minimumOffspring;
            IntSlider(ref y, width, "AquacultureFishing.SettingsMinimumOffspring",
                "AquacultureFishing.SettingsMinimumOffspringTip",
                ref settings.minimumOffspring, 1, 10);
            if (previousMinimum != settings.minimumOffspring)
                settings.maximumOffspring = Mathf.Max(settings.minimumOffspring, settings.maximumOffspring);
            IntSlider(ref y, width, "AquacultureFishing.SettingsMaximumOffspring",
                "AquacultureFishing.SettingsMaximumOffspringTip",
                ref settings.maximumOffspring, settings.minimumOffspring, 12);

            int previousLifespan = settings.minimumLifespanDays;
            IntSlider(ref y, width, "AquacultureFishing.SettingsMinimumLifespan",
                "AquacultureFishing.SettingsMinimumLifespanTip",
                ref settings.minimumLifespanDays, 5, 600);
            if (previousLifespan != settings.minimumLifespanDays)
                settings.maximumLifespanDays = Mathf.Max(settings.minimumLifespanDays,
                    settings.maximumLifespanDays);
            IntSlider(ref y, width, "AquacultureFishing.SettingsMaximumLifespan",
                "AquacultureFishing.SettingsMaximumLifespanTip",
                ref settings.maximumLifespanDays, settings.minimumLifespanDays, 1200);
            ResetButton(ref y, width, "AquacultureFishing.SettingsResetBreeding", ResetBreeding);
        }

        private void DrawAppearance(ref float y, float width)
        {
            Section(ref y, width, "AquacultureFishing.SettingsAppearance",
                "AquacultureFishing.SettingsAppearanceSubtitle");
            Toggle(ref y, width, "AquacultureFishing.SettingsEnhancedPondVisuals",
                "AquacultureFishing.SettingsEnhancedPondVisualsTip", ref settings.enhancedPondVisuals);
            Toggle(ref y, width, "AquacultureFishing.SettingsWaterTypeTint",
                "AquacultureFishing.SettingsWaterTypeTintTip", ref settings.waterTypeTint);
            Toggle(ref y, width, "AquacultureFishing.SettingsAlgaeTint",
                "AquacultureFishing.SettingsAlgaeTintTip", ref settings.algaeTint);
            Toggle(ref y, width, "AquacultureFishing.SettingsDepthShading",
                "AquacultureFishing.SettingsDepthShadingTip", ref settings.depthShading);
            Toggle(ref y, width, "AquacultureFishing.SettingsShoreline",
                "AquacultureFishing.SettingsShorelineTip", ref settings.shorelineVisuals);
            Toggle(ref y, width, "AquacultureFishing.SettingsDecorations",
                "AquacultureFishing.SettingsDecorationsTip", ref settings.pondDecorations);
            Toggle(ref y, width, "AquacultureFishing.SettingsFishShadows",
                "AquacultureFishing.SettingsFishShadowsTip", ref settings.fishShadows);
            Toggle(ref y, width, "AquacultureFishing.SettingsUnderwaterFishTint",
                "AquacultureFishing.SettingsUnderwaterFishTintTip", ref settings.underwaterFishTint);
            Toggle(ref y, width, "AquacultureFishing.SettingsSurfaceOverlay",
                "AquacultureFishing.SettingsSurfaceOverlayTip", ref settings.surfaceOverlay);
            Toggle(ref y, width, "AquacultureFishing.SettingsCaustics",
                "AquacultureFishing.SettingsCausticsTip", ref settings.caustics);
            Toggle(ref y, width, "AquacultureFishing.SettingsPseudoDepth",
                "AquacultureFishing.SettingsPseudoDepthTip", ref settings.pseudoDepth);
            Toggle(ref y, width, "AquacultureFishing.SettingsRipples",
                "AquacultureFishing.SettingsRipplesTip", ref settings.pondRipples);
            if (Widgets.ButtonText(new Rect(6f, y, Mathf.Max(40f, width - 12f), 34f),
                L("AquacultureFishing.SettingsOpenMaskPainter")))
                Find.WindowStack.Add(new Dialog_FishMaskPainter());
        }

        private void DrawAdvanced(ref float y, float width)
        {
            Section(ref y, width, "AquacultureFishing.SettingsAdvanced",
                "AquacultureFishing.SettingsAdvancedSubtitle");
            Section(ref y, width, "AquacultureFishing.SettingsPresentation",
                "AquacultureFishing.SettingsPresentationSubtitle");

            if (Widgets.ButtonText(new Rect(6f, y, Mathf.Max(20f, width - 12f), 34f),
                L("AquacultureFishing.SettingsPresentation") + ": " +
                DensityLabel(settings.PresentationPreferences.DensityIndex)))
            {
                ChooseDensity();
                return;
            }
            y += 42f;
            AquaculturePresentationPreferences current = settings.PresentationPreferences;
            bool highContrast = current.HighContrast;
            Toggle(ref y, width, "AquacultureFishing.SettingsHighContrast",
                "AquacultureFishing.SettingsHighContrastTip", ref highContrast);
            if (highContrast != current.HighContrast)
                settings.SetPresentationPreferences(AquaculturePresentationPreferences.FromSerializedValues(
                    current.DensityIndex, highContrast, current.ReducedMotion));

            current = settings.PresentationPreferences;
            bool reducedMotion = current.ReducedMotion;
            Toggle(ref y, width, "AquacultureFishing.SettingsReducedMotion",
                "AquacultureFishing.SettingsReducedMotionTip", ref reducedMotion);
            if (reducedMotion != current.ReducedMotion)
                settings.SetPresentationPreferences(AquaculturePresentationPreferences.FromSerializedValues(
                    current.DensityIndex, current.HighContrast, reducedMotion));

            Note(ref y, width, L("AquacultureFishing.SettingsMigrationBoundaryBody"));
            Slider(ref y, width, "AquacultureFishing.TraitWildExceptionalChance",
                "AquacultureFishing.SettingsWildExceptionalTip",
                ref settings.wildExceptionalTraitChance, 0f, 1f);
            Slider(ref y, width, "AquacultureFishing.TraitParentalInheritanceChance",
                "AquacultureFishing.SettingsParentalInheritanceTip",
                ref settings.parentalTraitInheritanceChance, 0f, 1f);
            IntSlider(ref y, width, "AquacultureFishing.TraitMaximumInherited",
                "AquacultureFishing.SettingsMaximumInheritedTip",
                ref settings.maxInheritedTraits, 0, 8);
            Slider(ref y, width, "AquacultureFishing.TraitOffspringMutationChance",
                "AquacultureFishing.SettingsOffspringMutationTip",
                ref settings.offspringMutationChance, 0f, 1f);
            IntSlider(ref y, width, "AquacultureFishing.TraitMaximumOffspringMutations",
                "AquacultureFishing.SettingsMaximumMutationsTip",
                ref settings.maxOffspringMutations, 0, 8);
            Slider(ref y, width, "AquacultureFishing.TraitRegisteredBreedReliability",
                "AquacultureFishing.SettingsBreedReliabilityTip",
                ref settings.registeredBreedDefiningTraitReliability, 0f, 1f);

            Note(ref y, width, L("AquacultureFishing.TraitSettingsExplanation"));
            SearchField(ref y, width, "AquacultureFishing.SettingsSearchTraits",
                ref traitSearch, ref scrollPosition);
            List<FishTraitDef> traits = FilterTraitCatalog();
            if (traits.Count == 0) Note(ref y, width, L("AquacultureFishing.PondNoTraits"));
            foreach (FishTraitDef trait in traits) DrawTraitRow(ref y, width, trait);
            ResetButton(ref y, width, "AquacultureFishing.SettingsResetTraitRules", ResetTraitRules);
        }

        private void DrawTraitRow(ref float y, float width, FishTraitDef trait)
        {
            float height = TraitRowHeight(trait.LabelCap.ToString(), width);
            if (IsVisible(y, height))
            {
                Rect row = new Rect(6f, y + 4f, Mathf.Max(20f, width - 12f), height - 8f);
                bool enabled = settings.GetTraitSetting(trait, false)?.enabled ?? true;
                bool toggled = enabled;
                Widgets.CheckboxLabeled(new Rect(row.x, row.y, row.width, height - 76f),
                    trait.LabelCap.ToString(), ref toggled);
                if (toggled != enabled)
                    settings.GetTraitSetting(trait).enabled = toggled;

                float savedWeight = settings.GetTraitSetting(trait, false)?.weight ??
                    trait.DefaultWeight;
                float weight = Mathf.Clamp(savedWeight, 0f, 5f);
                Widgets.Label(new Rect(row.x, y + height - 70f, row.width, 24f),
                    L("AquacultureFishing.SettingsTraitWeight") + ": " + weight.ToString("0.00"));
                float changed = Widgets.HorizontalSlider(
                    new Rect(row.x, y + height - 41f, row.width, 20f),
                    weight, 0f, 5f, true);
                if (!Mathf.Approximately(changed, weight))
                    settings.GetTraitSetting(trait).weight = changed;
                TooltipHandler.TipRegion(row,
                    (trait.description ?? string.Empty) + "\n" + FishTraitUtility.EffectLine(trait));
            }
            y += height + 6f;
        }

        private static string DensityLabel(int index)
        {
            switch (index)
            {
                case 0: return L("AquacultureFishing.SettingsDensityComfortable");
                case 2: return L("AquacultureFishing.SettingsDensityCompact");
                default: return L("AquacultureFishing.SettingsDensityNormal");
            }
        }

        private void ChooseDensity()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption(L("AquacultureFishing.SettingsDensityComfortable"), () => SetDensity(0)),
                new FloatMenuOption(L("AquacultureFishing.SettingsDensityNormal"), () => SetDensity(1)),
                new FloatMenuOption(L("AquacultureFishing.SettingsDensityCompact"), () => SetDensity(2))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SetDensity(int index)
        {
            AquaculturePresentationPreferences current = settings.PresentationPreferences;
            settings.SetPresentationPreferences(AquaculturePresentationPreferences.FromSerializedValues(
                index, current.HighContrast, current.ReducedMotion));
        }

        private static void Section(ref float y, float width, string titleKey, string subtitleKey)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(6f, y, width - 12f, 30f), L(titleKey));
            Text.Font = GameFont.Small;
            y += 32f;
            Note(ref y, width, L(subtitleKey));
            y += 8f;
        }

        private static void Note(ref float y, float width, string value)
        {
            float height = Mathf.Max(28f, Text.CalcHeight(value, Mathf.Max(20f, width - 12f)));
            Widgets.Label(new Rect(6f, y, Mathf.Max(20f, width - 12f), height), value);
            y += height + 8f;
        }

        private static void Toggle(ref float y, float width, string labelKey, string tooltipKey, ref bool value)
        {
            Rect row = new Rect(6f, y, Mathf.Max(40f, width - 12f), 32f);
            Widgets.CheckboxLabeled(row, L(labelKey), ref value);
            TooltipHandler.TipRegion(row, L(tooltipKey));
            y += 40f;
        }

        private static void Slider(ref float y, float width, string labelKey, string tooltipKey,
            ref float value, float minimum, float maximum)
        {
            Rect row = new Rect(6f, y, Mathf.Max(40f, width - 12f), 56f);
            Widgets.Label(new Rect(row.x, row.y, row.width, 25f),
                L(labelKey) + ": " + value.ToString("0.###"));
            float changed = Widgets.HorizontalSlider(
                new Rect(row.x, row.y + 28f, row.width, 20f), value, minimum, maximum, true);
            if (!Mathf.Approximately(value, changed)) value = changed;
            TooltipHandler.TipRegion(row, L(tooltipKey));
            y += 64f;
        }

        private static void IntSlider(ref float y, float width, string labelKey, string tooltipKey,
            ref int value, int minimum, int maximum)
        {
            Rect row = new Rect(6f, y, Mathf.Max(40f, width - 12f), 56f);
            Widgets.Label(new Rect(row.x, row.y, row.width, 25f),
                L(labelKey) + ": " + value);
            int changed = Mathf.RoundToInt(Widgets.HorizontalSlider(
                new Rect(row.x, row.y + 28f, row.width, 20f),
                value, minimum, maximum, true));
            if (changed != value) value = Mathf.Clamp(changed, minimum, maximum);
            TooltipHandler.TipRegion(row, L(tooltipKey));
            y += 64f;
        }

        private static void ResetButton(ref float y, float width, string labelKey, Action callback)
        {
            if (Widgets.ButtonText(new Rect(6f, y, Mathf.Max(40f, width - 12f), 34f),
                L(labelKey)))
                callback();
            y += 42f;
        }

        private void ResetGameplay()
        {
            settings.enableResearchProgression = true;
            settings.showPondAlerts = true;
            settings.fishingDurationFactor = 0.30f;
            settings.animaFishPerTree = 20;
            ResetNotice("AquacultureFishing.SettingsGameplay");
        }

        private void ResetEcology()
        {
            settings.ecologyIntervalHours = 1f;
            settings.foodDemandMultiplier = 1f;
            settings.algaeGrowthMultiplier = 1f;
            settings.starvationHours = 96f;
            settings.waterStressHours = 12f;
            settings.temperatureStressHours = 48f;
            settings.fishCapacityPerCell = PondCapacityRules.DefaultCapacityPerCell;
            settings.capacityTransitionWarning = false;
            settings.capacityModelVersion = AquacultureSettings.CurrentCapacityModelVersion;
            settings.predationEnabled = true;
            settings.feedValuePerUnit = 0.05f;
            settings.feederRange = 6f;
            ResetNotice("AquacultureFishing.SettingsEcology");
        }

        private void ResetBreeding()
        {
            settings.globalBreedingEnabled = true;
            settings.breedingIntervalDays = 15f;
            settings.eggHatchDays = 3f;
            settings.minimumOffspring = 1;
            settings.maximumOffspring = 3;
            settings.minimumLifespanDays = 48;
            settings.maximumLifespanDays = 72;
            ResetNotice("AquacultureFishing.SettingsBreeding");
        }

        private void ResetTraitRules()
        {
            settings.wildExceptionalTraitChance = AquacultureSettings.DefaultWildExceptionalTraitChance;
            settings.parentalTraitInheritanceChance =
                AquacultureSettings.DefaultParentalTraitInheritanceChance;
            settings.maxInheritedTraits = AquacultureSettings.DefaultMaximumInheritedTraits;
            settings.offspringMutationChance = AquacultureSettings.DefaultOffspringMutationChance;
            settings.maxOffspringMutations = AquacultureSettings.DefaultMaximumOffspringMutations;
            settings.registeredBreedDefiningTraitReliability =
                AquacultureSettings.DefaultRegisteredBreedDefiningTraitReliability;
            ResetNotice("AquacultureFishing.SettingsAdvanced");
        }

        private static void ResetNotice(string section)
        {
            Messages.Message("AquacultureFishing.SettingsResetComplete".Translate(
                L(section)).ToString(), MessageTypeDefOf.TaskCompletion, false);
        }

        private static string L(string key) => key.Translate().ToString();
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
