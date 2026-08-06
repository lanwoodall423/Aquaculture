# Framework Integration Results

Validation was run on branch `integration/framework-end-to-end-validation`.
This report deliberately separates executable, startup, and unavailable live
evidence. No normal RimWorld configuration or save was modified.

## Repository Inputs

| Repository | Checkout | SHA | State |
| --- | --- | --- | --- |
| Aquaculture | `integration/framework-end-to-end-validation` | `cb827e494f7a3ef7f0b2ff37d267bb10fda4aa77` | Worktree contains the merged integration changes and this validation work |
| Deferred Reality Framework | `main` | `0716a52136d2b3aed5babd61af471cc545fab3d2` | Existing dirty framework checkout; current source was built for validation |
| Knowledge Framework | `feature/safe-consumer-apis` | `d90fbcce98a4bdab59d3f2f84dbe7c15b22301dd` | Current local compatibility source |
| RimWorld DevBridge | `fix/unattended-restart-validation` | `01e994819bc4185234ec7cfd31d5880c6e867013` | Existing dirty checkout; live bridge unavailable |

## Contract

- Aquaculture owns active Things, ponds, maps, player actions, and legacy projections.
- Deferred Reality owns latent aggregate populations, regions, processes, topology, and durable operation markers.
- Knowledge owns observations, uncertainty, expertise, and evidence.
- Catch and stocking mutate the active Aquaculture state once, then reconcile one delta through DRF and submit one correlated Knowledge group.
- DRF demography mutates latent populations only; active births and deaths are not replayed by the latent process.
- Closed water, cross-category migration, zero sources, mismatched species, nonfinite values, and invalid identities are rejected.

## Assemblies

| Assembly | Result | SHA256 |
| --- | --- | --- |
| `1.6/Assemblies/AquacultureFishing.dll` | Release build | `B3E6294AF655F0F41FA0C6B8558F71CD6A334268AC45FAE80784DD143261F52C` |
| `1.6/Assemblies/DeferredReality.Aquaculture.dll` | Provider Release build | `D0D052AB74FD1D20FE2D2F47B6DFD2BAB733F258D4CBEB65896D05A6FB0C8D2D` |
| `1.6/Assemblies/DeferredRealityFramework.dll` | Current local DRF assembly | `81231193AE7E2B32BBE360DAE0FCCF80F864C0FA7437999D0DF4DB4CADBFD945` |
| `1.6/Assemblies/KnowledgeFramework.dll` | Current local Knowledge assembly | `33552DBEC78E0E777C3E074EA0EBA8F750629E879EA8A809B58D54AEFB1E71C0` |
| `DevTools/BridgeAdapter/bin/Release/AquacultureFishing.BridgeAdapter.dll` | Adapter Release build | `8A888202ED72C03B180A6EA75E79765FC2FB523C481F259D0E1CA0E000B370B2` |

Provider contract: `lan.aquaculture.natural-water`, semantic provider API `2`,
provider schema `2`, minimum DRF save schema `5`, capabilities `Regions`,
`Populations`, `Processes`, `Anchors`, and `Diagnostics`. The manifest matches
the provider DLL hash. Provider operation retention is indefinite and no
compaction proof is claimed.

## Automated Results

- DRF `Run-PureTests.ps1`: passed with 0 warnings/errors.
- DRF `Check-RepositoryIntegrity.ps1`: `PASS`.
- Knowledge Framework source build: passed with 18 existing framework deprecation warnings and 0 errors.
- Knowledge Framework behavioral harness: pure production suite passed 32 checks; public API declaration/regression audits passed; game-state and manual UI layers unavailable without an active map.
- Aquaculture gameplay Release build: passed with 0 warnings/errors.
- Provider Release build and manifest contract: passed with 0 warnings/errors.
- Bridge adapter Release build: passed with 0 warnings/errors.
- Aquaculture trait/recovery/commission/Knowledge/provider/integration executable tests: passed.
- Localization contract: 279 referenced keys passed.
- Existing Aquaculture regression validators: passed, including conservation, all 36 migration pairings, commissions, processing, rods, populations, ponds, and performance checks.
- Visual asset matrix: 33 checks passed; `FishingRod.png` remains the only owned production art.
- Fishing Treasures optional-content contract: passed.

## Invariants

The executable `FrameworkIntegrationRules` suite verifies:

- Duplicate consume and release operations are no-ops.
- One logical event produces one population mutation, one Knowledge group, one expertise award, and one journal effect.
- Failed Knowledge work can retry without duplicating a committed DRF mutation.
- Failed DRF work creates no Knowledge evidence for a nonexistent mutation.
- Active and latent simulation advancement are mutually exclusive.
- Save restoration preserves completed operation markers.
- Map removal clears active ownership while retaining latent state.
- Provider absence/restoration preserves records and resumes safely.
- Population values remain finite and nonnegative.
- Distinct pawns, maps, ticks, and event kinds receive distinct deterministic IDs.

## Runtime Matrix

| Case | Result |
| --- | --- |
| Isolated RimWorld startup with temporary savedata | `Requires manual RimWorld validation`; one run exited 0, later runs reached startup but timed out under `-quicktest` and were stopped |
| Startup log inspection | No Aquaculture or DRF exception/unresolved provider reference; unrelated `VFET_ResearchSpot`, VOE, and framework metadata warnings remained |
| Live map event execution | `Blocked, with reason: Dev Bridge returned bridge_not_active` |
| Failure injection before/after DRF mutation | `Blocked, with reason: no live diagnostic bridge` |
| Failure injection before/after Knowledge commit | `Blocked, with reason: no live diagnostic bridge` |
| Existing save and provider-state save | `Requires manual RimWorld validation`; no historical save was loaded |
| Provider missing/restored | `Requires manual RimWorld validation`; static opaque-state and retry contracts passed |
| Multiple maps and simultaneous events | `Requires manual RimWorld validation` |
| Long-running simulation and memory/tick baselines | `Requires manual RimWorld validation` |

The temporary savedata folder was isolated under
`C:\Users\Lan\AppData\Local\Temp\opencode\aquaculture-release-matrix`.
No normal user saves or `ModsConfig.xml` were changed. No RimWorld process was
left running.

## Dev Bridge

The diagnostic-only `AQUA_DEFERRED_REALITY` command is compiled into the
Aquaculture adapter. It reports provider availability, registration and
capabilities, regions, active maps, populations, processes, paused/failing
processes, quarantines, durable operation markers, duplicate counts, recent
correlations, migration counts, and the last error. The adapter was built but
not hot-published. Discovery returned `bridge_not_active`, so no live command
report was collected.

## Remaining Blockers

- Live event/failure-injection evidence is unavailable without an active Dev Bridge and map.
- Old Aquaculture, legacy progression, and existing provider-state saves were not loaded.
- Provider removal/restoration and map unload/reload require RimWorld execution.
- No long-running multi-map simulation baseline was collected.
- Current framework checkouts contain pre-existing dirty changes, so the manifest records `sourceDirty=true`.
- Visual production-art completeness remains a separate existing blocker.
