# Aquaculture Fishing release-candidate status

Date: 2026-08-14
Branch: `ui/insightcanvas-v2-overhaul`
Aquaculture candidate source SHA: `293625a271610911cf65e51183191d896d8be74d` (`Refresh provider provenance for closure commit`)
Verdict: **BLOCKED — not ready for external RC/beta testing**

This is an evidence ledger, not a claim that static checks prove gameplay.
Status values are exactly `PASS`, `FAIL`, `BLOCKED`, `NOT RUN`, or `WAIVED`.

## Build and dependency identity

| Item | Status | Evidence |
|---|---|---|
| RimWorld | PASS | `1.6.4871`; DevBridge2 generation 261 remained READY with a coordinator-managed playable quicktest map. |
| Harmony | PASS | 2.4.1.0; `353DAAFEC180BB8E7BBE4DA78F2A7CDC78067392E3A4E79DC8E7AF295F2371E6` |
| VFE Fishing | PASS | `VanillaExpanded.VCEF`, VCE-Fishing.dll 43,008 bytes; `AE30EE82F732862688B712174372554D716044840F80BEDFAF6D5FB7DD89F36D` |
| Deferred Reality Framework | PASS | upstream SHA `9ca3f959d9efd4784dc229723fbb77265a8078af`; local documentation commit `764d932`; provider contract API 2/schema 2/DRF save schema 5; installed DLL SHA `36ECC9AA4335C868A355C2FE1ED8827B2B52339C7C8D4E6FBACF58C1A932C72B`. |
| Knowledge Framework | PASS | `3.1.0-beta.1`, assembly 3.1.0.0; upstream SHA `8c2b98b66071e069f5afafceff14448fe22f8c11`; local workflow head `1847dc0`, verifier fix `13dbdaf`; installed DLL SHA `538981B18CE3C6A41FBD90F3D663DECD80E9F2F4D6CAC47BA7EF048D7BE5013B`; obsolete `<modClass>` metadata removed locally. |
| DevBridge2 | PASS | source SHA `8f234e2a2474d47687e434c690a9b3247e607479`; generation 261, coordinator-owned RimWorld PID 31440, launch `4b7277d3d90642899e3c26dbc2d892a8`; final status was READY with no active tests, lease, restart, or terminal failure. |
| Aquaculture production build | PASS | Release, `AquacultureDeveloperTests=false`, 0 warnings/errors; DLL 640,000 bytes; SHA `2593CBF49DC79CD573C2D25951D5C19D511A84C361502D0FA96E980C23134960`. |
| Aquaculture developer build | PASS | Release, explicit `AquacultureDeveloperTests=true`, 0 warnings/errors; used only for the mod-owned quicktest harness and diagnostics. |
| Natural-water provider build | PASS | 0 warnings/errors; DLL 45,568 bytes; SHA `352D0B9177739E76F52A6A15C4B7F95E6EF5CD168823D9D079F34994F751D393`; manifest records source commit `887b70f` with `sourceDirty=false`. |

## Current completed evidence

