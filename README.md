# Aquaculture - Fishing

Aquaculture is being developed **exclusively for RimWorld 1.7** (Waylith engine).
We are not maintaining or testing a RimWorld 1.6 release. The current 1.7
Waylith development target is still unqualified: **Aquaculture has not yet
passed its 1.7 build/load or gameplay gates**. About.xml, LoadFolders.xml
and the developer package paths now target 1.7 exclusively. Tracked 1.6
Defs/Languages/Textures remain temporary build-time source seeds, while
historical prebuilt 1.6 DLLs are excluded from the runtime and ZIP. Do not
publish a 1.7 player package until fresh compilation, dependency and
owner-managed runtime tests pass.

## Gameplay

- Individual fish with inheritable visual and behavioral traits, growth,
  variegation, and species-specific schooling.
- Managed ponds with stocking, feeding, aeration, ecology, natural fish
  populations, hunger, predation, and freshwater/saltwater/brackish conditions.
- Fishing rods and tackle, live-fish containers, fish processing, and
  commission and research progression.
- Colony-facing fish, pond, and Journal presentations, with Knowledge Framework,
  Deferred Reality Framework, and Insight Canvas integration.

These features describe authored mod content, not a completed live-game
acceptance test.

## Current development roadmap (2026-10-30)

**Priority: port to RimWorld 1.7 and prove a useful 1.7 dependency-backed
alpha before removing external dependencies.** This is a delivery sequence, not a claim of
runtime qualification. Aquaculture's current player build continues to require
the mods listed below. The detailed [Alpha 0 gate](About/TEST_PLAN.md#playable-alpha-0-acceptance-gate)
and the historical [release evidence ledger](About/RELEASE_READINESS_TEST_MATRIX.md)
remain separate: historical PASS results do not transfer to a new candidate.

| Priority / target | Current state | Exit gate |
| --- | --- | --- |
| P0 - **1.7-only port + Playable Alpha 0**, current dependencies | **Next; 1.7 build/load and live qualification NOT RUN** | Replace the 1.6 build/layout with 1.7 Waylith/.NET 10/Harmony 3 references and content, then require RimTest dev-check BUILD/STATIC/RUNTIME_LOAD/INFRASTRUCTURE/CLEANUP and fresh exact artifact hashes. On disposable 1.7 test colonies verify pond, fishing/stocking, feeding, breeding/traits, harvesting/processing, usable Journal/settings, and same-version save/reload. |
| P1 - Native Journal parity | **In progress** | Native Overview and Species prototype integrated (AQ-DEP-05B6B1); complete Ponds and Breeds, then Conservation and Commissions with the current knowledge/research disclosure and owner gameplay rules. Test scrolling, selection and navigation in RimWorld. |
| P2 - Native provider/build separation | **Not started** | Allow an exact built configuration without Insight Canvas or Knowledge Framework compile/runtime references; keep enhanced integrations optional. Prove real compile, startup and UI parity for each switch. |
| P3 - First-party independent gameplay/content | **Partially developed** | Qualify native fishing/rod lifecycle, fallback natural-water and knowledge behavior, optional Deferred Reality and VFE integration, and a small balanced first-party freshwater/saltwater/brackishwater fish catalog. Audit Harmony and replace any remaining mandatory external-mod hook if zero external mod requirements is the goal. |
| P4 - Standalone 1.7 player alpha and release qualification | **Blocked on P0-P3** | Remove only proven-unnecessary About.xml dependencies and build references; eliminate legacy provider binaries; validate a clean 1.7 player package, 1.7 save/reload, long-running ecology, UI/accessibility and dependency-absent/present variants. No 1.6 game or save compatibility gate. |

**Near-term definition of usefulness:** a player can create a pond, catch or
stock fish, keep them healthy, breed and process them, inspect the result and
save/reload without major failures. We can reach that test gate *before*
standalone work is complete; no calendar delivery promise is supported yet.

**Current critical blockers:** no fresh exact-head RimTest build/load and
disposable-colony gameplay/save evidence has been recorded for this roadmap
candidate; the main build and manifest still depend on external frameworks;
native Journal parity is incomplete; standalone first-party species coverage is
limited. The repository already has a RimTest catalog and in-game tests. Use
those through the owner-managed workflow before adding more test harnesses.

**Development rule:** every substantial code milestone requires portable
checks, then an owner-managed candidate-exact build/load check; gameplay, UI,
persistence and release gates are separate and must not be reported as passed
until executed. ProjectNexus review/acceptance stays required before integration.
Keep the dependency-backed **1.7** build as the comparison until the native
standalone **1.7** build is proven; never maintain a 1.6 build variant.

## RimWorld 1.7-only migration prerequisites

**Decision (2026-10-30):** no older RimWorld version is a supported runtime,
packaging target, or required test matrix. Past 1.6 evidence remains dated
history, not 1.7 proof; old-save import from 1.6 is not a release gate.
The initial 1.7 playtest may still require the current dependency mods, and
the later 1.7 standalone target can remove them independently.

1. **Generate a coherent 1.7 content package (source contract completed;
   runtime NOT RUN).** `About.xml` now declares **1.7 only** and
   `LoadFolders.xml` selects `v1.7`. A successful ordinary Release build
   copies the tracked 1.6 XML/PNG source seeds into generated `1.7/`
   content, and the strict package verifier excludes historical 1.6 DLLs.
   The generated content, actual 1.7 core DLL and clean player ZIP still
   need owner-managed build and runtime proof.
2. **Verify owned C# outputs (source contract completed; build NOT RUN).**
   Main mod, optional DRF provider and developer bridge target `net10.0`.
   Reference RimWorld 1.7 `Assembly-CSharp.dll`, `Waylith.dll` and
   `UnityStubs.dll` from the installed game root. Require Harmony 3 and
   compatible 1.7 framework builds, then audit injected Harmony parameter
   types and compiled graphics/asset compatibility.
3. **Pin compatible integration versions.** Verify 1.7 versions or
   isolated replacements for Insight Canvas, Knowledge Framework, Deferred
   Reality Framework and Vanilla Fishing Expanded. A dependency-backed
   Alpha 0 requires compatible installed dependencies; optionalization
   comes later and must be independently tested.
4. **Verify developer workflow and package assertions.** The RimTest
   stack and player-package tools now select 1.7-only paths and reject
   mismatched staged content or old/duplicate binaries. These source-only
   changes need a clean actual 1.7 package and owner-managed verification;
   never run game-changing operations outside RimLiaison/RimTest.
5. **Qualify the 1.7 Waylith runtime.** Against an isolated 1.7 beta
   installation, require exact-candidate BUILD/STATIC/RUNTIME_LOAD/
   INFRASTRUCTURE/CLEANUP results, followed by live gameplay, Journal,
   settings, same-version save/reload and package tests. Mark unrun stages
   **NOT RUN**, never PASS, even if older DLLs appear loadable.

Until steps 1–5 are verified, the repository is **1.7-only in source
contracts but not 1.7 runtime-qualified**. The version metadata is now 1.7;
that is not evidence of a working 1.7 mod or permission to release. Background:
[Ludeon's 1.7 announcement](https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/)
and the [1.7 modding update notes](https://www.rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates).

## Dependencies and compatibility

`About/About.xml` declares these mod dependencies:

- Harmony (`brrainz.harmony`)
- Insight Canvas (`lan.insightcanvas`)
- Knowledge Framework (`lan.knowledgeframework`)
- Deferred Reality Framework (`lan.deferredreality.framework`)
- Vanilla Fishing Expanded (`VanillaExpanded.VCEF`)

Use the normal RimWorld mod-management flow and check the individual dependency
projects for their supported versions. This repository does not bundle their
assemblies as substitute dependencies. `LoadFolders.xml` selects only 1.7
runtime folders; the tracked `1.6/` XML/PNG assets are source seeds copied by
the Release build to generated 1.7 content. Old prebuilt DLLs are excluded.
Neither source migration nor metadata alone qualifies 1.7 runtime support.

## Repository map

| Path | Purpose |
| --- | --- |
| `About/About.xml` | Player-facing metadata, dependencies, supported version |
| `1.6/` | Legacy content and assembly layout awaiting 1.7-only rehome; **not supported as a 1.6 target** |
| `Source/` | Mod implementation, UI, and framework integration |
| `DevTools/` | Portable rules and UI-contract test projects |
| `.rimdev/stack.json` | RimLiaison-owned project and build contract |
| `.rimdev/recipes/` | Project-owned bounded runtime smoke recipe |
| `TestCatalog/rimtest.catalog.json` | RimTest test selection and smoke catalog |

## Developer checks

With the .NET 8 SDK available, these **portable** development tests can be run
from the repository root without launching RimWorld:

```powershell
dotnet run --project DevTools/TraitBreedingRules.Tests.csproj --configuration Release --nologo
dotnet run --project DevTools/InsightCanvasUiTests.csproj --configuration Release --nologo
```

For authorized isolated mod build/content-load validation, follow
[`AGENTS.md`](AGENTS.md) and use the installed owner-managed RimLiaison/RimTest
entry point against the **exact candidate workspace**:

```text
rimtest dev-check <workspace> --json
```

A valid dev-check must independently report passing BUILD, STATIC,
RUNTIME_LOAD, INFRASTRUCTURE, and CLEANUP stages with matching candidate and
artifact-freshness evidence. Do not manually install developer assemblies,
launch/kill RimWorld, or substitute a direct bridge call for the owner workflow.

Passing portable tests does **not** establish live pond gameplay, game UI
accessibility, save/reload compatibility, framework failure recovery, clean
player installation, or release readiness.

## Release evidence and status

The following are **dated evidence ledgers**, not automatic proof for the
current source HEAD:

- [Release readiness test matrix](About/RELEASE_READINESS_TEST_MATRIX.md)
- [Release-candidate status and historical validation](About/Validation/RC_STATUS.md)
- [Insight Canvas UI architecture](About/INSIGHT_CANVAS_UI_ARCHITECTURE.md)

The historical release decision is **BLOCKED** until the unrun live gameplay,
persistence, cross-framework, visual/accessibility, clean-install, and other
required acceptance gates are independently completed for the exact candidate.

## License

Original Aquaculture source is licensed under
[GPL-3.0-or-later](LICENSE), except where individual files state otherwise.
Dependencies and game-owned assets retain their own licenses.
