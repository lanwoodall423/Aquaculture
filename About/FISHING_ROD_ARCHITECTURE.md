# Fishing Rod And Job Architecture

## Stable contracts

- Fishing requires access to a `ThingWithComps` whose `ThingDef` carries `FishingRodExtension` and whose instance carries `CompFishingRodTackle`. A pawn may use an equipped rod, a rod in direct inventory, or a rod in valid map storage.
- Rod stats are aggregated from five `FishingTacklePartDef` slots. Missing saved part keys resolve to each slot's def-selected default, preserving untouched-rod behavior.
- Tackle parts are defs, not things. They define phase multipliers, Max Fish Mass, water attraction, material costs, work, skill, and research requirements. Rod parts never alter Reel Time; Rod and Line parts supply Max Fish Mass.
- An upgrade request is saved on the individual rod as a slot and target part. It is completed by `JobDriver_UpgradeFishingRod`; ingredients are reserved, carried, consumed, and never become part items.
- One pending request per rod prevents duplicate bills. Pending rods are event-indexed, so normal Crafting scans do not search every weapon or pathfind to materials. A failed material allocation is retried after 600 ticks.

## Fishing state machine

- Supported framework jobs retain pathing, zone reservations, hauling, population notifications, effects, and catch placement.
- Their single fishing delay is replaced at enumeration time by three visible delayed toils: Cast, Wait, and Reel.
- Cast is fixed at 180 ticks and has no rod or expertise modifier. Wait uses the aggregate Wait Time factor from all tackle slots, expertise time reduction, and the global duration setting. The bite species is selected from the current natural-water ledger during Wait using lure attraction, fish water attraction, local population weight, and species knowledge.
- Reel uses the aggregate Reel Time factors from every non-Rod tackle slot, expertise time reduction, and the global duration setting. Catch resolution uses Bite Chance, Animals skill, species knowledge, expertise, and rod Catch Chance; Max Fish Mass is a separate eligibility limit at the dependency completion boundary.
- Species access requires current natural-ledger presence and the configured minimum expertise. Neither Animals skill, attraction, nor species knowledge bypasses that access rule.
- The hooked individual stores its generated traits and mass at bite time. Missing fish, changed minimum expertise, failed bite, line escape, and mass above Max Fish Mass all produce no fish and no progression.

## Compatibility

- The progression save keys and legacy journal keys are unchanged.
- New rod instance fields and pending upgrades are additive comp save nodes.
- Default tackle multipliers are neutral, so an unmodified rod preserves the prior duration and catch modifiers while supplying the newly required equipment contract.
- Fish mass starts from the loaded `Mass` stat and is modified by the individual's age, scale, numeric size, and body/meat-yield traits. The same persisted hooked traits are applied to the delivered catch.
- External rods and future lure types are added through defs without patching shared item defs at runtime.
- VFE and Odyssey WorkGivers select one rod at job creation. Map-storage rods are reserved in the dependency driver's `TryMakePreToilReservations`; the fishing job's existing reservations are otherwise unchanged.
- Inventory and map-storage rods are moved into equipment only for the active fishing job. The pawn's previous primary weapon is saved in an additive session record and restored by both job finish actions and the base `JobDriver.Cleanup` hook. Restoration is idempotent and never destroys or overwrites another weapon.
- The rod index only tracks spawned rod comps and is queried once while selecting a job. Selection prefers maximum fish mass, catch chance, then lower combined timing factors. Forbidden, reserved, unreachable, invalid-storage, prisoner, and slave cases are rejected with a workgiver failure reason.
- Empty or removed saved tackle keys resolve without calling `DefDatabase` with a null key; installed slots fall back to their def-selected defaults.

## Verification

`DevTools/Verify-FishingRods.ps1` maps the required phases, rod selection, reservation hook, temporary-equipment cleanup, def-driven parts, instance persistence, upgrade work, duplicate prevention, framework hooks, and compatibility keys before release builds are accepted. The focused workflow script covers source-level selection, reservation, session persistence, and cleanup contracts; in-game validation remains required for holder transitions and interruptions.
