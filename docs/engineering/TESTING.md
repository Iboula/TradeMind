# Testing

TradeMind uses unit tests and integration tests to protect the modular monolith.

## Unit tests

Unit tests live in `tests/TradeMind.KnowledgeHub.Tests`. They verify domain rules and infrastructure-independent behavior such as:

- Knowledge source initialization.
- Processing state rules.
- Fragment requirements.
- Sliding window fragmentation.

Unit tests should not require Docker, PostgreSQL, network access, or secrets.

## Integration tests

Integration tests live in `tests/TradeMind.KnowledgeHub.IntegrationTests`. They verify behavior that depends on real PostgreSQL and pgvector:

- EF Core migrations.
- `vector` extension installation.
- HNSW index creation.
- Knowledge source and fragment persistence.
- Retrieval by source id.
- Semantic search using cosine distance.

## Testcontainers

Integration tests use Testcontainers to start `pgvector/pgvector:pg17`. Each test run should create an isolated database environment and apply EF Core migrations before assertions.

Docker must be available locally and in CI for these tests to run.

## PostgreSQL and pgvector

PostgreSQL behavior must not be replaced by an in-memory provider when testing persistence, migrations, SQL, or vector search. In-memory tests can be used only for pure application behavior where persistence semantics are irrelevant.

## CI

GitHub Actions runs:

```bash
dotnet restore TradeMind.sln
dotnet build TradeMind.sln --configuration Release --no-restore
dotnet test TradeMind.sln --configuration Release --no-build
```

The solution includes both unit and integration test projects, so CI executes both through `dotnet test TradeMind.sln`.

## Local validation

Before pushing a branch, run:

```powershell
dotnet restore TradeMind.sln
dotnet build TradeMind.sln --configuration Release
dotnet test TradeMind.sln --configuration Release
```

The build should complete with zero warnings because warnings are treated as errors by project policy.
