# Execution Sessions Domain

This project contains the platform-level `ExecutionSession` aggregate. It has
no EF Core, PostgreSQL, ASP.NET Core, broker or AI-provider dependency.

The aggregate records correlation identity, tenant and user scope, lifecycle
status, monotonic pipeline stage, immutable metadata, artifact references and a
timeline. Mutations are explicit and version the aggregate. Terminal states
cannot be changed, timestamps must be UTC and stage regressions are rejected.

`ExecutionSessionId`, `ExecutionCorrelationId` and artifact identifiers are
validated value objects. Rehydration is available only through the explicit
factory used by persistence adapters; callers receive read-only collection
views and cannot mutate the aggregate through returned snapshots.
