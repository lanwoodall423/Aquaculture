# Insight Canvas UI Architecture

Status: Prompt 3 closure candidate on top of the Prompt 1 settings migration and
Prompt 2 workspace/planner/dossier implementation. Target: RimWorld 1.6,
Insight Canvas 2.1.0.0.

Validation baseline: local Insight Canvas source SHA
`93a09005fa15190009daee625352cf4004974472`, installed assembly version
`2.1.0.0`. The framework checkout had unrelated local changes when inspected;
those changes were preserved and not included in this Aquaculture commit.

Insight Canvas is an explicit runtime dependency (`lan.insightcanvas`) and is
compiled from the installed `1.6/Assemblies/InsightCanvas.dll`. Aquaculture
does not bundle a second copy, load it through reflection, or change its global
GUI state. Aquaculture's original source and packaged artifacts are licensed
under `GPL-3.0-or-later`, Copyright (C) 2026 lanwoodall423. The installed
Insight Canvas checkout separately declares GPLv3.0 in its own `LICENSE`; that
dependency license is confirmed here for release accounting, not implicitly
relicensed by Aquaculture.

## Ownership boundaries

| Owner | Responsibilities | Prompt 2 rule |
| --- | --- | --- |
| Native RimWorld | `Mod`, `ModSettings`, `ExposeData`, `WriteSettings`, save/load, main-tab/window/ITab shells, Defs, Things, maps, jobs, and native texture/`Texture2D` lifetime | The Journal main-tab shell, native pond ITabs, pond-proxy inspect/gizmos/FloatMenus, and mask pixel editor remain native. Their close/texture lifecycles stay with their existing owners. |
| Aquaculture | `AquacultureSettings`, serialized keys/defaults/migrations/clamps, persisted presentation preferences, pond/journal/breed/commission/conservation rules, Def/database catalog snapshots, translations, and gameplay | Workspace, dossier, planner, and registration callbacks read snapshots and call existing authorities. No gameplay authority moved into UI documents. |
| Insight Canvas | `InsightUiDocument`, `InsightUiHost`, compositional layout, stable-ID state scopes, bindings, focus/keyboard traversal, responsive navigation, virtual lists, theme/accessibility, effects/toasts/popovers, diagnostics, and transient cleanup | The settings screen owns one persistent document/host. `PostClose` is called when settings are written; document state is never global. |
| Knowledge Framework | Canonical knowledge, expertise, observations, evidence, and browsing semantics | The Journal Expertise page remains `KnowledgeMenuUI`; Species/dossier pages show only identity-known stage/confidence/facets and link back without duplicating the browser. |
| Deferred Reality Framework | Latent/off-map aggregates, regional processes, and exactly-once off-map state | Conservation shows prepared DRF-owned views and disclosure only; no latent state is copied or mutated by UI. |

The settings screen is an embedded document because RimWorld owns the Mod
settings surface. The separate mask editor remains an ordinary native
`Window`; its `PostClose` path destroys the overlay texture, increments the
mask revision, clears the visual cache, and writes settings as before.

## Source layout

