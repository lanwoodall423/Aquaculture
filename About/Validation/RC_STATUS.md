# Aquaculture Fishing release-candidate status

Date: 2026-08-14
Branch: `ui/insightcanvas-v2-overhaul`
Aquaculture candidate source SHA: `e407b656cc3071a38a81e111a53657d0f52a5fb7` (`Apply GPLv3 licensing and waive production art gate`)
Verdict: **BLOCKED — not ready for external RC/beta testing**

This is an evidence ledger, not a claim that static checks prove gameplay.
Status values are exactly `PASS`, `FAIL`, `BLOCKED`, `NOT RUN`, or `WAIVED`.

## Build and dependency identity

| Item | Status | Evidence |
|---|---|---|
| RimWorld | PASS | `1.6.4871`; DevBridge2 generation 260 remained READY with a coordinator-managed playable quicktest map. |
| Harmony | PASS | 2.4.1.0; `353DAAFEC180BB8E7BBE4DA78F2A7CDC78067392E3A4E79DC8E7AF295F2371E6` |
| VFE Fishing | PASS | `VanillaExpanded.VCEF`, VCE-Fishing.dll 43,008 bytes; `AE30EE82F732862688B712174372554D716044840F80BEDFAF6D5FB7DD89F36D` |
| Deferred Reality Framework | PASS | upstream SHA `9ca3f959d9efd4784dc229723fbb77265a8078af`; local documentation commit `764d932`; provider contract API 2/schema 2/DRF save schema 5; installed DLL SHA `36ECC9AA4335C868A355C2FE1ED8827B2B52339C7C8D4E6FBACF58C1A932C72B`. |
| Knowledge Framework | PASS | `3.1.0-beta.1`, assembly 3.1.0.0; upstream SHA `8c2b98b66071e069f5afafceff14448fe22f8c11`; local workflow head `1847dc0`, verifier fix `13dbdaf`; installed DLL SHA `538981B18CE3C6A41FBD90F3D663DECD80E9F2F4D6CAC47BA7EF048D7BE5013B`; obsolete `<modClass>` metadata removed locally. |
| DevBridge2 | PASS | source SHA `8f234e2a2474d47687e434c690a9b3247e607479`; generation 260, coordinator-owned RimWorld PID 29324, launch `723898827e50405ba7fead446d944b38`; final status/doctor were READY with no active tests, lease, restart, or terminal failure. |
| Aquaculture production build | PASS | Release, `AquacultureDeveloperTests=false`, 0 warnings/errors; DLL 640,000 bytes; SHA `2593CBF49DC79CD573C2D25951D5C19D511A84C361502D0FA96E980C23134960`. |
| Aquaculture developer build | PASS | Release, explicit `AquacultureDeveloperTests=true`, 0 warnings/errors; used only for the mod-owned quicktest harness and diagnostics. |
| Natural-water provider build | PASS | 0 warnings/errors; DLL 45,568 bytes; SHA `BDBAEE940E0EEEA487C691257C3E2749054A7D863B20F9EB4862E5C6C48F7E9F`; manifest SHA `DDD715E596AE04A7CED2A8AD4B603D8AC558E8BB440CDEA82C2094E692F97850`; `sourceDirty=false`. |

## Current completed evidence

