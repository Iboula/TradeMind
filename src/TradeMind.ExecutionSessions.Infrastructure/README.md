# Execution Sessions Infrastructure

This project owns the PostgreSQL adapter for the platform-level execution-session bounded context. The dedicated `ExecutionSessionsDbContext` persists session state, artifact references, timeline entries, append-only audit rows, transactional outbox messages and durable idempotency records.

The aggregate is loaded through application repository abstractions. State changes use an explicit `bigint` concurrency version and PostgreSQL optimistic concurrency. The unit of work wraps aggregate, audit and outbox writes in one transaction. Search queries project only indexed session fields and use deterministic ordering.

The infrastructure has no broker, MT5 or concrete LLM dependency. Migrations are checked into `Persistence/Migrations`; production code never calls `EnsureCreated`.
