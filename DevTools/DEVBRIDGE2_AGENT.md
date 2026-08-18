# Aquaculture live testing with DevBridge2 and RimBridgeServer

DevBridge2 is the only supported lifecycle and lease coordinator. RimBridgeServer
is a separate optional RimWorld mod that provides the authenticated live-game
tool endpoint. Aquaculture's developer companion is discovered by RimBridgeServer
from the global sibling BridgeTools folder.

## Preconditions

1. Build the developer AquacultureFishing assembly and companion:
   `dotnet build Source/AquacultureFishing.csproj -c Release /p:AquacultureDeveloperTests=true`.
   Set `RimWorldDir`, `HarmonyPath`, `InsightCanvasDir`,
   `KnowledgeFrameworkAssemblyPath`, and `RimBridgeSdkAssemblyPath` as needed.
2. Confirm the companion exists at
   `<RimWorld>\\BridgeTools\\AquacultureFishing\\AquacultureFishing.BridgeTools.dll`.
3. Install and enable RimBridgeServer in the active RimWorld profile.

RimBridgeServer and its SDK are not player dependencies. Do not copy the SDK or
server assemblies into the companion bundle.

## Supported workflow

RimTest owns test selection and execution. From the AquacultureFishing
repository, use the local RimTest entrypoint:

```powershell
$rimTest = 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd'
& $rimTest doctor --json
& $rimTest affected --run --json
```

Do not call a project-owned in-game harness or write Runtime request/result
files. If RimTest returns an owner handoff, follow that command and then
return to RimTest for the next test run.

When RimTest hands off to an owner command, the underlying DevBridge2
operations are:

```powershell
$devBridge = 'C:\\Games\\Steam\\steamapps\\common\\RimWorld\\Mods\\DevBridge2\\DevBridge.cmd'
& $devBridge project resolve aquaculture --json
& $devBridge project register aquaculture
& $devBridge restart
& $devBridge wait-ready
& $devBridge test begin
& $devBridge bridge status --json
& $devBridge bridge policy --json
& $devBridge bridge tools --lease <lease-id> --json
& $devBridge bridge call aquaculture/test_status '{}' --lease <lease-id> --json
& $devBridge bridge call aquaculture/run_baseline '{"runId":"baseline-1"}' --lease <lease-id> --json
& $devBridge bridge call aquaculture/run_golden_path '{"runId":"golden-1"}' --lease <lease-id> --json
& $devBridge test end <lease-id>
& $devBridge project release <registration-id>
```

Every `--json` coordinator response is an envelope. For bridge commands, read
`rimBridgeRoute.success`, `rimBridgeRoute.errorCode`, and
`rimBridgeRoute.result`; tool discovery is in `rimBridgeRoute.result.tools`.
Do not treat the envelope itself as the companion tool payload.

## Ownership and restart rules

- DevBridge2 validates launch ID, generation, process identity, endpoint,
  profile/policy, companion evidence, and the current lease before forwarding.
- DevBridge2 remains lifecycle authority; routed tool calls cannot mutate
  lifecycle or mod order.
- Gameplay, defs, Harmony, serialized types, or core changes require a full
  DevBridge2 restart. Request arguments and read-only diagnostics do not.
- The companion suite runs on the RimWorld main thread. It owns transient
  fixture setup, assertions, simulation, and cleanup.
- The player build excludes `Source`, `DevTools`, the companion, and all test
  code. DevBridge2 and RimBridgeServer remain optional developer tooling.

## Troubleshooting

Use `bridge status --json`, `bridge policy --json`, and `bridge tools --json`
with the active lease. If the host is reachable but Aquaculture tools are
missing, inspect RimBridgeServer's `rimbridge/get_bridge_status` through
`bridge call` and check the deployed companion path and SDK/host version.

Do not revive the removed Runtime JSON request/result protocol or the former
standalone BridgeAdapter. Those paths are not release inputs.
