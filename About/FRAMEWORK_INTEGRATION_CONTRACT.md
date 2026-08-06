# Framework Integration Contract

This document is the end-to-end contract for Aquaculture, Deferred Reality
Framework (DRF), and Knowledge Framework. It describes ownership and evidence;
it is not a second simulation specification.

## Ownership

- Aquaculture owns active Things, active ponds and maps, player actions, Def
  interpretation, active births/deaths/feeding/catches/stocking, and the
  legacy natural-population projection.
- DRF owns latent aggregate populations, stable regions and anchors, elapsed
  latent processes, compatible topology, exactly-once operation markers, and
  provider diagnostics.
- Knowledge Framework owns observations, uncertainty, expertise, evidence, and
  contextual presentation. Knowledge never mutates a population.
- A map is never advanced by both the Aquaculture natural tick and a DRF
  process. DRF suppresses the legacy tick only after the map is initialized,
  claimed, and migrated successfully.

## Correlation

The shared correlation ID is a length-prefixed deterministic ID derived from
event kind, stable source identity, event tick or serialized lifecycle tick,
stable map-parent identity, cell/anchor identity, and species or subject. It
does not use wall-clock time, `GetHashCode`, object identity, `Verse.Rand`,
dictionary order, or a nonpersisted counter.

Fishing attempts use pawn identity, serialized `startedTick`, stable map
identity, water cell, and fish DefName. Fish lifecycle events use the fish
identity and serialized birth identity. Stocking uses a persisted sequence in
the natural-population save record when no caller-supplied event ID exists.
Retries reuse the original ID. Distinct pawns, maps, cells, and events in one
tick remain distinct.

One active gameplay event has at most one DRF mutation group and one Knowledge
logical observation group. Personal and colony Knowledge evidence for that
event share one Knowledge transaction. A DRF mutation committed before a
Knowledge failure is not rolled back; the Knowledge group may retry with the
same correlation ID. A failed DRF mutation creates no Knowledge evidence for
that mutation.

## Event Contract

