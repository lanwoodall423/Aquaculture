# Aquaculture - Fishing

Aquaculture is a **RimWorld 1.6** content mod focused on living fish, selective
breeding, sustainable ponds, and fishing. The tracked mod metadata declares
RimWorld 1.6 support; newer game versions are **not** claimed here.

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

**Priority: prove a useful, dependency-backed RimWorld 1.6 alpha before
removing external dependencies.** This is a delivery sequence, not a claim of
runtime qualification. Aquaculture's current player build continues to require
the mods listed below. The detailed [Alpha 0 gate](About/TEST_PLAN.md#playable-alpha-0-acceptance-gate)
and the historical [release evidence ledger](About/RELEASE_READINESS_TEST_MATRIX.md)
remain separate: historical PASS results do not transfer to a new candidate.

| Priority / target | Current state | Exit gate |
| --- | --- | --- |
| P0 - Playable Alpha 0, **current dependencies** | **Next; current-head live qualification NOT RUN** | RimTest dev-check proves BUILD, STATIC, RUNTIME_LOAD, INFRASTRUCTURE and CLEANUP with matching source and artifact hashes; in a disposable colony verify pond construction, fishing or stocking, feeding, breeding/traits, harvesting/processing, usable Journal/settings and save/reload. Record actual results, not inferred PASS. |
| P1 - Native Journal parity | **In progress** | Native Overview and Species prototype integrated (AQ-DEP-05B6B1); complete Ponds and Breeds, then Conservation and Commissions with the current knowledge/research disclosure and owner gameplay rules. Test scrolling, selection and navigation in RimWorld. |
| P2 - Native provider/build separation | **Not started** | Allow an exact built configuration without Insight Canvas or Knowledge Framework compile/runtime references; keep enhanced integrations optional. Prove real compile, startup and UI parity for each switch. |
| P3 - First-party independent gameplay/content | **Partially developed** | Qualify native fishing/rod lifecycle, fallback natural-water and knowledge behavior, optional Deferred Reality and VFE integration, and a small balanced first-party freshwater/saltwater/brackishwater fish catalog. Audit Harmony and replace any remaining mandatory external-mod hook if zero external mod requirements is the goal. |
| P4 - Standalone player alpha and release qualification | **Blocked on P0-P3** | Remove only proven-unnecessary About.xml dependencies and build references; eliminate legacy provider binaries; validate a clean player package, legacy save migration, live long-running ecology, UI/accessibility and regression variants with dependencies absent/present. |

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
Keep the compatible build as a comparison until the native build is proven.

## Dependencies and compatibility

`About/About.xml` declares these mod dependencies:

- Harmony (`brrainz.harmony`)
- Insight Canvas (`lan.insightcanvas`)
- Knowledge Framework (`lan.knowledgeframework`)
- Deferred Reality Framework (`lan.deferredreality.framework`)
- Vanilla Fishing Expanded (`VanillaExpanded.VCEF`)

Use the normal RimWorld mod-management flow and check the individual dependency
projects for their supported versions. This repository does not bundle their
assemblies as substitute dependencies. `LoadFolders.xml` currently selects
only the `1.6/` content directory.

## Repository map

| Path | Purpose |
| --- | --- |
| `About/About.xml` | Player-facing metadata, dependencies, supported version |
| `1.6/` | RimWorld definitions, language, and other versioned content |
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
