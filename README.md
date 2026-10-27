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