| Flow | Initiating authority and ID | Aquaculture mutation | DRF operation | Knowledge group | Journal/save boundary | Retry/failure/diagnostics |
| --- | --- | --- | --- | --- | --- | --- |
| Fish hooked | FishingExpertise; attempt ID from pawn/start tick/map/cell/fish | Creates or updates the serialized fishing attempt only | None | One hooked observation, no population claim | Attempt is serialized; no catch journal entry | Same attempt ID is ignored on retry; malformed attempts are removed on load; adapter diagnostics record dispatch failure |
| Fish caught | FishingExpertise completion; catch correlation ID | Awards expertise/progression, consumes one legacy natural population when present, removes the attempt | `ReconcileActiveMap` for the post-mutation delta | One catch transaction; no second survey accrual | Attempt removal, progression, population ledger, and journal catch record are saved | Completed catch IDs make retries no-ops; DRF exact-once markers protect population mutation; Knowledge retry cannot duplicate it |
| Fish escaped | FishingExpertise resolution; attempt ID | Removes the failed attempt; no population decrement | None | One escape observation | Attempt removal is saved | Repeated escape notification is deduplicated by event ID; no journal catch entry is created |
| Fish stocked | Natural population stocking; caller ID or persisted stocking sequence | Adds the active natural-population record and publishes `FishStocked` | `ReconcileActiveMap` for the observed delta | One colony population observation | Population and bounded completed-event ledger are saved | Duplicate stocking IDs return without mutation; invalid habitat/species/amount fails before publication |
| Fish born | FishLifeCycle/CompFishTraits; fish birth identity and generation | Creates the active fish and journal/breed birth record | No latent mutation for an active birth; active-map reconciliation may later represent a delta | One colony breeding observation and parent relation group | Fish, egg, breed, and journal records are serialized | Repeated lifecycle publication uses the same birth identity; missing parents do not cause a map-wide lookup or duplicate relation |
| Fish died | CompFishTraits.MarkDead; fish birth identity plus death reason | Marks the active fish dead and updates journal/pond/commission caches | No separate latent mutation for an active death; reconciliation handles aggregate delta | One colony health/death observation | Fish alive state and population record are saved | `MarkDead` is idempotent; repeated death calls do not publish another event; invalid values are rejected by DRF |
| Fish migrated | DRF regional recovery process; provider/process/source/destination/execution identity | No active Thing mutation | Atomic `Transfer` with same-species, positive-source, same-open-category checks | No player expertise accrual; the operation ID is the latent observation boundary, and a later survey uses its own observation ID | DRF operation marker, process, topology, and population records are saved | Duplicate transfer is a successful no-op; closed/cross-category/zero-source/species-invalid transfers fail before mutation |
| Population surveyed | Aquaculture survey event; pawn/map/cell/species/tick identity | No population mutation | `RecordEstimate` only, when the provider receives a framework estimate | One uncertainty-bearing population survey | Observation is retained by Knowledge; population remains DRF/Aquaculture state | Same survey ID does not add evidence twice; invalid/nonfinite estimates are rejected |
| Ecology warning | FishEcology/pond causal system; map/cell/species/reason identity | Updates active stress/causal state only | None | One colony health observation | Ecology state and warning cache are saved | Warning presentation is deduplicated and does not mutate populations; diagnostics retain the last provider/adapter error |
| Ecology recovery | FishEcology recovery; same identity with recovery reason | Updates active stress/causal state only | None | One recovery observation | Ecology state and cache are saved | Recovery retries are idempotent by event ID; no population is created by an ecology message |
| Pond activation | Aquaculture pond/topology initialization; map/anchor/proxy/water identity | Initializes active pond state and caches | DRF does not claim a pond as a latent region unless it is a natural-water map | No observation unless a survey is emitted | Pond state and topology caches are saved | Initialization is retried after incomplete load; cache invalidation never deletes latent records |
| Map activation | RimWorld `Map.FinalizeInit` and DRF map lifecycle; persisted MapParent/region identity | Initializes natural populations before provider claim | `RegisterMap`, provider migration, topology/anchor/process registration | No automatic expertise evidence | DRF migration marker commits after all eligible records validate | Uninitialized maps remain pending; failure leaves legacy ticks active; diagnostics report pending/failed migration |
| Map unload | RimWorld `MapDeiniter.Deinit`; stable region identity | Active map ownership is removed; latent projection remains | `UnregisterMap`, retaining latent region/populations | None | Latent DRF state remains saved | Reload reuses the persisted region; active ownership is not mistaken for population deletion |
| Save/load | RimWorld/DRF persistence boundary; record IDs are the correlation boundary | Scribes active fish, populations, ledgers, attempts, and caches | DRF restores regions, operations, migrations, processes, and quarantine | Knowledge restores observations/claims/migrations | All durable markers are restored before retryable work | Completed operations are not replayed; incomplete migrations retry; unknown records remain opaque |
| Framework unavailable | DRF/Knowledge readiness gate | Aquaculture remains authoritative for active gameplay; legacy Knowledge fallback remains available | No provider operation is attempted | Knowledge V3 calls are skipped or use documented legacy fallback | Active Aquaculture saves remain loadable | Registration fails closed and retries; no partial suppression or phantom evidence |
| Provider unavailable | DRF has no active Aquaculture provider | Legacy active state remains available; provider does not suppress its tick | Processes pause as provider-unavailable; latent records remain opaque | Knowledge observations can continue independently where valid | Provider payload and markers remain saved | Restoring the provider re-runs migration/reconciliation idempotently; diagnostics expose unavailable state |
| Retry after failure | Original caller/event; original correlation ID | Only the owning subsystem retries the failed boundary | DRF marker is written only after a successful mutation | Knowledge transaction retries independently after a committed mutation | Failure state and durable markers determine replay safety | Before-mutation failure retries the whole operation; after-mutation failure retries only the uncommitted consumer; journal failures do not undo population state |

## Failure Ordering

- Aquaculture active mutations happen before their post-mutation event is
  published, so DRF reconciles a measured delta rather than consuming twice.
- DRF mutations are transactional and record an applied operation only after
  the population change is committed.
- Knowledge transactions are downstream evidence. A failed Knowledge commit
  does not roll back a committed population mutation and retries with the same
  logical ID.
- Journal notification is downstream presentation/history. A journal failure
  cannot create or undo a population mutation.
- Provider registration and migration become active only after all required
  phases succeed. Partial registration is retryable and does not disable the
  legacy simulation.

## Durable Bounds

- Natural-population completed-event markers retain at most 4,096 IDs in the
  active-map save record.
- Knowledge dynamic subjects are bounded at 2,048 by the adapter contract.
- DRF provider population records are bounded at 4,096 and operation markers
  are retained indefinitely because no replay-impossibility proof is claimed.
- Provider diagnostics expose population/process/marker/duplicate/quarantine
  counts and the latest failure without scanning every active Thing per tick.

## Evidence Boundary

Pure executable tests prove deterministic IDs, exactly-once ledger behavior,
failed-operation retry, finite-state invariants, source/category migration
rules, save restoration of markers, and active/latent ownership rules. They do
not prove a live RimWorld map, UI, old save, or long-running performance.
Those cases are listed as manual or blocked in the release-readiness matrix.
