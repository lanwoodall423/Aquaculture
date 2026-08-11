# Aquaculture Fishing release-candidate status

Date: 2026-08-11
Branch: `main`  
Aquaculture candidate code SHA: `85a5218` (`Route definition checks through DevBridge2`)
Verdict: **BLOCKED — not ready for external RC/beta testing**

This is an evidence ledger, not a claim that static checks prove gameplay.
Status values are limited to PASS, FAIL, BLOCKED, and NOT RUN.

## Build and dependency identity

| Item | Status | Evidence |
|---|---|---|
| RimWorld | PASS | `1.6.4871 rev590` |
| Harmony | PASS | 2.4.1.0; `353DAAFEC180BB8E7BBE4DA78F2A7CDC78067392E3A4E79DC8E7AF295F2371E6` |
| VFE Fishing | PASS | `VanillaExpanded.VCEF`, VCE-Fishing.dll 43,008 bytes; `AE30EE82F732862688B712174372554D716044840F80BEDFAF6D5FB7DD89F36D` |
| Deferred Reality Framework | PASS | upstream SHA `9ca3f959d9efd4784dc229723fbb77265a8078af`; local documentation commit `764d932`; provider contract reports API 2/schema 2/DRF save schema 5 |
| Knowledge Framework | PASS | `3.1.0-beta.1`, assembly 3.1.0.0; upstream SHA `8c2b98b66071e069f5afafceff14448fe22f8c11`; local workflow head `1847dc0`, verifier fix `13dbdaf`; DLL SHA `538981B18CE3C6A41FBD90F3D663DECD80E9F2F4D6CAC47BA7EF048D7BE5013B` |
| DevBridge2 | PASS | source SHA `8f234e2a2474d47687e434c690a9b3247e607479`; generation 144 READY, launch `f5100e2aeef0468eae8ffddefa04c021`, zero active tests; lifecycle/lease-only coordinator. Doctor reports unmanaged PID 59372, so no restart was attempted. |
| Aquaculture production build | PASS | Release, `AquacultureDeveloperTests=false`, 0 warnings/errors; DLL 550,912 bytes; SHA `A79F258F948B0D884BC6896AF730D7A7113958DE140BED510CEF68BDC33D2D73` |
| Aquaculture developer build | PASS | Release, explicit `AquacultureDeveloperTests=true`, 0 warnings/errors; live test evidence below |
| Natural-water provider build | PASS | 0 warnings/errors; DLL 45,056 bytes; SHA `730BD905406D332D44AA9AB010E895C5F2D01BDC207425F5599C12778FA4323D` |

## Completed evidence

| Area | Status | Evidence |
|---|---|---|
| Mod-owned inhabited-pond golden path | NOT RUN | Historical generation-104 runs passed, but they predate the current candidate identity and are not promoted as proof. A new developer-process run is blocked by unmanaged RimWorld PID 59372; no direct kill or launch was used. |
| DevBridge2 mod-owned diagnostics | NOT RUN | Historical generation-105 diagnostics passed, but no current-candidate diagnostic was run because the developer assembly could not be loaded without a restart. |
| Trait/processing/rod regression scripts | PASS | Trait rules passed; processing 16 checks; rod revision 17; rod workflow 17; job guards 7. |
| Natural population/conservation/migration/performance/scaling | PASS | Pure contracts passed: 44, 28, 41, 16, and scaling 70-cell/500-cell cases. These are not live map proof. |
| Pond safety/capacity/causal/swimming | PASS | Pure contracts passed: 13, 43, and 19 checks. |
| Def mutation and compatibility | PASS | 22 scoped mutation/save-compatibility checks. |
| RimWorld definition startup | NOT RUN | The coordinator-only definition check passed against the current managed log with 0 Aquaculture errors and one unrelated VFE research cross-reference, but a fresh candidate restart was not run. |
| Localization contract | PASS | One language file and 279 referenced keys. |
| Optional Fishing Treasures contract | PASS | No forbidden optional-content references. |
| Player package contract | NOT RUN | The prior 25-entry package passed its allowlist, but it was generated before the current candidate and recorded `sourceDirty=true`; a clean candidate package is pending. |
| Git hygiene rules | PASS | `.gitignore` covers build intermediates, tool cache, timestamped adapter outputs, and local test output; generated intermediates were removed from the index without deleting working files. |

