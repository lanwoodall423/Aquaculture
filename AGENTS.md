# Content Mod Development

This repository is a RimWorld content mod.

## Platform target: RimWorld 1.7 only

Starting 2026-10-30, all **new source changes, build candidate acceptance,
runtime validation and player packaging** target **RimWorld 1.7** (Waylith)
only. Version 1.7 was announced on the unstable beta branch; no 1.6 game
binary or older-save compatibility qualification is required. Existing 1.6
paths and release evidence are historical/unported, not an endorsement of a
1.6 development target. No Aquaculture 1.7 build/load pass is established
until the exact 1.7 candidate is tested.

The 1.7 port must align the main mod and developer/add-on projects with
`net10.0`, game-root `Assembly-CSharp.dll` / `Waylith.dll` /
`UnityStubs.dll` references and a compatible Harmony 3. The supported
version, content folder, load-folder mapping, dependency assemblies,
RimTest deployment target and package assertions must switch as one
coherent reviewed candidate before a player-facing 1.7 support claim.
Do not accidentally run a RimWorld 1.6 installation, compile against
1.6 managed assemblies or count a 1.6 DLL load as a 1.7 result.

For source changes, RimTest owns build, generation, local deployment,
RimWorld lifecycle, affected-test selection, and runtime validation.

For ordinary XML/C# development use the installed RimLiaison/RimTest
entrypoint against the exact candidate workspace:

rimtest dev-check <workspace> --json

A RUNTIME_LOAD_PASS establishes an isolated content-load result, not live
pond gameplay or release readiness. BUILD, STATIC, INFRASTRUCTURE, and CLEANUP
must also pass. Inspect the JSON stage statuses and preserve candidate identity.

For explicitly requested in-game smoke, gameplay, UI, or release qualification,
use the owner-managed RimLiaison workflow, its .rimdev/stack.json and
TestCatalog/rimtest.catalog.json, and the bounded diagnostics/lease handoff
reported by RimLiaison. A direct dev-check does not replace live gameplay tests.

Do not manually copy assemblies into the live mod directory, manually
substitute RimWorld launches for RimTest lifecycle operations, or call
RimContext/DevBridge2 directly when RimTest owns the workflow.

Artifact freshness must be proven before runtime results are accepted.