| Area | Status | Evidence |
|---|---|---|
| Prompt 3 mod-owned automatic baseline and inhabited-pond golden path | PASS | Generation 261, launch `4b7277d3d90642899e3c26dbc2d892a8`, baseline 17/17; golden runs `0c473786e1f94c578f61a11e0a1981f0` and `a6e195f6edab4a90839cd8293f538f87` passed 7/7 each. Lease B247 was released and final status was READY. |
| Current mod-owned read-only diagnostics | PASS | Eleven sequential generation-261 requests returned PASS: `AQUA_ADAPTER_STATUS`, `AQUA_PONDS`, `AQUA_DEFERRED_REALITY`, `AQUA_VALIDATE`, `AQUA_PERFORMANCE`, `AQUA_SETTINGS`, `AQUACULTURE`, `AQUA_SPECIES`, `AQUA_CATALOG`, `AQUA_JOURNAL`, and `AQUA_OPPORTUNITIES`. Each exact lease was released. |
| Deferred Reality live provider snapshot | PASS | Generation-261 `AQUA_DEFERRED_REALITY` passed API/schema 2, save schema 5, required capabilities, and zero reported failures/duplicates/reconciliation issues. |
| Trait/processing/rod regression scripts | PASS | Fifteen authored pure/regression scripts passed; processing 16 checks; rod revision 17; rod workflow 17; job guards 7. These are not live rod no-loss proof. |
| Natural population/conservation/migration/performance/scaling | PASS | Pure contracts passed: 44, 28, 41, 16, and scaling 70-cell/500-cell cases. These are not live map proof. |
| Pond safety/capacity/causal/swimming | PASS | Pure contracts passed: 13, 43, and 19 checks. |
| Def mutation and compatibility | PASS | 22 scoped mutation/save-compatibility checks. |
| Prompt 3 quicktest XML/Def startup | PASS | The generation-261 Aquaculture baseline reached a playable map, found required Defs, and reported zero Aquaculture-specific errors. The old Knowledge `<modClass>` error is absent after the metadata fix. |
| Localization contract | PASS | One language file and 534 referenced keys. |
| Optional Fishing Treasures contract | PASS | No forbidden optional-content references. |
| Player package contract | PASS | Final clean package from commit `293625a271610911cf65e51183191d896d8be74d`: 26 entries including `LICENSE`, package SHA `A5D857B600478CF91C63FE784F8C5511C237FED6A2D698D9C1F45454D9470A9D`, manifest `sourceDirty=false`, `license=GPL-3.0-or-later`, and Copyright (C) 2026 lanwoodall423. |
| Git hygiene rules | PASS | `.gitignore` covers build intermediates, tool cache, timestamped adapter outputs, local test output, PDBs, generated executables, and machine files while retaining authored source, tests, runtime assemblies, Defs, languages, textures, and required docs. |

## Release gates not evidenced

| Area | Status | Current boundary |
|---|---|---|
| Fresh isolated colony and player settings/save-load | NOT RUN | The quicktest map is not a disposable player colony and no full settings/save/reload matrix was run. |
| Legacy save migration and repeated second/third load | NOT RUN | No safe old-state fixture run. |
| Live natural populations, migration, conservation, terrain split/merge, map removal, save-load | NOT RUN | Pure contracts and a live provider snapshot are insufficient. |
| Broad live fish/trait/breed matrix | NOT RUN | The golden path covers one configured runtime species and one deterministic inheritance path. |
| Live rods, equipment restoration, interruption/map transfer/destruction/save-load | NOT RUN | Pure rod checks passed; live duplication/loss gate remains open. |
| Live commissions and delivery/consumption/reward/save-load | NOT RUN | No live commission lifecycle run. |
| Live Knowledge registration/events/retry/duplicates/two-map/save-load | NOT RUN | Behavioral suite passed 34 pure checks; live event and retry proof was unavailable. |
| DRF save transition/reload/provider failure/exact-once/multimap recovery | NOT RUN | Provider contract and diagnostic passed; failure injection and save transition were not run. |
| Cross-framework correlation and failure injection | NOT RUN | No end-to-end Aquaculture → DRF → Knowledge → Journal correlation run. |
| Long-run performance/memory/cache/log-spam audit | NOT RUN | Current performance diagnostics are point-in-time empty/quicktest snapshots, not a long-run load result. |
| Player-facing UX/text review | NOT RUN | No complete interactive review across settings, ponds, fish, journal, alerts, commissions, rods, and processing. |
| Clean temporary installation with dependencies/optional packs | NOT RUN | ZIP allowlist and hash passed; clean installation and player-colony smoke were not run. |

## Blocking items

| Item | Status | Detail |
|---|---|---|
| Dedicated player art | WAIVED | Per owner instruction dated 2026-08-14, the dedicated production-art requirement is skipped for this candidate. No art was generated or changed; existing vanilla/VFE/dependency-owned sources retain their original terms. |
| Complete live release matrix | BLOCKED | The unrun live/save-load/no-duplication/cross-framework/performance/UX gates above are release-blocking under the matrix. |
| Aquaculture/dependency license accounting | PASS | Aquaculture source/package is `GPL-3.0-or-later`, Copyright (C) 2026 lanwoodall423; the installed Insight Canvas checkout separately declares GPLv3.0. |
| Clean release provenance | PASS | Closure commit `293625a271610911cf65e51183191d896d8be74d` is recorded by the package manifest with `sourceDirty=false`; only allowlisted player files are packaged. |
| Unrelated dependency startup warning | FAIL | The earlier `VFET_ResearchSpot` unresolved reference belongs to installed VFE Tribals content, not Aquaculture. It remains an environment compatibility failure to resolve or explicitly exclude before external testing. |
| External quicktest graphics failure | FAIL | One coordinator-managed generation-151 restart attempt exited `-2147483645` in Horticulture Novel Seeds `PlantAutoMaskCache` while the graphics device was null. A subsequent DevBridge2 retry reached READY; no Aquaculture stack or error was implicated. |
| Knowledge verification artifact hygiene | PASS | Verifier fix `13dbdaf` distinguishes ignored local bin/obj/PDB directories from tracked or packaged contamination; full verification passed with `tracked=0 packaged=0`. |
| DevBridge2 restart ownership | PASS | The current successful launch was coordinator-owned and controlled only through `DevBridge.cmd`; no direct launch, kill, normal-save, or normal-config operation was used. |

