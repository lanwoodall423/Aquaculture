using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AquacultureFishing
{
    public sealed class CompProperties_FishTraits : CompProperties
    {
        public CompProperties_FishTraits() { compClass = typeof(CompFishTraits); }
    }

    public sealed class CompFishTraits : ThingComp
    {
        public List<string> traitDefNames = new List<string>();
        public Dictionary<string, float> traitValues = new Dictionary<string, float>();
        public bool initialized;
        public bool alive = true;
        public bool sterilized;
        public float airExposureTicks;
        public int birthTick;
        public int lifespanTicks;
        public int nextBreedTick;
        public bool colonyBorn;
        public string breedId;
        public int breedGeneration;
        public string qualifyingBreedId;
        public int qualifyingBreedGeneration;
        public int parentOneThingId;
        public int parentTwoThingId;
        public bool breedBirthRecorded;
        public bool qualifyingBirthRecorded;
        public bool breedMasteryAnnounced;
        public float foodReserve = 1f;
        public float starvationProgress;
        public float waterStress;
        public float temperatureStress;
        public float habitatFit = 1f;
        public float habitatStress;
        public float lastPondTemperature;
        internal int nextAgeCheckTick;

        private readonly List<FishTraitDef> cachedTraits = new List<FishTraitDef>();
        private bool traitCacheDirty = true;
        private bool cachedHasVisualTraits;
        private bool cachedIsAdult;
        private bool cachedIsFemale;
        private float cachedSizeFactor = 1f;
        private float cachedMovementSpeed = 1f;
        private float cachedTurnRate = 1f;
        private float cachedSchooling = 1f;
        private float cachedCohesion = 1f;
        private float cachedAlignment = 1f;
        private float cachedSeparation = 1f;
        private float cachedRestFrequency = 1f;
        private float cachedMeatYield = 1f;
        private float cachedBeautyOffset;
        private float cachedNutritionMultiplier = 1f;
        private float cachedVerticalScale = 1f;
        private FishDiet cachedDiet = FishDiet.Omnivore;
        private PondWaterKind cachedWater = PondWaterKind.Freshwater;
        private bool cachedLivebearer;
        private bool cachedSolitary;
        private bool cachedCurious;
        private float cachedBreedingCooldown = 1f;
        private Season cachedBreedingSeason = Season.Undefined;
        private float cachedTemperatureRangeOffset;
        private string cachedTraitSummary = string.Empty;
        private string cachedMaterialKey = string.Empty;
        internal int traitRevision;
        internal Graphic cachedVisualSource;
        internal Graphic cachedVisualReplacement;
        internal int cachedVisualTraitRevision = -1;
        internal int cachedVisualCacheRevision = -1;
        internal Material cachedAquariumMaterial;
        internal int cachedAquariumTraitRevision = -1;
        internal int cachedAquariumCacheRevision = -1;

        public List<FishTraitDef> ActiveTraits { get { EnsureTraitCache(); return cachedTraits; } }
        public bool HasAnyTraits { get { EnsureTraitCache(); return cachedTraits.Count > 0; } }
        public bool HasVisualTraits { get { EnsureTraitCache(); return cachedHasVisualTraits; } }
        public bool IsAdult { get { EnsureTraitCache(); return cachedIsAdult; } }
        public bool IsHarvestMature => traitDefNames?.Contains(FishTraitUtility.Adult) == true || traitDefNames?.Contains(FishTraitUtility.Elder) == true;
        public bool IsFemale { get { EnsureTraitCache(); return cachedIsFemale; } }
        public bool IsAlive => alive;
        public float SizeFactor { get { EnsureTraitCache(); return cachedSizeFactor; } }
        public float MassFactor { get { EnsureTraitCache(); return Mathf.Max(0.01f, cachedSizeFactor * cachedSizeFactor * cachedSizeFactor * cachedMeatYield); } }
        public float MovementSpeed { get { EnsureTraitCache(); return cachedMovementSpeed; } }
        public float TurnRateFactor { get { EnsureTraitCache(); return cachedTurnRate; } }
        public float SchoolingFactor { get { EnsureTraitCache(); return cachedSchooling; } }
        public float CohesionFactor { get { EnsureTraitCache(); return cachedCohesion; } }
        public float AlignmentFactor { get { EnsureTraitCache(); return cachedAlignment; } }
        public float SeparationFactor { get { EnsureTraitCache(); return cachedSeparation; } }
        public float RestFrequencyFactor { get { EnsureTraitCache(); return cachedRestFrequency; } }
        public float MeatYield { get { EnsureTraitCache(); return cachedMeatYield; } }
        public float BeautyOffset { get { EnsureTraitCache(); return cachedBeautyOffset; } }
        public float NutritionMultiplier { get { EnsureTraitCache(); return cachedNutritionMultiplier; } }
        public string TraitSummary { get { EnsureTraitCache(); return cachedTraitSummary; } }
        internal string MaterialKey { get { EnsureTraitCache(); return cachedMaterialKey; } }
        internal float VerticalScale { get { EnsureTraitCache(); return cachedVerticalScale; } }
        public FishDiet Diet { get { EnsureTraitCache(); return cachedDiet; } }
        public PondWaterKind WaterKind { get { EnsureTraitCache(); return cachedWater; } }
        public bool Livebearer { get { EnsureTraitCache(); return cachedLivebearer; } }
        public bool Solitary { get { EnsureTraitCache(); return cachedSolitary; } }
        public bool Curious { get { EnsureTraitCache(); return cachedCurious; } }
        public float BreedingCooldownFactor { get { EnsureTraitCache(); return cachedBreedingCooldown; } }
        public Season BreedingSeason { get { EnsureTraitCache(); return cachedBreedingSeason; } }
        public float TemperatureRangeOffset { get { EnsureTraitCache(); return cachedTemperatureRangeOffset; } }
        public float HabitatBreedingFactor => Mathf.Lerp(1.30f, 0.85f, Mathf.Clamp01(habitatFit));
        public FishBreedRecord Breed => AquacultureJournalComponent.Current?.BreedById(breedId);
        public string BreedName => Breed?.name;

        internal Vector2 schoolPosition;
        internal Vector2 schoolPreviousPosition;
        internal Vector2 schoolVelocity;
        internal Vector2 schoolNextPosition;
        internal Vector2 schoolNextVelocity;
        internal bool schoolInitialized;
        internal int schoolRestUntilTick;
        internal float schoolDrawRotation;
        internal int schoolLastUpdateTick;
        internal int schoolUpdateInterval = 6;
        internal float schoolDepth;
        internal float schoolTargetDepth;
        internal int nextDepthChangeTick;
        internal float ecologyDemand;
        internal float ecologyReceived;
        internal bool ecologyWaterCompatible;
        internal bool harvestReserved;

        public bool IsInPond => parent.Spawned && parent.Map.terrainGrid.TerrainAt(parent.Position).defName == "AF_Pond";
        public bool IsSwimmingInPond => alive && IsInPond;

        public float TraitValue(string defName) => traitValues != null && traitValues.TryGetValue(defName, out float value) ? value : 0f;

        private void EnsureTraitCache()
        {
            if (!traitCacheDirty) return;
            traitCacheDirty = false;
            cachedTraits.Clear();
            if (traitDefNames != null)
            {
                for (int pass = 0; pass < 5; pass++)
                {
                    for (int i = 0; i < traitDefNames.Count; i++)
                    {
                        FishTraitDef trait = DefDatabase<FishTraitDef>.GetNamedSilentFail(traitDefNames[i]);
                        if (trait == null) continue;
                        int order = trait.kind == FishTraitKind.Age ? 0 : trait.kind == FishTraitKind.Sex ? 1 : trait.kind == FishTraitKind.Diet ? 2 : trait.kind == FishTraitKind.Water ? 3 : 4;
                        if (order == pass) cachedTraits.Add(trait);
                    }
                }
            }

            cachedHasVisualTraits = false;
            cachedIsAdult = false;
            cachedIsFemale = false;
            cachedSizeFactor = 1f;
            cachedMovementSpeed = 1f;
            cachedTurnRate = 1f;
            cachedSchooling = 1f;
            cachedCohesion = 1f;
            cachedAlignment = 1f;
            cachedSeparation = 1f;
            cachedRestFrequency = 1f;
            AquaticSpeciesProfile speciesProfile = AquaticSpeciesProfile.For(parent?.def);
            cachedMeatYield = speciesProfile.meatYieldFactor;
            cachedBeautyOffset = speciesProfile.beautyOffset;
            cachedNutritionMultiplier = 1f;
            cachedVerticalScale = 1f;
            cachedDiet = FishDiet.Omnivore;
            cachedWater = PondWaterKind.Freshwater;
            cachedLivebearer = false;
            cachedSolitary = false;
            cachedCurious = false;
            cachedBreedingCooldown = speciesProfile.breedingIntervalFactor;
            cachedBreedingSeason = Season.Undefined;
            cachedTemperatureRangeOffset = 0f;
            var materialNames = new List<string>();
            var summary = new StringBuilder(cachedTraits.Count * 20);
            for (int i = 0; i < cachedTraits.Count; i++)
            {
                FishTraitDef trait = cachedTraits[i];
                float numericValue = TraitValue(trait.defName);
                cachedHasVisualTraits |= trait.kind == FishTraitKind.Color || trait.kind == FishTraitKind.Variegated || trait.kind == FishTraitKind.Scale ||
                    trait.kind == FishTraitKind.NumericSize || trait.kind == FishTraitKind.Age || trait.kind == FishTraitKind.Finish || trait.kind == FishTraitKind.Body;
                cachedIsAdult |= trait.defName == FishTraitUtility.Adult;
                cachedIsFemale |= trait.defName == FishTraitUtility.Female;
                if (trait.kind == FishTraitKind.Scale || trait.kind == FishTraitKind.Age) cachedSizeFactor *= trait.drawScale;
                if (trait.kind == FishTraitKind.NumericSize) cachedSizeFactor *= 1f + numericValue / 100f;
                cachedMovementSpeed *= trait.movementSpeed;
                cachedTurnRate *= trait.turnRate;
                cachedSchooling *= trait.schooling;
                cachedCohesion *= trait.cohesion;
                cachedAlignment *= trait.alignment;
                cachedSeparation *= trait.separation;
                cachedRestFrequency *= trait.restFrequency;
                cachedMeatYield *= trait.meatYield;
                cachedVerticalScale *= trait.verticalScale;
                cachedBeautyOffset += trait.beautyOffset;
                if (trait.kind == FishTraitKind.Diet) cachedDiet = trait.diet;
                if (trait.kind == FishTraitKind.Water) cachedWater = trait.waterKind;
                cachedLivebearer |= trait.livebearer;
                cachedSolitary |= trait.solitary;
                cachedCurious |= trait.curious;
                cachedBreedingCooldown *= trait.breedingCooldownFactor;
                if (trait.breedingSeason != Season.Undefined) cachedBreedingSeason = trait.breedingSeason;
                cachedTemperatureRangeOffset += trait.temperatureRangeOffset;
                if (trait.kind == FishTraitKind.Nutritious) cachedNutritionMultiplier += numericValue / 100f;
                if (i > 0) summary.Append(", ");
                summary.Append(FishTraitUtility.DisplayLabel(trait, this));
                if (trait.kind == FishTraitKind.Color || trait.kind == FishTraitKind.Variegated || trait.kind == FishTraitKind.Finish)
                    materialNames.Add(trait.defName);
            }
            float sizeRoot = Mathf.Sqrt(Mathf.Max(0.25f, cachedSizeFactor));
            cachedMovementSpeed /= sizeRoot;
            cachedSeparation *= sizeRoot;
            cachedTraitSummary = summary.ToString();
            materialNames.Sort(StringComparer.Ordinal);
            var materialKey = new StringBuilder(materialNames.Count * 14);
            for (int i = 0; i < materialNames.Count; i++) materialKey.Append(materialNames[i]).Append(';');
            cachedMaterialKey = materialKey.ToString();
        }

        public void NotifyTraitsChanged()
        {
            traitCacheDirty = true;
            traitRevision++;
            cachedVisualReplacement = null;
            cachedAquariumMaterial = null;
            if (parent?.Spawned == true) AquacultureJournalComponent.Current?.NotifyFishRecord(this);
            AquacultureCommissionManager.NotifyFishChanged(this);
            if (parent?.Spawned == true) parent.Map.GetComponent<FishPondMapComponent>()?.NotifyFishChanged(this);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            InitializeTraits();
            CaughtFishTraitTransfer.ApplyIfPending(this);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            RefreshAgeTrait();
            parent.Map.GetComponent<FishPondMapComponent>().Register(this);
            AquacultureCommissionManager.NotifyFishSpawned(this);
            if (!respawningAfterLoad)
            {
                AquacultureJournalComponent journal = AquacultureJournalComponent.Current;
                if (IsInPond) AquacultureEventRouter.FishEstablished(this);
                else if (!colonyBorn) journal?.NotifyCaught(this, FindLikelyDiscoverer());
                if (colonyBorn) AquacultureEventRouter.ColonyBorn(this);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            AquacultureCommissionManager.NotifyFishDespawned(this);
            map?.GetComponent<FishPondMapComponent>()?.Deregister(this);
            base.PostDeSpawn(map, mode);
        }

        public override void PostIngested(Pawn ingester)
        {
            if (ActiveTraits.Any(trait => trait.pondEffect?.delicious == true) && ingester?.needs?.mood?.thoughts?.memories != null)
            {
                ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("AF_AteDeliciousFish");
                if (thought != null) ingester.needs.mood.thoughts.memories.TryGainMemoryFast(thought);
            }
        }

        public void InitializeTraits()
        {
            if (initialized) return;
            initialized = true;
            alive = true;
            sterilized = false;
            airExposureTicks = 0f;
            int now = Find.TickManager?.TicksGame ?? 0;
            lifespanTicks = RollSpeciesLifespanTicks();
            birthTick = now - Mathf.RoundToInt(lifespanTicks * Rand.Range(0.05f, 0.88f));
            traitDefNames.Add(Rand.Bool ? FishTraitUtility.Male : FishTraitUtility.Female);
            EnsureEcologyTraits();
            foreach (FishTraitDef trait in FishTraitUtility.SelectWildExceptionalTraits()) AddTrait(trait);
            RefreshAgeTrait();
            NotifyTraitsChanged();
        }

        private void EnsureDemographics()
        {
            if (!initialized) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (lifespanTicks <= 0)
            {
                lifespanTicks = RollSpeciesLifespanTicks();
                birthTick = now - Mathf.RoundToInt(lifespanTicks * Rand.Range(0.05f, 0.88f));
            }
            if (!traitDefNames.Any(name => !name.NullOrEmpty() && name.StartsWith("AF_Sex_"))) { traitDefNames.Add(Rand.Bool ? FishTraitUtility.Male : FishTraitUtility.Female); NotifyTraitsChanged(); }
            EnsureEcologyTraits();
            RefreshAgeTrait();
        }

        public void InitializeFromEgg(IEnumerable<string> inheritedNames, IDictionary<string, float> inheritedValues)
        {
            InitializeFromEgg(inheritedNames, inheritedValues, null, 0);
        }

        public void InitializeFromEgg(IEnumerable<string> inheritedNames, IDictionary<string, float> inheritedValues,
            string inheritedBreedId, int inheritedBreedGeneration)
        {
            InitializeFromEgg(inheritedNames, inheritedValues, inheritedBreedId, inheritedBreedGeneration, 0, 0);
        }

        public void InitializeFromEgg(IEnumerable<string> inheritedNames, IDictionary<string, float> inheritedValues,
            string inheritedBreedId, int inheritedBreedGeneration, int parentOneId, int parentTwoId)
        {
            InitializeFromEgg(inheritedNames, inheritedValues, inheritedBreedId, inheritedBreedGeneration,
                inheritedBreedId, inheritedBreedGeneration, parentOneId, parentTwoId);
        }

        public void InitializeFromEgg(IEnumerable<string> inheritedNames, IDictionary<string, float> inheritedValues,
            string inheritedBreedId, int inheritedBreedGeneration, string qualifyingId, int qualifyingGeneration,
            int parentOneId, int parentTwoId)
        {
            initialized = true;
            alive = true;
            sterilized = false;
            colonyBorn = true;
            breedId = inheritedBreedId;
            breedGeneration = inheritedBreedGeneration;
            qualifyingBreedId = qualifyingId;
            qualifyingBreedGeneration = qualifyingGeneration;
            parentOneThingId = parentOneId;
            parentTwoThingId = parentTwoId;
            breedBirthRecorded = false;
            qualifyingBirthRecorded = false;
            breedMasteryAnnounced = false;
            airExposureTicks = 0f;
            traitDefNames.Clear();
            traitValues.Clear();
            int now = Find.TickManager.TicksGame;
            lifespanTicks = RollSpeciesLifespanTicks();
            birthTick = now;
            nextBreedTick = 0;
            foreach (string name in inheritedNames ?? Enumerable.Empty<string>())
                if (!traitDefNames.Contains(name)) traitDefNames.Add(name);
            if (inheritedValues != null)
                foreach (KeyValuePair<string, float> pair in inheritedValues) traitValues[pair.Key] = pair.Value;
            traitDefNames.RemoveAll(FishTraitUtility.IsDemographicOrEcology);
            traitDefNames.Add(FishTraitUtility.Fry);
            traitDefNames.Add(Rand.Bool ? FishTraitUtility.Male : FishTraitUtility.Female);
            EnsureEcologyTraits();
            NotifyTraitsChanged();
        }

        private int RollSpeciesLifespanTicks()
        {
            int minimum = AquacultureMod.Settings?.minimumLifespanDays ?? 48;
            int maximum = Mathf.Max(minimum, AquacultureMod.Settings?.maximumLifespanDays ?? 72);
            float factor = AquaticSpeciesProfile.For(parent?.def).lifespanFactor;
            return Mathf.Max(60000, Mathf.RoundToInt(Rand.RangeInclusive(minimum, maximum) * Mathf.Max(0.1f, factor) * 60000f));
        }

        private void EnsureEcologyTraits()
        {
            AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(parent?.def);
            traitDefNames.RemoveAll(name => name.NullOrEmpty() || name.StartsWith(FishTraitUtility.DietPrefix) || name.StartsWith(FishTraitUtility.WaterPrefix));
            traitDefNames.Add(FishTraitUtility.DietPrefix + profile.diet);
            traitDefNames.Add(FishTraitUtility.WaterPrefix + profile.waterKind);
        }

        public void AddTrait(FishTraitDef trait)
        {
            if (trait == null || traitDefNames.Contains(trait.defName)) return;
            traitDefNames.Add(trait.defName);
            if (trait.IsNumeric) traitValues[trait.defName] = FishTraitUtility.RollPercent(trait);
            NotifyTraitsChanged();
        }

        public void ApplyCaughtTraits(IEnumerable<string> names, IDictionary<string, float> values)
        {
            if (names == null) return;
            traitDefNames = names.Where(name => !name.NullOrEmpty()).Distinct().ToList();
            traitValues = values == null
                ? new Dictionary<string, float>()
                : new Dictionary<string, float>(values);
            initialized = true;
            NotifyTraitsChanged();
        }

        public void ResetAirExposure()
        {
            if (!alive) return;
            airExposureTicks = 0f;
            CompRottable rottable = parent.TryGetComp<CompRottable>();
            if (rottable != null) rottable.RotProgress = 0f;
            if (parent?.Spawned == true) parent.Map.GetComponent<FishPondMapComponent>()?.NotifyFishChanged(this);
        }

        public void MarkDead()
        {
            if (!alive) return;
            AquacultureJournalComponent.Current?.NotifyFishRecord(this);
            alive = false;
            AquacultureCommissionManager.NotifyFishChanged(this);
            airExposureTicks = 0f;
            nextBreedTick = 0;
            PondMovementUtility.Reset(this);
            CompRottable rottable = parent.TryGetComp<CompRottable>();
            if (rottable != null) rottable.RotProgress = 0f;
            if (parent?.Spawned == true) parent.Map.GetComponent<FishPondMapComponent>()?.NotifyFishChanged(this);
        }

        public void RefreshAgeTrait()
        {
            if (!initialized || lifespanTicks <= 0 || traitDefNames == null) return;
            int now = Find.TickManager?.TicksGame ?? birthTick;
            if (nextAgeCheckTick > now) return;
            float fraction = Mathf.Clamp01((now - birthTick) / (float)lifespanTicks);
            string age = fraction < 0.10f ? FishTraitUtility.Fry : fraction < 0.30f ? FishTraitUtility.Juvenile : fraction < 0.80f ? FishTraitUtility.Adult : FishTraitUtility.Elder;
            float nextFraction = fraction < 0.10f ? 0.10f : fraction < 0.30f ? 0.30f : fraction < 0.80f ? 0.80f : 1f;
            nextAgeCheckTick = birthTick + Mathf.RoundToInt(lifespanTicks * nextFraction) + 1;
            if (!traitDefNames.Contains(age))
            {
                traitDefNames.RemoveAll(name => !name.NullOrEmpty() && name.StartsWith("AF_Age_"));
                traitDefNames.Add(age);
                NotifyTraitsChanged();
            }
        }

        public override void PostExposeData()
        {
            Scribe_Collections.Look(ref traitDefNames, "fishTraits", LookMode.Value);
            Scribe_Collections.Look(ref traitValues, "fishTraitValues", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref initialized, "traitsInitialized");
            Scribe_Values.Look(ref alive, "alive", true);
            Scribe_Values.Look(ref sterilized, "sterilized");
            Scribe_Values.Look(ref airExposureTicks, "airExposureTicks");
            Scribe_Values.Look(ref birthTick, "birthTick");
            Scribe_Values.Look(ref lifespanTicks, "lifespanTicks");
            Scribe_Values.Look(ref nextBreedTick, "nextBreedTick");
            Scribe_Values.Look(ref colonyBorn, "colonyBorn");
            Scribe_Values.Look(ref breedId, "breedId");
            Scribe_Values.Look(ref breedGeneration, "breedGeneration");
            Scribe_Values.Look(ref qualifyingBreedId, "qualifyingBreedId");
            Scribe_Values.Look(ref qualifyingBreedGeneration, "qualifyingBreedGeneration");
            Scribe_Values.Look(ref parentOneThingId, "parentOneThingId");
            Scribe_Values.Look(ref parentTwoThingId, "parentTwoThingId");
            Scribe_Values.Look(ref breedBirthRecorded, "breedBirthRecorded");
            Scribe_Values.Look(ref qualifyingBirthRecorded, "qualifyingBirthRecorded");
            Scribe_Values.Look(ref breedMasteryAnnounced, "breedMasteryAnnounced");
            Scribe_Values.Look(ref foodReserve, "foodReserve", 1f);
            Scribe_Values.Look(ref starvationProgress, "starvationProgress");
            Scribe_Values.Look(ref waterStress, "waterStress");
            Scribe_Values.Look(ref temperatureStress, "temperatureStress");
            Scribe_Values.Look(ref habitatFit, "habitatFit", 1f);
            Scribe_Values.Look(ref habitatStress, "habitatStress");
            Scribe_Values.Look(ref lastPondTemperature, "lastPondTemperature");
            if (traitDefNames == null) traitDefNames = new List<string>();
            if (traitValues == null) traitValues = new Dictionary<string, float>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (qualifyingBreedId.NullOrEmpty() && !breedId.NullOrEmpty())
                {
                    qualifyingBreedId = breedId;
                    qualifyingBreedGeneration = breedGeneration;
                    qualifyingBirthRecorded = breedBirthRecorded;
                }
                traitCacheDirty = true;
                nextAgeCheckTick = 0;
                EnsureDemographics();
            }
        }

        public override string CompInspectStringExtra()
        {
            var parts = new List<string>();
            if (!alive) parts.Add("Dead");
            else if (sterilized) parts.Add("Sterilized");
            RefreshAgeTrait();
            if (!alive && FishUtility.IsRuntimeFish(parent?.def))
            {
                ThingDef meatDef = DefDatabase<ThingDef>.GetNamedSilentFail(FishProcessingYield.MeatDefName);
                parts.Add("AquacultureFishing.ExpectedProcessingYield".Translate(
                    FishProcessingYield.ExpectedMeatCount(this), meatDef?.LabelCap ?? "fish meat").ToString());
            }
            if (!BreedName.NullOrEmpty()) parts.Add("Breed: " + BreedName + " (generation " + breedGeneration + ")");
            if (HasAnyTraits) parts.Add("Traits: " + TraitSummary);
            if (alive && lifespanTicks > 0) parts.Add("Lifespan remaining: " + Mathf.Max(0, lifespanTicks - ((Find.TickManager?.TicksGame ?? birthTick) - birthTick)).ToStringTicksToPeriod());
            if (IsSwimmingInPond) parts.Add("Swimming in pond");
            if (alive && IsInPond) parts.Add(starvationProgress > 0f ? "Starvation: " + starvationProgress.ToStringPercent() : "Food reserve: " + foodReserve.ToStringPercent());
            if (alive && IsInPond) parts.Add("Habitat fit: " + habitatFit.ToStringPercent() +
                (habitatStress > 0.01f ? " (stress " + habitatStress.ToStringPercent() + ")" : ""));
            if (alive && IsInPond)
            {
                PondWaterKind pondWater = parent.Map.GetComponent<FishPondMapComponent>()?.WaterKindAt(parent.Position) ?? PondWaterKind.Freshwater;
                if (!AquaticSpeciesProfile.WaterCompatible(WaterKind, pondWater)) parts.Add("Wrong water - osmotic stress: " + waterStress.ToStringPercent());
                AquaticSpeciesProfile profile = AquaticSpeciesProfile.For(parent.def);
                float minimum = profile.minimumTemperature - TemperatureRangeOffset;
                float maximum = profile.maximumTemperature + TemperatureRangeOffset;
                float currentTemperature = GenTemperature.GetTemperatureForCell(parent.Position, parent.Map);
                parts.Add("Water temperature: " + currentTemperature.ToString("0.#") + " C (livable " + minimum.ToString("0.#") + " to " + maximum.ToString("0.#") + " C)");
                if (temperatureStress > 0f) parts.Add("Temperature stress: " + temperatureStress.ToStringPercent());
            }
            return string.Join("\n", parts);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (alive && parent.Spawned && AquacultureProgression.IsAvailable("AF_SelectiveBreeding"))
            {
                if (!sterilized) yield return new Command_Action
                {
                    defaultLabel = "Sterilize",
                    defaultDesc = "Designate this fish for permanent sterilization by a handler. Requires one unit of medicine.",
                    icon = TexCommand.DesirePower,
                    action = () => FishHarvestUtility.DesignateSterilization(this)
                };
                var registerBreed = new Command_Action
                {
                    defaultLabel = "Register Breed",
                    defaultDesc = "Register a stable male and female population with matching inheritable traits as a named breed.",
                    icon = TexCommand.ForbidOff,
                    action = () => Find.WindowStack.Add(new Dialog_RegisterFishBreed(this))
                };
                string reason = null;
                if (AquacultureJournalComponent.Current?.CanRegisterBreed(this, out reason, out _) != true)
                    registerBreed.Disable(reason ?? "This fish cannot found a breed.");
                yield return registerBreed;
            }
            if (!alive || !parent.Spawned || IsInPond) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Place in pond",
                defaultDesc = "Place this living fish in a constructed pond cell.",
                icon = TexCommand.Install,
                action = () => Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target => PlaceInPond(target.Cell))
            };
        }

        private void PlaceInPond(IntVec3 cell)
        {
            Map map = parent.Map;
            if (!cell.InBounds(map) || map.terrainGrid.TerrainAt(cell).defName != "AF_Pond")
            {
                Messages.Message("Select a constructed pond cell.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (cell.GetThingList(map).Any(t => t.TryGetComp<CompFishTraits>() != null))
            {
                Messages.Message("That pond cell already contains a fish.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            FishPondMapComponent pond = map.GetComponent<FishPondMapComponent>();
            if (pond != null && !pond.FishFitsWater(cell, this))
            {
                Messages.Message("This fish requires " + (WaterKind == PondWaterKind.Brackishwater ? "compatible" : WaterKind.ToString().ToLowerInvariant()) + " water.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            parent.DeSpawn();
            GenSpawn.Spawn(parent, cell, map);
            ResetAirExposure();
            PondMovementUtility.Reset(this);
        }

        private Pawn FindLikelyDiscoverer()
        {
            if (parent?.Spawned != true) return null;
            Pawn best = null;
            float bestDistance = 24f * 24f;
            List<Pawn> colonists = parent.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                string jobName = pawn.CurJob?.def?.defName;
                if (jobName.NullOrEmpty() || jobName.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) < 0) continue;
                float distance = pawn.Position.DistanceToSquared(parent.Position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = pawn;
            }
            return best;
        }

        internal Pawn FindLikelyDiscovererForKnowledge() => FindLikelyDiscoverer();
    }

    public static class FishUtility
    {
        private static readonly HashSet<ThingDef> RuntimeFishDefs = new HashSet<ThingDef>();

        public static void RegisterRuntimeFish(ThingDef def)
        {
            if (def != null) RuntimeFishDefs.Add(def);
        }

        public static bool IsRuntimeFish(ThingDef def) => def != null && RuntimeFishDefs.Contains(def);

        public static bool IsFish(ThingDef def)
        {
            if (def?.thingCategories != null && def.thingCategories.Any(c => c.defName == "VCEF_RawFishCategory" || c.defName == "Fish")) return true;
            return def?.comps != null && def.comps.Any(c => c.compClass?.FullName == "Aquariums.ThingComp_AquariumFish");
        }

    }

    [StaticConstructorOnStartup]
    public static class AquacultureStartup
    {
        private static FieldInfo aquariumFishThingField;
        private static readonly HashSet<string> DefDiagnostics = new HashSet<string>();

        static AquacultureStartup()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                List<ThingDef> fishDefs = DefDatabase<ThingDef>.AllDefs.Where(FishUtility.IsFish).ToList();
                foreach (ThingDef def in fishDefs)
                {
                    FishUtility.RegisterRuntimeFish(def);
                    ConfigureRuntimeFishDef(def);
                }
                ConfigureFishProcessingRecipe(fishDefs);
                AquacultureMod.Harmony.PatchAll(Assembly.GetExecutingAssembly());
                AquacultureSharedKnowledgeIntegration.Register();
                PatchFishingJob();
                MethodInfo cleanup = AccessTools.Method(typeof(JobDriver), "Cleanup");
                if (cleanup != null)
                    AquacultureMod.Harmony.Patch(cleanup,
                        postfix: new HarmonyMethod(typeof(FishingRodWorkflow), nameof(FishingRodWorkflow.CleanupPostfix)));
                AquacultureMod.Harmony.Patch(AccessTools.Method(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.GetGizmos)),
                    postfix: new HarmonyMethod(typeof(FishingRodUtility), nameof(FishingRodUtility.EquipmentGizmosPostfix)));
                PatchFishingDurationAndPreference();
                PatchAquariums();
                RegisterWildlifeMenu();
            });
        }

        private static void RegisterWildlifeMenu()
        {
            try
            {
                Type registry = AccessTools.TypeByName("Herds.WildlifeMenuRegistry");
                MethodInfo register = registry?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string), typeof(string), typeof(int),
                        typeof(Func<bool>), typeof(Action) }, null);
                if (register == null) return;
                Action open = () =>
                {
                    MainButtonDef journal = DefDatabase<MainButtonDef>.GetNamedSilentFail("AF_AquacultureJournal");
                    if (journal != null) Find.MainTabsRoot.ToggleTab(journal, false);
                };
                Func<bool> visible = () =>
                {
                    MainButtonDef journal = DefDatabase<MainButtonDef>.GetNamedSilentFail("AF_AquacultureJournal");
                    return journal?.tabWindowClass == typeof(MainTabWindow_AquacultureJournal);
                };
                register.Invoke(null, new object[]
                {
                    "aquaculture.fish-journal",
                    "Aquaculture",
                    "Open the existing Aquaculture Fish Journal.",
                    20,
                    visible,
                    open
                });
                MainButtonDef standalone = DefDatabase<MainButtonDef>.GetNamedSilentFail("AF_AquacultureJournal");
                if (standalone != null) standalone.buttonVisible = false;
            }
            catch (Exception exception)
            {
                Log.Warning("[Aquaculture - Fishing] Wildlife menu integration was unavailable: " + exception.GetBaseException().Message);
            }
        }

        private static void ConfigureFishProcessingRecipe(List<ThingDef> fishDefs)
        {
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("AF_ProcessDeadFish");
            if (recipe == null) return;
            foreach (ThingDef fishDef in fishDefs)
            {
                recipe.fixedIngredientFilter.SetAllow(fishDef, true);
                foreach (IngredientCount ingredient in recipe.ingredients) ingredient.filter.SetAllow(fishDef, true);
            }
        }

        private static void ConfigureRuntimeFishDef(ThingDef def)
        {
            if (def == null) return;
            if (def.stackLimit != 1)
            {
                LogDefDiagnostic("stack:" + def.defName,
                    "Fish " + def.defName + " uses stackLimit " + def.stackLimit + "; individual fish state requires stackLimit 1.");
                def.stackLimit = 1;
            }
            if (def.drawerType == DrawerType.MapMeshAndRealTime)
                def.drawerType = DrawerType.RealtimeOnly;
            else if (def.drawerType != DrawerType.RealtimeOnly)
                LogDefDiagnostic("drawer:" + def.defName,
                    "Fish " + def.defName + " keeps explicit drawer type " + def.drawerType + "; realtime fish drawing may be limited.");
            EnsureFishComp(def);
            EnsureFishStat(def, StatDefOf.Beauty, 1f);
            EnsureFishStat(def, StatDefOf.Mass, 0.1f);
            EnsureTraitsTab(def);
        }

        private static void EnsureFishComp(ThingDef def)
        {
            if (def.comps == null) def.comps = new List<CompProperties>();
            if (def.comps.Any(comp => comp?.compClass == typeof(CompFishTraits))) return;
            def.comps.Add(new CompProperties_FishTraits());
        }

        private static void EnsureFishStat(ThingDef def, StatDef stat, float fallback)
        {
            if (def.statBases == null) def.statBases = new List<StatModifier>();
            if (def.statBases.Any(modifier => modifier?.stat == stat)) return;
            try
            {
                float inherited = def.GetStatValueAbstract(stat);
                if (Mathf.Abs(inherited) > 0.0001f)
                {
                    LogDefDiagnostic("stat:" + def.defName + ":" + stat.defName,
                        "Fish " + def.defName + " keeps inherited " + stat.defName + " value " + inherited + ".");
                    return;
                }
            }
            catch (Exception exception)
            {
                LogDefDiagnostic("stat-error:" + def.defName + ":" + stat.defName,
                    "Could not inspect " + stat.defName + " for fish " + def.defName + ": " + exception.GetBaseException().Message);
            }
            def.statBases.Add(new StatModifier { stat = stat, value = fallback });
        }

        private static void LogDefDiagnostic(string key, string message)
        {
            if (DefDiagnostics.Add(key)) Log.Warning("[Aquaculture - Fishing] " + message);
        }

        private static void EnsureTraitsTab(ThingDef def)
        {
            if (def == null) return;
            Type tabType = typeof(ITab_FishTraits);
            if (def.inspectorTabs == null) def.inspectorTabs = new List<Type>();
            if (!def.inspectorTabs.Contains(tabType)) def.inspectorTabs.Add(tabType);
            if (def.inspectorTabsResolved != null && !def.inspectorTabsResolved.Any(tab => tab.GetType() == tabType))
                def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
        }

        private static void PatchFishingJob()
        {
            Type type = AccessTools.TypeByName("VCE_Fishing.JobDriver_Fish");
            MethodInfo method = AccessTools.Method(type, "TryMakePreToilReservations");
            if (method != null)
            {
                AquacultureMod.Harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(FishingRodWorkflow), nameof(FishingRodWorkflow.FishingPreToilReservationsPrefix)));
                MethodInfo makeToils = AccessTools.Method(type, "MakeNewToils");
                if (makeToils != null) AquacultureMod.Harmony.Patch(makeToils,
                    postfix: new HarmonyMethod(typeof(FishingAttemptIntegration), nameof(FishingAttemptIntegration.VfeMakeNewToilsPostfix)));
                Type closure = type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(nested => AccessTools.Method(nested, "<MakeNewToils>b__3") != null);
                MethodInfo catchMethod = AccessTools.Method(closure, "<MakeNewToils>b__3");
                if (catchMethod != null)
                    AquacultureMod.Harmony.Patch(catchMethod,
                        prefix: new HarmonyMethod(typeof(FishingAttemptIntegration), nameof(FishingAttemptIntegration.VceCatchPrefix)),
                        postfix: new HarmonyMethod(typeof(FishingAttemptIntegration), nameof(FishingAttemptIntegration.VceCatchPostfix)),
                        finalizer: new HarmonyMethod(typeof(FishingAttemptIntegration), nameof(FishingAttemptIntegration.VceCatchFinalizer)));
            }

            Type utilityType = AccessTools.TypeByName("RimWorld.FishingUtility");
            MethodInfo catchesMethod = AccessTools.Method(utilityType, "GetCatchesFor");
            if (catchesMethod != null)
            {
                AquacultureMod.Harmony.Patch(catchesMethod,
                    prefix: new HarmonyMethod(typeof(FishingAttemptIntegration), nameof(FishingAttemptIntegration.OdysseyCatchesPrefix)));
            }

            Type odysseyDriver = AccessTools.TypeByName("RimWorld.JobDriver_Fish");
            if (odysseyDriver != null)
            {
                MethodInfo reservations = AccessTools.Method(odysseyDriver, "TryMakePreToilReservations");
                if (reservations != null)
                    AquacultureMod.Harmony.Patch(reservations,
                        prefix: new HarmonyMethod(typeof(FishingRodWorkflow), nameof(FishingRodWorkflow.FishingPreToilReservationsPrefix)));
                MethodInfo makeToils = AccessTools.Method(odysseyDriver, "MakeNewToils");
                if (makeToils != null) AquacultureMod.Harmony.Patch(makeToils,
                    postfix: new HarmonyMethod(typeof(FishingAttemptIntegration), nameof(FishingAttemptIntegration.OdysseyMakeNewToilsPostfix)));
            }

            Type odysseyWorkGiver = AccessTools.TypeByName("RimWorld.WorkGiver_Fish");
            MethodInfo nonScanJob = AccessTools.Method(odysseyWorkGiver, "NonScanJob");
            if (nonScanJob != null) AquacultureMod.Harmony.Patch(nonScanJob,
                postfix: new HarmonyMethod(typeof(FishingRodUtility), nameof(FishingRodUtility.FishingJobPostfix)));

            if (method == null && catchesMethod == null)
                Log.Error("[Aquaculture - Fishing] No supported fishing catch method was found.");
        }

        private static void PatchFishingDurationAndPreference()
        {
            Type workGiverType = AccessTools.TypeByName("VCE_Fishing.WorkGiver_Fish");
            MethodInfo jobOnCell = AccessTools.Method(workGiverType, "JobOnCell");
            if (jobOnCell != null)
            {
                AquacultureMod.Harmony.Patch(jobOnCell,
                    postfix: new HarmonyMethod(typeof(FishingRodUtility), nameof(FishingRodUtility.FishingJobPostfix)));
                AquacultureMod.Harmony.Patch(jobOnCell,
                    postfix: new HarmonyMethod(typeof(AquacultureStartup), nameof(VceFishingJobPostfix)));
            }
        }

        public static void VceFishingJobPostfix(Pawn pawn, IntVec3 c, ref Job __result)
        {
            if (__result == null || pawn?.Map == null) return;
            Zone zone = c.GetZone(pawn.Map);
            FishContainerUtility.PreferContainerCell(pawn, zone, __result, false);
        }

        private static void PatchAquariums()
        {
            Type type = AccessTools.TypeByName("Aquariums.AquariumFish");
            MethodInfo getter = AccessTools.PropertyGetter(type, "CachedMaterial");
            if (getter != null)
            {
                aquariumFishThingField = AccessTools.Field(type, "fishThing");
                AquacultureMod.Harmony.Patch(getter, postfix: new HarmonyMethod(typeof(AquacultureStartup), nameof(AquariumMaterialPostfix)));
            }
        }

        public static void AquariumMaterialPostfix(object __instance, ref Material __result)
        {
            Thing fish = aquariumFishThingField?.GetValue(__instance) as Thing;
            CompFishTraits traits = fish?.TryGetComp<CompFishTraits>();
            if (traits?.HasVisualTraits == true)
            {
                if (traits.cachedAquariumMaterial == null || traits.cachedAquariumTraitRevision != traits.traitRevision || traits.cachedAquariumCacheRevision != FishVisualCache.CacheRevision)
                {
                    traits.cachedAquariumMaterial = FishVisualCache.MaterialFor(fish, traits, fish.DefaultGraphic);
                    traits.cachedAquariumTraitRevision = traits.traitRevision;
                    traits.cachedAquariumCacheRevision = FishVisualCache.CacheRevision;
                }
                __result = traits.cachedAquariumMaterial;
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Graphic), MethodType.Getter)]
    public static class ThingGraphic_FishTraits_Patch
    {
        public static void Postfix(Thing __instance, ref Graphic __result)
        {
            if (__result == null || !FishUtility.IsRuntimeFish(__instance.def)) return;
            CompFishTraits traits = __instance.TryGetComp<CompFishTraits>();
            if (traits == null) return;
            if (FishVisualCache.TryGetReplacementGraphic(__instance, traits, __result, out Graphic replacement))
                __result = replacement;
        }
    }

    [HarmonyPatch(typeof(CompRottable), "TickInterval")]
    public static class LivingFishRottablePatch
    {
        private const float AirTicksToDeath = 10000f;

        public static bool Prefix(CompRottable __instance, int delta)
        {
            if (!FishUtility.IsRuntimeFish(__instance.parent.def)) return true;
            CompFishTraits traits = __instance.parent.TryGetComp<CompFishTraits>();
            if (traits == null) return true;
            if (!traits.IsAlive) return true;
            __instance.RotProgress = 0f;
            if (traits.IsInPond) return false;
            traits.airExposureTicks += GenTemperature.RotRateAtTemperature(__instance.parent.AmbientTemperature) * delta;
            if (traits.airExposureTicks >= AirTicksToDeath) traits.MarkDead();
            return false;
        }
    }

    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.CompInspectStringExtra))]
    public static class LivingFishRottableInspectPatch
    {
        public static void Postfix(CompRottable __instance, ref string __result)
        {
            if (!FishUtility.IsRuntimeFish(__instance.parent.def)) return;
            CompFishTraits traits = __instance.parent.TryGetComp<CompFishTraits>();
            if (traits?.IsAlive != true) return;
            if (traits.IsInPond) __result = "Alive (supported by pond water)";
            else __result = "Alive out of water - dies in " + Mathf.Max(0, Mathf.RoundToInt(10000f - traits.airExposureTicks)).ToStringTicksToPeriod();
        }
    }

    [HarmonyPatch]
    public static class LivingFishDeteriorationPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(SteadyEnvironmentEffects).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == nameof(SteadyEnvironmentEffects.FinalDeteriorationRate));
        }

        public static void Postfix(Thing t, ref float __result)
        {
            if (t != null && FishUtility.IsRuntimeFish(t.def) && t.TryGetComp<CompFishTraits>()?.IsAlive == true) __result = 0f;
        }
    }

}
