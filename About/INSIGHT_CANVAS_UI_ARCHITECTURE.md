# Insight Canvas UI Architecture

Status: Prompt 2 workspace/planner/dossier continuation on top of the Prompt 1
settings migration. Target: RimWorld 1.6, Insight Canvas 2.1.0.0.

Validation baseline: local Insight Canvas source SHA
`93a09005fa15190009daee625352cf4004974472`, installed assembly version
`2.1.0.0`. The framework checkout had unrelated local changes when inspected;
those changes were preserved and not included in this Aquaculture commit.

Insight Canvas is an explicit runtime dependency (`lan.insightcanvas`) and is
compiled from the installed `1.6/Assemblies/InsightCanvas.dll`. Aquaculture
does not bundle a second copy, load it through reflection, or change its global
GUI state. The owner-license selection for Insight Canvas is still unresolved;
that is a release blocker until the framework owner selects a license.

## Ownership boundaries

| Owner | Responsibilities | Prompt 2 rule |
| --- | --- | --- |
| Native RimWorld | `Mod`, `ModSettings`, `ExposeData`, `WriteSettings`, save/load, main-tab/window/ITab shells, Defs, Things, maps, jobs, and native texture/`Texture2D` lifetime | The Journal main-tab shell, native pond ITabs, pond-proxy inspect/gizmos/FloatMenus, and mask pixel editor remain native. Their close/texture lifecycles stay with their existing owners. |
| Aquaculture | `AquacultureSettings`, serialized keys/defaults/migrations/clamps, pond/journal/breed/commission/conservation rules, Def/database catalog snapshots, translations, and gameplay | Workspace, dossier, planner, and registration callbacks read snapshots and call existing authorities. No gameplay authority moved into UI documents. |
| Insight Canvas | `InsightUiDocument`, `InsightUiHost`, compositional layout, stable-ID state scopes, bindings, focus/keyboard traversal, responsive navigation, virtual lists, theme/accessibility, effects/toasts/popovers, diagnostics, and transient cleanup | The settings screen owns one persistent document/host. `PostClose` is called when settings are written; document state is never global. |
| Knowledge Framework | Canonical knowledge, expertise, observations, evidence, and browsing semantics | The Journal Expertise page remains `KnowledgeMenuUI`; Species/dossier pages show only identity-known stage/confidence/facets and link back without duplicating the browser. |
| Deferred Reality Framework | Latent/off-map aggregates, regional processes, and exactly-once off-map state | Conservation shows prepared DRF-owned views and disclosure only; no latent state is copied or mutated by UI. |

The settings screen is an embedded document because RimWorld owns the Mod
settings surface. The separate mask editor remains an ordinary native
`Window`; its `PostClose` path destroys the overlay texture, increments the
mask revision, clears the visual cache, and writes settings as before.

## Source layout

- `Source/UI/AquacultureUiContracts.cs` contains effectively immutable display snapshots (`PondUiSnapshot`, `FishUiSnapshot`, `SpeciesUiSnapshot`, `BreedUiSnapshot`, `PopulationUiSnapshot`, `CommissionUiSnapshot`, `TraitUiSnapshot`, and `SettingsUiSnapshot`), stable-ID/duplicate helpers, revision invalidation, and pure responsive math.
- `Source/UI/AquacultureUiTheme.cs` clones `InsightTheme.Default` into a document-local restrained aquatic palette. Normal, compact, and comfortable density, high contrast, reduced motion, and green/amber/coral semantic states are document settings; status is also communicated with labels, text, and controls rather than color alone.
- `Source/UI/AquacultureUiComponents.cs` contains stable-ID-scoped callouts, headers, stat rows, meters, research gates, empty states, toggles, sliders, selectors, reset buttons, and page compositions.
- `Source/UI/AquacultureSettingsDocument.cs` builds the six Prompt 1 sections: Gameplay, Fishing, Ecology, Breeding, Appearance, and Advanced. Fish and trait catalogs are captured before painting, and fixed-height `InsightUiVirtualList` controls bound the searchable lists.
- `Source/UI/AquacultureJournalWorkspaceDocument.cs` is the Prompt 2 central Journal document: Overview, Ponds, Species, Breeds, Conservation, and Commissions. It captures pond/journal/water data before paint, applies deterministic health priority, uses searchable virtual lists, and binds controls to `FishPondMapComponent`.
- `Source/UI/AquacultureFishDossierDocument.cs` embeds the structured fish dossier inside the native `ITab_FishTraits`; `Source/UI/AquacultureStockingPlannerDocument.cs` embeds the planner and caches copied forecasts by plan/water/revision inputs.
- `Source/UI/AquacultureBreedRegistrationDocument.cs` replaces the substantial breed-registration content while `Dialog_RegisterFishBreed` retains native `Window` ownership and delegates validation/registration to the journal authority.
- `Source/SettingsAndMasks.cs` still owns `AquacultureSettings`, serialization, migration, clamps, cache invalidation, and the native mask window. Its live `DoSettingsWindowContents` delegates to the document host.

