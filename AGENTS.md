# Content Mod Development

This repository is a RimWorld content mod.

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