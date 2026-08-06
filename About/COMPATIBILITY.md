# Compatibility Contract

## Required

- Harmony
- Knowledge Framework
- Deferred Reality Framework
- Vanilla Fishing Expanded

## Optional

- Vanilla Fishing Expanded - Fishing Treasures AddOn: VFE's optional fishing-loot content. Aquaculture has no direct Def, assembly, recipe, species, treasure, or patch dependency on it; when installed, load it after Vanilla Fishing Expanded and before Aquaculture for its own VFE content.
- Aquariums!: detected without an assembly reference. Compatible fish are recognized through loaded defs and aquarium comps.
- Odyssey: fishing research is used as the Pondkeeping prerequisite when available.
- Additional fish packs: fish in the Vanilla Fishing categories are discovered automatically. `AquaticSpeciesExtension` can provide habitat, biome, temperature, season, rarity, density, and suitability metadata; unextended VFE fish reuse VFE water, biome, and commonality metadata.

## Load Order

Load Harmony, Knowledge Framework, Deferred Reality Framework, and Vanilla Fishing Expanded before Aquaculture - Fishing. Fishing Treasures is optional; when present, load it after Vanilla Fishing Expanded. Aquariums may load before Aquaculture - Fishing. No compatibility mod should be required for ordinary fish packs.

## Def Mutation Contract

- Only positively identified runtime fish receive the Def-backed `CompFishTraits`, individual stack limit, realtime drawing behavior, missing Beauty/Mass defaults, and Traits tab required by fish lifecycle, rendering, stats, and save compatibility.
- Explicit compatible Beauty/Mass values are preserved. Nonstandard drawer types are preserved with one diagnostic; stack limit one remains required because fish traits and lifecycle state are individual and serialized.
- The owned dead-fish recipe receives recognized fish in its ingredient filters. Food provenance is attached to relevant output Things, not every ingestible Def.
- Runtime graphics, stat, rotting, deterioration, and integration hooks remain guarded by runtime-fish or relevant-component checks. No storage, trade, or unrelated recipe Def is changed.

## Save And Removal

Active collector commissions are additive game-component state. They store requirements, improvement kind, deadline, reward, and request-history keys and load without Royalty, Ideology, or another DLC. Completed and expired requests increment additive counters on the saved breed record. Valid active records preserve every existing requirement across save/load; malformed records with missing breed/species/trait references, invalid deadlines, or no reward are discarded while request history remains intact. The transient fish index is rebuilt after load and is never serialized.

Food provenance is attached to relevant recipe output Things rather than every ingestible Def. During loading of an existing save, the old food component is temporarily available on ingestible Defs so serialized data can deserialize, then those temporary Def entries are removed. Ordinary vanilla/modded food, storage, trade, and unrelated recipes are not modified.

Pond state, natural connected-water populations, fish traits, life-cycle state, eggs, schools, management policies, containers, feeders, masks, fishing attempts, species knowledge, fishing expertise, installed rod tackle, pending rod upgrades, fish-food provenance, and the aggregate pond-swimming effect are serialized. New natural-population records and lifecycle fields are additive. Existing Odyssey scalar populations and VFE zone species seed first initialization; later framework queries cannot silently reintroduce extinct species. Pond, lake, and marsh records are closed; river, coastal, and ocean records use bounded same-category migration. Closed-water zero-population tombstones persist, while open-water reintroduction occurs only through bounded migration or an explicit compatible stocking call. Numerical excess is reduced gradually, so loading an old save does not abruptly delete fish populations. The constructed-pond capacity model uses physical space, approximate sustainable support, industrial support, and management limits as separate derived values. Existing `fishCapacityPerCell` settings are preserved; older saves receive a one-time notice about the lower 0.75 default, and no fish are mass-removed during the transition. Missing natural records initialize deterministically from world seed, map identity, connected-water anchor, and loaded defs. New traits remain ordinary additive fish trait names and use the existing egg/offspring inheritance records. Food and pawns lacking new comp or hediff data use neutral defaults. Fishing progression uses additive save nodes and fish `defName` keys, so existing saves load with empty progression while newly added fish packs receive default Novice requirements. Existing manually equipped rods continue to work. New fishing jobs can reserve a suitable rod from direct inventory or valid map storage; temporary equipment sessions restore the previous primary weapon after normal completion, interruption, danger, drafting, cancellation, despawn, and save/load recovery. Removing a fish content mod from an active save can still invalidate that mod's pond fish defs and is not supported.
## Trait Breeding Compatibility

The trait model is additive. Existing `traitDefNames`, numeric `traitValues`, breed IDs, generations, parent IDs, eggs, and journal records remain valid. New fields only describe the qualifying registered-breed attempt and its evidence; missing fields receive safe legacy values during post-load migration.

