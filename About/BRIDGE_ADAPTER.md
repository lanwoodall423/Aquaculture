# DevBridge2 diagnostics

DevBridge2 is a development/testing dependency only. Its current coordinator
API owns process lifecycle, readiness, and test leases; it does not register or
execute standalone mod adapters.

Aquaculture's supported live diagnostics are owned by
`Source/AquacultureInGameTests.cs` and coordinated by
`DevTools/Run-AquacultureInGameTests.ps1`. Requests and results are atomic JSON
files under the DevBridge2 `Runtime` directory. The mod owns fixture setup,
assertions, simulation, cleanup, and result status.

Use:

```powershell
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd' status
& 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd' test begin
& .\DevTools\Run-AquacultureInGameTests.ps1 -DevBridgeRoot 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2' -Runs 2 -SkipRestart
```

`DevTools/BridgeAdapter/AquacultureBridgeAdapter.cs` and its project are kept
as historical source for reference only. They are not registered, published,
required by the gameplay assembly, or included in the player package. The
legacy build/validation entry points report `NOT RUN` rather than calling the
retired bridge publisher.