- `Source/UI/AquacultureUiContracts.cs` contains effectively immutable display snapshots (`PondUiSnapshot`, `FishUiSnapshot`, `SpeciesUiSnapshot`, `BreedUiSnapshot`, `PopulationUiSnapshot`, `CommissionUiSnapshot`, `TraitUiSnapshot`, and `SettingsUiSnapshot`), stable-ID/duplicate helpers, revision invalidation, pure responsive math, and the content-aware virtual-list sizing contract.
- `Source/UI/AquacultureUiTheme.cs` clones `InsightTheme.Default` into a document-local restrained aquatic palette and defines the shared `4 / 8 / 12` micro/row/section spacing tokens. High contrast has stronger surface/selection/focus separation; status is also communicated with labels, text, and controls rather than color alone.
- `Source/UI/AquacultureUiPresentation.cs` is the one adoption point for the persisted Aquaculture density, high-contrast, and reduced-motion preferences. Every player-facing Canvas document applies it after construction.
- `Source/UI/AquacultureUiComponents.cs` contains stable-ID-scoped callouts, headers, stat rows, meters, semantic status, panels, research gates, empty states, toggles, value-labelled sliders, selectors, reset groups, page compositions, and the shared content-aware virtual-list factory/refresh operation.
- `Source/UI/AquacultureUiDisclosure.cs` is the runtime snapshot adapter that combines existing research, Knowledge, and gameplay snapshots into the non-persistent disclosure contract without owning any of that state.
- `Source/UI/AquacultureSettingsDocument.cs` builds the six Prompt 1 sections: Gameplay, Fishing, Ecology, Breeding, Appearance, and Advanced. Fish and trait catalogs are captured before painting, and searchable catalogs use content-aware bounded virtual-list viewports.
- `Source/UI/AquacultureJournalWorkspaceDocument.cs` is the central Journal document: Overview and Species are always available, while Ponds, Breeds, Conservation, and Commissions are composed from the current disclosure snapshot. It captures pond/journal/water data before paint, applies deterministic health priority, uses searchable content-aware virtual lists, separates conservation master/detail panes, and binds controls to `FishPondMapComponent`.
- `Source/UI/AquacultureFishDossierDocument.cs` embeds the structured fish dossier inside the native `ITab_FishTraits`; `Source/UI/AquacultureStockingPlannerDocument.cs` embeds the planner and caches copied forecasts by plan/water/revision inputs.
- `Source/UI/AquacultureBreedRegistrationDocument.cs` replaces the substantial breed-registration content while `Dialog_RegisterFishBreed` retains native `Window` ownership and delegates validation/registration to the journal authority.
- `Source/UI/AquacultureCommissionDeliveryDocument.cs` owns the searchable, virtualized commission-specimen content while `Dialog_DeliverAquacultureCommission` retains native `Window` pause/close ownership and delegates delivery to `AquacultureCommissionComponent.TryDeliver`.
- `Source/SettingsAndMasks.cs` still owns `AquacultureSettings`, serialization, migration, clamps, cache invalidation, and the native mask window. Its live `DoSettingsWindowContents` delegates to the document host.

## Prompt 2 player-experience pass

The Journal workspace now treats captured pond health as a player action queue:
`AquacultureWorkspaceOrdering` sorts critical conditions before warnings and
healthy rows, while `AquaculturePondFilter` provides explicit status filtering
and search. Overview opens with one action-oriented bounded priority list; each
priority row selects the pond workspace, focuses the live pond only from an
explicit callback, and the selected detail can open the existing authoritative
stocking planner. Healthy and no-pond states are
explicit text states rather than empty color or blank lists.

Pond detail always presents basic observed state, then composes Feeding and
Harvesting at Managed Aquaculture, Population management and industrial
diagnostics at Industrial Aquaculture, and Breeding at Selective Breeding.
Unavailable groups are absent from the layout rather than disabled beneath
future-feature labels. Slider controls are wrapped with live value labels,
tooltips, and existing authority setters; no pond defaults, balance values, or
research rules are defined in the document. Species, breeds, conservation, and
commissions retain bounded lists, selected-row treatments, semantic badges, and
explicit empty/status states. Knowledge and Deferred Reality remain owned by
their canonical owners.

The fish dossier captures food reserve, habitat fit, starvation pressure,
expected yield, sterilization, and a pure `AquacultureDossierHealthState`.
Health is elevated in a semantic callout and badge; exact food/habitat meters
require Industrial Aquaculture plus learned health data, expected yield requires
Managed Aquaculture plus learned identity/size, and traits/lineage remain
Knowledge-authorized. Identity/life stage remain observable when initialized,
while unavailable groups/rows are omitted. Unknown source values stay unknown.

The workspace uses `AquacultureUiResponsiveLayout.MasterDetailMode` to stack
master/detail panes at narrow widths. Stable selections are preserved through
snapshot refreshes, duplicate IDs remain enabled, tooltips are attached to
non-obvious controls, and all six documents continue to inherit the persistent
Comfortable/Normal/Compact, high-contrast, and reduced-motion preferences.