The settings migration keeps the old `globalMutationRate` as the initial `wildExceptionalTraitChance` and `maxMutations` as the initial `maxInheritedTraits`. New settings use these safe defaults when no old value exists: 20% wild exceptional-trait chance, 55% per-parental-trait inheritance chance, two maximum inherited traits, 5% new offspring-mutation chance, one maximum new offspring mutation, and a 95% registered-breed defining-trait reliability ceiling. The old keys remain serialized for one-way compatibility.

Natural-water recovery has one authoritative rule: an extinct or below-floor species can receive regional recovery only when a positive recorded source population for the same species exists in a compatible open-water category. Rivers connect only to rivers, coasts only to coasts, and oceans only to oceans; ponds, lakes, and marshes are closed. Migration pressure and species weighting affect the rate only after this source predicate succeeds and never create a species from nowhere. Population summaries and prepared fishing views are invalidated after catches, stocking, births, mortality, migration, topology changes, source extinction, and save/load, then rebuilt from the shared cache.

Generated wild fish and temporary fish used for caught-fish records roll wild exceptional traits only. Caught fish retain the generated names and numeric values captured by the fishing attempt. Ordinary offspring evaluate every eligible trait in the parental union independently; a trait present in both parents has a 1.5x chance, incompatible successes are reduced to one per compatibility group, and the inherited cap is applied after all rolls. New offspring mutations use a separate event roll and exclude inherited names, duplicates, occupied groups, unknown defs, disabled defs, and non-mutation-eligible traits.

Registered-breed parents add a qualifying birth record. Each defining trait is then rolled separately at `resulting stability * registered-breed reliability ceiling`; a failed roll produces an ordinary offspring while retaining the qualifying attempt for stability evidence. Thus even a low-stability breed never guarantees every defining trait. Numeric values inherited from a parent remain compatible with the existing trait-value serialization.

Stability is `clamp(0.50 + success rate * 0.40 + generation contribution, 0.50, 0.98)`, where success rate is matching births divided by total qualifying births and generation contribution is `min((highest generation - 1) * 0.02, 0.10)`. Non-qualifying crosses do not change a breed record. Legacy breed records use their saved `births` as the qualifying-birth denominator and retain their prior computed stability as a floor, avoiding retroactive loss when failed-birth totals were not recorded. Unknown legacy child records are not counted a second time.
## Knowledge Framework Integration Contract

Aquaculture's optional runtime integration targets package `lan.knowledgeframework`. The supported baseline is semantic release `3.0.0-beta.1` or later with integer `ApiVersion >= 3`; the semantic release string is not used as an API generation. The adapter also requires `CapabilityVersion >= 3` and `Supports(3, capability)` for every V3 capability it calls: domains, pawn/colony knowledge, expertise, domain UI, evidence transactions, facets, confidence, discovery stages, relationships, claims, typed measurements, subject archetypes, requirement stages, observation recipes, contextual knowledge, milestones, structural relations, subject lifecycle, accrual policies, claim staleness, and transmission. If this contract is unavailable, Aquaculture retains its existing legacy expertise fallback and does not call partial V3 APIs.

Stable IDs are part of the save contract: domain `aquaculture`, global subject `aquaculture.global`, fish/breed/individual/water/pond/angler archetypes, context types `global`, `biome`, `map_region`, `water_body`, `fishing_cell`, `managed_pond`, and `pond_type`, and the existing facet, claim, observation, milestone, and relation IDs. Context identities include map, anchor/proxy, water kind, and the stable topology marker; context fallback is explicit parent-then-global rather than an implicit current-map lookup. V3 stages use `Balanced` aggregation. The additive migration marker `aquaculture.knowledge.balanced-stage` version 1 is committed only after the framework accepts and durably records it; it does not delete or reset existing knowledge evidence.

Registration is deterministic and retry-safe. Domain, context, relation, provider, and UI registration must all succeed before the adapter is marked ready; a partial or rejected phase leaves it retryable. Logical Aquaculture events carry stable IDs derived from their source attempt/lifecycle identity, and duplicate IDs are ignored while failed dispatches are removed for retry. Personal and colony observations from one logical event share one Knowledge Framework transaction; derived catch surveys do not accrue a second event.

Legacy progression migration uses consumer IDs `aquaculture.legacy.fishing-progress/...`, version 1, and the existing global consumer. Every import result and the durable `IsCommitted` state must succeed before migration is considered complete. Invalid Defs, non-finite values, missing subjects, or partial imports leave legacy data intact for a later retry. Dynamic framework subjects are bounded to 2,048 and the adapter uses the fish registry for individual resolution instead of repeated whole-map scans; rejected registrations emit one bounded diagnostic. Framework diagnostics and the Aquaculture bridge are the supported inspection paths for registration, migration, event, context, and retention counts.
## Deferred Reality Integration

The natural-water provider contract, authority boundary, stable IDs, migration,
exactly-once operations, active/latent reconciliation, and DRF build/manifest
workflow are documented in `About/DEFERRED_REALITY_INTEGRATION.md`.
