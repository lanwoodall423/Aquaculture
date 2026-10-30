# Compatibility Contract

## Required

- Harmony
- Insight Canvas 2.1.0 (`lan.insightcanvas`)
- Knowledge Framework
- Deferred Reality Framework
- Vanilla Fishing Expanded

## Standalone-core migration (planned; not yet available)

**Current status: dependency-required.** The list above is the current player
install contract, not an optional list. Do not remove these `modDependencies`
or treat successful portable/quicktest checks as proof of a standalone mod.
The target for AQ-DEP is **RimWorld 1.6 + Harmony**, with Insight Canvas,
Knowledge Framework, Deferred Reality Framework and Vanilla Fishing Expanded
as separately enabled enhancements. No standalone build, clean-install, live
gameplay or old-save compatibility qualification has passed for that target.

The developer-only AQ-DEP-01 inventory runs as part of the portable
`DevTools/InsightCanvasUiTests.csproj` executable. It reports the current
required package IDs, direct main-project references and build errors, core
C# namespace uses, VFE-dependent XML Def references, first-party fish content
with an explicit recognized category, and the separately built DRF provider's
output/package boundary. Its passing result means that the audit ran; any
`BLOCKER` lines remain blockers. Zero static blockers still means **UNVERIFIED**
until the exact standalone artifact and in-game scenarios pass. The inventory
reads project files only and does not need framework assemblies or a
RimWorld process.

The inventory can also run **without the rest of the legacy UI-contract
suite** during later decoupling work:

```powershell
dotnet run --project DevTools/InsightCanvasUiTests.csproj --configuration Release -- --dependency-audit
```

| Integration | Current hard edge | Intended optional boundary |
| --- | --- | --- |
| Insight Canvas | The core project directly references `InsightCanvas.dll`; settings, Journal, dossier, planner and dialogs construct Canvas documents. | Keep authoritative game state in Aquaculture; provide usable native UI when absent, with optional enhanced documents in a safely isolated adapter. |
| Knowledge Framework | `KnowledgeFramework.dll` is required at build time; progression/rank and Journal types also appear in core, beyond the retry-safe framework adapter. | Move core rank/progression types and basic understanding to Aquaculture; isolate V3 observations, evidence, migration and browsing behind an optional adapter without rewriting saved legacy keys. |
| Deferred Reality Framework | AQ-DEP-02 places future provider builds in an `IfModActive`-gated folder, but legacy prebuilt provider artifacts still reside in the repository's unconditional `1.6/Assemblies` and the DRF dependency is still declared required. | Verify the conditional build/player ZIP/RimTest package; separately migrate old tracked binary artifacts, then prove active and latent ownership, provider removal/restoration and saves without a second simulation. |
| Vanilla Fishing Expanded | Fishing patches are dynamically discovered from VFE and Odyssey job drivers, but VFE categories and texture paths occur in base Defs; there are no first-party explicitly categorized fish ThingDefs or proven independent natural-water catch job. | Add core-owned fish species, assets and a reliable native fishing path; keep VFE jobs, species, zones and fish packs as opt-in compatibility. |
| Harmony | Core patches depend on it throughout. | Remains required in the initial standalone target. |

AQ-DEP-03A narrows Knowledge Framework coupling without changing its player
requirement. Aquaculture now owns the `FishingRank` model and legacy
0/2/3/4 settings mapping; fishing progression, rods and settings no longer
import the framework rank type. The original Knowledge Framework
`KnowledgeRanks.ForExperience` and `KnowledgeRanks.Bonus` behavior remains
inside the adapter, and the Knowledge Journal converts only when constructing
the framework-owned browser model. This is a partial refactor: the main
assembly still references the framework and cannot be loaded without it.
Portable rank tests do not substitute for exact-build and save/load tests.

