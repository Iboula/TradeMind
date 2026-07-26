# Execution Session Model

An `ExecutionSession` is a versioned aggregate identified by a stable session
ID and a correlation ID. It carries the instrument, timeframe, tenant, user,
trigger, core/API versions and a bounded metadata dictionary.

## Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Created
    Created --> Running: Begin
    Running --> Running: AdvanceStage / LinkArtifact
    Running --> Completed: Complete
    Running --> Failed: Fail
    Running --> Cancelled: Cancel
    Running --> Expired: Expire
    Completed --> [*]
    Failed --> [*]
    Cancelled --> [*]
    Expired --> [*]
```

The pipeline stages are ordered from `MarketContext` through `PaperTrading` and
then `Completed`. Stage regression, terminal mutation, duplicate artifact
identity and non-monotonic timestamps are rejected by the domain model.

## Identity and isolation

The optional tenant and user values are persisted on the aggregate and are
included in the API resource. Search and retrieval must be scoped by the host
authorization boundary before being exposed to a tenant. Sprint 25 provides
the storage fields and correlation behavior; authentication and authorization
remain a later platform phase.

## Artifacts

Artifacts contain only a typed reference, schema version, creation time,
content hash, replayability flag and optional storage reference. The aggregate
does not embed large analytical payloads. This keeps session rows bounded and
allows module-specific result schemas to evolve independently.
