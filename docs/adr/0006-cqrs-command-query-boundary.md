# ADR 0006: CQRS command and query boundary

- Status: Accepted
- Date: 2026-07-17

## Context

KnowledgeHub combines transactional ingestion with a SQL-specific nearest-neighbour search. Treating both paths identically would either leak database concerns into application code or make the read path unnecessarily complex.

## Decision

MediatR commands and queries define the application boundary. EF Core handles transactional writes and migrations. Dapper handles the pgvector semantic-search query. Both implementations remain behind application interfaces.

## Consequences

The write model preserves aggregate invariants and transaction boundaries. The read model can use optimized SQL without contaminating the domain. Additional projections may be added without changing command handlers or aggregate behavior.
