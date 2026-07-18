# Architecture Documentation

This directory describes the current TradeMind architecture.

TradeMind is a .NET 9 modular monolith. The implemented business capability is KnowledgeHub, exposed through an ASP.NET Core API and backed by PostgreSQL with pgvector.

## Documents

- [System Context](SYSTEM_CONTEXT.md): users, external systems, and boundaries.
- [Containers](CONTAINERS.md): deployable/runtime units and storage.
- [Components](COMPONENTS.md): internal projects and layer responsibilities.
- [Data Flow](DATA_FLOW.md): ingestion, retrieval, semantic search, startup, and test flows.

## Current architectural shape

The system currently contains:

- ASP.NET Core API host.
- KnowledgeHub Domain, Application, and Infrastructure projects.
- PostgreSQL database with pgvector extension.
- EF Core migrations.
- Docker Compose for local PostgreSQL.
- GitHub Actions for restore, build, and tests.
- Testcontainers for PostgreSQL/pgvector integration tests.

Future modules should be added as modules inside the monolith before any service extraction is considered.
