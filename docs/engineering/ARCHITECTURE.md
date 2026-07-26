# Architecture

TradeMind uses a modular monolith architecture on .NET 9. The application is deployed as one system today, while each business capability is intended to live inside a clear module boundary.

## Modular monolith

A modular monolith gives the project a simple deployment model while preserving internal separation. Modules should own their domain concepts, application services, persistence mappings, and tests. Cross-module access should happen through explicit contracts, not through direct database table access or shared mutable state.

The implemented module is `KnowledgeHub`. It ingests knowledge sources, splits content into fragments, generates embeddings through an abstraction, persists data to PostgreSQL, and searches fragments with pgvector cosine distance.

## Layers

The current layer structure is:

- `TradeMind.KnowledgeHub.Domain`: entities, value-oriented domain state, enums, invariants.
- `TradeMind.KnowledgeHub.Application`: use cases, service orchestration, interfaces for external capabilities.
- `TradeMind.KnowledgeHub.Infrastructure`: EF Core DbContext, PostgreSQL repository, text extraction, fragmentation, deterministic embedding implementation, DI registration.
- `TradeMind.Api`: HTTP host, OpenAPI registration, endpoint mapping, module composition, startup migration execution.
- `tests`: unit and integration test projects.

## Dependency direction

Allowed dependencies:

- Domain depends only on the .NET base class library.
- Application may depend on Domain.
- Infrastructure may depend on Application and Domain.
- API may depend on Application and Infrastructure.
- Tests may depend on the projects required by the behavior under test.

Disallowed dependencies:

- Domain must not reference Infrastructure, API, EF Core, Npgsql, pgvector, HTTP, or AI SDKs.
- Application must not reference Infrastructure or API.
- Modules must not bypass application contracts to manipulate another module's storage.

## Clean Architecture

TradeMind follows Clean Architecture by keeping business rules toward the center and technology details toward the outside. The Domain layer models what must always be true. The Application layer coordinates behavior through ports. Infrastructure implements those ports using concrete tools such as EF Core, PostgreSQL, pgvector, Docker-compatible dependencies, and future AI provider SDKs.

## DDD

Domain-Driven Design is used to keep trading and knowledge concepts explicit. `KnowledgeSource` is an aggregate-like domain object that controls processing state and fragment creation. It enforces that fragments can only be added while processing and that ready sources contain at least one fragment.

Future modules such as Trading Journal, AI Coach, Market Analysis, Strategy Builder, Backtesting, Risk Engine, Portfolio, SaaS administration, and Enterprise controls should define their own domain language and module boundaries.

## CQRS

TradeMind uses CQRS as an architectural direction. Commands should change state through application services or command handlers. Queries should read data through explicit query paths and can use projections when the read model becomes different from the write model.

The current KnowledgeHub implementation has command-like ingestion and query-like retrieval/search in one service. Future growth should split commands and queries when complexity justifies it.

## SOLID principles

- Single Responsibility: each layer owns one category of concern.
- Open/Closed: new extractors, embedding providers, and repositories should be added behind interfaces.
- Liskov Substitution: test doubles and provider implementations must honor the same contracts.
- Interface Segregation: ports should describe focused capabilities.
- Dependency Inversion: application behavior depends on abstractions, not provider details.

## Persistence

PostgreSQL is the primary relational database. EF Core owns schema mapping and migrations. pgvector stores embeddings as `vector(64)` in the current Sprint 1 implementation, with an HNSW index using cosine operators for semantic search.

Raw SQL is acceptable for provider-specific vector search when EF Core cannot express the operation cleanly. Such SQL must use parameters for values and must match the schema created by migrations.

## AI providers

AI providers are intentionally abstracted. `IEmbeddingGenerator` is the current provider boundary. The deterministic implementation is useful for local and test behavior, while future OpenAI or other providers should be added without leaking SDK types into Domain or Application contracts.
