# Compatibility Contract

## Required

- Harmony
- Vanilla Fishing Expanded
- Vanilla Fishing Expanded - Fishing Treasures AddOn

## Optional

- Aquariums!: detected without an assembly reference. Compatible fish are recognized through loaded defs and aquarium comps.
- Odyssey: fishing research is used as the Pondkeeping prerequisite when available.
- Additional fish packs: fish in the Vanilla Fishing categories are discovered automatically. `AquaticSpeciesExtension` can provide habitat, biome, temperature, season, rarity, density, and suitability metadata; unextended VFE fish reuse VFE water, biome, and commonality metadata.

## Load Order

Load Harmony and the two required fishing mods first. Aquariums may load before Aquaculture - Fishing. No compatibility mod should be required for ordinary fish packs.

## Save And Removal

Pond state, natural connected-water populations, fish traits, life-cycle state, eggs, schools, management policies, containers, feeders, masks, fishing attempts, species knowledge, fishing expertise, installed rod tackle, pending rod upgrades, fish-food provenance, and the aggregate pond-swimming effect are serialized. New natural-population records and lifecycle fields are additive. Existing Odyssey scalar populations and VFE zone species seed first initialization; later framework queries cannot silently reintroduce extinct species. Closed-water zero-population tombstones persist, while open-water reintroduction occurs only through bounded migration or an explicit compatible stocking call. Numerical excess is reduced gradually, so loading an old save does not abruptly delete fish populations. Missing natural records initialize deterministically from world seed, map identity, connected-water anchor, and loaded defs. New traits remain ordinary additive fish trait names and use the existing egg/offspring inheritance records. Food and pawns lacking new comp or hediff data use neutral defaults. Fishing progression uses additive save nodes and fish `defName` keys, so existing saves load with empty progression while newly added fish packs receive default Untrained requirements. Existing saves must craft or obtain and equip a fishing rod before starting new fishing jobs. Untouched rods use neutral default parts. Removing a fish content mod from an active save can still invalidate that mod's pond fish defs and is not supported.
