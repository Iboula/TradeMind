# TradeMind Architecture

## System shape

TradeMind is a .NET 9 Modular Monolith organized around bounded contexts. The API project is the composition root. Each module owns its Domain, Application and Infrastructure projects and its PostgreSQL tables.

## Dependency rule

```text
API → Module Infrastructure → Module Application → Module Domain → BuildingBlocks.Domain
```

Domain projects contain no database, HTTP or AI-provider dependencies. Cross-module communication must use public contracts or integration events.

## KnowledgeHub

KnowledgeHub is the platform memory boundary. Its pipeline is:

```text
KnowledgeSource → Import → Extract → Normalize → Fragment → Embedding → Index → Semantic Search
```

The application layer depends on `IKnowledgeImporter`, `ITextExtractor`, `IFragmenter`, `IEmbeddingGenerator`, `IKnowledgeIndexer` and `IKnowledgeSearcher`. Infrastructure owns the current TXT, fake embedding, EF Core, Dapper and pgvector adapters.

The persistence model uses `knowledge_sources`, `knowledge_fragments` and `embeddings`. EF Core handles transactional writes and migrations. Dapper performs the nearest-neighbour query because that path is read-heavy and SQL-specific.

## Operational baseline

- PostgreSQL 17 with pgvector
- automatic migrations at API startup
- Docker Compose for local execution
- GitHub Actions restore, build and tests on pushes and pull requests
- OpenAPI, Problem Details and health checks

## Evolution constraints

Multi-tenancy will be introduced through explicit tenant ownership on aggregates and database rows. External embedding providers will replace the fake generator behind the existing interface. Asynchronous ingestion can later be introduced without changing the domain model or public endpoints.
