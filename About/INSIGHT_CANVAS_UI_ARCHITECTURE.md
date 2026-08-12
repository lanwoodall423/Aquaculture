# Insight Canvas UI Architecture

Status: Prompt 1 settings migration only. Target: RimWorld 1.6, Insight Canvas 2.1.0.0.

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

| Owner | Responsibilities | Prompt 1 rule |
| --- | --- | --- |
| Native RimWorld | `Mod`, `ModSettings`, `ExposeData`, `WriteSettings`, save/load, window stack, Defs, Things, maps, jobs, and native texture/`Texture2D` lifetime | The existing `Dialog_FishMaskPainter` remains native because its pixel editor is a complex renderer-owned editor. It is opened from the document and keeps its existing cleanup and serialized mask behavior. |
| Aquaculture | `AquacultureSettings` as the authoritative model, serialized keys/defaults/migrations/clamps, settings effects, Def/database catalog snapshots, translations, and gameplay | Insight Canvas callbacks bind to the existing fields. Callbacks may change settings, but paint never runs gameplay or map/world work. |
| Insight Canvas | `InsightUiDocument`, `InsightUiHost`, compositional layout, stable-ID state scopes, bindings, focus/keyboard traversal, responsive navigation, virtual lists, theme/accessibility, effects/toasts/popovers, diagnostics, and transient cleanup | The settings screen owns one persistent document/host. `PostClose` is called when settings are written; document state is never global. |
| Knowledge Framework | Canonical knowledge, expertise, observations, evidence, and browsing semantics | Aquaculture only presents the current expertise snapshot and stable links in Prompt 1. It does not duplicate Knowledge state or mutate populations. |
| Deferred Reality Framework | Latent/off-map aggregates, regional processes, and exactly-once off-map state | No Deferred Reality state is moved into this UI. Future views consume snapshots/links only. |

The settings screen is an embedded document because RimWorld owns the Mod
settings surface. The separate mask editor remains an ordinary native
`Window`; its `PostClose` path destroys the overlay texture, increments the
mask revision, clears the visual cache, and writes settings as before.

## Source layout

- `Source/UI/AquacultureUiContracts.cs` contains effectively immutable display snapshots (`PondUiSnapshot`, `FishUiSnapshot`, `SpeciesUiSnapshot`, `BreedUiSnapshot`, `PopulationUiSnapshot`, `CommissionUiSnapshot`, `TraitUiSnapshot`, and `SettingsUiSnapshot`), stable-ID/duplicate helpers, revision invalidation, and pure responsive math.
- `Source/UI/AquacultureUiTheme.cs` clones `InsightTheme.Default` into a document-local restrained aquatic palette. Normal, compact, and comfortable density, high contrast, reduced motion, and green/amber/coral semantic states are document settings; status is also communicated with labels, text, and controls rather than color alone.
- `Source/UI/AquacultureUiComponents.cs` contains stable-ID-scoped callouts, headers, stat rows, meters, research gates, empty states, toggles, sliders, selectors, reset buttons, and page compositions.
- `Source/UI/AquacultureSettingsDocument.cs` builds the six Prompt 1 sections: Gameplay, Fishing, Ecology, Breeding, Appearance, and Advanced. Fish and trait catalogs are captured before painting, and fixed-height `InsightUiVirtualList` controls bound the searchable lists.
- `Source/SettingsAndMasks.cs` still owns `AquacultureSettings`, serialization, migration, clamps, cache invalidation, and the native mask window. Its live `DoSettingsWindowContents` delegates to the document host.

## UI invariants

1. Stable IDs are scoped by feature and entity (`fish.<defName>`,
   `advanced.trait.<defName>`, and so on). `TrackDuplicateIds` is enabled on the
   document so duplicate effective IDs are diagnostics, not silent state
   collisions.
2. Snapshot construction is a deliberate revision boundary. Document paint
   reads immutable catalog values and current authoritative settings bindings;
   it does not query `Current.Game`, maps, ponds, Things, or the world.
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

## Scope deliberately deferred

Prompt 1 does not migrate the Journal, pond management, fish-traits dialog,
stocking planner, commission UI, Deferred Reality views, or fishing rod UI.
Those remain with their current owners until their own prompts establish
snapshots and lifecycle boundaries. No gameplay, balance, population,
Knowledge, Deferred Reality, breeding, commission, or rod behavior was changed.

## Validation and release notes

Portable UI contract tests live in `DevTools/InsightCanvasUiTests.csproj` and
cover snapshot copying, stable IDs, duplicate detection, responsive math,
dependency metadata, serialized-key preservation, configurable build paths,
and the no-bundled-DLL rule. The full settings validation must still be run
through `DevTools/DEVBRIDGE2_AGENT.md`: coordinator status, restart after the
assembly build, readiness, a leased mod-owned test run, repeated settings
open/close, all categories, persistence, widths, keyboard/accessibility,
duplicate-ID diagnostics, render-error/log checks, and lease release.

The unresolved Insight Canvas owner license is a release blocker even when all
automated and DevBridge2 checks pass.