AQ-DEP-03B1 adds a framework-free, read-only projection of legacy fishing
experience, rank progress, and species discovery in
`NativeFishingKnowledgeRules`. The existing Knowledge adapter now uses
that projection only when the framework is not ready. It also fixes a
fallback recursion: `ExpertiseProgressFor` previously read the pawn's
`ExpertiseProgress` property, which calls back into the adapter. The
fallback now reads saved `expertiseExperience` directly. Corrupt
or nonfinite legacy read values are treated as unknown/zero rather than
creating invalid UI progress. Knowledge V3 remains authoritative when
available; its rank/bonus APIs, registration, event identities, migrations,
and save keys have not changed. This still cannot run without the framework
DLL while the core project retains its mandatory assembly reference.

AQ-DEP-03B2 extends the native fallback to colony-wide species identity
knowledge. While the framework is not ready, both the direct colony query and
the colony species view aggregate existing per-pawn discovery for the player's
faction, clamping the total to 100%. Non-player pawn records are excluded
before any knowledge is read. This reuses legacy saved values without a
separate knowledge store or new save fields; the Knowledge V3 colony query,
migration and provider remain authoritative whenever the framework is ready.
The mandatory framework assembly reference and player dependency have **not**
been removed, and exact game/save compatibility testing is still required.

AQ-DEP-03C1 moves the Knowledge Framework expertise browser model, UI state and
drawing out of the native Journal window into a per-window `ExpertiseMenu` owned
by the existing Knowledge adapter. Journal navigation and colonist/colony
selection remain the same, with framework rank conversion and row-locking
inside the adapter. `Source/AquacultureJournal.cs` no longer imports Knowledge
Framework types; `Source/AquacultureKnowledgeAdapter.cs` is now the sole core
source file importing that namespace. This is a **source boundary only**:
the adapter remains in the core assembly and its Knowledge Framework DLL
is still required. Portable UI contracts verify the boundary, not a live
RimWorld render or a dependency-free build.

AQ-DEP-03C2 adds a **read-only native RimWorld expertise page** for the
Aquaculture Journal when Knowledge V3 is unready. It shows recorded
player-faction anglers or colony discovery from the legacy progress save,
using the existing native rank and knowledge projections. Undiscovered fish
and personal species locked by the selected angler's minimum expertise remain
hidden. The original Knowledge Framework browser remains unchanged and is
used whenever registered and ready. Both views retain independent
window-local selection and reset it on Back. No new knowledge database,
serialized keys, observations or migration markers were introduced.
Because the core still compiles against `KnowledgeFramework.dll`,
this is a native UI **fallback**, not a dependency-free player package;
live UI/save qualification is still required.

AQ-DEP-03D1 introduces **an opt-in, developer-only native-Knowledge build
variant**, not a new player package. The ordinary `AquacultureFishing` build
continues to compile the existing Knowledge V3 adapter and requires its
framework DLL. Setting `-p:AquacultureNativeKnowledge=true` instead
excludes that source file and its Knowledge Framework assembly reference,
defines `AQUACULTURE_NATIVE_KNOWLEDGE`, and builds a read-only native
adapter against the existing fishing progression save fields and Journal
fallback. Output goes only to `artifacts/NativeKnowledge/`, not
`1.6/Assemblies/`, to prevent accidental player packaging.

With the RimWorld 1.6 managed assemblies, Harmony, and Insight Canvas
development references available, the intended **unqualified experiment**
can be requested through the project owner and RimTest using
`dotnet build Source/AquacultureFishing.csproj -c Release -p:AquacultureNativeKnowledge=true`.
The native path intentionally does not publish Knowledge V3 events or
restore its external observation/evidence state. Ordinary Aquaculture
catch completion still updates legacy saved species discovery and expertise.
Portable checks verify the compiler switch, adapter source surface and
metadata boundary only. **No framework-free main DLL compilation, live
gameplay, old Knowledge V3 save migration or framework add/remove scenario
has yet passed.** Do not install or publish the experimental DLL and do not
remove the Knowledge Framework mod dependency on this evidence alone.

