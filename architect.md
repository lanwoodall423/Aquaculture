# Natural Fish Population Architecture

## Objective

Replace framework-local species selection with one deterministic habitat population model for natural water. Population and diversity are derived from connected water cells, constrained by habitat, biome, temperature, season, rarity, density, and carrying capacity, and consumed by both VFE and Odyssey fishing.

## Stable ownership

- `NaturalFishPopulationMapComponent` owns additive save records for natural water on one map.
- One `NaturalWaterPopulation` is keyed by the lexicographically lowest connected water cell. Every cell in that body resolves to the same record.
- Odyssey `WaterBody.cells` is the preferred connected-region source. A bounded cardinal flood fill over `TerrainDef.IsWater` is the fallback when Odyssey is unavailable.
- Constructed `AF_Pond` cells remain owned by `FishPondMapComponent` and are excluded.
- VFE and Odyssey job, placement, reservation, and presentation behavior remain external. Aquaculture owns species pairing and one-fish depletion.
- The bottom-left water readout replaces Odyssey's one-entry fish summary with the complete shared population ledger; it does not maintain a second UI population source.
- Odyssey's Fishing tab reads the same prepared ledger summary for total, capacity, species rows, estimates, and fishing-threshold checks. UI reads never discover water regions or run lifecycle simulation.

## Def contracts

- `FishPopulationHabitatDef` configures habitat classification, density, carrying capacity, a smooth cell-count diversity curve, viable and breeding population floors, local variation, lifecycle cadence, mortality, breeding, migration, and over-cap rebalancing for Pond, Lake, River, Marsh, Coastal, and Ocean habitats.
- `AquaticSpeciesExtension` adds compatible habitats, biome allow/deny lists, preferred seasons, population temperature limits, rarity, density, and per-habitat suitability.
- Fish without an extension remain supported through loaded VFE/Odyssey water metadata and conservative name/profile inference.

## Determinism

- Generation uses stable hashes of world seed, map identity, connected-body anchor, species defName, and purpose salts.
- It never consumes global `Rand` state.
- Weighted deterministic ranking favors common and suitable fish while allowing uncommon and rare fish at decreasing frequency.
- Local catch weights use the same body population plus a small deterministic cell/species variation, so connected spots remain similar but not identical.

## Population lifecycle

- Initial records import established Odyssey population/species and VFE zone species where available, then add compatible species toward the habitat diversity target.
- Imported entries count toward that target only when they are compatible and currently present. Remaining slots are filled deterministically, and capacity is divided across the selected set with a breeding-viable floor.
- Capacity and diversity scale from connected cell count through habitat defs, never map size. Diversity is also capped by the number of viable species populations the carrying capacity can support.
- Pond, lake, and marsh records are closed populations after initialization. Coastal records are open and migrate only with other coastal records. Closed habitats receive no automatic species additions or migration; only surviving populations at or above the breeding floor reproduce, and zero-valued extinction tombstones prevent stale framework lists from silently restoring a lost species.
- Connected-body membership and formatted population summaries are cached by the map component. Terrain changes debounce a topology rebuild; catches, explicit stocking, generation, breeding, mortality, and migration refresh only the affected summary. Lifecycle work wakes at the next record-specific interval rather than polling every 2,500 ticks.
- River, coastal, and ocean records receive bounded same-category migration during the same infrequent lifecycle pass. Compatibility remains authoritative, pressure controls direction and magnitude, and matching populations in the same open-water category bias the incoming species mix.
- Natural mortality represents aging/death. Breeding is capacity-limited, habitat/biome/temperature/season constrained, and cannot restart a nonviable population. Over-cap totals decline gradually.
- Records from existing saves are additive. Missing records initialize lazily; retained species survive changed defs, seasons, temperatures, and reclassification.
- A successful catch removes one unit from the selected species and total population. Failed catches do not deplete the body.

## Compatibility verification

- Old saves without population records load with an empty list and initialize lazily.
- Missing fish defs are pruned only from serialized natural-water records after load.
- Existing VFE zone lists and Odyssey body populations seed initial records rather than being overwritten first. Once a closed-water record is initialized, repeated framework lists cannot reintroduce an extinct species.
- Framework species lists seed a resolved natural-water record during initialization; after that, the natural-water ledger is authoritative for species access.

## Knowledge and expertise interface

The Journal Expertise page delegates presentation to `KnowledgeFramework.KnowledgeMenuUI` while
continuing to read `aquacultureFishingProgression` directly. Colonist mode preserves per-pawn
expertise and species knowledge. Colony mode uses the best expertise record and accumulated,
clamped normalized knowledge for each fish species; no aggregate data is persisted.
