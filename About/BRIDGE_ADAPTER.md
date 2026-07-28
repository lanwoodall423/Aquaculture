# RimWorld Dev Bridge Adapter

Aquaculture exposes optional diagnostics through a standalone, hot-reloadable adapter. RimWorld Dev Bridge is not a gameplay dependency.

## Commands

- `AQUACULTURE`: compact map, pond, fish, health, and progression summary.
- `AQUA_PONDS`: one cached summary line per connected pond.
- `AQUA_POND <proxyThingId|x,z>`: ecology, health, work, policy, warnings, and schools for one pond.
- `AQUA_HABITAT <proxyThingId|x,z>`: habitat structures, weighted supply and demand, fit, and stressed fish.
- `AQUA_BLUEPRINT <proxyThingId|x,z>`: persistent species targets, actual counts, deficits, and surplus.
- `AQUA_FISH <thingId>`: traits, life state, ecology, biology, and species profile for one fish.
- `AQUA_SPECIES [filter]`: species aggregates for fish currently on the map.
- `AQUA_CATALOG [filter]`: all loaded fish definitions and inferred ecological roles.
- `AQUA_JOURNAL`: species milestones, specimen records, registered breeds, generations, and stability.
- `AQUA_OPPORTUNITIES`: loaded-content analysis for future feature design.
- `AQUA_SETTINGS`: active simulation, capacity, trait, and visual settings.
- `AQUA_ADAPTER_STATUS`: loaded adapter identity and hot-reload capabilities.
- `AQUA_PERFORMANCE`: scheduler cadence and current cached workload.
- `AQUA_VALIDATE`: read-only state invariants for ponds, proxies, fish, required traits, and eggs.
- `AQUA_OPEN_PLANNER <proxyThingId|x,z>`: opens a pond's Stocking Planner for UI testing.

The adapter does not reference the bridge assembly, mutate game state, or add ticking behavior. Diagnostics only traverse map state when explicitly requested.

## Development

Build and deploy a uniquely versioned adapter while RimWorld is running:

```powershell
& "C:\Games\Steam\steamapps\common\RimWorld\Mods\AquacultureFishing\DevTools\Build-HotBridgeAdapter.ps1"
& "C:\Games\Steam\steamapps\common\RimWorld\Mods\RimWorldDevBridge\DevTools\Send-RimWorldBridge.ps1" RELOAD_HOT_ADAPTERS
```

Old adapter generations remain in memory until RimWorld restarts. Gameplay changes still require rebuilding and deploying `AquacultureFishing.dll` while RimWorld is closed.