Complete optionalization only after a build **without the four optional
mods or their APIs**, a clean player package, a managed RimTest load, a
fresh-colony fishing/stocking/breeding/harvest path, UI access, save/reload and
dependency add/remove scenarios have independent evidence. Removing a
fish-content provider from a save containing its ThingDefs is not automatically
safe; retain that explicit compatibility limitation. The historical
release-readiness matrix remains blocked until its own acceptance gates pass.

## Optional

- Vanilla Fishing Expanded - Fishing Treasures AddOn: VFE's optional fishing-loot content. Aquaculture has no direct Def, assembly, recipe, species, treasure, or patch dependency on it; when installed, load it after Vanilla Fishing Expanded and before Aquaculture for its own VFE content.
- Aquariums!: detected without an assembly reference. Compatible fish are recognized through loaded defs and aquarium comps.
- Odyssey: fishing research is used as the Pondkeeping prerequisite when available.
- Additional fish packs: VFE/vanilla Fish categories and the Aquariums comp remain recognized; `AquaticSpeciesExtension` can also explicitly identify a fish without those categories and supply habitat, biome, temperature, season, rarity, density, and suitability metadata. Do not attach this marker to non-fish resources. Unextended VFE fish reuse VFE water, biome, and commonality metadata.

## Load Order

Load Harmony, Insight Canvas, Knowledge Framework, Deferred Reality Framework, and Vanilla Fishing Expanded before Aquaculture - Fishing. Fishing Treasures is optional; when present, load it after Vanilla Fishing Expanded. Aquariums may load before Aquaculture - Fishing. No compatibility mod should be required for ordinary fish packs.

Insight Canvas is a normal player dependency for the Prompt 1 settings surface. Aquaculture compiles against the installed framework assembly and does not ship a duplicate DLL. Aquaculture's original source is licensed under `GPL-3.0-or-later` (Copyright (C) 2026 lanwoodall423); the installed Insight Canvas checkout separately declares GPLv3.0 in its own license file. See `INSIGHT_CANVAS_UI_ARCHITECTURE.md` for the ownership boundary. The owner-controlled dedicated production-art requirement is waived for the current candidate; third-party asset terms remain separate.

## Def Mutation Contract

- Only positively identified runtime fish receive the Def-backed `CompFishTraits`, individual stack limit, realtime drawing behavior, missing Beauty/Mass defaults, and Traits tab required by fish lifecycle, rendering, stats, and save compatibility.
- Explicit compatible Beauty/Mass values are preserved. Nonstandard drawer types are preserved with one diagnostic; stack limit one remains required because fish traits and lifecycle state are individual and serialized.
- The owned dead-fish recipe starts with an empty `AF_RecognizedFishIngredient` category (parent `Root`), which is **not assigned** to fish or non-fish ThingDefs. Startup allows only positively recognized fish in both filters, and the existing live-fish veto remains. The fish-meat graphic uses the generic vanilla `Things/Item/Resource/MeatFoodRaw/Meat_Small` path rather than VFE's cod graphic. Food provenance is attached to relevant output Things, not every ingestible Def.
- Runtime graphics, stat, rotting, deterioration, and integration hooks remain guarded by runtime-fish or relevant-component checks. No storage, trade, or unrelated recipe Def is changed.

AQ-DEP-04B removed static VFE category/graphic references from core Def XML. It did **not** make Vanilla Fishing Expanded optional: required mod metadata remains unchanged, and an actual RimWorld 1.6 load with and without VFE must verify category resolution, the vanilla graphic, the processing bill's allowed fish and refusal of live/non-fish ingredients, original VFE species behavior, and existing saved bills before dependency removal or player release.

AQ-DEP-04C1 adds a single first-party freshwater prototype, `AF_PondMinnow`, tagged with Aquaculture's `AquaticSpeciesExtension` and a separate `AF_NativeFish` storage category. It is recognized without a VFE category, but uses a **provisional generic-meat graphic** (tinted RimWorld small raw-meat resource) in lieu of dedicated fish artwork. The separate `AF_RecognizedFishIngredient` recipe category still starts empty; runtime population and the existing live-fish veto remain the only intended eligibility path. This is not production-finished art or gameplay qualification. AQ-DEP-04D1 now adds a native fishing job prototype for that missing-provider situation, but a fish ThingDef by itself still does not qualify a game-safe fishing loop. RimTest must independently verify Def loading, fish/pond behaviors, vanilla graphic resolution, processing bills and live-fish rejection, fresh-colony fishing, and saved-game add/remove/reload before removing VFE or publishing this content.

