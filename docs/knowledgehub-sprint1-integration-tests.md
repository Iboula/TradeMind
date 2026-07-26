# KnowledgeHub Sprint 1 integration tests

This change adds PostgreSQL/pgvector integration coverage for the KnowledgeHub Sprint 1 persistence path.

- Starts PostgreSQL with pgvector through Testcontainers using `pgvector/pgvector:pg17`.
- Applies EF Core migrations before integration tests run.
- Verifies `KnowledgeSource` and `KnowledgeFragment` persistence, lookup by source id, cosine-distance semantic search, the `vector` extension, and the expected HNSW index.
- Enables the pgvector EF Core provider with `UseVector()` so the runtime mapping matches the migration model.
