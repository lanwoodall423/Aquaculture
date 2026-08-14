# Aquaculture Fishing

- Package ID: `lan.aquaculture.fishing`.
- DevBridge2 is the only supported live-test coordinator. Use `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd` for status, leases, restart, and readiness.
- Mod-owned test source: `Source/AquacultureInGameTests.cs`; coordinator harness: `DevTools\Run-AquacultureInGameTests.ps1`.
- DevBridge2 does not currently expose an adapter-registration protocol. The old standalone adapter source is retained only as historical development code and is not a release input.
- Gameplay, defs, Harmony, serialized types, or core changes require a full DevBridge2 restart; request/result-only diagnostics do not.
- DevBridge2 remains optional and must never be a player dependency.
- Full workflow: `DevTools/DEVBRIDGE2_AGENT.md`.
- Insight Canvas 2.x settings architecture: `About/INSIGHT_CANVAS_UI_ARCHITECTURE.md`.
- Portable UI contract tests: `DevTools\Test-InsightCanvasUi.ps1` (no RimWorld process required).
- Build the player assembly with configurable `RimWorldDir`, `HarmonyPath`, `InsightCanvasDir`, and `KnowledgeFrameworkAssemblyPath`; the installed Insight Canvas DLL is referenced with `Private=false` and is never packaged.
- Prompt 1 migrated settings. Prompt 2 adds the Journal-owned Insight Canvas workspace, hybrid fish dossier, responsive planner, and breed-registration document; native pond tabs, tiny FloatMenus/gizmos, rod UI, and canonical Knowledge browsing remain intentionally native or deferred.
- Aquaculture source/package licensing is `GPL-3.0-or-later`, Copyright (C) 2026 lanwoodall423; the installed Insight Canvas dependency separately declares GPLv3.0 in its own license file.
- The dedicated production-art requirement is waived by owner instruction for the current candidate; do not generate or alter production art, and do not treat the waiver as third-party relicensing.
- Prompt 3 closure evidence is tracked in `About/Validation/RC_STATUS.md`; older UI-release evidence is superseded and must not be relabeled as fresh-colony UX, accessibility, multi-map, save/load, performance, or clean-package evidence.
