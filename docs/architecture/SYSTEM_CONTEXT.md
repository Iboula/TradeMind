# System Context

TradeMind is an AI-assisted trading research and decision-support platform.

## Primary actors

## Trader

The trader uses TradeMind to collect knowledge, search trusted material, and eventually journal trades, review decisions, analyze strategies, and manage risk.

## Developer

The developer extends modules, maintains tests, updates migrations, and validates changes through local commands and GitHub Actions.

## Codex or automation agent

Codex can contribute code and documentation when operating within the repository rules. It must preserve module boundaries, avoid secrets, and validate changes.

## External systems

## PostgreSQL

PostgreSQL is the primary database. KnowledgeHub stores sources, fragments, processing state, content hashes, and embeddings in PostgreSQL.

## pgvector

pgvector is installed as a PostgreSQL extension. It stores vector embeddings and supports cosine-distance semantic search through an HNSW index.

## Docker

Docker runs local PostgreSQL through Docker Compose and disposable integration test databases through Testcontainers.

## GitHub Actions

GitHub Actions validates restore, Release build, and tests on pushes to `main` and `feature/**`, and pull requests targeting `main`.

## OpenAI and AI providers

OpenAI is an expected provider category, but provider access is abstracted. The current application boundary is `IEmbeddingGenerator`, and the implemented generator is deterministic for local behavior. Future provider implementations should remain outside Domain and Application business rules.

## System boundary

Inside TradeMind:

- API endpoints.
- KnowledgeHub domain model and use cases.
- Infrastructure adapters.
- EF Core mappings and migrations.
- Tests and documentation.

Outside TradeMind:

- Database engine.
- Docker runtime.
- CI runtime.
- Future AI provider APIs.
- Future market data or broker systems.