## Known non-blocking diagnostic noise

Historical generation-43 logs contained generic RimWorld `UIRoot`/`OnGUI`/`Update`
NullReferenceException spam. Targeted searches found no Aquaculture exception,
fixture leak, golden-path failure, or AquacultureFishing error. This is not current
candidate gameplay proof and is not attributed to Aquaculture.

## Implemented release-safety changes

- DevBridge2 is lifecycle/readiness/lease coordination only; the mod owns diagnostic
  and golden-path request/result files and all fixture mutation/cleanup.
- Developer diagnostics are opt-in with `-p:AquacultureDeveloperTests=true`; the
  default project build and player package are production-safe.
- Diagnostic polling is on a separate bounded real-time schedule from slower fixture
  polling, so on-demand diagnostics are not starved after a completed baseline.
- Atomic request/results, exact lease release, stale identity checks, sequential runs,
  explicit cleanup, package allowlisting, and output hashing are documented/scripted.
- Knowledge Framework's obsolete `<modClass>` metadata and Deferred Reality's
  deprecated `Lan.RimWorldDevBridge` load-after entry were removed in the local
  framework checkouts; those framework worktrees still require their own release
  commits/provenance before publication.

## Prompt 3 closure revalidation — current candidate

Prompt 2's exact-result prerequisite was explicitly overridden by the owner;
Prompt 3 therefore ran regardless of the earlier Prompt 2 result. The current
branch is `ui/insightcanvas-v2-overhaul` at closure source SHA
`293625a271610911cf65e51183191d896d8be74d`. Current dependency evidence is
Insight Canvas source SHA `93a09005fa15190009daee625352cf4004974472`, installed
version `2.1.0.0`, with Knowledge Framework and Deferred Reality retained as
runtime dependencies.

The closure implementation removes the unreachable duplicate Journal renderer,
preserves the Knowledge Framework browser callbacks, and embeds a bounded
Insight Canvas commission-delivery document inside the native delivery Window.
The commission component remains authoritative for eligibility, delivery,
reward placement, rollback, and specimen consumption. A complete player-facing
surface inventory and the decision not to add lineage/timeline/map-bridge
visualizations are recorded in `About/INSIGHT_CANVAS_UI_ARCHITECTURE.md`.

### Prompt 3 live closure evidence

The coordinator-owned restart advanced DevBridge2 to generation 261, launch
`4b7277d3d90642899e3c26dbc2d892a8`, with a quicktest map and the expected
Aquaculture/Insight Canvas/Knowledge Framework/Deferred Reality profile. The
mod-owned baseline passed 17/17. Golden runs
`0c473786e1f94c578f61a11e0a1981f0` and
`a6e195f6edab4a90839cd8293f538f87` each passed 7/7. Lease B247 was released;
the final coordinator status was READY with zero tests and leases.

Eleven sequential read-only diagnostics also passed under the registered
session identity: `AQUA_ADAPTER_STATUS`, `AQUA_PONDS`,
`AQUA_DEFERRED_REALITY`, `AQUA_VALIDATE`, `AQUA_PERFORMANCE`, `AQUA_SETTINGS`,
`AQUACULTURE`, `AQUA_SPECIES`, `AQUA_CATALOG`, `AQUA_JOURNAL`, and
`AQUA_OPPORTUNITIES`. An initial wrapper-identity mismatch was recovered by
releasing the exact orphaned lease owner; no foreign lease was released.

