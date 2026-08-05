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

## Development and packaging

The adapter is owned and packaged by Aquaculture in `DevTools/BridgeAdapters`.
Dev Bridge discovers that directory when Aquaculture is loaded; Aquaculture does
not depend on Dev Bridge at runtime.

```powershell
& "C:\Games\Steam\steamapps\common\RimWorld\Mods\AquacultureFishing\DevTools\Build-HotBridgeAdapter.ps1"
```

The helper creates a generation-specific DLL and manifest from the exact build.
Old generations remain in memory until RimWorld restarts. Gameplay changes still
require rebuilding and deploying `AquacultureFishing.dll` while RimWorld is closed.