AQ-DEP-04D1 adds a **native fishing job prototype** with a Handling WorkGiver and JobDef that are inactive whenever the VFE or Odyssey fishing packages are active. With neither active, it scans only near a player's undrafted angler, requires a reachable shore adjacent to natural water with an existing prepared and rank-eligible fish population, and uses the existing native rod, cast/wait/reel, catch/escape, fish population, knowledge and exactly-once ledger. The attempt is cancelled on interrupted jobs, and catches count only after the fish is placed. This has only portable source-level and rule tests, **not a qualified standalone fishing release**. RimTest must still build the actual RimWorld 1.6 DLL, exercise automatic job discovery and rod acquisition (equipped/inventory/storage), successful and unsuccessful catch/placement, job interruption, provider presence/absence, gameplay, and save/reload, including native fish Def and graphic resolution. The current VFE metadata requirement stays in place.

AQ-DEP-04E1 keeps native fishing work offers read-only: the WorkGiver only checks whether an equipped, inventoried, or approved reachable stored rod is available. It no longer creates or replaces serialized fishing-rod sessions while considering candidate jobs. Rod selection, reservation, equipment swapping and restoration still occur only in the accepted job's original workflow. A rod being carried during acquisition does not suppress the active fishing job's water/species safety checks. This is portable source-contract validation only; an actual RimWorld 1.6 compile and RimTest workgiver, acquisition, interruption and save/reload run are still mandatory.

AQ-DEP-04F1 extends the existing developer-only `AquacultureBridgeTestSuite.RunBaseline` with read-only checks for the native fishing JobDef/WorkGiverDef links, the extension-marked `AF_PondMinnow` runtime registration, and provider gating with VFE/Odyssey active or absent. The existing authenticated RimBridge entry point `aquaculture/run_baseline` can execute these assertions only after its owner establishes a real game generation; the test itself neither spawns Things nor changes jobs, rod sessions, populations or saves. The RimTest catalog now selects native fishing source and Def changes for the existing `aquaculture-development-smoke` recipe and marks it as the artifact-freshness anchor. **That recipe checks quicktest readiness and does not execute the separate baseline assertions automatically.** A developer-owned RimTest build/load transaction and explicit in-game baseline invocation must both report real evidence before asserting native provider runtime compatibility. Portable source/catalog checks alone are not RimWorld build, gameplay, graphics, save/reload, or release qualification.

AQ-DEP-05A1 introduces the Aquaculture-owned `IAquacultureJournalWorkspace` boundary for the Journal main window. The window now obtains its renderer through a factory in the existing Insight Canvas document source, with exactly the same first-draw, species/page selection, expertise-tab closure, drawing and cleanup calls. This is an additive source boundary only: the standard player build **still requires Insight Canvas** for the current renderer and the other UI documents. No native replacement provider is yet implemented or selected; no dependencies, saved keys, layout preferences or gameplay rules have been changed. Real RimWorld 1.6 compilation, visuals, navigation, closures and persistence must be qualified through RimTest before an optional Insight Canvas mode or player release.

AQ-DEP-05A2 introduces the Aquaculture-owned `IAquacultureSettingsView` interface for the mod settings owner. `AquacultureMod` now creates the default settings renderer through `AquacultureSettingsViewFactory` in the existing Insight Canvas document source. The default document, catalog snapshots, theme, first-use caching and eight-pixel window inset are unchanged. When settings are written, the renderer preserves the original order of `Host.PostClose()` followed by `Document.Invalidate()`, after mask, visual and pond-menu invalidation and before the base settings write. This remains a boundary only: the current player build **still requires Insight Canvas** and retains every existing metadata dependency. No native settings renderer is implemented, and no settings/save schema, gameplay or player assembly output is changed. Only a future separately qualified native implementation and real RimWorld 1.6 build, settings UI interactions, save/reload and RimTest validation could justify removing Insight Canvas from the player build.

