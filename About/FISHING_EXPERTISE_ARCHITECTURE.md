# Fishing Expertise Architecture

## Stable boundaries

- A fishing attempt is paired once, when the dependency job reserves its fishing spot, with one loaded fish `ThingDef` currently exposed by that water.
- The pair is saved by pawn reference, fish `defName`, water cell, and framework. Existing saves load an empty list because every new save node is optional.
- Catch completion rechecks the same water and species. A missing zone, water body, population, fish definition, or newly raised expertise requirement produces no catch.
- Vanilla Fishing Expanded remains responsible for its job, effects, hauling, and spawned fish. Aquaculture only replaces its selected `fishCaught`, catch count, delay, and completion eligibility.
- Odyssey remains responsible for its job, placement, population depletion, and zone notification. Aquaculture supplies the paired single-fish result to `FishingUtility.GetCatchesFor`.

## Progression

- Fishing progression is stored in an additive `GameComponent`, separately from pawns and fish definitions.
- Each pawn record contains expertise experience and species knowledge keyed by fish `defName`.
- The five expertise states are Untrained, Novice, Adept, Expert, and Master. Species settings default to Untrained so old saves and newly loaded fish remain catchable.
- Only a successful catch increases species knowledge and expertise experience.
- Animals skill, species knowledge, and expertise all contribute to catch duration and escape chance.

## Compatibility invariants

- No existing save key is renamed or removed.
- Missing pawn, fish, or framework records are ignored safely after load.
- Fish discovery continues to use `FishUtility.IsFish`; no built-in species list is introduced.
- Per-species settings are keyed by `ThingDef.defName`, tolerate removed definitions, and lazily cover newly added fish packs.
- The Field Journal keeps its existing Species and Breeds content. Breeds navigation and breed counts are absent until Selective Fish Breeding is available.

## Verification

`DevTools/Verify-FishingExpertise.ps1` checks the additive save contract, framework hooks, dynamic species discovery, research gate, settings, and journal surface before the release build is accepted.
