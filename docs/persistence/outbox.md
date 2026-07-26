# Transactional Outbox

Each command that mutates a session writes an outbox message in the same
transaction as the aggregate and audit row.

```mermaid
sequenceDiagram
    participant Handler as Command handler
    participant DB as PostgreSQL
    participant Worker as Future outbox worker
    Handler->>DB: Update session
    Handler->>DB: Insert audit row
    Handler->>DB: Insert pending outbox message
    DB-->>Handler: Commit atomically
    Worker->>DB: Claim pending message
    Worker->>Worker: Publish to selected consumer
    Worker->>DB: Mark processed or record failure
```

Sprint 25 persists and indexes pending messages but does not introduce a
broker, worker or external delivery protocol. Processing is therefore a future
platform concern. Consumers must be idempotent and use the outbox message ID
as their deduplication key when delivery is implemented.
