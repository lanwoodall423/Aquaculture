# Aquaculture Fishing

- Package ID: `lan.aquaculture.fishing`.
- Adapter source: `DevTools/BridgeAdapter/AquacultureBridgeAdapter.cs`; package output: `DevTools/BridgeAdapters`.
- Build: `DevTools\Build-HotBridgeAdapter.ps1`; validate: `DevTools\Test-BridgeAdapter.ps1`.
- Query live Dev Bridge context with `DevTools\devbridge.ps1` from the Dev Bridge checkout before runtime tests.
- Adapter-only changes can reload the adapter; gameplay, defs, Harmony, serialized types, or core changes require a full restart.
- This integration and its adapter distribution are Aquaculture-controlled. Dev Bridge remains optional.
- Full workflow: `DevTools/DEVBRIDGE_AGENT.md`.