## Prompt 2 layout, scroll, and information-density pass

The local spacing contract is deliberately small: `AquacultureUiSpacing.Micro`
for label/detail relationships, `Row` for ordinary controls, and `Section` for
page and panel boundaries. This keeps the aquatic surface expressive without
letting every document invent a new gap scale.

`AquacultureUiListSizing.HeightForCount` is the pure viewport rule used by every
player-facing virtual list. Zero rows produce zero list height, one/few rows use
their actual row height, and larger collections stop at a bounded viewport while
Insight Canvas virtualizes the visible range. The helper is applied after every
filter/snapshot refresh, so disclosure changes or search results cannot leave a
stale empty viewport behind.

Scroll ownership is now pane-local:

- Overview and the simple hybrid dialogs have one document scroll owner. Their
  bounded lists only become an inner scroll owner when a large collection needs
  virtualization.
- Ponds, Species, Breeds, and Conservation use a master/detail split. The
  master owns the bounded searchable list; the detail pane owns its vertical
  scroll. Conservation no longer places a flex-growing list and its selected
  detail in one outer scroll column.
- Settings pages retain their page scroll for surrounding controls. Fishing and
  Advanced use bounded catalog viewports only for large searchable catalogs;
  short and empty catalogs do not reserve the old fixed 264/304 pixel spaces.
- The fish dossier keeps one primary dossier scroll. Its entitlement-scoped
  trait labels are a short ordinary group, which avoids a second fixed list
  viewport for a naturally small inspection surface.
- The planner gives each catalog its own bounded virtual viewport and keeps the
  forecast as the independently scrollable explanatory panel. Commission
  specimens use the same bounded-list policy because eligibility can exceed the
  dialog's comfortable height.

Overview presents current work first and leaves detailed diagnosis to the Pond
page. Dossier identity, production, traits, and Knowledge sections are composed
only when entitled; the quick status badge and explanatory health callout have
different jobs, while duplicate overview attention callouts/statistics/legends
are not constructed.

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
   flags. Aquaculture applies its persisted preferences to each document;
   reduced motion is passed to the framework effect pipeline, and no
   `GUI.skin` mutation is used by the new UI.
5. `AquacultureSettings` keeps every existing serialized key, default,
   migration field, and clamp. The old `globalMutationRate`, `maxMutations`,
   `traitBreedingSettingsVersion`, `capacityModelVersion`, and `fishMasks`
   paths remain intact.

## Information authority and progressive disclosure

The player-facing invariant is explicit: research controls capability, Knowledge
Framework controls understanding, Aquaculture owns authoritative gameplay state,
and Insight Canvas presents only the intersection the colony is entitled to see.
`AquacultureUiDisclosureSnapshot` is a display-only, non-persistent contract;
it records no learned facts and does not replace any framework authority.

The Journal policy is:

- Overview and Species are early surfaces; individual species rows still show
  Knowledge-hidden identity as unknown.
- Overview keeps one coarse priority list for current pond work; detailed pond
  diagnosis is left to the Pond page rather than repeated through health buckets,
  legends, and overview callouts.
- Ponds appear when Pondkeeping is available or a pond exists. Basic observed
  pond state remains available; managed feeding/harvesting, industrial
  diagnostics/precision, population management, and selective-breeding controls
  appear only at their capability tiers. Exact diagnosis/metrics also require
  the relevant learned health/population state.
- Breeds appear after Selective Breeding or a meaningful registered-breed
  encounter. Conservation appears only when a known species has a learned
  population observation tied to a prepared water view. Deferred Reality's
  internal simulation alone does not advertise the page. Commissions appear
  when an active commission or registered-breed state makes the system relevant.

