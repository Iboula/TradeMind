# Brokers Infrastructure

This project supplies adapters for the broker application ports.

`InMemoryBrokerConnector` is a deterministic simulation-only reference connector. It has no network path and does not support Demo or Live execution. PostgreSQL infrastructure persists normalized executions, orders, fills, positions, idempotency records, audit entries and reconciliation reports through `BrokerDbContext` and the explicit `InitialBrokerExecution` migration.

Persistence is opt-in through `TradeMind:Brokers:Persistence:Enabled` and a configured connection string. The PostgreSQL idempotency store coordinates concurrent instances with a unique key and explicit unique-violation handling. It never stores raw idempotency keys or connector credentials. Reconciliation reads connector state and writes a deterministic report without modifying orders or positions.
