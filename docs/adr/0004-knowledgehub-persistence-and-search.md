# ADR 0004: KnowledgeHub persistence and semantic search

- Status: Accepted
- Date: 2026-07-17

## Context

KnowledgeHub must ingest text sources, fragment normalized content, generate embeddings through a replaceable provider, persist vectors and return the five nearest fragments. The domain must remain independent from PostgreSQL, pgvector and any AI vendor.

## Decision

- PostgreSQL is the source of truth for knowledge metadata and fragments.
- pgvector stores 64-dimensional embeddings for the MVP.
- EF Core owns schema migrations and transactional writes.
- Dapper executes the read-optimized nearest-neighbour query.
- `IEmbeddingGenerator` isolates the application layer from AI providers.
- `FakeEmbeddingGenerator` provides deterministic local and CI behavior until a production embedding adapter is introduced.
- An HNSW cosine index supports semantic search.
- Each source, fragment and embedding is persisted in its own table.

## Consequences

The MVP works without external AI credentials and can be demonstrated locally through Docker Compose. A future embedding provider may change vector dimensions; that change requires a new migration and index rebuild. Provider selection and model metadata must therefore be introduced before production ingestion begins.
