# Architecture Decision Records

TradeMind uses Architecture Decision Records to capture decisions that shape the system over time.

An ADR should be added or updated when a decision affects architecture, persistence, module boundaries, deployment, security, or provider strategy. ADRs are not task notes; they explain durable choices and their consequences.

## Current ADRs

- [ADR-0001: Modular monolith](ADR-0001-modular-monolith.md)
- [ADR-0002: CQRS](ADR-0002-cqrs.md)
- [ADR-0003: PostgreSQL and pgvector](ADR-0003-postgresql-pgvector.md)

## ADR format

Each ADR should include:

- Status.
- Context.
- Decision.
- Consequences.
- Alternatives considered when useful.

The repository also contains earlier module-specific notes such as `0002-knowledge-source-model.md`. New foundational ADRs use the `ADR-0000-name.md` naming convention.