AQ-DEP-05A3 isolates the pond stocking planner's native RimWorld dialog from the current Insight Canvas document through the Aquaculture-owned `IAquacultureStockingPlannerView` interface. `Dialog_PondStockingPlanner` retains its window flags, blueprint and colony-visible species selection, forecast cache, unavailable-pond guard, save callbacks, draw and PostClose lifecycle. A factory in the existing Canvas document source still constructs the same renderer after blueprint initialization, retaining snapshot refresh, theme, input bindings and host cleanup. This is a source boundary only: the default player package **still requires Insight Canvas**, and no native stocking planner renderer, optional Canvas build, new saved state or gameplay behavior is introduced. RimWorld 1.6 compilation, planner interactions/forecasts, cleanup and save/reload need separately authorized RimTest validation before optional rendering or a standalone release.

AQ-DEP-05A4 isolates the native breed-registration dialog from its concrete Insight Canvas document through the Aquaculture-owned `IAquacultureBreedRegistrationView` two-method interface. `Dialog_RegisterFishBreed` still owns breed-name input authority, duplicate/name/cohort validation, Enter/KeypadEnter acceptance, cancel behavior, and registration/close lifecycle. Its existing window flags, first construction timing, UI snapshots, labels, and save behavior are unchanged; the factory in the Canvas source still constructs the original document with theme, enabled-state refresh and host cleanup. This is source-only isolation: the default build **still requires Insight Canvas**, and there is no native breed-registration renderer or optional UI build. Real RimWorld 1.6 compilation, breed registration keyboard/mouse/validation interactions, save/reload, and RimTest are independently required before changing dependencies or releasing standalone.

AQ-DEP-05A5 isolates the native commission-delivery dialog's renderer ownership through the Aquaculture-owned `IAquacultureCommissionDeliveryView` interface. `Dialog_DeliverAquacultureCommission` still owns pause/close/window flags, reads only eligible spawned specimens into the existing copied snapshot, and delegates actual delivery exclusively to `AquacultureCommissionComponent.TryDeliver`. The default factory in the existing Insight Canvas source constructs the unchanged document, preserving snapshot refresh before drawing, virtualized specimen rows, success/failure toasts, close callbacks and host cleanup. This is an additive source boundary only: the default player build **still requires Insight Canvas**, and the renderer-neutral snapshot type still resides in the current UI source; no native commission renderer, optional UI build, new saved state or gameplay behavior is introduced. A separately authorized RimWorld 1.6 compile and RimTest coverage of commission delivery, UI interactions, save/reload and absence of Insight Canvas are necessary before relaxing dependencies or releasing standalone.

AQ-DEP-05A6 isolates the fish trait tab's default Insight Canvas dossier behind Aquaculture-owned `IAquacultureFishDossierView` with only draw and cleanup operations. The native `ITab_FishTraits` still owns visibility, selected-fish lookup, twelve-pixel layout inset, first-use construction, the existing fallback rendering, and close notification when no fish is selected. The Canvas-only factory still constructs the same dossier, preserving snapshot capture before drawing, health/trait presentation, Knowledge-based disclosure, selection-change cleanup, and document host lifecycle. This is source-level separation only: the default build **still requires Insight Canvas**, and no native fish dossier, optional renderer switch, save schema, gameplay or player assembly change has been made. Real RimWorld 1.6 compilation, fish selection, disclosure, UI visuals, save/reload and RimTest are required before any player-facing dependency relaxation or standalone release.

