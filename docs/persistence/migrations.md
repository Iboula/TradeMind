# Migrations and Operations

The initial migration is
`20260726150000_InitialExecutionSessions` in
`src/TradeMind.ExecutionSessions.Infrastructure/Persistence/Migrations`.
It creates the session, artifact, timeline, audit, outbox and durable
idempotency tables plus their indexes and foreign keys.

Production migration guidance:

1. Build the release artifact from a clean checkout.
2. Review the generated SQL in the deployment environment.
3. Apply migrations with the deployment identity before enabling the API.
4. Set `TradeMind:Persistence:Provider=PostgreSql` and the named connection.
5. Keep `ApplyMigrationsOnStartup=false` unless the host is a controlled single
   migrator environment.
6. Verify `/health/ready` after the migration and inspect outbox/idempotency
   retention metrics.

The module never calls `EnsureCreated`. PostgreSQL is required for production;
an in-memory database is not a substitute for concurrency, JSONB, durable
idempotency or transaction behavior.
