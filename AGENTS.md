# Aquaculture Fishing

- Package ID: `lan.aquaculture.fishing`.
- DevBridge2 is the only supported live-test coordinator. Use `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd` for status, leases, restart, and readiness.
- Mod-owned test source: `Source/AquacultureInGameTests.cs`; coordinator harness: `DevTools\Run-AquacultureInGameTests.ps1`.
- DevBridge2 does not currently expose an adapter-registration protocol. The old standalone adapter source is retained only as historical development code and is not a release input.
- Gameplay, defs, Harmony, serialized types, or core changes require a full DevBridge2 restart; request/result-only diagnostics do not.
- DevBridge2 remains optional and must never be a player dependency.
- Full workflow: `DevTools/DEVBRIDGE2_AGENT.md`.
