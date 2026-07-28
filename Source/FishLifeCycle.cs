using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public sealed class FishEggThing : ThingWithComps
    {
        public string fishDefName;
        public List<string> inheritedTraits = new List<string>();
        public Dictionary<string, float> inheritedValues = new Dictionary<string, float>();
        public string inheritedBreedId;
        public int inheritedBreedGeneration;
        public int hatchTick;

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            int remaining = Mathf.Max(0, hatchTick - (Find.TickManager?.TicksGame ?? 0));
            string hatchLine = "Hatches in: " + remaining.ToStringTicksToPeriod();
            return text.NullOrEmpty() ? hatchLine : text + "\n" + hatchLine;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fishDefName, "fishDefName");
            Scribe_Collections.Look(ref inheritedTraits, "inheritedTraits", LookMode.Value);
            Scribe_Collections.Look(ref inheritedValues, "inheritedValues", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref inheritedBreedId, "inheritedBreedId");
            Scribe_Values.Look(ref inheritedBreedGeneration, "inheritedBreedGeneration");
            Scribe_Values.Look(ref hatchTick, "hatchTick");
            if (inheritedTraits == null) inheritedTraits = new List<string>();
            if (inheritedValues == null) inheritedValues = new Dictionary<string, float>();
        }

        public void Hatch()
        {
            if (!Spawned) return;
            ThingDef fishDef = DefDatabase<ThingDef>.GetNamedSilentFail(fishDefName);
            if (fishDef == null) { Destroy(); return; }
            IntVec3 cell = Position;
            Map map = Map;
            Thing fish = ThingMaker.MakeThing(fishDef);
            fish.TryGetComp<CompFishTraits>()?.InitializeFromEgg(inheritedTraits, inheritedValues,
                inheritedBreedId, inheritedBreedGeneration);
            Destroy();
            GenSpawn.Spawn(fish, cell, map);
        }
    }

    public sealed class CompProperties_FishMeatTraits : CompProperties
    {
        public CompProperties_FishMeatTraits() { compClass = typeof(CompFishMeatTraits); }
    }

    public sealed class CompFishMeatTraits : ThingComp
    {
        public float nutritionMultiplier = 1f;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref nutritionMultiplier, "nutritionMultiplier", 1f);
        }

        public override string CompInspectStringExtra() => nutritionMultiplier > 1.001f ? "Inherited nutrition: " + nutritionMultiplier.ToStringPercent() : null;

        public override void PostSplitOff(Thing piece)
        {
            CompFishMeatTraits split = piece?.TryGetComp<CompFishMeatTraits>();
            if (split != null) split.nutritionMultiplier = nutritionMultiplier;
        }

        public override bool AllowStackWith(Thing other)
        {
            CompFishMeatTraits comp = other?.TryGetComp<CompFishMeatTraits>();
            return comp != null && Mathf.Approximately(comp.nutritionMultiplier, nutritionMultiplier);
        }
    }

    public sealed partial class FishPondMapComponent : MapComponent
    {
        internal readonly HashSet<CompFishTraits> fish = new HashSet<CompFishTraits>();
        private readonly Dictionary<IntVec3, float> beautyByCell = new Dictionary<IntVec3, float>();
        private int nextEggCheckTick;

        public FishPondMapComponent(Map map) : base(map) { }
        public void Register(CompFishTraits comp)
        {
            if (comp != null && fish.Add(comp)) pondMembershipDirty = true;
        }

        public void Deregister(CompFishTraits comp)
        {
            if (comp != null && fish.Remove(comp))
            {
                pondMembershipDirty = true;
                if (pondByFish.TryGetValue(comp, out PondState pond))
                {
                    pond.menuSnapshot = null;
                    pond.beautyDirty = true;
                }
            }
        }

        public float PondBeautyAt(IntVec3 cell) => beautyByCell.TryGetValue(cell, out float value) ? value : 0f;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager.TicksGame;
            SchoolTick(now);
            if (now % 250 != 0) return;
            foreach (CompFishTraits comp in fish)
            {
                if (comp?.parent?.Spawned != true || !comp.IsSwimmingInPond) continue;
                if (now >= comp.nextAgeCheckTick) comp.RefreshAgeTrait();
                if (comp.lifespanTicks > 0 && now - comp.birthTick >= comp.lifespanTicks) comp.MarkDead();
            }
            EnsurePondState();
            ProcessEcology(now);
            ProcessBreeding(now);
            ProcessEggs(now);
            RebuildBeautyIfDirty();
        }

        private void ProcessBreeding(int now)
        {
            if (AquacultureMod.Settings?.globalBreedingEnabled == false) return;
            int interval = Mathf.RoundToInt((AquacultureMod.Settings?.breedingIntervalDays ?? 15f) * 60000f);
            for (int pondIndex = 0; pondIndex < pondStates.Count; pondIndex++)
            {
                PondState pond = pondStates[pondIndex];
                PondBreedingMode breedingMode = pond.ecology.breedingMode;
                if (breedingMode == PondBreedingMode.Paused) continue;
                float intervalFactor = breedingMode == PondBreedingMode.Intensive ? 0.5f : breedingMode == PondBreedingMode.Encouraged ? 0.75f : 1f;
                int availableSlots = Mathf.Max(0, EffectivePopulationLimit(pond) - pond.fish.Count);
                if (availableSlots <= 0) continue;
                for (int schoolIndex = 0; schoolIndex < pond.schools.Count; schoolIndex++)
                {
                    SchoolState school = pond.schools[schoolIndex];
                    if (now < school.nextBreedingCheckTick) continue;
                    List<CompFishTraits> members = school.members;
                    for (int femaleIndex = 0; femaleIndex < members.Count; femaleIndex++)
                    {
                        CompFishTraits female = members[femaleIndex];
                        if (!CanBreed(female, now) || !female.IsFemale) continue;
                        CompFishTraits male = null;
                        for (int maleIndex = 0; maleIndex < members.Count; maleIndex++)
                        {
                            CompFishTraits candidate = members[maleIndex];
                            if (candidate != female && CanBreed(candidate, now) && !candidate.IsFemale) { male = candidate; break; }
                        }
                        if (male == null) continue;
                        float habitatFactor = (female.HabitatBreedingFactor + male.HabitatBreedingFactor) * 0.5f;
                        female.nextBreedTick = now + Mathf.Max(250, Mathf.RoundToInt(interval * intervalFactor * female.BreedingCooldownFactor * habitatFactor));
                        male.nextBreedTick = now + Mathf.Max(250, Mathf.RoundToInt(interval * intervalFactor * male.BreedingCooldownFactor * habitatFactor));
                        int minimumOffspring = (AquacultureMod.Settings?.minimumOffspring ?? 1) + (breedingMode == PondBreedingMode.Intensive ? 1 : 0);
                        int maximumOffspring = Mathf.Max(minimumOffspring, (AquacultureMod.Settings?.maximumOffspring ?? 3)
                            + (breedingMode == PondBreedingMode.Intensive ? 2 : breedingMode == PondBreedingMode.Encouraged ? 1 : 0));
                        float speciesOffspring = AquaticSpeciesProfile.For(female.parent.def).offspringFactor;
                        minimumOffspring = Mathf.Max(1, Mathf.RoundToInt(minimumOffspring * Mathf.Lerp(1f, speciesOffspring, 0.5f)));
                        maximumOffspring = Mathf.Max(minimumOffspring, Mathf.RoundToInt(maximumOffspring * speciesOffspring));
                        int eggCount = Mathf.Min(availableSlots, Rand.RangeInclusive(minimumOffspring, maximumOffspring));
                        float interventionFeed = breedingMode == PondBreedingMode.Intensive ? 0.08f * eggCount
                            : breedingMode == PondBreedingMode.Encouraged ? 0.03f * eggCount : 0f;
                        if (interventionFeed > 0f && pond.ecology.preparedFeed < interventionFeed)
                        {
                            intervalFactor = 1f;
                            female.nextBreedTick = now + Mathf.Max(250, Mathf.RoundToInt(interval * female.BreedingCooldownFactor * habitatFactor));
                            male.nextBreedTick = now + Mathf.Max(250, Mathf.RoundToInt(interval * male.BreedingCooldownFactor * habitatFactor));
                            minimumOffspring = AquacultureMod.Settings?.minimumOffspring ?? 1;
                            maximumOffspring = Mathf.Max(minimumOffspring, AquacultureMod.Settings?.maximumOffspring ?? 3);
                            minimumOffspring = Mathf.Max(1, Mathf.RoundToInt(minimumOffspring * Mathf.Lerp(1f, speciesOffspring, 0.5f)));
                            maximumOffspring = Mathf.Max(minimumOffspring, Mathf.RoundToInt(maximumOffspring * speciesOffspring));
                            eggCount = Mathf.Min(availableSlots, Rand.RangeInclusive(minimumOffspring, maximumOffspring));
                        }
                        else if (interventionFeed > 0f) pond.ecology.preparedFeed -= interventionFeed;
                        for (int eggIndex = 0; eggIndex < eggCount; eggIndex++)
                        {
                            if (female.Livebearer) SpawnLiveFry(female, male);
                            else SpawnEgg(female, male, now);
                            availableSlots--;
                        }
                        if (availableSlots <= 0) break;
                    }
                    int femaleDue = int.MaxValue;
                    int maleDue = int.MaxValue;
                    for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
                    {
                        CompFishTraits member = members[memberIndex];
                        if (!member.IsAdult || member.sterilized) continue;
                        if (member.IsFemale) femaleDue = Math.Min(femaleDue, member.nextBreedTick);
                        else maleDue = Math.Min(maleDue, member.nextBreedTick);
                    }
                    school.nextBreedingCheckTick = femaleDue == int.MaxValue || maleDue == int.MaxValue
                        ? int.MaxValue
                        : Math.Max(now + 250, Math.Max(femaleDue, maleDue));
                }
            }
        }

        private bool CanBreed(CompFishTraits fish, int now)
        {
            return fish?.IsAlive == true
                && fish.IsAdult
                && !fish.sterilized
                && now >= fish.nextBreedTick
                && fish.foodReserve >= 0.35f
                && fish.starvationProgress <= 0f
                && fish.waterStress < 0.1f
                && fish.temperatureStress < 0.1f
                && fish.habitatStress < 0.75f
                && CanBreedThisSeason(fish);
        }

        private bool CanBreedThisSeason(CompFishTraits fish)
        {
            Season required = fish.BreedingSeason;
            if (required == Season.Undefined) return true;
            Season current = GenLocalDate.Season(map);
            if (current == Season.PermanentSummer) current = Season.Summer;
            if (current == Season.PermanentWinter) current = Season.Winter;
            return current == required;
        }

        private void SpawnLiveFry(CompFishTraits first, CompFishTraits second)
        {
            Thing fish = ThingMaker.MakeThing(first.parent.def);
            BuildInheritance(first, second, out List<string> traits, out Dictionary<string, float> values,
                out string breedId, out int breedGeneration);
            fish.TryGetComp<CompFishTraits>()?.InitializeFromEgg(traits, values, breedId, breedGeneration);
            GenSpawn.Spawn(fish, first.parent.Position, map);
        }

        private void SpawnEgg(CompFishTraits first, CompFishTraits second, int now)
        {
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_FishEgg");
            if (eggDef == null) return;
            FishEggThing egg = (FishEggThing)ThingMaker.MakeThing(eggDef);
            egg.fishDefName = first.parent.def.defName;
            BuildInheritance(first, second, out List<string> inheritedTraits, out Dictionary<string, float> inheritedValues,
                out string inheritedBreedId, out int inheritedBreedGeneration);
            egg.inheritedTraits = inheritedTraits;
            egg.inheritedValues = inheritedValues;
            egg.inheritedBreedId = inheritedBreedId;
            egg.inheritedBreedGeneration = inheritedBreedGeneration;
            egg.hatchTick = now + Mathf.RoundToInt((AquacultureMod.Settings?.eggHatchDays ?? 3f) * 60000f);
            if (nextEggCheckTick == 0 || egg.hatchTick < nextEggCheckTick) nextEggCheckTick = egg.hatchTick;
            GenSpawn.Spawn(egg, first.parent.Position, map);
        }

        private static void BuildInheritance(CompFishTraits first, CompFishTraits second, out List<string> inheritedTraits,
            out Dictionary<string, float> inheritedValues, out string inheritedBreedId, out int inheritedBreedGeneration)
        {
            inheritedTraits = new List<string>();
            inheritedValues = new Dictionary<string, float>();
            inheritedBreedId = null;
            inheritedBreedGeneration = 0;
            List<string> union = first.traitDefNames.Concat(second.traitDefNames)
                .Where(name => !name.StartsWith("AF_Age_") && !name.StartsWith("AF_Sex_") && !name.StartsWith(FishTraitUtility.DietPrefix) && !name.StartsWith(FishTraitUtility.WaterPrefix))
                .Distinct().InRandomOrder().ToList();
            int cap = Mathf.Min(AquacultureMod.Settings?.maxMutations ?? 2, union.Count);
            int desired = Rand.RangeInclusive(0, cap);
            var usedGroups = new HashSet<string>();
            foreach (string name in union)
            {
                if (inheritedTraits.Count >= desired) break;
                FishTraitDef trait = DefDatabase<FishTraitDef>.GetNamedSilentFail(name);
                string group = FishTraitUtility.ExclusiveGroup(trait);
                if (trait == null || !usedGroups.Add(group)) continue;
                inheritedTraits.Add(name);
                float a = first.TraitValue(name);
                float b = second.TraitValue(name);
                float value = a > 0f && b > 0f ? (Rand.Bool ? a : b) : Mathf.Max(a, b);
                if (value > 0f) inheritedValues[name] = value;
            }
            AquacultureJournalComponent journal = AquacultureJournalComponent.Current;
            if (journal != null)
                journal.ApplyBreedInheritance(first, second, inheritedTraits, inheritedValues,
                    out inheritedBreedId, out inheritedBreedGeneration);
        }

        private void ProcessEggs(int now)
        {
            if (nextEggCheckTick > now) return;
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamedSilentFail("AF_FishEgg");
            if (eggDef == null) return;
            List<Thing> eggs = map.listerThings.ThingsOfDef(eggDef);
            nextEggCheckTick = int.MaxValue;
            for (int i = eggs.Count - 1; i >= 0; i--)
            {
                if (!(eggs[i] is FishEggThing egg)) continue;
                if (map.designationManager.DesignationOn(egg, AquacultureJobDefOf.AF_RemovePondEggDesignation) != null)
                {
                    nextEggCheckTick = Math.Min(nextEggCheckTick, now + 250);
                    continue;
                }
                if (now >= egg.hatchTick) egg.Hatch();
                else if (egg.hatchTick < nextEggCheckTick) nextEggCheckTick = egg.hatchTick;
            }
        }

        private int EffectivePopulationLimit(PondState pond)
        {
            if (pond?.ecology == null) return 0;
            if (pond.ecology.populationLimit > 0) return pond.ecology.populationLimit;
            int baseCapacity = Mathf.Max(1, Mathf.FloorToInt(pond.info.cells.Count *
                (AquacultureMod.Settings?.fishCapacityPerCell ?? 2f)));
            return baseCapacity + EnsureHabitat(pond).activeAerators * 8;
        }

        private void RebuildBeautyIfDirty()
        {
            bool dirty = false;
            for (int i = 0; i < pondStates.Count; i++) dirty |= pondStates[i].beautyDirty;
            if (!dirty) return;
            beautyByCell.Clear();
            for (int pondIndex = 0; pondIndex < pondStates.Count; pondIndex++)
            {
                PondState pond = pondStates[pondIndex];
                float beauty = 0f;
                for (int fishIndex = 0; fishIndex < pond.fish.Count; fishIndex++)
                {
                    CompFishTraits fish = pond.fish[fishIndex];
                    beauty += Mathf.Max(0f, 1f + fish.BeautyOffset + (fish.Breed?.BeautyBonus ?? 0f));
                }
                beauty += EnsureHabitat(pond).beauty;
                if (pond.ecology.organisms != null)
                    for (int organismIndex = 0; organismIndex < pond.ecology.organisms.Count; organismIndex++)
                    {
                        PondOrganismPopulation population = pond.ecology.organisms[organismIndex];
                        PondOrganismDef organism = population.Organism;
                        if (organism != null) beauty += organism.pondBeauty *
                            Mathf.Min(10f, population.biomass / Mathf.Max(0.01f, organism.seedBiomass));
                    }
                pond.beauty = beauty;
                pond.beautyDirty = false;
                pond.menuSnapshot = null;
                for (int pondCellIndex = 0; pondCellIndex < pond.info.cells.Count; pondCellIndex++)
                {
                    IntVec3 pondCell = pond.info.cells[pondCellIndex];
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(pondCell, 5f, true))
                    {
                        if (!cell.InBounds(map)) continue;
                        float contribution = beauty * Mathf.Clamp01(1f - pondCell.DistanceTo(cell) / 6f);
                        if (!beautyByCell.TryGetValue(cell, out float current) || contribution > current) beautyByCell[cell] = contribution;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(BeautyUtility), nameof(BeautyUtility.CellBeauty))]
    public static class PondBeautyPatch
    {
        public static void Postfix(IntVec3 c, Map map, ref float __result)
        {
            __result += map?.GetComponent<FishPondMapComponent>()?.PondBeautyAt(c) ?? 0f;
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue), new Type[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) })]
    public static class FishStatPatch
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            if (thing == null) return;
            if (FishUtility.IsRuntimeFish(thing.def))
            {
                CompFishTraits fish = thing.TryGetComp<CompFishTraits>();
                if (stat == StatDefOf.Beauty) __result += fish.BeautyOffset;
                if (stat == StatDefOf.Nutrition) __result *= fish.NutritionMultiplier;
                if (stat == StatDefOf.MarketValue && fish.Breed != null) __result *= fish.Breed.MarketValueFactor;
            }
            else if (stat == StatDefOf.Nutrition && thing.def.defName == "AF_FishMeat")
            {
                CompFishMeatTraits meat = thing.TryGetComp<CompFishMeatTraits>();
                if (meat != null) __result *= meat.nutritionMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
    public static class FishProcessingYieldPatch
    {
        public static void Postfix(RecipeDef recipeDef, List<Thing> ingredients, ref IEnumerable<Thing> __result)
        {
            if (recipeDef?.defName != "AF_ProcessDeadFish" || ingredients == null || __result == null) return;
            Thing dead = ingredients.FirstOrDefault(thing => thing.TryGetComp<CompFishTraits>() is CompFishTraits traits && !traits.IsAlive);
            if (dead == null) return;
            __result = Adjust(__result, dead.TryGetComp<CompFishTraits>());
        }

        private static IEnumerable<Thing> Adjust(IEnumerable<Thing> products, CompFishTraits dead)
        {
            float yield = dead.MeatYield;
            float nutrition = dead.NutritionMultiplier;
            foreach (Thing product in products)
            {
                if (product.def.defName == "AF_FishMeat")
                {
                    product.stackCount = Mathf.Max(1, Mathf.RoundToInt(product.stackCount * yield));
                    CompFishMeatTraits comp = product.TryGetComp<CompFishMeatTraits>();
                    if (comp != null) comp.nutritionMultiplier = nutrition;
                }
                yield return product;
            }
        }
    }

    [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), new Type[] { typeof(Thing) })]
    public static class DeadFishRecipeFilterPatch
    {
        private static readonly HashSet<ThingFilter> RecipeFilters = new HashSet<ThingFilter>();

        private static void EnsureFilters()
        {
            if (RecipeFilters.Count > 0) return;
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("AF_ProcessDeadFish");
            if (recipe == null) return;
            RecipeFilters.Add(recipe.fixedIngredientFilter);
            for (int i = 0; i < recipe.ingredients.Count; i++) RecipeFilters.Add(recipe.ingredients[i].filter);
        }

        public static void Postfix(ThingFilter __instance, Thing t, ref bool __result)
        {
            if (!__result || t == null || !FishUtility.IsRuntimeFish(t.def)) return;
            EnsureFilters();
            if (!RecipeFilters.Contains(__instance)) return;
            CompFishTraits traits = t.TryGetComp<CompFishTraits>();
            if (traits != null) __result = !traits.IsAlive;
        }
    }
}
