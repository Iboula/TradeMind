# ADR-0002: Model knowledge as sources and fragments

- Status: Accepted
- Date: 2026-07-18

## Context

TradeMind must ingest text files now and later support PDFs, websites, video transcripts, broker rules, prop-firm rules, journals, conversations, images and other formats.

## Decision

The KnowledgeHub bounded context uses `KnowledgeSource` as its aggregate root and `KnowledgeFragment` as the searchable unit. The domain does not depend on a specific AI or embedding provider.

The ingestion pipeline is:

`Source -> Extract -> Normalize -> Fragment -> Embed -> Index -> Search`

Provider-specific behavior is isolated behind application interfaces.

## Consequences

- New source formats are added through adapters without changing the domain model.
- Embedding providers can be replaced without changing use cases.
- The first vertical slice supports UTF-8 `.txt` and `.md` files.
- PostgreSQL and pgvector are the target durable storage; the first branch uses an in-memory adapter while the persistence adapter and migrations are completed.
