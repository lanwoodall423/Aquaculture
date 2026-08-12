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
- Prompt 1 migrates settings only. Journal, pond, fish-traits, stocking planner, commission, rod, Knowledge browsing, and Deferred Reality UI migrations remain intentionally incomplete.
- Insight Canvas owner-license selection is unresolved and remains a release blocker.
