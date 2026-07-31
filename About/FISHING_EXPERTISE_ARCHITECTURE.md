# Fishing Expertise Architecture

## Stable boundaries

- A fishing attempt is paired once at the Wait phase with one loaded fish `ThingDef` currently present in the shared connected-water population.
- The pair is saved by pawn reference, fish `defName`, water cell, and framework. Existing saves load an empty list because every new save node is optional.
- Catch completion rechecks the same water and species. A missing zone, water body, population, fish definition, or newly raised expertise requirement produces no catch.
- Vanilla Fishing Expanded remains responsible for its job, effects, hauling, and spawned fish. Aquaculture only replaces its selected `fishCaught`, catch count, delay, and completion eligibility.
- Odyssey remains responsible for its job, placement, and zone notification. Aquaculture supplies the paired single-fish result and depletes the shared connected-water species record on successful completion.

## Progression

- Fishing progression is stored in an additive `GameComponent`, separately from pawns and fish definitions.
- Each pawn record contains expertise experience and species knowledge keyed by fish `defName`.
- Fishing uses the shared four expertise states: Novice, Adept, Expert, and Master. The serialized enum values remain numeric and settings default to Novice, so existing untrained/default entries remain catchable.
- Only a successful catch increases species knowledge and expertise experience.
- Animals skill, species knowledge, and expertise all contribute to catch duration and escape chance.
- `KnowledgeFramework.dll` owns the single pawn Bio panel. Aquaculture adapts the existing `aquacultureFishingProgression` records directly, and clicking its row opens the existing Fish Journal Expertise page.

## Compatibility invariants

- No existing save key is renamed or removed.
- Missing pawn, fish, or framework records are ignored safely after load.
- Fish discovery continues to use `FishUtility.IsFish`; no built-in species list is introduced.
- VFE and Odyssey species lists seed a natural-water record only during initialization, then both frameworks query the same connected-body population. Later lists cannot bypass closed-water extinction or open-water migration limits.
- Per-species settings are keyed by `ThingDef.defName`, tolerate removed definitions, and lazily cover newly added fish packs.
- The Field Journal keeps its existing Species and Breeds content. Breeds navigation and breed counts are absent until Selective Fish Breeding is available.

## Verification

`DevTools/Verify-FishingExpertise.ps1` checks the additive save contract, framework hooks, dynamic species discovery, research gate, settings, and journal surface before the release build is accepted.