The dossier audits fields individually: species identity and Knowledge links are
Knowledge-authorized; sex and life stage are observable for initialized fish;
visible trait labels and generation/lineage require learned facets; food reserve,
habitat fit, starvation precision, and expected meat yield require the matching
Knowledge facts plus research capability; sterilization is shown only when the
state is true or the breeding context is authorized; health remains a coarse
observed classification until exact diagnostics are entitled. Hidden numerical
values are represented by unknown sentinels in display snapshots and never
formatted for paint.

Disclosure is refreshed at the snapshot boundary while a Journal or dossier is
open. A changed page/group policy replaces the document root using stable IDs,
and an active page that is no longer entitled falls back to Overview. Paint and
callbacks continue to operate on copied display rows and explicit authority
setters.

## Persistent presentation contract

Aquaculture presentation preferences are owned by `AquacultureSettings`, not by
Insight Canvas global settings. The new serialized keys are
`presentationDensity`, `presentationHighContrast`, `presentationReducedMotion`,
and `presentationSettingsVersion`. Density uses the stable integer order
`Comfortable = 0`, `Normal = 1`, `Compact = 2`; missing or invalid density values
resolve to Normal or clamp to the supported range, while the two boolean values
default to false. Post-load migration advances the presentation version without
changing gameplay settings, and save-time normalization prevents invalid values
from being written.

`AquacultureInsightPresentation.Apply` maps the authority to each document's
local theme, density, high-contrast, and reduced-motion flags. The settings
document calls it after a presentation control changes, so the current screen
updates immediately; Journal, dossier, planner, registration, and commission
documents call it on construction and inherit the next opened surface's saved
preferences. Existing serialized keys and sparse gameplay settings remain
unchanged.

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
Deferred Reality gameplay behavior was not moved or changed. The commission
delivery dialog is now a documented hybrid surface; rod UI, tiny pond actions,
and detailed conservation remain intentionally native or workspace-snapshot
surfaces rather than being migrated for framework coverage alone.

## Validation and release notes

Portable UI contract tests live in `DevTools/InsightCanvasUiTests.csproj` and
cover snapshot copying, health-priority ordering, planner cache identity,
stable IDs, duplicate detection, responsive math, content-aware list sizing for
empty/short/large/min/max and density inputs, persisted presentation
preference reload/clamping, centralized document adoption, localization and
surface contracts, behavioral progressive-disclosure states for
research/Knowledge disagreement, dependency metadata, serialized-key
preservation, configurable build paths, and the no-bundled-DLL rule. The full settings and
Prompt 2 validation must still be run
through `DevTools/DEVBRIDGE2_AGENT.md`: coordinator status, restart after the
assembly build, readiness, a leased mod-owned test run, workspace/pond/planner/
dossier/registration scenarios, repeated open/close, persistence, widths,
keyboard/accessibility, bounded lists, duplicate-ID diagnostics,
render-error/log checks, and lease release.

The owner-controlled production-art requirement is explicitly waived for this
candidate; see `About/ART_REQUIREMENTS.md`. That waiver does not change the
license or ownership of vanilla, VFE, or other third-party assets.

## Prompt 3 closure inventory and decisions

The following is the player-facing ownership inventory for the current closure
candidate. `Insight Canvas` means the substantial content is document-owned;
`hybrid` means RimWorld retains the shell and Aquaculture retains authoritative
callbacks; `native` is intentional because the surface is a compact interaction
or owns native texture/job/map lifetime; `debug` is not shipped as player UI.

