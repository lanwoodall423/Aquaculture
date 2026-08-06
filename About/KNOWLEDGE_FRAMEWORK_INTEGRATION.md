# Knowledge Framework Integration

Aquaculture uses Knowledge Framework as an observation and expertise consumer,
not as a population authority. The required baseline is package
`lan.knowledgeframework`, semantic release `3.0.0-beta.1` or later, integer API
generation 3, and capability generation 3 for every V3 API used by the
adapter. The framework does not expose a separate `Supports` API generation;
the adapter uses its documented semantic/API/capability contract and falls
back safely when it is unavailable.

## Event Boundary

`AquacultureEvent.logicalEventId` is the single correlation input. Catch and
establishment personal/colony evidence share one Knowledge transaction.
Stocking and death now have explicit event routes; catch surveys do not create a
second accrual event. Knowledge observation IDs include the event ID, reason,
subject, facet, and scope, so two events with the same reason remain distinct.

Knowledge failure is downstream of population mutation. It removes the
in-memory dispatch deduplication entry for retry, but never asks DRF or
Aquaculture to mutate the population again. A repeated successful Knowledge
submission is an idempotent no-op at the framework observation ID boundary.

## Registration And Aggregation

Registration phases are domain, contexts, relations, provider, and UI. The
adapter is ready only when every phase succeeds and the registered schema is
available. Partial registration is retried. V3 stage aggregation is explicitly
`Balanced`; the migration marker is committed only after the framework accepts
the schema change.

Species presentation uses `KnowledgeDiscovery.StageSnapshot` and framework
facet/claim snapshots. It does not manually sum every facet or invent a second
normalization formula. Context fallback is explicit parent-then-global and
context IDs include map, anchor/proxy, water, and topology identity.

## Migration And Retention

Legacy progression migration uses stable consumer/version IDs, finite-safe
values, and checks every `Import` result plus durable `IsCommitted` before
considering cleanup complete. Invalid or partial migration leaves the legacy
state available for retry. Dynamic subjects are bounded to 2,048; transient
fish subjects are tracked on spawn and forgotten on despawn while framework
records remain durable.

## Diagnostics And Tests

The adapter reports registration readiness, capability status, migration state,
event deduplication, subject bounds, context/relationship failures, and the
latest deferred error. Pure tests cover API/capability gating, deterministic
IDs, event groups, migration commit rules, finite values, subject limits, and
retry semantics. The Knowledge Framework behavioral harness separately covers
the public API baseline and production pure suite; live game-state and UI
layers require a running RimWorld map.
