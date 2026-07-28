using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    public enum PondOrganismRole
    {
        Producer,
        Zooplankton,
        Detritivore,
        FilterFeeder
    }

    public sealed class PondOrganismDef : Def
    {
        public PondOrganismRole role;
        public bool freshwater = true;
        public bool saltwater = true;
        public bool brackishwater = true;
        public float seedBiomass = 0.12f;
        public float capacityPerCell = 0.04f;
        public float growthPerHour = 0.035f;
        public float pondBeauty;

        public bool Compatible(PondWaterKind water)
        {
            switch (water)
            {
                case PondWaterKind.Saltwater: return saltwater;
                case PondWaterKind.Brackishwater: return brackishwater;
                default: return freshwater;
            }
        }
    }

    public sealed class PondOrganismPopulation : IExposable
    {
        public string organismDefName;
        public float biomass;

        public PondOrganismDef Organism => DefDatabase<PondOrganismDef>.GetNamedSilentFail(organismDefName);

        public void ExposeData()
        {
            Scribe_Values.Look(ref organismDefName, "organismDefName");
            Scribe_Values.Look(ref biomass, "biomass");
            biomass = Mathf.Max(0f, biomass);
        }
    }

    public sealed class CompProperties_PondCulture : CompProperties
    {
        public PondOrganismDef organism;

        public CompProperties_PondCulture()
        {
            compClass = typeof(CompPondCulture);
        }
    }

    public sealed class CompPondCulture : ThingComp
    {
        private PondOrganismDef Organism => ((CompProperties_PondCulture)props).organism;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (!parent.Spawned || Organism == null) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Seed Pond",
                defaultDesc = "Release one " + Organism.label + " culture into a compatible constructed pond. It becomes a named biomass population in the pond ecosystem.",
                icon = TexCommand.Install,
                action = () => Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target => Seed(target.Cell))
            };
        }

        private void Seed(IntVec3 cell)
        {
            Map map = parent.Map;
            if (map == null || !cell.InBounds(map) || map.terrainGrid.TerrainAt(cell).defName != "AF_Pond")
            {
                Messages.Message("Select a constructed pond cell.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            FishPondMapComponent component = map.GetComponent<FishPondMapComponent>();
            PondWaterKind water = component?.WaterKindAt(cell) ?? PondWaterKind.Freshwater;
            if (!Organism.Compatible(water))
            {
                Messages.Message(Organism.LabelCap + " cannot live in " + water.ToString().ToLowerInvariant() + " ponds.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (component?.SeedOrganisms(cell, Organism) != true)
            {
                Messages.Message("No connected pond was found.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message(Organism.LabelCap + " established in the pond.", MessageTypeDefOf.PositiveEvent, false);
            if (parent.stackCount > 1) parent.stackCount--;
            else parent.Destroy(DestroyMode.Vanish);
        }
    }

    public sealed class PlaceWorker_NaturalWaterNearby : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
            Thing thingToIgnore = null, Thing thing = null)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(loc, 3f, true))
            {
                if (!cell.InBounds(map)) continue;
                TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                if (terrain.IsWater && terrain.defName != "AF_Pond") return true;
            }
            return "Must be placed within three cells of natural water.";
        }
    }

    public sealed partial class FishPondMapComponent
    {
        public bool SeedOrganisms(IntVec3 cell, PondOrganismDef organism)
        {
            EnsurePondState();
            if (organism == null || !pondByCell.TryGetValue(cell, out PondState pond) ||
                !organism.Compatible(pond.ecology.waterKind)) return false;
            if (pond.ecology.organisms == null) pond.ecology.organisms = new List<PondOrganismPopulation>();
            PondOrganismPopulation population = pond.ecology.organisms.FirstOrDefault(item => item.organismDefName == organism.defName);
            if (population == null)
            {
                population = new PondOrganismPopulation { organismDefName = organism.defName };
                pond.ecology.organisms.Add(population);
            }
            float capacity = Mathf.Max(0.05f, pond.info.cells.Count * organism.capacityPerCell);
            population.biomass = Mathf.Min(capacity, population.biomass + Mathf.Max(0.01f, organism.seedBiomass));
            pond.menuSnapshot = null;
            pond.beautyDirty = true;
            return true;
        }

        private static float OrganismBiomass(PondState pond, PondOrganismRole role)
        {
            if (pond?.ecology?.organisms == null) return 0f;
            float total = 0f;
            for (int i = 0; i < pond.ecology.organisms.Count; i++)
                if (pond.ecology.organisms[i].Organism?.role == role) total += pond.ecology.organisms[i].biomass;
            return total;
        }

        private static float OrganismCapacity(PondState pond, PondOrganismRole role)
        {
            if (pond?.ecology?.organisms == null) return 0f;
            float total = 0f;
            for (int i = 0; i < pond.ecology.organisms.Count; i++)
            {
                PondOrganismDef def = pond.ecology.organisms[i].Organism;
                if (def?.role == role) total += pond.info.cells.Count * def.capacityPerCell;
            }
            return total;
        }
    }
}
