# Prompt 2 UI validation ledger

This ledger records the Prompt 2 continuation after the explicit user override
of the recorded Prompt 1 prerequisite. It does not claim release readiness.

## Implemented surfaces

| Surface | Result | Ownership decision |
| --- | --- | --- |
| Journal main tab | Insight Canvas workspace for Overview, Ponds, Species, Breeds, Conservation, and Commissions | RimWorld keeps the main-tab shell; the Expertise page remains the canonical Knowledge Framework browser. |
| Pond workspace | Searchable virtualized master/detail, causal health priority, research disclosure, meters, and direct pond-control bindings | `FishPondMapComponent` remains authoritative. Snapshot construction precedes paint. |
| Fish dossier | Species, sex, life stage, breed, generation, condition, production, ordered virtualized traits, Knowledge stage/confidence/link | Native `ITab_FishTraits` remains the shell and fallback owner. |
| Stocking planner | Searchable available species, planned stock, water selection, cached forecast, wide adaptive grid/narrow stacking, warnings, and save/load actions | Native `Dialog_PondStockingPlanner` owns plan state and the authoritative forecast/blueprint setter. |
| Breed registration | Localized Insight Canvas form, validation feedback, keyboard-capable text/button controls | Native `Dialog_RegisterFishBreed` owns the window and `RegisterBreed` remains authoritative. |

Intentionally native: pond ITabs, pond-proxy inspect/gizmos/FloatMenus, the
mask pixel editor, tiny native interactions, and Knowledge browsing. Rod UI,
standalone commission dialogs, and deeper Deferred Reality views remain outside
this prompt. No gameplay, balance, save schema, population, or framework
authority was moved.

## Static checks

- `DevTools/Test-InsightCanvasUi.ps1`: PASS, including immutable-copy snapshots,
  health-priority ordering, deterministic planner cache identity, stable IDs,
  responsive branches, localization-key coverage, dependency metadata, no
  `GUI.skin` mutation, serialized settings preservation, and no bundled DLL.
- Configured net472 Release developer build: PASS, 0 warnings/errors, using
  explicit RimWorld, Harmony, Insight Canvas, and Knowledge Framework paths.
- Language XML parse: PASS.
- `git diff --check`: PASS.
- The native Journal handoff was corrected so the workspace does not get its
  active page overwritten every frame; canonical Knowledge expertise remains
  the native page.
- Display-only pond rows, commission text, breed trait text, and planner
  forecast meters are populated from pre-paint snapshots/caches. Live pond
  component references are retained only as callback targets for explicit
  controls; no map/world query is performed by the Insight Canvas renderer.

## Live validation boundary

The required DevBridge2-only restart was accepted for generation 214 -> 215,
then ended in `ERROR` with `PROCESS_INSPECTION_AMBIGUOUS`. `doctor` confirmed
the executable, metadata, built coordinator assembly, active coordinator, and
one coordinator-owned RimWorld PID, but `wait-ready` could not proceed and the
coordinator rejected another restart. No direct launch, kill, GUI automation,
or process control was used. The Prompt 2 workspace/planner/dossier scenarios,
keyboard/resize/accessibility matrix, duplicate-ID/render/log checks, and
post-change regression suite therefore remain BLOCKED pending coordinator
repair and a fresh generation.

The earlier generation-205 baseline/golden-path/runtime-showcase and read-only
diagnostic evidence remains valid for the pre-Prompt-2 assembly, but is not
substituted for this post-change interactive UI pass.

## Remaining release blockers

- DevBridge2 process-inspection recovery and the post-change live matrix.
- Insight Canvas owner-license selection (`Owner license selection required`).
- Production art/provenance and the broader clean-install/save-load/player UX
  gates recorded by the release matrix.
