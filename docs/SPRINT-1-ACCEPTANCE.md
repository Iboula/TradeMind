# Sprint 1 Acceptance — KnowledgeHub MVP

The sprint is accepted when the following capabilities are available:

- Docker Compose starts PostgreSQL with pgvector and the TradeMind API.
- EF Core automatically enables the `vector` extension and applies the KnowledgeHub schema.
- `POST /knowledge/sources` accepts a non-empty UTF-8 `.txt` file.
- The pipeline imports, extracts, normalizes, fragments, embeds and indexes the source.
- `GET /knowledge/sources/{id}` returns the persisted source and fragment count.
- `GET /knowledge/search?query=...` returns at most five nearest fragments.
- Unit tests protect domain invariants.
- Integration tests exercise upload, persistence and semantic search against pgvector.
- GitHub Actions restores, builds and tests the solution on pushes and pull requests.

The fake embedding adapter is deliberately deterministic and credential-free. It is an infrastructure implementation of `IEmbeddingGenerator`, not a domain dependency.
