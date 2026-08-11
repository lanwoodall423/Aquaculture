# Aquaculture DevBridge2 workflow

DevBridge2 is the only supported RimWorld coordinator for Aquaculture live
validation. The current DevBridge2 provider exposes lifecycle and test-lease
coordination only; it has no adapter registration or arbitrary command API.
Aquaculture therefore owns its diagnostics and tests in the gameplay assembly,
using atomic request/result JSON under DevBridge2's `Runtime` directory.

## Rules

- Do not launch, kill, or control RimWorld directly.
- Use `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd`.
- Query `status` before acting and use `wait-ready` after an interrupted command.
- Acquire a lease with `test begin` before interacting with the live map.
- Release exactly the lease printed by that command with `test end <lease>`.
- Never release another agent's lease.
- Keep test setup, assertions, simulation, and cleanup inside `AquacultureFishing.dll`.
- Do not write normal player saves or alter the user's normal configuration.

## Standard golden-path run

After a gameplay/Defs/Harmony/core build, use the coordinator-managed restart:

```powershell
dotnet build Source\AquacultureFishing.csproj --configuration Release --no-restore -p:AquacultureDeveloperTests=true
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd' restart
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd' wait-ready
& .\DevTools\Run-AquacultureInGameTests.ps1 -DevBridgeRoot 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2' -Runs 2
```

For a read-only live diagnostic, use the mod-owned diagnostic coordinator:

```powershell
& .\DevTools\Run-AquacultureDiagnostic.ps1 -Command AQUA_ADAPTER_STATUS
& .\DevTools\Run-AquacultureDiagnostic.ps1 -Command AQUA_PONDS
```

The command is checked again inside the mod; mutating commands such as
`AQUA_OPEN_PLANNER` are rejected.

The harness queues unique request IDs, waits for matching launch/generation
results, collects every run even when one fails, releases only its own lease,
and verifies that DevBridge2 returns to `READY` with no owned lease. The
`inhabited-pond-golden-path` fixture uses the current quicktest map only and
restores every created object and terrain cell.

## Build/reload boundary

The gameplay assembly, Defs, Harmony patches, serialized types, provider, and
core changes require `restart` followed by `wait-ready`. A request/result or
documentation-only change can be exercised without a restart when the loaded
assembly is unchanged. DevBridge2 currently has no supported cooperative
adapter hot-reload API, so no adapter reload command is used.

## Build, register, query, restart, reload, savedata, and config

- **Build:** use the explicit developer property for live tests. Use the
  default (false) property for the player assembly and package.
- **Register:** there is no DevBridge2 adapter-registration command. Do not
  publish or register the historical standalone adapter. The loaded developer
  assembly installs the mod-owned request/result runner itself.
- **Query:** use `DevBridge.cmd status` for lifecycle identity and
  `Run-AquacultureDiagnostic.ps1` for read-only mod diagnostics. Results are
  atomic files under `DevBridge2\Runtime` and are matched by run ID, launch ID,
  and generation.
- **Restart:** after gameplay changes, run `DevBridge.cmd restart` and then
  `DevBridge.cmd wait-ready`; discard old launch/generation context.
- **Reload:** no live adapter reload is supported. Restart is the reload
  boundary for gameplay, Defs, Harmony, serialized types, and providers.
- **Savedata/config:** use only disposable quicktest or explicitly isolated
  test data. Do not edit the player's normal saves or `ModsConfig.xml`; the
  mod-owned fixture restores its temporary map state and the coordinator does
  not manage player data migration.

## Diagnostics

The fixed smoke report and per-run golden reports are written atomically under
`DevBridge2\Runtime` and include suite, run ID, launch ID, generation, UTC
timestamps, status, and individual checks. The bridge only coordinates process
lifecycle and leases; it does not create fixtures, mutate gameplay state, or
declare test results.

## Packaging

DevBridge2, `DevTools`, `Source`, test code, request/result files, adapter
artifacts, and build intermediates are development-only and must not enter the
player package. The old `RimWorldDevBridge` publisher and client are not used.
