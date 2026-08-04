# Compatibility Contract

## Required

- Harmony
- Knowledge Framework
- Deferred Reality Framework
- Vanilla Fishing Expanded
- Vanilla Fishing Expanded - Fishing Treasures AddOn

## Optional

- Aquariums!: detected without an assembly reference. Compatible fish are recognized through loaded defs and aquarium comps.
- Odyssey: fishing research is used as the Pondkeeping prerequisite when available.
- Additional fish packs: fish in the Vanilla Fishing categories are discovered automatically. `AquaticSpeciesExtension` can provide habitat, biome, temperature, season, rarity, density, and suitability metadata; unextended VFE fish reuse VFE water, biome, and commonality metadata.

## Load Order

Load Harmony, Knowledge Framework, Deferred Reality Framework, and the two required fishing mods first. Aquariums may load before Aquaculture - Fishing. No compatibility mod should be required for ordinary fish packs.

## Def Mutation Contract

- Only positively identified runtime fish receive the Def-backed `CompFishTraits`, individual stack limit, realtime drawing behavior, missing Beauty/Mass defaults, and Traits tab required by fish lifecycle, rendering, stats, and save compatibility.
- Explicit compatible Beauty/Mass values are preserved. Nonstandard drawer types are preserved with one diagnostic; stack limit one remains required because fish traits and lifecycle state are individual and serialized.
- The owned dead-fish recipe receives recognized fish in its ingredient filters. Food provenance is attached to relevant output Things, not every ingestible Def.
- Runtime graphics, stat, rotting, deterioration, and integration hooks remain guarded by runtime-fish or relevant-component checks. No storage, trade, or unrelated recipe Def is changed.

## Save And Removal

Active collector commissions are additive game-component state. They store requirements, deadline, reward, and request-history keys and load without Royalty, Ideology, or another DLC. Completed commissions increment the saved breed record. If a referenced fish or breed is unavailable after loading, the active request is discarded safely rather than generating an impossible delivery.

Food provenance is attached to relevant recipe output Things rather than every ingestible Def. During loading of an existing save, the old food component is temporarily available on ingestible Defs so serialized data can deserialize, then those temporary Def entries are removed. Ordinary vanilla/modded food, storage, trade, and unrelated recipes are not modified.

Pond state, natural connected-water populations, fish traits, life-cycle state, eggs, schools, management policies, containers, feeders, masks, fishing attempts, species knowledge, fishing expertise, installed rod tackle, pending rod upgrades, fish-food provenance, and the aggregate pond-swimming effect are serialized. New natural-population records and lifecycle fields are additive. Existing Odyssey scalar populations and VFE zone species seed first initialization; later framework queries cannot silently reintroduce extinct species. Pond, lake, and marsh records are closed; river, coastal, and ocean records use bounded same-category migration. Closed-water zero-population tombstones persist, while open-water reintroduction occurs only through bounded migration or an explicit compatible stocking call. Numerical excess is reduced gradually, so loading an old save does not abruptly delete fish populations. The constructed-pond capacity model uses physical space, approximate sustainable support, industrial support, and management limits as separate derived values. Existing `fishCapacityPerCell` settings are preserved; older saves receive a one-time notice about the lower 0.75 default, and no fish are mass-removed during the transition. Missing natural records initialize deterministically from world seed, map identity, connected-water anchor, and loaded defs. New traits remain ordinary additive fish trait names and use the existing egg/offspring inheritance records. Food and pawns lacking new comp or hediff data use neutral defaults. Fishing progression uses additive save nodes and fish `defName` keys, so existing saves load with empty progression while newly added fish packs receive default Novice requirements. Existing manually equipped rods continue to work. New fishing jobs can reserve a suitable rod from direct inventory or valid map storage; temporary equipment sessions restore the previous primary weapon after normal completion, interruption, danger, drafting, cancellation, despawn, and save/load recovery. Removing a fish content mod from an active save can still invalidate that mod's pond fish defs and is not supported.
