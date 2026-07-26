# Containers

This document describes runtime containers and major deployable units in the current TradeMind architecture.

## TradeMind API

The API is the .NET 9 ASP.NET Core host in `src/TradeMind.Api`.

Responsibilities:

- Configure OpenAPI and ProblemDetails.
- Register KnowledgeHub through dependency injection.
- Apply EF Core migrations at startup.
- Expose `/health`.
- Expose KnowledgeHub endpoints for source ingestion, source retrieval, and semantic search.

The API is currently one deployable process. It hosts module composition for the modular monolith.

## KnowledgeHub module

KnowledgeHub is not a separate deployable container. It is a module inside the monolith with separate projects:

- Domain.
- Application.
- Infrastructure.

It owns knowledge source processing, fragments, embeddings, and persistence contracts.

## PostgreSQL with pgvector

PostgreSQL is the persistent database container for local development and the target database technology for runtime environments.

The local Docker Compose service uses:

```text
pgvector/pgvector:pg17
```

The database stores:

- `knowledge_sources`.
- `knowledge_fragments`.
- EF Core migration history.
- pgvector extension metadata.

## Testcontainers PostgreSQL

Integration tests start disposable PostgreSQL containers using the same pgvector image family. Tests apply EF Core migrations before assertions.

This container is not a production dependency. It is a test runtime dependency.

## GitHub Actions runner

The CI runner restores packages, builds the solution in Release mode, and runs all tests. Because integration tests use Testcontainers, the runner must support Docker.

## Future containers

Future deployment may add:

- Worker process for long-running ingestion and AI jobs.
- Background scheduler.
- Separate frontend application.
- Observability stack.

These should be added only when product and operational needs justify them.