AQ-DEP-05B1 introduces `NativeAquacultureFishDossierView` in the Canvas-free fish trait tab source as the first separately instantiable native RimWorld Widgets dossier prototype. It implements `IAquacultureFishDossierView`, resets scroll when selection changes or closes, and presents read-only identity, health, breed, production, trait labels and knowledge with the same `AquacultureUiDisclosure.ForFish` and learned Knowledge facet permissions as the existing document. Visible traits expose labels only, never numeric effects, and the Journal link is restricted to known species. The default `AquacultureFishDossierViewFactory.Create()` **still chooses the Insight Canvas document**, so the standard player build **still requires Insight Canvas**. Other UI owners lack native renderers; no optional build switch or dependency metadata is changed. Actual RimWorld 1.6 compilation, native selection and scrolling, unknown-facet disclosure, save/reload and RimTest verification remain necessary before a player-facing standalone renderer.

AQ-DEP-05B2 adds `NativeAquacultureBreedRegistrationView` in the Canvas-free Aquaculture Journal source as a second separately instantiable native RimWorld Widgets renderer prototype. The renderer offers translated breed-name entry, live validation messages, a disabled-until-valid registration button, cancel, scrollable content and PostClose cleanup; it delegates name changes, duplicate/cohort checks, accept, cancel and Return/KeypadEnter behavior to the unchanged `Dialog_RegisterFishBreed` owner. The default `AquacultureBreedRegistrationViewFactory.Create()` **still chooses the Insight Canvas document**, so the player build **still requires Insight Canvas**. This source-only prototype does not change registration rules, save data, other interfaces, metadata, build references or runtime provider selection. A real RimWorld 1.6 build plus RimTest coverage of mouse/keyboard input, invalid/duplicate names, empty cohorts, cancellation, successful registration, scroll and save/reload remain required before enabling a native player renderer or relaxing mandatory dependencies.

AQ-DEP-05B3 moves the unchanged `AquacultureCommissionDeliverySnapshot` data contract from the Insight Canvas document source into Canvas-free `Source/AquacultureCommissions.cs` and adds `NativeAquacultureCommissionDeliveryView` as a third separately instantiable native RimWorld Widgets prototype. The view captures a fresh owner-controlled, spawned-only, ID-sorted specimen list on each draw, displays localized requirements, preview, count, rules and per-specimen details in a scrollable panel, and sends Deliver clicks only through `Dialog_DeliverAquacultureCommission.TryDeliverForUi`. The owner remains solely responsible for eligibility, rewards and consumption; the native view reports success/failure with native messages, closes on success, and clears its scroll state on close. The default `AquacultureCommissionDeliveryViewFactory.Create()` **still selects the original Insight Canvas document** with its existing toasts and host lifecycle, so the standard player build **still requires Insight Canvas**. No provider-selection switch, save schema, commission gameplay rule, build reference or required mod metadata changes in this source-only milestone. The Journal, settings and stocking planner still require native alternatives. Real RimWorld 1.6 compilation, native UI behavior, changing eligibility, success/failure and interrupted-delivery gameplay, save/reload, and RimTest remain required before optionalizing dependencies or releasing standalone.

AQ-DEP-05B4 adds `NativeAquacultureStockingPlannerView` to the Canvas-free pond stocking planner owner as a fourth separately instantiable native RimWorld Widgets prototype. It renders a search-filtered catalog of colony-known species, editable planned counts, a freshwater/saltwater/brackishwater menu, current/blueprint/clear/save actions, and the existing forecast's capacity, food, risk and warning information in a scrollable native layout. Species visibility still comes exclusively from `Dialog_PondStockingPlanner.SpeciesCatalog`; planned stock and forecasts come from `PlanEntries` and `ForecastSnapshot`. All player changes route through the owner's existing methods, preserving pond/blueprint authority, knowledge gating, cached forecast calculations, and saved data. Search and scrolling are transient and cleared on close. The default `AquacultureStockingPlannerViewFactory.Create()` **still selects the original Insight Canvas document**, so the player build **still requires Insight Canvas**. No optional-renderer switch, mandatory dependency metadata, build references, save schema, gameplay or release output changes. The Journal workspace and mod settings still need independent native implementations. Real RimWorld 1.6 compilation, native search and controls, water menu, forecast updates, blueprint persistence, save/reload, and RimTest qualification remain required before enabling this renderer or shipping without Insight Canvas.

