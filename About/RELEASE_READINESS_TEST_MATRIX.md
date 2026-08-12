# Release-readiness test matrix

Candidate: `7e7e86cd4c5c35509005ec9a0711f4065f53738e`

Current live evidence: DevBridge2 generation 151, launch
`bdceb514f14748f48f8195bfbd2d9ea7`, coordinator-owned PID 88676.
Current verdict: **BLOCKED — not ready for external RC/beta testing**.

Status values in this matrix are exactly `PASS`, `FAIL`, `BLOCKED`, or `NOT RUN`.
`PASS` means the named check actually ran and passed; source inspection and a
main-menu or quicktest startup do not prove the corresponding gameplay gate.

## Automatic checks

| Gate | Status | Evidence / automatic command |
|---|---|---|
| Production gameplay assembly | PASS | `dotnet build Source/AquacultureFishing.csproj --configuration Release --no-restore -p:AquacultureDeveloperTests=false`; 0 warnings/errors; `AquacultureFishing.dll` SHA `A79F258F948B0D884BC6896AF730D7A7113958DE140BED510CEF68BDC33D2D73`. |
| Developer test assembly | PASS | Same build with `AquacultureDeveloperTests=true`; 0 warnings/errors. Developer source is excluded from the player package. |
| Framework provider build/contract | PASS | Deferred Reality provider build and contract checks passed with API 2/schema 2/save schema 5; provider manifest `sourceDirty=false`. |
| Knowledge behavioral suite | PASS | 34 pure behavioral checks passed; live game-state and manual UI portions remain separate gates. |
| Def mutation/save compatibility | PASS | 22 scoped checks passed. |
| Trait inheritance and mutation | PASS | 31 checks passed, including parental independence, caps, mutation bounds, and registered-breed evidence. |
| Breed stability | PASS | 12 checks passed. |
| Natural populations | PASS | 44 checks passed. |
| Migration/recovery | PASS | 28 checks passed. |
| Conservation | PASS | 41 checks passed. |
| Pond capacity and safety | PASS | 13 capacity/safety checks passed. |
| Pond causal/swimming behavior | PASS | 43 causal and 19 swimming checks passed. |
| Processing | PASS | 16 checks passed. |
| Rod revision/workflow/job guards | PASS | 17, 17, and 7 checks passed respectively; these are not live item no-loss proof. |
| Performance/scaling contracts | PASS | 16 performance checks and 70-cell/500-cell scaling checks passed. |
| XML/localization/optional content | PASS | XML startup checks, one language/279 referenced keys, and Fishing Treasures contract passed. |
| Generated-file/package hygiene | PASS | Local verifier passed; player package contract passed with 25 entries and no source/tests/DevTools/PDB/cache/bridge files. |
| Current mod-owned baseline | PASS | `DevTools/Run-AquacultureInGameTests.ps1 -SkipRestart`: generation 151 baseline 17/17. |
| Current mod-owned golden path | PASS | Same run, two inhabited-pond runs, 7/7 each: `76282a60976342209486c0cda864f04e` and `8ffb2da2f1dc44aeaeae3d2e612a6c44`. |
| Current read-only diagnostics | PASS | Eleven sequential `DevTools/Run-AquacultureDiagnostic.ps1` requests passed: `AQUA_ADAPTER_STATUS`, `AQUA_PONDS`, `AQUA_DEFERRED_REALITY`, `AQUA_VALIDATE`, `AQUA_PERFORMANCE`, `AQUA_SETTINGS`, `AQUACULTURE`, `AQUA_SPECIES`, `AQUA_CATALOG`, `AQUA_JOURNAL`, and `AQUA_OPPORTUNITIES`. |

## Automatic in-game test plan

The following commands are the repeatable development-only plan. The mod owns
setup, simulation, assertions, cleanup, atomic requests, and atomic results;
DevBridge2 only owns lifecycle, readiness, generation/launch identity, and the
exact test lease.