Revalidated local gates passed: Insight Canvas portable contracts, localization
(534 referenced keys), trait rules, fishing expertise, rods, optional content,
Deferred Reality provider contract, Release player/developer builds (zero
warnings/errors), both portable .NET test projects, and the package allowlist.
The final clean package has 26 entries and SHA
`A5D857B600478CF91C63FE784F8C5511C237FED6A2D698D9C1F45454D9470A9D`; its
manifest records `sourceDirty=false` for commit
`293625a271610911cf65e51183191d896d8be74d`.

The required fresh-colony natural UX interaction was not performed before
DevBridge2 manipulation, so it remains `NOT RUN` and is not inferred from the
quicktest fixture. Accessibility, multi-map, save/load, long-run performance,
clean-install/player-smoke, and full cross-framework failure-injection gates
also remain `NOT RUN`. Production artwork is `WAIVED` by owner instruction;
Aquaculture and the separately licensed Insight Canvas dependency have recorded
their applicable GPL terms.

The player-first fresh-colony interaction was not performed before DevBridge2
manipulation because this run is restricted to the coordinator and its
mod-owned request/result API; it remains `NOT RUN` and is not inferred from the
quicktest fixture. Accessibility density/contrast/motion/input/layout review,
multi-map correctness, save/load and legacy migration, long-run performance,
clean temporary installation/player smoke, and full cross-framework failure
injection likewise remain `NOT RUN`. Production artwork is `WAIVED` by owner
instruction and was not generated or altered. These unrun release gates keep
the verdict `BLOCKED`.

## Prompt 1 presentation work — 2026-08-18 validation addendum

This addendum supplements the historical evidence above; it does not relabel
older Prompt 2 or Prompt 3 results.

| Check | Status | Evidence |
|---|---|---|
| Persistent presentation contract and document adoption | PASS | Portable `InsightCanvasUiTests` passed preference save/reload, density clamping, serialized-key preservation, centralized adoption across all six player-facing documents, shared surfaces/status/tooltips, high-contrast focus/theme checks, and no `GUI.skin` mutation. |
| Localization | PASS | `DevTools\Test-AquacultureLocalization.ps1`; one English language file and 536 referenced keys. |
| DevBridge-compatible production build | PASS | The standard installed Steam layout now supplies conservative local defaults for `RimWorldDir` and Harmony while preserving explicit MSBuild overrides. The exact owner-shaped build completed with 0 warnings and 0 errors. |
| RimTest readiness | PASS | `rimtest doctor --json`: `status=ready`, catalog/rimctx/devbridge/rimerror all `ok`, RimBridge `configured`. |
| RimTest affected runtime and artifact freshness | PASS | `rimtest affected --run --json`; workflow `rw-ed6c8c792016413789481e9ce30be2d9`, transaction `ca5098d4a6ed4fdf946b546635210de3`, source fingerprint `a87545d9dcffc09103c00721baa8a6714f2d8efd0e4a5b6026ea84db89039299`, generation 315, run `run-bb0195ca74b748cca9f6438043352f12`, one test passed, built/deployed SHA256 `ad6b9005ff03f5e51131838fc5afe1e321935c78fe3131d53b3741c05400effb`, deployment `unchanged`, `loadedArtifactFreshnessProven=true`, proof `identical-deployment-hash-plus-owned-generation-state`. |
| Owner-scoped runtime error logs | FAIL | DevBridge `logs query --generation 315 --since-launch --severity ERROR --limit 64 --json` and `--trace` both returned `available=false`, `rawBytes=0`, `records=[]`, error `PLAYER_LOG_BOUNDARY_INVALID`: `Player.log was shortened after the launch boundary.` |

Strict Prompt 1 verdict: **FAIL — the runtime test and artifact freshness passed,
but the required generation-scoped log gate could not be proven because the
owner-controlled Player.log boundary was invalid.**

## Prompt 1 current revalidation — 2026-08-19

This entry supersedes neither the historical release matrix nor the prior
boundary-invalid result; it records the current worktree candidate separately.