| Surface | Ownership | Closure decision |
| --- | --- | --- |
| Aquaculture settings | hybrid | Native ModSettings shell; persistent Insight Canvas document and serialized Aquaculture authority. |
| Journal Overview/Ponds/Species/Breeds/Conservation/Commissions | Insight Canvas | Snapshot-before-paint workspace with stable IDs, bounded lists, research gates, and authoritative pond callbacks. |
| Journal Expertise | native/hybrid | Native main-tab shell routes to Knowledge Framework `KnowledgeMenuUI`; canonical browser is not duplicated. |
| Fish dossier / `ITab_FishTraits` | hybrid | Native ITab selection shell; copied fish dossier document and Knowledge link. |
| Stocking planner / `Dialog_PondStockingPlanner` | hybrid | Native Window ownership; responsive planner document, cached forecast, and owner blueprint callbacks. |
| Breed registration / `Dialog_RegisterFishBreed` | hybrid | Native Window lifecycle; document-owned validation presentation; journal owns registration. |
| Commission delivery / `Dialog_DeliverAquacultureCommission` | hybrid | Native Window lifecycle; document-owned searchable specimen list; commission component owns eligibility, reward, and consumption. |
| Pond overview/management/breeding ITabs | native | Progressive research visibility and compact map/Thing authority remain with native pond ownership. |
| Pond inspect strings, water FloatMenus, pond/fish FloatMenus, gizmos, commands | native | Small, context-sensitive actions remain native; no duplicate Canvas shell is justified. |
| Fishing tackle dialog/rod jobs | native | Native item/job interaction and reservation lifecycle remain authoritative. |
| Fish-mask editor | native | Native texture editor owns `Texture2D` cleanup and settings persistence. |
| Knowledge Framework browser | native framework | Knowledge Framework remains canonical for evidence, expertise, confidence, and browsing. |
| Deferred Reality diagnostics/provider | debug/developer | Provider and diagnostics are framework integration/test surfaces, never player dependencies. |

Optional advanced Canvas features were reviewed. A lineage constellation and
population timeline are not added: the current authoritative data is better
explained by the existing breed rows, milestones, and prepared conservation
snapshots, and a graph/timeline would expose either redundant or hidden exact
history. The causal pond callout/meter model is retained because it explains
unsafe conditions without a second visualization. Map focus remains a callback
concern and is not invoked from paint.

Prompt 2 release evidence is superseded by Prompt 3 closure work. The closure
candidate baseline is commit `c50990a`; the exact framework evidence remains
Insight Canvas source SHA `93a09005fa15190009daee625352cf4004974472`, installed
assembly version `2.1.0.0`. Current closure checks and any unexecuted owner-
controlled gates are recorded in `About/Validation/RC_STATUS.md`.

## Prompt 3 authority matrix and release-safety policy

The final information-authority invariant is: research grants a capability to
act or measure; the Knowledge Framework grants interpretation of biological
facts; Aquaculture gameplay records grant event/state evidence; and the UI may
render only the intersection of those authorities. A raw simulation value,
profile, causal issue, or complete Def catalog is internal-only until the
matching entitlement has been established. `Unknown`, a coarse status, or an
empty state is preferable to an inferred exact value.