## UI invariants

1. Stable IDs are scoped by feature and entity (`fish.<defName>`,
   `advanced.trait.<defName>`, and so on). `TrackDuplicateIds` is enabled on the
   document so duplicate effective IDs are diagnostics, not silent state
   collisions.
2. Snapshot construction is a deliberate revision boundary. Workspace, dossier,
   and planner documents capture live map/Thing/forecast data before
   `Host.Draw`; retained paint/layout code reads copied rows only. No map/world
   query is performed from the Insight Canvas composition or custom paint path.
3. Display bindings use non-creating accessors. A trait or fish record is
   created only when the player changes that setting, preserving the existing
   sparse-settings save behavior.
4. Insight Canvas controls own Tab/Shift+Tab focus, Enter/Space activation,
   scrolling, responsive navigation, transient effects, and accessibility
   flags. No `GUI.skin` mutation is used by the new UI.
5. `AquacultureSettings` keeps every existing serialized key, default,
   migration field, and clamp. The old `globalMutationRate`, `maxMutations`,
   `traitBreedingSettingsVersion`, `capacityModelVersion`, and `fishMasks`
   paths remain intact.

## Dependency and build contract

`About/About.xml` declares `lan.insightcanvas` and loads it before Aquaculture.
The project accepts portable paths rather than requiring a machine-specific
checkout:

```powershell
dotnet build Source\AquacultureFishing.csproj --configuration Release --no-restore `
  -p:RimWorldDir="C:\Games\Steam\steamapps\common\RimWorld" `
  -p:HarmonyPath="<path-to-0Harmony.dll>" `
  -p:InsightCanvasDir="<InsightCanvas-checkout>" `
  -p:KnowledgeFrameworkAssemblyPath="<KnowledgeFramework.dll>"
```

`InsightCanvas.dll` is a normal RimWorld dependency and is never copied by
the Aquaculture player-package allowlist. DevBridge2 remains optional and is
the only supported live-test coordinator; it is not a player dependency.

## Prompt 2 migration decisions and deferred scope

The Journal remains the central native main-tab owner, but all non-Expertise
pages route to the document workspace. Expertise continues through the
canonical Knowledge browser. Pond management ITabs and tiny pond-proxy
FloatMenus/gizmos remain native because they are compact interactions tied to
inspect/job ownership. The fish ITab is hybrid: the native shell remains while
the dossier owns structured content and transient cleanup. The planner and
breed-registration dialog are hybrid windows whose substantial content is
Insight Canvas while authoritative state and close behavior remain native.

Journal, pond, fish-traits, planner, commission, rod, Knowledge browsing, and
Deferred Reality gameplay behavior was not moved or changed. Rod UI and any
future standalone commission/detailed conservation dialogs remain deferred;
the workspace presents commission/conservation snapshots and links only.

## Validation and release notes

Portable UI contract tests live in `DevTools/InsightCanvasUiTests.csproj` and
cover snapshot copying, health-priority ordering, planner cache identity,
stable IDs, duplicate detection, responsive math, Prompt 2 localization and
surface contracts, dependency metadata, serialized-key preservation,
configurable build paths, and the no-bundled-DLL rule. The full settings and
Prompt 2 validation must still be run
through `DevTools/DEVBRIDGE2_AGENT.md`: coordinator status, restart after the
assembly build, readiness, a leased mod-owned test run, workspace/pond/planner/
dossier/registration scenarios, repeated open/close, persistence, widths,
keyboard/accessibility, bounded lists, duplicate-ID diagnostics,
render-error/log checks, and lease release.

The unresolved Insight Canvas owner license is a release blocker even when all
automated and DevBridge2 checks pass.