## Save And Removal

Active collector commissions are additive game-component state. They store requirements, improvement kind, deadline, reward, and request-history keys and load without Royalty, Ideology, or another DLC. Completed and expired requests increment additive counters on the saved breed record. Valid active records preserve every existing requirement across save/load; malformed records with missing breed/species/trait references, invalid deadlines, or no reward are discarded while request history remains intact. The transient fish index is rebuilt after load and is never serialized.

Food provenance is attached to relevant recipe output Things rather than every ingestible Def. During loading of an existing save, the old food component is temporarily available on ingestible Defs so serialized data can deserialize, then those temporary Def entries are removed. Ordinary vanilla/modded food, storage, trade, and unrelated recipes are not modified.

Pond state, natural connected-water populations, fish traits, life-cycle state, eggs, schools, management policies, containers, feeders, masks, fishing attempts, species knowledge, fishing expertise, installed rod tackle, pending rod upgrades, fish-food provenance, and the aggregate pond-swimming effect are serialized. New natural-population records and lifecycle fields are additive. Existing Odyssey scalar populations and VFE zone species seed first initialization; later framework queries cannot silently reintroduce extinct species. Pond, lake, and marsh records are closed; river, coastal, and ocean records use bounded same-category migration. Closed-water zero-population tombstones persist, while open-water reintroduction occurs only through bounded migration or an explicit compatible stocking call. Numerical excess is reduced gradually, so loading an old save does not abruptly delete fish populations. The constructed-pond capacity model uses physical space, approximate sustainable support, industrial support, and management limits as separate derived values. Existing `fishCapacityPerCell` settings are preserved; older saves receive a one-time notice about the lower 0.75 default, and no fish are mass-removed during the transition. Missing natural records initialize deterministically from world seed, map identity, connected-water anchor, and loaded defs. New traits remain ordinary additive fish trait names and use the existing egg/offspring inheritance records. Food and pawns lacking new comp or hediff data use neutral defaults. Fishing progression uses additive save nodes and fish `defName` keys, so existing saves load with empty progression while newly added fish packs receive default Novice requirements. Existing manually equipped rods continue to work. New fishing jobs can reserve a suitable rod from direct inventory or valid map storage; temporary equipment sessions restore the previous primary weapon after normal completion, interruption, danger, drafting, cancellation, despawn, and save/load recovery. Removing a fish content mod from an active save can still invalidate that mod's pond fish defs and is not supported.
## Trait Breeding Compatibility

The trait model is additive. Existing `traitDefNames`, numeric `traitValues`, breed IDs, generations, parent IDs, eggs, and journal records remain valid. New fields only describe the qualifying registered-breed attempt and its evidence; missing fields receive safe legacy values during post-load migration.

The settings migration keeps the old `globalMutationRate` as the initial `wildExceptionalTraitChance` and `maxMutations` as the initial `maxInheritedTraits`. New settings use these safe defaults when no old value exists: 20% wild exceptional-trait chance, 55% per-parental-trait inheritance chance, two maximum inherited traits, 5% new offspring-mutation chance, one maximum new offspring mutation, and a 95% registered-breed defining-trait reliability ceiling. The old keys remain serialized for one-way compatibility.

Natural-water recovery has one authoritative rule: an extinct or below-floor species can receive regional recovery only when a positive recorded source population for the same species exists in a compatible open-water category. Rivers connect only to rivers, coasts only to coasts, and oceans only to oceans; ponds, lakes, and marshes are closed. Migration pressure and species weighting affect the rate only after this source predicate succeeds and never create a species from nowhere. Population summaries and prepared fishing views are invalidated after catches, stocking, births, mortality, migration, topology changes, source extinction, and save/load, then rebuilt from the shared cache.