| Player-facing item | Authority classification | Expected presentation stage |
| --- | --- | --- |
| Journal Overview | Aquaculture snapshot; colony-recorded pond state | Always; sparse action summary with coarse warnings before diagnostic entitlement. |
| Ponds navigation | Research/technology-dependent plus existing pond gameplay state | Pondkeeping research or an existing pond; no page from Knowledge alone. |
| pond basic identity/water/population | Directly observable and colony-recorded `PondMenuSnapshot` state | On the Ponds page; water and population/capacity are shown without exact health interpretation. |
| pond warnings | Aquaculture causal summary, with Knowledge interpretation for exact detail | Coarse “healthy/observed distress” until Industrial plus a learned health facet; exact issue labels then. |
| exact pond health classification | Research/technology-dependent and Knowledge Framework health facet | Industrial Aquaculture plus colony-known health; exact priority/filter terminology is absent otherwise. |
| exact feeding reserve/diagnostics | Research/technology-dependent and Knowledge Framework health facet | Industrial plus initialized/recorded diagnostic context; raw reserve, stress, and feeder metrics remain hidden otherwise. |
| harvesting controls | Research/technology-dependent, backed by pond authority | Managed Aquaculture; controls are absent before the tier. |
| population-management controls | Research/technology-dependent, backed by pond authority | Industrial Aquaculture; controls are absent before the tier. |
| stocking planner | Research/technology-dependent shell; Knowledge-authorized species facts; owner forecast | Industrial opens the planner. A species is available to the forecast only when identity, feeding, habitat, and pond-compatibility facets are colony-known. |
| breeding controls | Research/technology-dependent, backed by pond authority | Selective Breeding; no locked control group is constructed before the tier. |
| Species identity | Knowledge Framework identity claim | Species rows use an unknown placeholder until identity is known. |
| Species known facets | Knowledge Framework facet evidence | Facets are listed only for an identity-known species; unknown facts are not inferred from Defs. |
| Knowledge stage/confidence | Knowledge Framework stage and claim confidence | Shown only for identity-known species and linked to the canonical Knowledge browser. |
| Breeds navigation | Research capability or gameplay/event record | Selective Breeding or a registered breed record; no unrelated empty page. |
| breed name/registration | Colony-recorded gameplay event and journal breed record | Only an actual registered record is rendered. |
| generation/lineage | Gameplay/event record, with breeding Knowledge gating in fish dossier | Registered breed records may show their recorded analytics; individual lineage requires a registered breed plus the learned breeding facet. |
| breed analytics | Colony-recorded breed record | Stability, success, and recorded generation metrics only for an existing breed. |
| Conservation navigation | Knowledge Framework population facet plus prepared water gameplay state | A known species population observation must intersect a prepared water record. |
| conservation status/trends | Deferred Reality/Aquaculture prepared snapshot filtered through Knowledge | Known species statuses are shown; exact abundance requires Industrial plus known population/conservation context. |
| Commissions navigation | Gameplay/event state | Active commission or registered-breed relevance; no speculative commission page. |
| fish-dossier identity | Knowledge identity claim, with direct initialized-fish state | Species name requires identity; sex and life stage are shown only for an initialized specimen. |
| fish health | Directly observable coarse classification plus Knowledge/research exact diagnostics | Initialized fish may show coarse health; exact reserves and diagnostic text require Industrial plus learned health. |
| food reserve | Research/technology-dependent and Knowledge Framework health facet | Exact value only for an initialized fish with Industrial and known health; hidden sentinel otherwise. |
| habitat fit | Research/technology-dependent and Knowledge Framework health facet | Same exact-health gate as food reserve. |
| traits | Knowledge Framework traits facet and player-readable trait policy | Trait labels only after the traits facet is known; internal trait definitions are not copied into the UI. |
| expected yield | Research/technology-dependent plus Knowledge identity/size facets | Initialized fish with Managed Aquaculture, known identity, and known size. |
| sterilization | Directly observable true state or Selective Breeding plus breeding Knowledge | Shown for an initialized sterile fish, or when the selective/breeding entitlement permits the state; otherwise omitted. |

Raw causal issue objects, exact reserves, complete species/profile catalogs,
permission booleans, and forecast intermediates remain internal-only. The
stocking planner defensively prunes any plan entry that is no longer within its
Knowledge boundary before exposing rows, totals, warnings, or saving a UI
blueprint. Exact pond filters use the same `PondDiagnosisVisible` boundary as
the detail labels, so a research-only colony cannot select “Critical” and then
read a raw priority.

Navigation disclosure is monotonic with evidence but not sticky with stale UI
state: a research/event snapshot rebuilds the root, preserves valid entity
selection, and resolves a removed active page to Overview. Empty/few/many lists
use content-aware bounded viewports; each split-pane master owns its list and
each detail pane owns its own scroll. No virtual list flex-grows inside a
scrolling parent, and hidden groups do not reserve a viewport. Stable IDs remain
scoped by entity and duplicate-ID diagnostics remain enabled. Density,
high-contrast, reduced-motion, keyboard focus, and transient cleanup continue
to be owned by Insight Canvas through the centralized presentation contract.

The owner-controlled validation evidence and the distinction between portable,
smoke, interactive, and log gates are recorded as a dated ledger in
`About/Validation/RC_STATUS.md`; this document never upgrades an unexecuted
interactive matrix to a live PASS.