```powershell
dotnet build Source\AquacultureFishing.csproj --configuration Release --no-restore -p:AquacultureDeveloperTests=true
DevTools\Run-AquacultureInGameTests.ps1 -DevBridgeRoot C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2 -Runs 2
DevTools\Run-AquacultureDiagnostic.ps1 -DevBridgeRoot C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2 -Command AQUA_ADAPTER_STATUS
DevTools\Run-AquacultureDiagnostic.ps1 -DevBridgeRoot C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2 -Command AQUA_PONDS
DevTools\Run-AquacultureDiagnostic.ps1 -DevBridgeRoot C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2 -Command AQUA_DEFERRED_REALITY
DevTools\Run-AquacultureDiagnostic.ps1 -DevBridgeRoot C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2 -Command AQUA_VALIDATE
DevTools\Run-AquacultureDiagnostic.ps1 -DevBridgeRoot C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2 -Command AQUA_PERFORMANCE
DevTools\Run-AquacultureDiagnostic.ps1 -DevBridgeRoot C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2 -Command AQUA_SETTINGS
```

Run a full DevBridge2 restart after gameplay, Defs, Harmony, serialized types,
provider, or core changes. Use no direct RimWorld launch/kill, no normal-save
mutation, no normal-config edits, no Windows Control, and no player dependency
on DevBridge2. No supported adapter-registration or hot-reload protocol exists.

## Live gameplay and persistence gates

| Gate | Status | Required evidence still missing or current boundary |
|---|---|---|
| Fresh isolated colony and sane settings/tooltips | NOT RUN | The quicktest is not a player colony and no complete settings/tooltip review ran. |
| Research progression and fail-closed missing Defs | NOT RUN | Baseline only confirms the four-project chain is loaded; unresearched/Pondkeeping/Managed/Industrial play was not run. |
| Save/reload and legacy migration | NOT RUN | No copied fixture or second/third load was exercised. |
| Pond health priority and player advice | NOT RUN | No deterministic healthy/starving/wrong-water/temperature/lethal/overcrowded/reproduction-blocked multi-problem UI review. |
| Natural population zero/floor/above-floor behavior | NOT RUN | Pure conservation checks and a provider snapshot are not live population proof. |
| Manual/automatic risky catch warnings | NOT RUN | No live selection, modal timing, deduplication, or failed-catch conservation run. |
| Migration sources and cache invalidation | NOT RUN | River/coast/ocean/pond/lake/marsh, source exhaustion, split/merge, terrain change, map removal, and save/load cases remain unrun. |
| Live fish generation and outlier species | NOT RUN | No live standard/modded/traitless/malformed species generation matrix. |
| Live breeding and multi-generation inheritance | NOT RUN | Golden path covers one deterministic inheritance fixture, not the full ordinary/matching/traitless/incompatible/mutation matrix. |
| Live rods and equipment/item safety | NOT RUN | Success/miss/bite failure, drafting, cancellation, danger, transfer, destruction, save/load, pawn/target removal, exact-once restoration, and no duplication/loss remain unrun. |
| Live commissions | NOT RUN | Reachability, impossible filtering, deadlines, delivery interruption, reward placement, save/load, and exactly-one specimen consumption remain unrun. |
| Live Knowledge integration | NOT RUN | Registration/retry, capability gating, contexts, bounded subjects, event hooks, save/load retry, two pawns/maps, same-tick duplicates, and exactly-one consequence remain unrun. |
| Live Deferred Reality integration | NOT RUN | Registration, active/latent transition, save during transition, reload, map removal, provider loss/restoration, long intervals, and duplicate durable operations remain unrun. |
| Cross-framework correlation/failure injection | NOT RUN | No supported failure-injection trace has followed one identity through Aquaculture, DRF, Knowledge, and Journal. |
| Long-run performance and memory | NOT RUN | No large pond, many fish/breeds/schools, multi-map, repeated UI, repeated Knowledge, long elapsed interval, memory, or log-volume run. |
| Complete player-facing UX review | NOT RUN | Settings, research, build menu, inspect panes, pond/fish UI, journal, Knowledge, conservation, alerts, commissions, rods, processing, and failure text were not reviewed end to end. |
| Production artwork and provenance | BLOCKED | See `About/ART_REQUIREMENTS.md`; dedicated art is incomplete and borrowed/unlicensed placeholders cannot ship. |
| Clean temporary package install/player smoke | NOT RUN | ZIP contract passed, but clean install with core dependencies, optional fish packs, start/catch/inspect/save/reload/catch-again was not run. |

