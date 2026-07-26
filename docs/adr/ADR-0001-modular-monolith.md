# ADR-0001: Modular monolith

## Status

Accepted.

## Context

TradeMind is an AI-assisted trading research and decision-support platform. The roadmap includes several substantial capabilities: KnowledgeHub, AI providers, memory, trading journal, AI coach, market analysis, strategy building, backtesting, risk, portfolio features, SaaS operations, and enterprise controls.

The system needs clear boundaries early, but it does not yet need the operational complexity of distributed services. The current implementation already follows a module-oriented shape with separate Domain, Application, Infrastructure, API, and test projects for KnowledgeHub.

## Decision

TradeMind will be built as a modular monolith.

The application deploys as one system, while business capabilities are organized as modules. Each module owns its domain model, application contracts, infrastructure adapters, persistence mapping, and tests. Modules should communicate through explicit contracts rather than direct access to each other's internals.

The initial module is KnowledgeHub. It owns knowledge ingestion, source state, fragments, embeddings, and semantic search.

## Consequences

Positive consequences:

- Simple deployment and local development.
- Clear code ownership and business boundaries.
- Easier refactoring than premature microservices.
- Shared transactions remain possible where a module needs them.
- Future extraction to services remains possible if boundaries stay clean.

Tradeoffs:

- Developers must enforce boundaries through code review and conventions.
- A single deployable can grow large if modules are not kept focused.
- Cross-module coupling can creep in if shortcuts are accepted.

## Guidance

New modules should follow the KnowledgeHub layering pattern:

- Domain for business concepts and invariants.
- Application for use cases and ports.
- Infrastructure for adapters and persistence.
- API for composition and HTTP exposure.
- Unit and integration tests appropriate to the risk.
