# Pond Swimming And Effect Traits

## Stable contracts

- Fish traits remain `FishTraitDef` names and optional numeric values on `CompFishTraits`. Eggs and offspring use the existing additive inheritance records.
- `FishTraitPondEffect` is def-driven. Its values describe modest per-fish contributions, diminishing-return caps, duration, day/night behavior, food propagation, and ecology behavior.
- Connected ponds aggregate only living, spawned fish that belong to that pond. Duplicate trait effects use `sqrt(count)` scaling and each def's cap.
- Swimming creates one `AF_SpentTimeInPond` hediff. Its effect map is refreshed in place, so the Health tab never receives one hediff per fish trait.
- Food carries one additive `CompFishFoodTraits` metadata record only when a relevant fish-processing output needs provenance. The component is added to the actual Thing for dynamic recipe outputs; it is not injected into every ingestible Def. Nutrition stores the strongest source multiplier rather than multiplying prior products, and Delicious is a boolean provenance flag.

## Job boundary

- `Enter Pond` is supplied by a RimWorld 1.6 `FloatMenuOptionProvider` for one selected, undrafted humanlike pawn.
- The order validates pond terrain, living fish, health, temperature, danger, reachability, reservation, and normal recreation restrictions before creating the job.
- `JobDriver_EnterPond` reserves and enters one valid pond cell, gains recreation for a bounded duration, reevaluates living fish, and remains normally interruptible.

## Pond ecology

- Beautiful and Sparkling contribute capped pond beauty; Sparkling receives its larger contribution at night.
- Grazer consumes extra algae during the existing ecology pass.
- Fertilizing modifies nearby crop growth at the plant stat boundary.
- Each powered aerator adds eight industrial-support fish, capped by one physical fish per valid pond cell. The base biological setting defaults to 0.75 fish per cell. Habitat, Management, and the Stocking Planner separately show physical space, approximate current sustainability, industrial support, and any management limit; food or habitat support can be the lower constraint.

## Save compatibility

- Existing fish, ponds, food, and pawns require no migration. Missing trait defs, food comps, or hediff effect entries load as empty/default values.
- The existing fish trait, egg inheritance, and recipe output keys are unchanged.
- During an old-save load, a temporary compatibility pass restores the former food component on ingestible Defs long enough for serialized component data to deserialize, then removes only those temporary Def entries. New provenance is attached only to relevant recipe Things.

## Causal management summary

- `PondMenuSnapshot` rebuilds a pond-wide causal summary together with the existing cached ecology and habitat data. It combines population, physical/sustainable support, prepared-food reserve, water and temperature compatibility, habitat deficits, and breeding blockers instead of emitting one message per fish.
- Issues are ordered from lethal conditions through hunger, severe stress, reproduction blockers, and advice. Built-in status icons, color, localized text, affected-fish counts, and optional Details navigation are all used so color is not the only signal.
- Breeding status uses the same shared rules as the lifecycle pass. A next-opportunity estimate is shown only when a compatible adult pair and available capacity make it meaningful; otherwise the summary names the blocking condition.
