# Compatibility Contract

## Required

- Harmony
- Vanilla Fishing Expanded
- Vanilla Fishing Expanded - Fishing Treasures AddOn

## Optional

- Aquariums!: detected without an assembly reference. Compatible fish are recognized through loaded defs and aquarium comps.
- Odyssey: fishing research is used as the Pondkeeping prerequisite when available.
- Additional fish packs: fish in the Vanilla Fishing categories are discovered automatically.

## Load Order

Load Harmony and the two required fishing mods first. Aquariums may load before Aquaculture - Fishing. No compatibility mod should be required for ordinary fish packs.

## Save And Removal

Pond state, fish traits, life-cycle state, eggs, schools, management policies, containers, feeders, masks, fishing attempts, species knowledge, and fishing expertise are serialized. Fishing progression uses additive save nodes and fish `defName` keys, so existing saves load with empty progression while newly added fish packs receive default Untrained requirements. Removing a fish content mod from an active save can still invalidate that mod's pond fish defs and is not supported.