## Current runtime evidence and boundaries

The generation-151 baseline passed 17/17 at game tick 60. It found the required
game/map components, 32 configured fish definitions, 61 traits, the research
chain, valid fish state, pond/capacity rules, 33 natural-water populations,
journal/breed state, commission state, finite settings, Knowledge species view,
and repeatable snapshot caches. The map had no pond proxies; this is a healthy
empty quicktest fixture, not evidence for every player scenario.

The current Deferred Reality diagnostic reported API/schema 2, required
Regions/Processes/Populations/Anchors/Diagnostics capabilities, one world and
one active map, no pauses/failures/quarantine/reconciliation/duplicate
operations, and no last error. The performance diagnostic reported cached,
event/batched scheduling and no diagnostic-added tick work. These are point-in-
time diagnostics and do not replace live save/load or long-run gates.

One earlier coordinator-managed retry exited with code `-2147483645` in
Horticulture Novel Seeds `PlantAutoMaskCache` while the graphics device was
null. A subsequent DevBridge2-only retry reached READY. This is external
Horticulture/graphics startup noise, not an Aquaculture failure; it is recorded
as an environment `FAIL` in `About/Validation/RC_STATUS.md`. The historical
`VFET_ResearchSpot` reference was traced to installed VFE Tribals content, not
Aquaculture. After the generation-151 evidence was captured, a separate shared
agent requested generation 152 and retained lease `AFCE`; the coordinator is
currently DRAINING generation 151. That shared restart was not touched and is
not a new Aquaculture test result.

## Prompt 1 Insight Canvas settings gate

| Gate | Status | Evidence |
|---|---|---|
| Insight Canvas 2.1.0.0 reference/build | PASS | Release build against the installed/current `InsightCanvas.dll`; local framework checkout HEAD is `93a09005fa15190009daee625352cf4004974472` with preserved uncommitted 2.1 changes documented in the architecture record; 0 warnings/errors; no bundled framework DLL. |
| Snapshot/stable-ID/responsive contracts | PASS | `DevTools/Test-InsightCanvasUi.ps1`; immutable-copy, revision, duplicate-ID, rail/compact, metadata, serialized-key, build-path, and package-boundary checks. |
| Existing pure/localization/package checks | PASS | Trait breeding executable, localization validator, and 25-entry player-package contract passed. |
| DevBridge2 live settings validation | BLOCKED | The earlier generation-201 attempt was blocked by unmanaged PID 30604. After DevBridge2 later reported generation 202 `READY`, the supported harness requested a coordinator restart, but the durable generation-203 `WAITING_FOR_BRIDGE` deadline expired with no launch attempt while another agent's lease `9F8D` remained active. This run acquired/released no lease. No direct process control or Windows Computer Use was performed. Re-run when the coordinator is unoccupied, then exercise repeated open/close, all six categories, persistence, resize/keyboard/accessibility, duplicate-ID diagnostics, render/log checks, and diagnostics. |

## Release decision

The candidate remains **BLOCKED**. PASS results above are retained as current
evidence, but the missing live matrix, clean-install/player smoke, external
environment failures, and incomplete production art prevent a PASS verdict.
Do not create `About/BETA_TEST_PLAN.md` until those blockers are cleared and the
verdict is exactly `PASS — ready for external RC/beta testing`.
