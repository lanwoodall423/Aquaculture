# Deferred Reality Integration

Aquaculture's Deferred Reality provider is an integration layer, not a second
aquaculture simulation.

## Authority

- Aquaculture owns active Things, ponds, active natural-water maps, player
  actions, Def interpretation, and the legacy natural-population projection.
- Deferred Reality Framework (DRF) owns latent aggregate natural-water
  populations, durable regions, population processes, topology links,
  exactly-once operation markers, and provider diagnostics.
- Knowledge Framework owns observations, uncertainty, expertise, and evidence.
- The provider never runs the Aquaculture map simulation and the legacy natural
  population tick is suppressed only after that map has been claimed and
  migrated by DRF.

## Provider Contract

- Provider ID: `lan.aquaculture.natural-water`.
- Provider semantic API contract: `2`.
- Provider save schema: `2`.
- Minimum DRF world save schema: `5`.
- Required DRF capabilities: regions, populations, processes, anchors, and
  diagnostics.
- Population operation markers are retained indefinitely. No cursor or
  compaction proof is claimed.
- Registration depends on `lan.deferredreality.framework`, is idempotent, and
  is deferred by DRF when registration is requested off the main thread.
- Incompatible or unavailable DRF leaves the legacy Aquaculture simulation in
  control. It does not partially suppress map simulation.

## Stable Identity

New provider IDs are length-prefixed and deterministic. Framework-owned region
IDs use DRF's canonical `rr1` serialization. Compatibility-preserved population
and process IDs retain the historical provider key shape so old provider saves
are upgraded in place. IDs are derived from the provider namespace, world tile,
water habitat, persisted water anchor, species DefName, process kind, migration
version, and logical event ID. IDs exist for:

- Regions and map aliases
- Water anchors and natural-water populations
- Demography processes
- Breeding-floor constraints
- Compatible topology links
- Migration markers
- Catch, stocking, demography, reconciliation, and transfer operations
- The `natural-water-population` exactly-once sequence domain

Durable identities do not use `GetHashCode`, object identity, dictionary order,
`Verse.Rand`, wall clock time, UI state, or a non-persisted counter. Runtime map
unique IDs are aliases used only to attach an active map to its stable region;
they are not components of the durable region identity.

## Population Mapping

| Aquaculture state | DRF state | Contract |
| --- | --- | --- |
| Connected water anchor | `RealityAnchorRecord` | Stable anchor, habitat, map region, and location |
| Fish DefName | Population subject | `<waterId>:fish:<defName>`; this preserves the historical DRF key shape; missing Defs remain in legacy state |
| Species population | `RealityPopulationRecord.amount` | Finite, nonnegative, capacity-bounded |
| Water carrying capacity | `carryingCapacity` | Nonnegative aggregate capacity |
| Breeding floor | Provider constraint payload | One stable constraint per population |
| Birth/death and recovery rates | Process payload | Fish-equivalent rates interpreted over DRF elapsed ticks; only the DRF process mutates latent aggregates |
| Process elapsed time | `RealityProcessExecution.elapsedTicks` | Bounded deterministic steps; no wall-clock or double elapsed simulation |
| Aggregate estimate confidence | `uncertainty` and observations | Estimate metadata only; it never becomes a second population mutation |
| Open-water category | Population migration flag/topology | River, Coastal, and Ocean only; same category only |
| Pond/Lake/Marsh | Closed population | No regional transfer or recovery |
| Legacy map record | Active-map projection | DRF is authoritative after successful migration |

Unknown fish DefNames are not deleted during migration. They remain in the
legacy record and are reported as skipped until the Def is available.

Active fish births, deaths, feeding, catches, and player stocking remain
Aquaculture-owned Things and events. DRF demography applies only to latent
aggregate records; active-map reconciliation projects one authoritative delta
and never replays the same birth or death as a second population operation.

## Exactly-Once Operations

Catch and stocking first update the legacy active map, then publish one logical
event. The provider reconciles the observed delta through
`RealityPopulationService.ReconcileActiveMap`. The event identity is reused on
retry, and duplicate operation IDs are successful no-ops.

Demography uses `ReproduceOrDie` exactly once per DRF process execution.
Regional recovery uses atomic `Transfer` and is allowed only when a positive
same-species source exists in a compatible open-water category. Closed water,
cross-category water, zero sources, and species mismatches are rejected before
the operation is attempted.

All operation markers remain durable until replay is impossible by an explicit
framework proof. No such proof is currently claimed. Knowledge observations
are consumers of the same logical event correlation; a Knowledge failure does
not roll back a committed population mutation.

## Map Reconciliation

Map finalization claims a stable DRF region and migrates initialized legacy
records. An active map that has not finished Aquaculture's own initialization
remains pending and is retried after initialization.
Map deinitialization unregisters only the active map; latent regions and
populations remain in DRF. When a map returns, the provider reuses the region,
reconciles active state, and projects DRF amounts back to the legacy summaries.
Provider failure pauses DRF processes and leaves Aquaculture's legacy state
available rather than running both simulations.

## Migration and Save Compatibility

Provider migration is versioned by DRF's provider marker and commits only after
all eligible map records, anchors, populations, processes, constraints, and
topology links validate. Interrupted migration retries; committed migrations
are idempotent. Existing DRF opaque records and unknown legacy DefNames are
preserved. A missing provider leaves persisted latent data opaque and paused;
restoring the provider resumes migration/reconciliation without creating a
second population record.

The complete cross-framework event, correlation, retry, save, and failure
ordering contract is in `About/FRAMEWORK_INTEGRATION_CONTRACT.md`. It is the
authority for the boundary between active Aquaculture mutations, latent DRF
operations, and Knowledge observations.

## Diagnostics

Provider diagnostics report compatibility, schema, active maps, populations,
processes, skipped Defs, processed events, duplicate events, retained marker
policy, and the last error. DRF diagnostics additionally report quarantines,
paused processes, pending migrations, operation records, and identity audit
failures.

RimTest-selected live recipes expose the bounded provider checks through the
developer companion and authenticated RimBridge route. There is no mod-owned
diagnostic request/result protocol; DevBridge2 remains the lifecycle/readiness/
lease owner and never becomes a player dependency.

## Build and Manifest

Build with:

```powershell
dotnet build Source\DeferredRealityProvider\DeferredReality.Aquaculture.csproj --configuration Release -p:HarmonyPath=<path-to-0Harmony.dll>
```

`DevTools/Build-DeferredRealityProvider.ps1` writes a deterministic manifest
beside the provider DLL containing the provider ID, source commit, SHA256,
semantic API contract, schema, capabilities, DRF save schema, and retention
policy. The manifest and DLL must be treated as one release artifact.
