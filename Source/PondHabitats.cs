using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public enum PondHabitatKind
    {
        PlantCover,
        RockShelter,
        Substrate,
        Current
    }

    public sealed class CompProperties_PondHabitat : CompProperties
    {
        public PondHabitatKind kind;
        public float capacity;
        public float pondBeauty;

        public CompProperties_PondHabitat()
        {
            compClass = typeof(CompPondHabitat);
        }
    }

    public sealed class CompPondHabitat : ThingComp
    {
        public CompProperties_PondHabitat HabitatProps => (CompProperties_PondHabitat)props;
        public bool Active => HabitatProps.kind != PondHabitatKind.Current ||
            parent.TryGetComp<CompPowerTrader>()?.PowerOn == true;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            parent.Map?.GetComponent<FishPondMapComponent>()?.NotifyHabitatChanged(parent.Position);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            map?.GetComponent<FishPondMapComponent>()?.NotifyHabitatChanged(parent.Position);
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            string status = HabitatProps.kind == PondHabitatKind.Current && !Active ? "Inactive: no power" : "Active";
            return HabitatLabel(HabitatProps.kind) + "\nSupport: " + HabitatProps.capacity.ToString("0.#") +
                " fish\n" + status;
        }

        public static string HabitatLabel(PondHabitatKind kind)
        {
            switch (kind)
            {
                case PondHabitatKind.PlantCover: return "Plant cover";
                case PondHabitatKind.RockShelter: return "Rock shelter";
                case PondHabitatKind.Substrate: return "Prepared substrate";
                default: return "Water current";
            }
        }
    }

    public sealed class PlaceWorker_PondHabitat : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
            Thing thingToIgnore = null, Thing thing = null)
        {
            CellRect rect = GenAdj.OccupiedRect(loc, rot, checkingDef.Size);
            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map) || map.terrainGrid.TerrainAt(cell).defName != "AF_Pond")
                    return "Must be built inside a constructed pond.";
                if (cell.GetThingList(map).Any(existing => existing != thingToIgnore &&
                    existing.TryGetComp<CompPondHabitat>() != null))
                    return "Only one habitat structure can occupy a pond cell.";
            }
            return true;
        }
    }

    public sealed class PondHabitatSnapshot
    {
        public int plantStructures;
        public int shelterStructures;
        public int substrateStructures;
        public int aerators;
        public int activeAerators;
        public float plantDemand;
        public float shelterDemand;
        public float substrateDemand;
        public float currentDemand;
        public float openWaterDemand;
        public float plantSupply;
        public float shelterSupply;
        public float substrateSupply;
        public float currentSupply;
        public float openWaterSupply;
        public float overallFit = 1f;
        public float averageFishFit = 1f;
        public int stressedFish;
        public float beauty;
        public int naturalCapacity;
        public int effectiveCapacity;
        public string developmentPath;
        public readonly List<IntVec3> plantCells = new List<IntVec3>();
        public readonly List<IntVec3> shelterCells = new List<IntVec3>();
        public readonly List<IntVec3> substrateCells = new List<IntVec3>();
        public readonly List<IntVec3> currentCells = new List<IntVec3>();

        public float PlantFit => DemandFit(plantSupply, plantDemand);
        public float ShelterFit => DemandFit(shelterSupply, shelterDemand);
        public float SubstrateFit => DemandFit(substrateSupply, substrateDemand);
        public float CurrentFit => DemandFit(currentSupply, currentDemand);
        public float OpenWaterFit => DemandFit(openWaterSupply, openWaterDemand);

        private static float DemandFit(float supply, float demand) => demand <= 0.001f ? 1f : Mathf.Clamp01(supply / demand);
    }

    public sealed partial class FishPondMapComponent
    {
        public void NotifyHabitatChanged(IntVec3 cell)
        {
            EnsurePondState();
            if (pondByCell.TryGetValue(cell, out PondState pond))
            {
                pond.habitatDirty = true;
                pond.menuSnapshot = null;
                pond.beautyDirty = true;
                for (int i = 0; i < pond.schools.Count; i++) pond.schools[i].runtime.targetUntilTick = 0;
            }
        }

        public PondHabitatSnapshot HabitatAt(IntVec3 cell)
        {
            EnsurePondState();
            if (!pondByCell.TryGetValue(cell, out PondState pond)) return null;
            return EnsureHabitat(pond);
        }

        private PondHabitatSnapshot EnsureHabitat(PondState pond)
        {
            if (!pond.habitatDirty && pond.habitat != null) return pond.habitat;
            PondHabitatSnapshot habitat = BuildHabitatSnapshot(pond);
            pond.habitat = habitat;
            pond.habitatDirty = false;
            return habitat;
        }

        private PondHabitatSnapshot BuildHabitatSnapshot(PondState pond)
        {
            var habitat = new PondHabitatSnapshot();
            int occupiedCells = 0;
            for (int i = 0; i < pond.info.cells.Count; i++)
            {
                IntVec3 cell = pond.info.cells[i];
                List<Thing> things = cell.GetThingList(map);
                for (int thingIndex = 0; thingIndex < things.Count; thingIndex++)
                {
                    CompPondHabitat comp = things[thingIndex].TryGetComp<CompPondHabitat>();
                    if (comp == null) continue;
                    occupiedCells++;
                    switch (comp.HabitatProps.kind)
                    {
                        case PondHabitatKind.PlantCover:
                            habitat.plantStructures++;
                            habitat.plantSupply += comp.HabitatProps.capacity;
                            habitat.plantCells.Add(cell);
                            break;
                        case PondHabitatKind.RockShelter:
                            habitat.shelterStructures++;
                            habitat.shelterSupply += comp.HabitatProps.capacity;
                            habitat.shelterCells.Add(cell);
                            break;
                        case PondHabitatKind.Substrate:
                            habitat.substrateStructures++;
                            habitat.substrateSupply += comp.HabitatProps.capacity;
                            habitat.substrateCells.Add(cell);
                            break;
                        case PondHabitatKind.Current:
                            habitat.aerators++;
                            if (comp.Active)
                            {
                                habitat.activeAerators++;
                                habitat.currentSupply += comp.HabitatProps.capacity;
                                habitat.currentCells.Add(cell);
                            }
                            break;
                    }
                    if (comp.Active) habitat.beauty += comp.HabitatProps.pondBeauty;
                }
            }
            habitat.openWaterSupply = Mathf.Max(0f, pond.info.cells.Count - occupiedCells) * 2f;
            habitat.naturalCapacity = Mathf.Max(1, Mathf.FloorToInt(pond.info.cells.Count *
                (AquacultureMod.Settings?.fishCapacityPerCell ?? 2f)));
            habitat.effectiveCapacity = habitat.naturalCapacity + habitat.activeAerators * 8;
            habitat.developmentPath = habitat.activeAerators > 0 && pond.ecology.automaticFeeding
                ? "Intensive aquaculture"
                : habitat.activeAerators > 0 || pond.ecology.preparedFeed > 0.01f
                    ? "Managed hybrid"
                    : "Natural ecosystem";
            for (int i = 0; i < pond.fish.Count; i++)
                AddHabitatDemand(pond.fish[i], habitat);
            float weightedDemand = habitat.plantDemand + habitat.shelterDemand + habitat.substrateDemand +
                habitat.currentDemand + habitat.openWaterDemand;
            habitat.overallFit = weightedDemand <= 0.001f ? 1f :
                (habitat.PlantFit * habitat.plantDemand + habitat.ShelterFit * habitat.shelterDemand +
                 habitat.SubstrateFit * habitat.substrateDemand + habitat.CurrentFit * habitat.currentDemand +
                 habitat.OpenWaterFit * habitat.openWaterDemand) / weightedDemand;
            float totalFishFit = 0f;
            int stressed = 0;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                float fit = HabitatFitForFish(pond.fish[i], habitat);
                totalFishFit += fit;
                if (pond.fish[i].habitatStress > 0.05f) stressed++;
            }
            habitat.averageFishFit = pond.fish.Count == 0 ? 1f : totalFishFit / pond.fish.Count;
            habitat.stressedFish = stressed;
            return habitat;
        }

        private static void AddHabitatDemand(CompFishTraits fish, PondHabitatSnapshot habitat)
        {
            if (fish == null) return;
            float plant = 0f;
            float shelter = 0f;
            float substrate = 0f;
            float current = 0f;
            float openWater = 0f;
            bool young = fish.traitDefNames.Contains(FishTraitUtility.Fry) ||
                fish.traitDefNames.Contains(FishTraitUtility.Juvenile);
            string name = fish.parent.def.defName.ToLowerInvariant() + " " + fish.parent.def.label.ToLowerInvariant();
            if (young) plant += 1f;
            if (fish.Diet == FishDiet.Herbivore) plant += 0.75f;
            if (fish.Livebearer) plant += 0.35f;
            if (fish.Solitary) shelter += 1f;
            else if (fish.Diet == FishDiet.Carnivore) shelter += 0.25f;
            if (Contains(name, "eel", "angler", "lobster", "crab")) shelter += 0.6f;
            if (fish.Diet == FishDiet.Detritivore) substrate += 1f;
            if (Contains(name, "flounder", "halibut", "catfish", "lobster", "crab", "shrimp"))
                substrate += 0.7f;
            if (fish.Diet == FishDiet.FilterFeeder) current += 1f;
            if (!fish.Solitary) openWater += Mathf.Clamp(fish.SchoolingFactor, 0.25f, 1.25f) * 0.5f;
            if (fish.SizeFactor > 1.35f) openWater += 0.75f;

            AquaticSpeciesExtension extension = fish.parent.def.GetModExtension<AquaticSpeciesExtension>();
            if (extension != null)
            {
                if (extension.plantCoverDemand >= 0f) plant = extension.plantCoverDemand;
                if (extension.shelterDemand >= 0f) shelter = extension.shelterDemand;
                if (extension.substrateDemand >= 0f) substrate = extension.substrateDemand;
                if (extension.currentDemand >= 0f) current = extension.currentDemand;
                if (extension.openWaterDemand >= 0f) openWater = extension.openWaterDemand;
            }
            habitat.plantDemand += plant;
            habitat.shelterDemand += shelter;
            habitat.substrateDemand += substrate;
            habitat.currentDemand += current;
            habitat.openWaterDemand += openWater;
        }

        private void UpdateHabitat(PondState pond, float hours)
        {
            pond.habitatDirty = true;
            PondHabitatSnapshot habitat = EnsureHabitat(pond);
            float totalFit = 0f;
            int stressed = 0;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits fish = pond.fish[i];
                float fit = HabitatFitForFish(fish, habitat);
                fish.habitatFit = fit;
                if (fit < 0.55f) fish.habitatStress = Mathf.Min(1f,
                    fish.habitatStress + (0.55f - fit) * hours / 36f);
                else fish.habitatStress = Mathf.Max(0f, fish.habitatStress - hours / 24f);
                totalFit += fit;
                if (fish.habitatStress > 0.05f) stressed++;
            }
            habitat.averageFishFit = pond.fish.Count == 0 ? 1f : totalFit / pond.fish.Count;
            habitat.stressedFish = stressed;
        }

        private static float HabitatFitForFish(CompFishTraits fish, PondHabitatSnapshot habitat)
        {
            float total = 0f;
            float demand = 0f;
            var individual = new PondHabitatSnapshot();
            AddHabitatDemand(fish, individual);
            AddFit(individual.plantDemand, habitat.PlantFit, ref total, ref demand);
            AddFit(individual.shelterDemand, habitat.ShelterFit, ref total, ref demand);
            AddFit(individual.substrateDemand, habitat.SubstrateFit, ref total, ref demand);
            AddFit(individual.currentDemand, habitat.CurrentFit, ref total, ref demand);
            AddFit(individual.openWaterDemand, habitat.OpenWaterFit, ref total, ref demand);
            return demand <= 0.001f ? 1f : Mathf.Lerp(0.45f, 1f, total / demand);
        }

        private static void AddFit(float weight, float fit, ref float total, ref float demand)
        {
            if (weight <= 0f) return;
            total += weight * fit;
            demand += weight;
        }

        private Vector2 HabitatTargetFor(CompFishTraits fish, PondState pond, Vector2 fallback)
        {
            PondHabitatSnapshot habitat = EnsureHabitat(pond);
            List<IntVec3> cells = null;
            bool young = fish.traitDefNames.Contains(FishTraitUtility.Fry) ||
                fish.traitDefNames.Contains(FishTraitUtility.Juvenile);
            string name = fish.parent.def.defName.ToLowerInvariant() + " " + fish.parent.def.label.ToLowerInvariant();
            if ((young || fish.Diet == FishDiet.Herbivore || fish.Livebearer) && habitat.plantCells.Count > 0)
                cells = habitat.plantCells;
            else if ((fish.Solitary || fish.Diet == FishDiet.Carnivore ||
                Contains(name, "eel", "angler", "lobster", "crab")) && habitat.shelterCells.Count > 0)
                cells = habitat.shelterCells;
            else if ((fish.Diet == FishDiet.Detritivore ||
                Contains(name, "flounder", "halibut", "catfish", "lobster", "crab", "shrimp")) &&
                habitat.substrateCells.Count > 0)
                cells = habitat.substrateCells;
            else if (fish.Diet == FishDiet.FilterFeeder && habitat.currentCells.Count > 0)
                cells = habitat.currentCells;
            if (cells == null || cells.Count == 0) return fallback;
            IntVec3 cell = cells[PositiveMod(fish.parent.thingIDNumber, cells.Count)];
            return new Vector2(cell.x + 0.5f, cell.z + 0.5f);
        }

        private static bool Contains(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
                if (value.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }

    public sealed class ITab_PondHabitat : ITab
    {
        private Vector2 scrollPosition;

        public ITab_PondHabitat()
        {
            size = new Vector2(660f, 620f);
            labelKey = "AF_PondHabitatTab";
        }

        public override bool IsVisible => SelThing is PondProxyThing &&
            AquacultureProgression.IsAvailable("AF_Pondkeeping");

        protected override void FillTab()
        {
            PondProxyThing pond = SelThing as PondProxyThing;
            FishPondMapComponent component = pond?.Map?.GetComponent<FishPondMapComponent>();
            PondHabitatSnapshot habitat = component?.HabitatAt(pond.Position);
            PondMenuSnapshot snapshot = component?.MenuSnapshotAt(pond.Position);
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(14f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Living Habitat");
            Text.Font = GameFont.Small;
            if (habitat == null)
            {
                Widgets.Label(new Rect(rect.x, rect.y + 44f, rect.width, 30f), "No habitat data is available.");
                return;
            }
            Widgets.Label(new Rect(rect.x, rect.y + 42f, rect.width, 28f),
                habitat.developmentPath + "   Fish fit: " + habitat.averageFishFit.ToStringPercent() +
                "   Capacity: " + habitat.naturalCapacity +
                (habitat.effectiveCapacity > habitat.naturalCapacity ? " + " +
                    (habitat.effectiveCapacity - habitat.naturalCapacity) + " aerated" : ""));
            string balance = snapshot == null ? "Unknown" :
                snapshot.starving > 0 || snapshot.hungry > 0 ? "Food web under pressure" :
                snapshot.organisms.Count >= 2 && snapshot.algaePercent >= 0.2f ? "Self-renewing food web" :
                snapshot.population == 0 ? "Unstocked" : "Developing food web";
            GUI.color = balance == "Food web under pressure" ? new Color(1f, 0.62f, 0.40f) : Color.gray;
            Widgets.Label(new Rect(rect.x, rect.y + 66f, rect.width, 24f),
                "Natural balance: " + balance + "   Stressed fish: " + habitat.stressedFish);
            GUI.color = Color.white;
            Rect outRect = new Rect(rect.x, rect.y + 94f, rect.width, rect.height - 94f);
            float contentHeight = 5f * 72f + 118f + (snapshot?.organisms?.Count ?? 0) * 23f;
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, contentHeight));
            Widgets.BeginScrollView(outRect, ref scrollPosition, view);
            Rect content = new Rect(0f, 0f, view.width, view.height);
            float y = 0f;
            DrawHabitatRow(content, ref y, "Plant Cover", habitat.plantStructures, habitat.plantSupply,
                habitat.plantDemand, habitat.PlantFit, "Fry, juveniles, livebearers, and herbivores");
            DrawHabitatRow(content, ref y, "Rock Shelter", habitat.shelterStructures, habitat.shelterSupply,
                habitat.shelterDemand, habitat.ShelterFit, "Solitary fish, ambush predators, eels, and crustaceans");
            DrawHabitatRow(content, ref y, "Prepared Substrate", habitat.substrateStructures, habitat.substrateSupply,
                habitat.substrateDemand, habitat.SubstrateFit, "Detritivores and bottom-dwelling species");
            DrawHabitatRow(content, ref y, "Powered Current", habitat.activeAerators, habitat.currentSupply,
                habitat.currentDemand, habitat.CurrentFit, "Filter-feeders; inactive aerators provide no current");
            DrawHabitatRow(content, ref y, "Open Water", 0, habitat.openWaterSupply,
                habitat.openWaterDemand, habitat.OpenWaterFit, "Schooling and large-bodied fish");
            y += 2f;
            Widgets.DrawLineHorizontal(content.x, y, content.width);
            y += 8f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(content.x, y, content.width, 28f), "Living Community");
            Text.Font = GameFont.Small;
            y += 30f;
            if (snapshot?.organisms == null || snapshot.organisms.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(content.x, y, content.width, 24f),
                    "No gathered pond plants or invertebrates have been established.");
                GUI.color = Color.white;
                y += 26f;
            }
            else
            {
                for (int i = 0; i < snapshot.organisms.Count; i++)
                {
                    PondOrganismSnapshot organism = snapshot.organisms[i];
                    Widgets.Label(new Rect(content.x + 8f, y, 210f, 22f), organism.label);
                    Widgets.Label(new Rect(content.x + 220f, y, 145f, 22f), organism.role);
                    Text.Anchor = TextAnchor.UpperRight;
                    Widgets.Label(new Rect(content.x + 370f, y, content.width - 378f, 22f),
                        organism.biomass.ToString("0.000") + " / " + organism.capacity.ToString("0.000") +
                        "   " + organism.percent.ToStringPercent());
                    Text.Anchor = TextAnchor.UpperLeft;
                    y += 23f;
                }
            }
            y += 4f;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(content.x, y, content.width, 48f),
                "Natural ponds sustain conservative stocking through plants, invertebrates, cover, and balanced diets. Powered aeration and prepared feed support higher industrial stocking.");
            GUI.color = Color.white;
            Widgets.EndScrollView();
        }

        private static void DrawHabitatRow(Rect rect, ref float y, string label, int structures,
            float supply, float demand, float fit, string description)
        {
            Rect row = new Rect(rect.x, y, rect.width, 64f);
            Widgets.DrawMenuSection(row);
            Widgets.Label(new Rect(row.x + 10f, row.y + 7f, 190f, 26f), label);
            string amount = demand <= 0.001f ? supply.ToString("0.#") + " supplied / no demand"
                : supply.ToString("0.#") + " / " + demand.ToString("0.#") + " fish";
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = fit < 0.55f ? new Color(1f, 0.55f, 0.35f) :
                fit < 0.999f ? new Color(1f, 0.80f, 0.35f) : new Color(0.55f, 0.92f, 0.62f);
            Widgets.Label(new Rect(row.x + 205f, row.y + 7f, row.width - 215f, 26f),
                amount + "   " + fit.ToStringPercent());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(row.x + 10f, row.y + 35f, row.width - 20f, 22f),
                (structures > 0 ? structures + " structure" + (structures == 1 ? "" : "s") + ". " : "") + description);
            GUI.color = Color.white;
            y += 72f;
        }
    }
}