| Area | Status | Evidence |
|---|---|---|
| Prompt 3 mod-owned automatic baseline and inhabited-pond golden path | PASS | Generation 259, launch `73e7b345c9ed4552b5b115f08ec62da5`, baseline 17/17; two golden runs passed 7/7 each. The exact lease was released. |
| Historical mod-owned diagnostics | PASS | Eleven read-only commands returned PASS results on the earlier generation-151 launch; these remain historical evidence and are not represented as current-generation results. |
| Deferred Reality live provider snapshot | PASS | API 2/schema 2; 1 world, 1 active map; the diagnostic reported 23 populations/processes and the same required capabilities; no paused processes, failures, quarantine, reconciliation, duplicate operations, or last error. The baseline separately observed 33 prepared natural-water populations. |
| Trait/processing/rod regression scripts | PASS | Fifteen authored pure/regression scripts passed; processing 16 checks; rod revision 17; rod workflow 17; job guards 7. These are not live rod no-loss proof. |
| Natural population/conservation/migration/performance/scaling | PASS | Pure contracts passed: 44, 28, 41, 16, and scaling 70-cell/500-cell cases. These are not live map proof. |
| Pond safety/capacity/causal/swimming | PASS | Pure contracts passed: 13, 43, and 19 checks. |
| Def mutation and compatibility | PASS | 22 scoped mutation/save-compatibility checks. |
| Prompt 3 quicktest XML/Def startup | PASS | The generation-259 Aquaculture baseline reached a playable map, found required Defs, and reported zero Aquaculture-specific errors. The old Knowledge `<modClass>` error is absent after the metadata fix. |
| Localization contract | PASS | One language file and 534 referenced keys. |
| Optional Fishing Treasures contract | PASS | No forbidden optional-content references. |
| Player package contract | PASS | Final package from commit `e407b656cc3071a38a81e111a53657d0f52a5fb7`: 26 entries including `LICENSE`, package SHA `D364F68C1D365D9236D1634E50660DA1048894C30E2F75960CCD24DBB3260A1B`, manifest `sourceDirty=false`, `license=GPL-3.0-or-later`, and Copyright (C) 2026 lanwoodall423. |
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
| Clean release provenance | PASS | Closure implementation baseline `c047773`; final license/waiver commit `e407b656cc3071a38a81e111a53657d0f52a5fb7` is recorded by the package manifest with `sourceDirty=false` and only allowlisted player files. |
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

The older RC entries above are superseded for UI release purposes. Prompt 2
ended exactly `Prompt 2, PASS`; the closure baseline was clean on
`ui/insightcanvas-v2-overhaul` at `c047773`. Current dependency evidence is
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

DevBridge2 generation 259, launch `73e7b345c9ed4552b5b115f08ec62da5`, passed the
mod-owned baseline 17/17 and two golden runs 7/7 each
(`b330eb11f27849bc93280f0d5f501523`, `f90aa7b89c47421b8afd23531197d770`). The
same generation passed `AQUA_DEFERRED_REALITY` with API/schema 2, save schema 5,
one region/map, 48 populations/processes, zero failure/duplicate/reconciliation
issues, and 48 migrated populations. The harness lease initially used
inconsistent generated owner identities; status inspection and an explicit
owner-matched `test end` released it, leaving zero tests/leases.

A later read-only `AQUA_ADAPTER_STATUS` request on generation 260 acquired and
released its lease but produced no result within 120 seconds. DevBridge2
`doctor --json` then reported READY, zero active tests, no lease, and no
terminal failure. The request is not counted as a pass and no restart was
performed for this documentation/package-only change.

The subsequent coordinator-owned `Run-AquacultureInGameTests.ps1
-SkipRestart -Runs 2` attempt on generation 260 acquired lease `640B`, but
the baseline result file was not produced within its 120-second bound. The
harness released the lease and DevBridge2 returned READY with zero active
tests; this attempt is not counted as a pass.

The clean player package contract also passed: 26 allowlisted entries including
the GPL license file, no
DevBridge2/source/tests/caches/PDBs or duplicate InsightCanvas DLL, and
package SHA `D364F68C1D365D9236D1634E50660DA1048894C30E2F75960CCD24DBB3260A1B`.
The committed manifest records `sourceDirty=false` for commit
`e407b656cc3071a38a81e111a53657d0f52a5fb7`.

The required fresh-colony natural UX interaction was not performed before
DevBridge2 manipulation, so it remains `NOT RUN` and is not inferred from the
quicktest fixture. Accessibility, multi-map, save/load, long-run performance,
clean-install/player-smoke, and full cross-framework failure-injection gates
also remain `NOT RUN`. Production artwork is `WAIVED` by owner instruction;
Aquaculture and the separately licensed Insight Canvas dependency have recorded
their applicable GPL terms.

Revalidated local gates: configured Release build (0 warnings/errors), portable
Insight Canvas contracts, executable trait rules, and localization. DevBridge2
live baseline/golden/provider evidence from the preceding clean provider
closure remains valid for unchanged gameplay/provider code, but it does not
replace the Prompt 3 fresh-colony, accessibility, multi-map, save/load,
performance, and clean-package gates. Those gates remain `NOT RUN` until a
fresh isolated player run supplies evidence. Production artwork remains
`WAIVED` by owner instruction and is not an engineering blocker for this
candidate.
