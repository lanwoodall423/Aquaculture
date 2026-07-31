# Fishing Rod And Job Architecture

## Stable contracts

- Fishing requires an equipped `ThingWithComps` whose `ThingDef` carries `FishingRodExtension` and whose instance carries `CompFishingRodTackle`.
- Rod stats are aggregated from five `FishingTacklePartDef` slots. Missing saved part keys resolve to each slot's def-selected default, preserving untouched-rod behavior.
- Tackle parts are defs, not things. They define phase multipliers, Max Fish Mass, water attraction, material costs, work, skill, and research requirements. Rod parts never alter Reel Time; Rod and Line parts supply Max Fish Mass.
- An upgrade request is saved on the individual rod as a slot and target part. It is completed by `JobDriver_UpgradeFishingRod`; ingredients are reserved, carried, consumed, and never become part items.
- One pending request per rod prevents duplicate bills. Pending rods are event-indexed, so normal Crafting scans do not search every weapon or pathfind to materials. A failed material allocation is retried after 600 ticks.

## Fishing state machine

- Supported framework jobs retain pathing, zone reservations, hauling, population notifications, effects, and catch placement.
- Their single fishing delay is replaced at enumeration time by three visible delayed toils: Cast, Wait, and Reel.
- Cast is short. Wait is the longest phase and applies lure wait and expertise factors. The bite species is selected from the current water at the start of Wait using lure attraction weights.
- Reel is second-longest and applies handle, reel, and expertise factors. Catch resolution remains at the dependency completion boundary.
- The hooked individual stores its generated traits and mass at bite time. Missing fish, changed minimum expertise, failed bite, line escape, and mass above Max Fish Mass all produce no fish and no progression.

## Compatibility

- The progression save keys and legacy journal keys are unchanged.
- New rod instance fields and pending upgrades are additive comp save nodes.
- Default tackle multipliers are neutral, so an unmodified rod preserves the prior duration and catch modifiers while supplying the newly required equipment contract.
- Fish mass starts from the loaded `Mass` stat and is modified by the individual's age, scale, numeric size, and body/meat-yield traits. The same persisted hooked traits are applied to the delivered catch.
- External rods and future lure types are added through defs without patching shared item defs at runtime.
- The equipped-rod requirement is enforced where VFE and Odyssey WorkGivers produce jobs. Reservation results are never changed after `StartJob()`.
- Empty or removed saved tackle keys resolve without calling `DefDatabase` with a null key; installed slots fall back to their def-selected defaults.

## Verification

`DevTools/Verify-FishingRods.ps1` maps the required phases, equipment gate, def-driven parts, instance persistence, upgrade work, duplicate prevention, framework hooks, and compatibility keys before release builds are accepted.