Generated wild fish and temporary fish used for caught-fish records roll wild exceptional traits only. Caught fish retain the generated names and numeric values captured by the fishing attempt. Ordinary offspring evaluate every eligible trait in the parental union independently; a trait present in both parents has a 1.5x chance, incompatible successes are reduced to one per compatibility group, and the inherited cap is applied after all rolls. New offspring mutations use a separate event roll and exclude inherited names, duplicates, occupied groups, unknown defs, disabled defs, and non-mutation-eligible traits.

Registered-breed parents add a qualifying birth record. Each defining trait is then rolled separately at `resulting stability * registered-breed reliability ceiling`; a failed roll produces an ordinary offspring while retaining the qualifying attempt for stability evidence. Thus even a low-stability breed never guarantees every defining trait. Numeric values inherited from a parent remain compatible with the existing trait-value serialization.

Stability is `clamp(0.50 + success rate * 0.40 + generation contribution, 0.50, 0.98)`, where success rate is matching births divided by total qualifying births and generation contribution is `min((highest generation - 1) * 0.02, 0.10)`. Non-qualifying crosses do not change a breed record. Legacy breed records use their saved `births` as the qualifying-birth denominator and retain their prior computed stability as a floor, avoiding retroactive loss when failed-birth totals were not recorded. Unknown legacy child records are not counted a second time.
## Knowledge Framework Integration Contract

Aquaculture's optional runtime integration targets package `lan.knowledgeframework`. The supported baseline is semantic release `3.0.0-beta.1` or later with integer `ApiVersion >= 3`; the semantic release string is not used as an API generation. The adapter also requires `CapabilityVersion >= 3` and `Supports(3, capability)` for every V3 capability it calls: domains, pawn/colony knowledge, expertise, domain UI, evidence transactions, facets, confidence, discovery stages, relationships, claims, typed measurements, subject archetypes, requirement stages, observation recipes, contextual knowledge, milestones, structural relations, subject lifecycle, accrual policies, claim staleness, and transmission. If this contract is unavailable, Aquaculture retains its existing legacy expertise fallback and does not call partial V3 APIs.

Stable IDs are part of the save contract: domain `aquaculture`, global subject `aquaculture.global`, fish/breed/individual/water/pond/angler archetypes, context types `global`, `biome`, `map_region`, `water_body`, `fishing_cell`, `managed_pond`, and `pond_type`, and the existing facet, claim, observation, milestone, and relation IDs. Context identities include map, anchor/proxy, water kind, and the stable topology marker; context fallback is explicit parent-then-global rather than an implicit current-map lookup. V3 stages use `Balanced` aggregation. The additive migration marker `aquaculture.knowledge.balanced-stage` version 1 is committed only after the framework accepts and durably records it; it does not delete or reset existing knowledge evidence.

Registration is deterministic and retry-safe. Domain, context, relation, provider, and UI registration must all succeed before the adapter is marked ready; a partial or rejected phase leaves it retryable. Logical Aquaculture events carry stable IDs derived from their source attempt/lifecycle identity, and duplicate IDs are ignored while failed dispatches are removed for retry. Personal and colony observations from one logical event share one Knowledge Framework transaction; derived catch surveys do not accrue a second event.

Legacy progression migration uses consumer IDs `aquaculture.legacy.fishing-progress/...`, version 1, and the existing global consumer. Every import result and the durable `IsCommitted` state must succeed before migration is considered complete. Invalid Defs, non-finite values, missing subjects, or partial imports leave legacy data intact for a later retry. Dynamic framework subjects are bounded to 2,048 and the adapter uses the fish registry for individual resolution instead of repeated whole-map scans; rejected registrations emit one bounded diagnostic. Framework diagnostics and the Aquaculture bridge are the supported inspection paths for registration, migration, event, context, and retention counts.
## Deferred Reality Integration

The natural-water provider contract, authority boundary, stable IDs, migration,
exactly-once operations, active/latent reconciliation, and DRF build/manifest
workflow are documented in `About/DEFERRED_REALITY_INTEGRATION.md`.
