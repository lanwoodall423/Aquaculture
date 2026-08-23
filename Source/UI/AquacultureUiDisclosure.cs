using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AquacultureFishing
{
    /// <summary>
    /// Runtime adapter for the pure disclosure contract. It reads existing authorities at the
    /// snapshot boundary and never persists or mutates knowledge/gameplay state.
    /// </summary>
    public static class AquacultureUiDisclosure
    {
        public static AquacultureUiDisclosureSnapshot CaptureCapabilities()
        {
            return new AquacultureUiDisclosureSnapshot(
                AquacultureProgression.IsAvailable("AF_Pondkeeping"),
                AquacultureProgression.IsAvailable("AF_ManagedAquaculture"),
                AquacultureProgression.IsAvailable("AF_IndustrialAquaculture"),
                AquacultureProgression.IsAvailable("AF_SelectiveBreeding"),
                false, false, false, false, false, false, false, false);
        }

        public static AquacultureUiDisclosureSnapshot CaptureJournal(
            IEnumerable<AquacultureSpeciesViewSnapshot> species, int pondCount,
            IEnumerable<NaturalWaterViewSnapshot> waters, IEnumerable<FishBreedRecord> breeds,
            CommissionUiSnapshot commission)
        {
            List<AquacultureSpeciesViewSnapshot> speciesRows = (species ?? Enumerable.Empty<AquacultureSpeciesViewSnapshot>())
                .Where(item => item != null).ToList();
            List<NaturalWaterViewSnapshot> waterRows = (waters ?? Enumerable.Empty<NaturalWaterViewSnapshot>())
                .Where(item => item != null).ToList();
            bool hasKnownSpecies = speciesRows.Any(item => item.identityKnown);
            bool hasEstablishedSpecies = speciesRows.Any(item => item.record?.establishedTick >= 0);
            bool hasKnownHealth = speciesRows.Any(item => HasFacet(item, "health"));
            bool hasKnownPopulation = speciesRows.Any(item => HasFacet(item, "population"));
            bool hasConservationKnowledge = HasConservationKnowledge(speciesRows, waterRows);
            bool hasBreeds = (breeds ?? Enumerable.Empty<FishBreedRecord>()).Any(item => item != null && !item.id.NullOrEmpty());
            AquacultureUiDisclosureSnapshot capabilities = CaptureCapabilities();
            return new AquacultureUiDisclosureSnapshot(
                capabilities.PondkeepingAvailable, capabilities.ManagedAquacultureAvailable,
                capabilities.IndustrialAquacultureAvailable, capabilities.SelectiveBreedingAvailable,
                pondCount > 0, hasKnownSpecies, hasEstablishedSpecies, hasKnownHealth, hasKnownPopulation,
                hasConservationKnowledge, hasBreeds, commission != null);
        }

        public static AquacultureFishDossierDisclosure ForFish(AquacultureUiDisclosureSnapshot capabilities,
            bool initialized, bool identityKnown, bool traitsKnown, bool sizeKnown, bool healthKnown,
            bool breedingKnown, bool hasRegisteredBreed, bool sterilized)
        {
            AquacultureUiDisclosureSnapshot safe = capabilities ?? AquacultureUiDisclosureSnapshot.None;
            return new AquacultureFishDossierDisclosure(initialized, identityKnown, traitsKnown, sizeKnown,
                healthKnown, breedingKnown, hasRegisteredBreed, sterilized,
                safe.ManagedAquacultureAvailable, safe.IndustrialAquacultureAvailable,
                safe.SelectiveBreedingAvailable);
        }

        public static bool HasFacet(AquacultureSpeciesViewSnapshot snapshot, string facet)
        {
            return snapshot != null && snapshot.knownFacets != null &&
                snapshot.knownFacets.Contains(facet, StringComparer.Ordinal);
        }

        /// <summary>
        /// Tests a Knowledge facet only for the species represented by the observed rows. Every
        /// observed species must carry the facet; a colony-wide facet for another species or a
        /// partial pond observation must not authorize an aggregate exact diagnostic.
        /// </summary>
        public static bool HasFacetForFish(IEnumerable<AquacultureSpeciesViewSnapshot> species,
            IEnumerable<ThingDef> fishDefs, string facet)
        {
            HashSet<ThingDef> relevant = new HashSet<ThingDef>((fishDefs ?? Enumerable.Empty<ThingDef>())
                .Where(def => def != null));
            if (relevant.Count == 0) return false;
            List<AquacultureSpeciesViewSnapshot> rows = (species ?? Enumerable.Empty<AquacultureSpeciesViewSnapshot>())
                .Where(item => item?.fishDef != null && relevant.Contains(item.fishDef)).ToList();
            return relevant.All(def => rows.Any(item => item.identityKnown && item.fishDef == def && HasFacet(item, facet)));
        }

        /// <summary>
        /// Exact water totals require a population claim for every species in the prepared view;
        /// one known species cannot authorize an aggregate over hidden species.
        /// </summary>
        public static bool HasCompleteFacetForWater(IEnumerable<AquacultureSpeciesViewSnapshot> species,
            NaturalWaterViewSnapshot water, string facet)
        {
            if (water?.conservation == null || water.conservation.Count == 0) return false;
            List<AquacultureSpeciesViewSnapshot> rows = (species ?? Enumerable.Empty<AquacultureSpeciesViewSnapshot>())
                .Where(item => item?.fishDef != null && item.identityKnown).ToList();
            return water.conservation.Keys.All(def => rows.Any(item => item.fishDef == def && HasFacet(item, facet)));
        }

        public static bool HasConservationKnowledge(IEnumerable<AquacultureSpeciesViewSnapshot> species,
            IEnumerable<NaturalWaterViewSnapshot> waters)
        {
            List<ThingDef> knownPopulationSpecies = (species ?? Enumerable.Empty<AquacultureSpeciesViewSnapshot>())
                .Where(item => item?.fishDef != null && item.identityKnown && HasFacet(item, "population"))
                .Select(item => item.fishDef).Distinct().ToList();
            if (knownPopulationSpecies.Count == 0) return false;
            foreach (NaturalWaterViewSnapshot water in waters ?? Enumerable.Empty<NaturalWaterViewSnapshot>())
            {
                if (water?.conservation == null) continue;
                if (knownPopulationSpecies.Any(fish => water.conservation.ContainsKey(fish))) return true;
            }
            return false;
        }
    }
}
