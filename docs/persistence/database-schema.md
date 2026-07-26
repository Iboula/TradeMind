# Database Schema

The `ExecutionSessionsDbContext` owns six tables in PostgreSQL. The migration
uses explicit `snake_case` names and JSONB only for bounded metadata and event
payloads.

```mermaid
erDiagram
    execution_sessions ||--o{ execution_session_artifacts : contains
    execution_sessions ||--o{ execution_session_timeline : records
    execution_sessions {
        uuid id PK
        varchar correlation_id
        varchar instrument
        varchar timeframe
        varchar status
        varchar current_stage
        bigint concurrency_version
    }
    execution_session_artifacts {
        uuid id PK
        uuid session_id FK
        varchar artifact_type
        varchar artifact_id
        varchar content_hash
        boolean is_replayable
    }
    execution_session_timeline {
        uuid id PK
        uuid session_id FK
        varchar event_type
        timestamptz occurred_at_utc
    }
    execution_session_audit {
        uuid audit_id PK
        uuid session_id
        varchar event_type
        timestamptz occurred_at_utc
    }
    execution_session_outbox {
        uuid outbox_message_id PK
        uuid session_id
        varchar event_type
        jsonb payload
        timestamptz processed_at_utc
    }
    execution_idempotency_records {
        varchar idempotency_key PK
        varchar request_hash
        varchar status
        bytea response_body
        timestamptz expires_at_utc
    }
```

Foreign keys cascade artifact and timeline rows when a session is deleted.
Audit and outbox rows are deliberately append-oriented and retain their
session ID as a durable correlation value without making event publication
depend on a live aggregate query.

Indexes cover correlation, status/start time, instrument/start time, current
stage, artifact identity, timeline/audit ordering, pending outbox messages and
idempotency expiry. Artifact identity is unique per session and artifact type.