| Check | Status | Evidence |
|---|---|---|
| Portable Insight Canvas UI contracts | PASS | `dotnet run --project DevTools/InsightCanvasUiTests.csproj --configuration Release`; preference reload/clamping, adoption, stable IDs, compatibility keys, shared components, high-contrast tokens, localization references, and no `GUI.skin` mutation passed. |
| Directly relevant portable regression checks | PASS | `dotnet run --project DevTools/TraitBreedingRules.Tests.csproj --configuration Release`; executable trait-breeding tests passed. |
| Localization | PASS | `DevTools\\Test-AquacultureLocalization.ps1`; one language file and 536 referenced keys. |
| RimTest readiness | PASS | `rimtest doctor --json`; `status=ready`, catalog/rimctx/devbridge/rimerror `ok`, RimBridge `configured`. |
| RimTest affected runtime and artifact freshness | PASS | `rimtest affected --run --json`; final workflow `rw-0dc55663ab8a4fddb40fe1e3692561b1`, transaction `c7d744b5f8384f4e984f2cfd944ee553`, source fingerprint `5b5cda5403c2a7017f92168187776594eee98a7bc0a3685d0cd51d9275ad8bc6`, generation 326, run `run-fbd37b11226f4b6aa7c68d8ed91e19c8`, one test passed, zero failed, matching built/deployed SHA256 `ad6b9005ff03f5e51131838fc5afe1e321935c78fe3131d53b3741c05400effb`, `loadedArtifactFreshnessProven=true`, proof `identical-deployment-hash-plus-owned-generation-state`; selection was conservative with `RIMCONTEXT_RESULT_TRUNCATED` and the declared smoke fallback. |
| Generation-scoped runtime log inspection | PASS (bounded) | DevBridge `logs query --generation 326 --since-launch --severity ERROR --limit 64 --json` and the matching WARN/Aquaculture/InsightCanvas queries returned `available=true`, `records=[]`, and `rawBytes=21854`; the tool reported `truncated=true` because its semantic log result is bounded, so this is not an unrestricted Player.log claim. |

The current Prompt 1 implementation gates exercised in this revalidation pass;
the older unrun release-matrix items and the previous boundary-invalid run remain
historical evidence and are not relabeled.

## Prompt 2 current player-experience validation — 2026-08-19

This entry records the current Prompt 2 worktree candidate separately from the
historical release matrix and earlier validation addenda.

| Check | Status | Evidence |
|---|---|---|
| Portable Prompt 2 UI contracts | PASS | `dotnet run --project DevTools/InsightCanvasUiTests.csproj --configuration Release`; urgency ordering, status filtering/search, selection preservation, dossier health classification, responsive mode, source contracts, stable IDs, and presentation adoption passed. |
| Localization | PASS | `DevTools\\Test-AquacultureLocalization.ps1`; one English language file and 593 referenced keys. |
| RimTest readiness | PASS | `rimtest doctor --json`; `status=ready`, catalog/rimctx/devbridge/rimerror `ok`, RimBridge `configured`. |
| RimTest affected runtime and artifact freshness | PASS | Final `rimtest affected --run --json` against the completed worktree: generation 328, one passed/zero failed, matching built/deployed SHA256 `757edb229f9251d2e75a89456a4e6bdf081d471708ef1caac95d034697a6863a`, `loadedArtifactFreshnessProven=true`, proof `identical-deployment-hash-plus-owned-generation-state`; conservative selection used the declared smoke fallback after `RIMCONTEXT_RESULT_TRUNCATED`. The structured workflow, transaction, run, and source-fingerprint identifiers were retained in the task result. |
| Owner-scoped runtime logs | PASS (bounded) | Generation 328 ERROR/WARN/Aquaculture queries with `--since-launch --limit 64` returned `available=true`, `records=[]`, `rawBytes=21853`, and `truncated=true`; this is bounded evidence, not an unrestricted Player.log claim. |
| Interactive UI targets/screenshots and width/density review | FAIL gate | `rimtest capabilities --json` and `rimtest ui targets --json` returned `RIMBRIDGE_LEASE_REQUIRED`; no interactive target, keyboard/focus, responsive-width, density/contrast, or screenshot evidence was available. The smoke recipe mapped the UI types but did not exercise these surfaces. |

Strict Prompt 2 verdict: **FAIL — the implementation, portable contracts,
runtime smoke, and bounded logs passed, but the required interactive UI/runtime
gate was not executed.**

Two earlier affected attempts were infrastructure/freshness failures while the
DevBridge owner was restarting; they were not treated as application failures.
The stabilized final run above is the authoritative current-source result.
