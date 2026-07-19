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
- Adapt explicitly requested Tool Engine execution into the orchestration pipeline without provider-native tool calling.

## AI Memory

Project: `src/TradeMind.AI.Memory`

Responsibilities:

- Manage provider-agnostic conversational memory for AI sessions.
- Scope memory by conversation, tenant, and user through `ConversationMemoryKey`.
- Store ordered conversation entries and optional summaries through `IMemoryStore`.
- Read bounded context windows through `IMemoryReader`.
- Write user and assistant messages through `IMemoryWriter`.
- Estimate tokens deterministically through `ITokenEstimator`.
- Provide a deterministic local summarizer for tests and development.
- Register optional AI orchestration steps for memory read and memory write.

The Memory module does not depend on OpenAI, EF Core, Npgsql, pgvector, ASP.NET Core, `HttpContext`, JWT, KnowledgeHub, file storage, or provider infrastructure. The current implementation is in-memory and not durable.

## AI Knowledge

Project: `src/TradeMind.AI.Knowledge`

Responsibilities:

- Retrieve KnowledgeHub fragments for one AI request.
- Apply query, score, ordering, deduplication, and context-budget policy.
- Compose bounded provider-agnostic RAG context.
- Create internal citations such as `[K1]`.
- Register an optional orchestration step before prompt construction.

The RAG module depends on KnowledgeHub Application contracts and AI Application orchestration contracts. It does not depend on KnowledgeHub Infrastructure, EF Core, Npgsql, pgvector, OpenAI, ASP.NET Core, or Memory Store implementations.

## AI Tools

Project: `src/TradeMind.AI.Tools`

Responsibilities:

- Define immutable tool ids, definitions, parameters, requests, contexts, metrics, errors, and results.
- Register and discover tools through an immutable in-memory registry.
- Authorize availability, permissions, identity restrictions, scenarios, and side-effect levels.
- Validate and normalize structured arguments with invariant culture.
- Execute one explicitly requested tool with timeout and caller cancellation.
- Compose bounded successful output as untrusted provider-agnostic context.
- Provide deterministic `echo` and `add-numbers` demonstration tools.

The module has no provider SDK, EF Core, Npgsql, ASP.NET Core, `HttpContext`, filesystem, network, broker, market data, database, or trading dependency. The AI Application adapter adds optional `ToolExecution` and `ToolResultComposition` steps.

## AI Infrastructure

Project: `src/TradeMind.AI.Infrastructure`

Responsibilities:

- Bind and validate `AI` configuration.
- Select the active provider.
- Register provider implementations through dependency injection.
- Isolate the official OpenAI SDK.
- Translate between provider-independent contracts and OpenAI SDK types.

## AI Agents

Project: `src/TradeMind.AI.Agents`

Responsibilities:

- Define immutable agent ids, semantic versions, definitions, capabilities, policies, requests, contexts, responses, metrics, and safe errors.
- Snapshot registered agents and resolve `Exact`, `Latest`, or `LatestStable` versions.
- Discover public definitions by availability, permission, tenant, user, scenario, capability, tag, and side-effect boundary.
- Authorize every execution and reject request overrides that widen the definition.
- Map agent policy to the existing Prompt, Memory, Knowledge, Tool, identity, and orchestration contracts.
- Invoke `IAIOrchestrator` once with timeout and caller cancellation, optional bounded hooks, no retry, and structured logs.
- Provide `generic-assistant` `1.0.0` and the development-only `trading-coach` `0.1.0` educational skeleton.

The module composes public AI contracts only. It has no OpenAI SDK, EF Core, Npgsql, pgvector, ASP.NET Core, HTTP context, filesystem, network, market data, broker, or agent persistence dependency.

## Persistence model

`KnowledgeHubDbContext` maps:

- `KnowledgeSource` to `knowledge_sources`.
- `KnowledgeFragment` to `knowledge_fragments`.

The model installs the PostgreSQL `vector` extension and maps embeddings to `vector(64)`. The migration creates a unique content hash index, a unique source sequence index for fragments, and an HNSW vector index.

## Tests

Unit tests verify domain and pure infrastructure behavior.

Integration tests verify PostgreSQL and pgvector behavior with Testcontainers, including migrations, extension installation, HNSW index creation, persistence, retrieval, and cosine search.
