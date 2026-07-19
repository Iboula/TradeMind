# Data Flow

This document describes the current KnowledgeHub data flows.

## API startup

1. ASP.NET Core builds the application.
2. `AddKnowledgeHub` registers KnowledgeHub services.
3. A scoped `KnowledgeHubDbContext` is created.
4. EF Core applies pending migrations.
5. The API maps health and KnowledgeHub endpoints.

## Source ingestion

1. A client uploads a file to `POST /knowledge/sources`.
2. The API rejects empty files.
3. The API opens the uploaded stream and sends an `IngestKnowledgeSourceRequest` to `KnowledgeHubService`.
4. The service copies the stream into memory and computes a SHA-256 content hash.
5. The repository checks whether the hash already exists.
6. The service creates a `KnowledgeSource`.
7. The source enters `Processing` state.
8. The extractor reads supported text from `.txt` or `.md`.
9. The fragmenter splits text into sliding-window fragments.
10. The embedding generator creates a 64-dimensional embedding for each fragment.
11. The source adds fragments while processing.
12. The source is marked `Ready`.
13. The repository saves changes through EF Core and PostgreSQL.

If extraction or processing fails, the source is marked `Failed`, the failure reason is stored, and the exception is rethrown.

## Source retrieval

1. A client calls `GET /knowledge/sources/{id}`.
2. The API calls `KnowledgeHubService.GetAsync`.
3. The repository queries PostgreSQL with `AsNoTracking` and includes fragments.
4. The API returns source metadata and fragment summaries, or `404` when no source exists.

## Semantic search

1. A client calls `GET /knowledge/search?q=...`.
2. The service validates the query and clamps the result limit.
3. The embedding generator creates a query embedding.
4. The PostgreSQL repository executes parameterized SQL against pgvector.
5. PostgreSQL orders fragments by cosine distance using `<=>`.
6. The repository returns `KnowledgeSearchResult` records with a similarity score.

## Integration test flow

1. Testcontainers starts `pgvector/pgvector:pg17`.
2. The test fixture creates `KnowledgeHubDbContext`.
3. EF Core applies migrations.
4. Tests verify schema, extension, HNSW index, persistence, retrieval, and vector search.
5. The container is disposed after the test fixture completes.

## Future AI provider flow

OpenAI and future providers implement provider-agnostic interfaces in `TradeMind.AI.Abstractions`. Infrastructure owns credentials, SDK clients, logging, and exception translation.

For KnowledgeHub, `AIEmbeddingGeneratorAdapter` can bridge `IEmbeddingProvider` into the existing `IEmbeddingGenerator` port. The current default remains deterministic 64-dimensional embeddings so it continues to match the existing `vector(64)` schema until a future migration intentionally changes embedding dimensions.

## AI orchestration flow

1. A product module creates an `AIOrchestrationRequest` with a logical scenario, user message, optional system instruction, optional model controls, metadata, optional session id, optional conversation id, optional correlation id, and optional identity context.
2. `IAISessionFactory` creates `AISession`, generating missing session and correlation identifiers with `TimeProvider`.
3. `IAIOrchestrator` creates `AIExecutionContext` and moves it from `Created` to `Running`.
4. The pipeline executes registered `IAIOrchestrationStep` implementations ordered by `Order`.
5. `RequestValidationStep` validates structural input.
6. If Memory Engine is registered and `UseMemory` is true, `MemoryReadStep` loads a bounded conversation window before prompt construction.
7. `PromptConstructionStep` runs context contributors if any exist, renders a prompt template when `PromptTemplateId` is supplied, injects memory messages before the current user message when present, and builds a provider-agnostic `ChatRequest`.
8. `ProviderCapabilityValidationStep` checks that the active provider supports chat.
9. `ProviderExecutionStep` calls `IChatProvider`, passes the caller's cancellation token, and records provider duration and token metrics.
10. If Memory Engine is registered and the provider call succeeds, `MemoryWriteStep` writes the current user message and assistant response.
11. `ResponseNormalizationStep` creates `AIOrchestrationResponse` with session id, conversation id, correlation id, scenario, provider, model, usage, durations, state, response id, and executed steps.
12. The orchestrator marks the context `Completed`, `Failed`, or `Cancelled`.

The flow does not call KnowledgeHub semantic search yet. Memory is conversation history only; KnowledgeHub semantic retrieval remains a separate future integration.

## Prompt rendering flow

1. A caller supplies `PromptTemplateId`, optional version, and prompt variables on `AIOrchestrationRequest`.
2. `PromptConstructionStep` calls `IPromptRenderer`.
3. The renderer resolves the template from `IPromptTemplateRegistry`.
4. The renderer validates declared variables, applies defaults, converts types, and rejects unknown variables or unresolved placeholders.
5. The renderer returns provider-independent messages.
6. `PromptConstructionStep` adapts rendered messages to `ChatRequest`.

If no template id is supplied, the existing system-instruction and user-message path is preserved.

## Memory flow

1. A caller sets `UseMemory = true` and provides `ConversationId`; tenant and user identifiers are copied from `AIIdentityContext` when present.
2. `MemoryReadStep` builds `ConversationMemoryKey` from conversation, tenant, and user.
3. `IMemoryReader` selects a bounded recent window and optional summary.
4. The selected memory is converted to provider-agnostic chat messages.
5. `PromptConstructionStep` inserts summary and history before the current user message.
6. After a successful provider response, `MemoryWriteStep` writes the current user message and assistant response.
7. `MemoryWriter` may update a summary when compaction thresholds are reached.

Memory logs contain identifiers, counts, and sequence ranges only. They do not contain user content, assistant content, summaries, prompts, provider responses, or secrets.
