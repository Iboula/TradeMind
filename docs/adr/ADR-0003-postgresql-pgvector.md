# ADR-0003: PostgreSQL and pgvector

## Status

Accepted.

## Context

TradeMind needs durable relational data and semantic retrieval. KnowledgeHub stores knowledge sources, fragments, processing state, content hashes, and vector embeddings. Semantic search needs cosine-distance ranking over embeddings.

The project already uses EF Core migrations, Docker, PostgreSQL, and pgvector. Integration tests start a real pgvector-enabled PostgreSQL container with Testcontainers.

## Decision

TradeMind will use PostgreSQL as the primary relational database and pgvector for vector similarity search.

EF Core owns schema mapping and migrations. The current KnowledgeHub migration installs the `vector` extension, stores embeddings as `vector(64)`, and creates an HNSW index using cosine operators. Infrastructure registers the pgvector EF provider with `UseVector()`.

Raw SQL may be used in Infrastructure for pgvector search when it is clearer or more reliable than forcing the operation through LINQ.

## Consequences

Positive consequences:

- One durable store can support relational data and vector search.
- EF Core migrations keep schema changes reviewable.
- pgvector keeps semantic search close to the data.
- Testcontainers can verify real PostgreSQL and pgvector behavior in CI.

Tradeoffs:

- Docker is required for integration tests.
- pgvector-specific SQL and indexes introduce provider coupling in Infrastructure.
- Embedding dimensions and index choices must be managed carefully as providers evolve.

## Alternatives considered

- In-memory storage: useful for pure unit tests, not acceptable for persistence or vector-search behavior.
- External vector database: may be useful later, but it adds operational complexity before the product needs it.
- Plain PostgreSQL without pgvector: insufficient for first-class semantic search.
