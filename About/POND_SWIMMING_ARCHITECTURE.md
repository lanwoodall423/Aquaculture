# Pond Swimming And Effect Traits

## Stable contracts

- Fish traits remain `FishTraitDef` names and optional numeric values on `CompFishTraits`. Eggs and offspring use the existing additive inheritance records.
- `FishTraitPondEffect` is def-driven. Its values describe modest per-fish contributions, diminishing-return caps, duration, day/night behavior, food propagation, and ecology behavior.
- Connected ponds aggregate only living, spawned fish that belong to that pond. Duplicate trait effects use `sqrt(count)` scaling and each def's cap.
- Swimming creates one `AF_SpentTimeInPond` hediff. Its effect map is refreshed in place, so the Health tab never receives one hediff per fish trait.
- Food carries one additive `CompFishFoodTraits` metadata record. Nutrition stores the strongest source multiplier rather than multiplying prior products, and Delicious is a boolean provenance flag.

## Job boundary

- `Enter Pond` is supplied by a RimWorld 1.6 `FloatMenuOptionProvider` for one selected, undrafted humanlike pawn.
- The order validates pond terrain, living fish, health, temperature, danger, reachability, reservation, and normal recreation restrictions before creating the job.
- `JobDriver_EnterPond` reserves and enters one valid pond cell, gains recreation for a bounded duration, reevaluates living fish, and remains normally interruptible.

## Pond ecology

- Beautiful and Sparkling contribute capped pond beauty; Sparkling receives its larger contribution at night.
- Grazer consumes extra algae during the existing ecology pass.
- Fertilizing modifies nearby crop growth at the plant stat boundary.
- Oxygenator adds capped biological carrying capacity without changing player management limits.

## Save compatibility

- Existing fish, ponds, food, and pawns require no migration. Missing trait defs, food comps, or hediff effect entries load as empty/default values.
- The existing fish trait, egg inheritance, and recipe output keys are unchanged.
