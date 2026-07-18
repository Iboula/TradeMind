# ADR 0002: Knowledge as an autonomous module

- Status: Accepted
- Date: 2026-07-17

## Context

TradeMind needs a governed knowledge base for trading books, strategy notes, specifications, research documents and future retrieval-augmented generation. This capability must evolve independently from journaling, risk management and market analysis.

## Decision

Knowledge is implemented as an autonomous module inside the modular monolith with three projects:

- `TradeMind.Modules.Knowledge.Domain`
- `TradeMind.Modules.Knowledge.Application`
- `TradeMind.Modules.Knowledge.Infrastructure`

The domain owns document lifecycle and indexing invariants. The application layer exposes use cases and ports. Infrastructure provides adapters and HTTP composition. Other modules must integrate through explicit contracts or integration events, never by referencing Knowledge internals.

The initial persistence adapter is intentionally in-memory. It establishes the port boundary without prematurely coupling the domain to PostgreSQL, a vector database or a specific embedding provider.

## Consequences

- The module can later adopt PostgreSQL and `pgvector` without changing its domain model or API use cases.
- Document ingestion, chunking, embeddings and semantic retrieval can be introduced incrementally.
- In-memory persistence is not production durable and must be replaced before the first deployed release.
- Cross-module access will require public contracts and eventual consistency where appropriate.
