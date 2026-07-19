# Components

This document describes the main internal components of TradeMind.

## API host

Project: `src/TradeMind.Api`

Responsibilities:

- Compose application services.
- Configure middleware.
- Run startup migrations.
- Map HTTP endpoints.
- Convert HTTP input and output to application calls.

Current KnowledgeHub endpoints:

- `POST /knowledge/sources`
- `GET /knowledge/sources/{id}`
- `GET /knowledge/search`
- `GET /health`

## KnowledgeHub Domain

Project: `src/TradeMind.KnowledgeHub.Domain`

Responsibilities:

- Define `KnowledgeSource`.
- Define `KnowledgeFragment`.
- Define source type and processing status.
- Enforce source processing invariants.

The Domain layer does not know about EF Core, PostgreSQL, pgvector, HTTP, Docker, or AI provider SDKs.

## KnowledgeHub Application

Project: `src/TradeMind.KnowledgeHub.Application`

Responsibilities:

- Orchestrate ingestion, retrieval, and search.
- Define ports for text extraction, fragmentation, embeddings, and repository access.
- Define request and result contracts.

Important contracts:

- `ITextExtractor`
- `IFragmenter`
- `IEmbeddingGenerator`
- `IKnowledgeSourceRepository`

## KnowledgeHub Infrastructure

Project: `src/TradeMind.KnowledgeHub.Infrastructure`

Responsibilities:

- Implement text extraction for text and Markdown.
- Implement sliding window fragmentation.
- Implement deterministic embeddings.
- Implement EF Core DbContext and mapping.
- Implement PostgreSQL repository.
- Register module services with dependency injection.

Infrastructure contains provider-specific details such as EF Core, Npgsql, pgvector registration, raw SQL vector search, and migrations.

## AI Abstractions

Project: `src/TradeMind.AI.Abstractions`

Responsibilities:

- Define provider-agnostic chat contracts.
- Define provider-agnostic embedding contracts.
- Define provider metadata and capabilities.
- Avoid all concrete AI SDK dependencies.

## AI Application

Project: `src/TradeMind.AI.Application`

Responsibilities:

- Expose `IAIOrchestrator`.
- Create `AISession` values through `IAISessionFactory`.
- Carry tenant, user, agent, conversation, and correlation identity through `AIIdentityContext`.
- Validate normalized AI orchestration requests.
- Build provider-agnostic `ChatRequest` values.
- Define, version, validate, and render provider-agnostic prompt templates through the Prompt Engine.
- Execute an ordered DI-driven orchestration pipeline.
- Track `AIExecutionContext`, `AIExecutionState`, `AIExecutionMetrics`, and `AIExecutionError`.
- Check provider chat capability through metadata.
- Call `IChatProvider` without depending on provider infrastructure.
- Normalize AI responses and record executed steps.
- Provide `IAIContextContributor` for future Memory and KnowledgeHub context enrichment.

## AI Infrastructure

Project: `src/TradeMind.AI.Infrastructure`

Responsibilities:

- Bind and validate `AI` configuration.
- Select the active provider.
- Register provider implementations through dependency injection.
- Isolate the official OpenAI SDK.
- Translate between provider-independent contracts and OpenAI SDK types.

## Persistence model

`KnowledgeHubDbContext` maps:

- `KnowledgeSource` to `knowledge_sources`.
- `KnowledgeFragment` to `knowledge_fragments`.

The model installs the PostgreSQL `vector` extension and maps embeddings to `vector(64)`. The migration creates a unique content hash index, a unique source sequence index for fragments, and an HNSW vector index.

## Tests

Unit tests verify domain and pure infrastructure behavior.

Integration tests verify PostgreSQL and pgvector behavior with Testcontainers, including migrations, extension installation, HNSW index creation, persistence, retrieval, and cosine search.
