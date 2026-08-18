# Aquaculture RimBridge companion

AquacultureFishing uses two separate bridge responsibilities:

- DevBridge2 owns RimWorld lifecycle, project/profile coordination, generation identity, readiness, test leases, and authenticated routing.
- RimBridgeServer owns the live in-game GABP service and discovers Aquaculture's companion tools.

The companion is developer-only. It is built from
`DevTools/RimBridgeTools/AquacultureFishing.BridgeTools.csproj`, references
only `RimBridgeServer.Sdk.dll` and the developer AquacultureFishing assembly,
and is deployed as:

```
<RimWorld>\\BridgeTools\\AquacultureFishing\\AquacultureFishing.BridgeTools.dll
```

No SDK or RimBridgeServer dependency is copied into that bundle. The player
package contains neither the companion nor the developer test suite.

## Tools

The companion exposes:

- `aquaculture/test_status` — confirms companion discovery.
- `aquaculture/run_baseline` — checks the current playable map and mod state.
- `aquaculture/run_golden_path` — creates, exercises, and cleans up the pond,
  breeding, inheritance, and processing fixture.

The test methods run on RimWorld's main thread. Optional paused-tick settling is
requested through RimBridgeServer's game clock; fixture setup and cleanup remain
owned by the Aquaculture suite.

## Build and run

Build the developer assembly and companion with:

```powershell
dotnet build Source/AquacultureFishing.csproj -c Release /p:AquacultureDeveloperTests=true /p:RimWorldDir='C:\\Games\\Steam\\steamapps\\common\\RimWorld' /p:HarmonyPath='...\\0Harmony.dll' /p:InsightCanvasDir='...\\InsightCanvas' /p:KnowledgeFrameworkAssemblyPath='...\\KnowledgeFramework.dll' /p:RimBridgeSdkAssemblyPath='...\\RimBridgeServer.Sdk.dll'
```

Then use the repository's RimTest entrypoint. RimTest selects the configured
DevBridge2 recipe, and DevBridge2 routes any approved companion calls through
RimBridgeServer while owning lifecycle, readiness, and leases. No project
script writes or polls mod-owned Runtime request/result files.

RimBridgeServer must be installed and enabled in the active RimWorld profile
before live discovery can succeed. If discovery fails, inspect
`bridge status --json`, `bridge policy --json`, and
`rimbridge/get_bridge_status` through the routed service.