## Release gates not evidenced

| Area | Status | Current boundary |
|---|---|---|
| Fresh isolated colony and player settings/save-load | NOT RUN | No disposable fresh-colony matrix was run in this pass. |
| Legacy save migration and repeated second/third load | NOT RUN | No safe old-state fixture run. |
| Live natural populations, migration, conservation, terrain split/merge, map removal, save-load | NOT RUN | Pure contracts and a live provider snapshot are insufficient. |
| Broad live fish/trait/breed matrix | NOT RUN | Golden path covers one configured runtime species and one deterministic inheritance path. |
| Live rods, equipment restoration, interruption/map transfer/destruction/save-load | NOT RUN | Pure rod checks passed; live duplication/loss gate remains open. |
| Live commissions and delivery/consumption/reward/save-load | NOT RUN | No live commission lifecycle run. |
| Live Knowledge registration/events/retry/duplicates/two-map/save-load | NOT RUN | Behavioral suite passed 34 pure checks; live event and retry proof was unavailable. |
| DRF save transition/reload/provider failure/exact-once/multimap recovery | NOT RUN | Provider contract and diagnostic passed; failure injection and save transition were not run. |
| Cross-framework correlation and failure injection | NOT RUN | No end-to-end Aquaculture → DRF → Knowledge → Journal correlation run. |
| Long-run performance/memory/cache/log-spam audit | NOT RUN | One diagnostic timing sample is not a long-run result. |
| Player-facing UX/text review | NOT RUN | No complete interactive review across settings, ponds, fish, journal, alerts, commissions, rods, and processing. |
| Clean temporary installation with dependencies/optional packs | NOT RUN | ZIP contract passed; clean installation and colony smoke were not run. |

## Blocking items

| Item | Status | Detail |
|---|---|---|
| Dedicated player art | BLOCKED | `About/ART_REQUIREMENTS.md` records the gap. Only `1.6/Textures/Things/Item/Equipment/FishingRod.png` is owned production art; the remaining player-facing visual Defs borrow vanilla/VFE assets or lack dedicated art. Placeholders/unlicensed art are not acceptable for release. |
| Complete live release matrix | BLOCKED | The unrun live/save-load/no-duplication/cross-framework/performance/UX gates above are release-blocking under the matrix. |
| Clean release provenance | BLOCKED | The current candidate package has not yet been regenerated after the committed stabilization work; final evidence must show `sourceDirty=false` and a clean tree. |
| Unrelated startup warning | FAIL | Fresh definition load left one unresolved `VFET_ResearchSpot` cross-reference from another mod. It is not an Aquaculture Def error, but the dependency combination needs a compatibility decision before external testing. |
| Knowledge verification artifact hygiene | PASS | Verifier fix `13dbdaf` distinguishes ignored local bin/obj/PDB directories from tracked or packaged contamination. Full verification passed with `tracked=0 packaged=0`; ignored local directories were reported separately. |
| DevBridge2 restart ownership | BLOCKED | DevBridge2 doctor reports unmanaged RimWorld PID 59372 and requires closure through Steam before the next restart. No direct process control was used. |

## Known non-blocking diagnostic noise

The generation-43 live log contained repeated generic RimWorld `UIRoot`/`OnGUI`/`Update`
NullReferenceException spam (9,894 occurrences in the inspected log). Targeted searches
found no Aquaculture exception, fixture leak, golden-path failure, or AquacultureFishing
error. This remains an environment/quicktest usability issue to resolve or explicitly
attribute before beta, but it was not produced by the mod-owned golden path.

## Implemented release-safety changes

- DevBridge2 is lifecycle/readiness/lease coordination only; the mod owns diagnostic and
  golden-path request/result files and all fixture mutation/cleanup.
- The old standalone adapter publisher is disabled and documented as historical; the
  player build does not depend on DevBridge2.
- Developer tests and diagnostic adapter code are opt-in with
  `-p:AquacultureDeveloperTests=true`; the default project build is production-safe.
- Atomic request/results, exact lease release, stale identity checks, two sequential runs,
  explicit cleanup, package allowlisting, and output hashing are documented and scripted.
