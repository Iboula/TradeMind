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
7. If Knowledge RAG Engine is registered and `Knowledge.Enabled` is true, `KnowledgeRetrievalStep` retrieves bounded KnowledgeHub context.
8. `PromptConstructionStep` runs context contributors if any exist, renders a prompt template when `PromptTemplateId` is supplied, injects memory and knowledge messages before the current user message when present, and builds a provider-agnostic `ChatRequest`.
9. If Tool Engine orchestration is registered and `Tool.Enabled` is true, `ToolExecutionStep` executes the explicit structured invocation once after authorization and validation.
10. `ToolResultCompositionStep` inserts only a successful bounded result as untrusted external data before the current user message.
11. `ProviderCapabilityValidationStep` checks that the active provider supports chat.
12. `ProviderExecutionStep` calls `IChatProvider`, passes the caller's cancellation token, and records provider duration and token metrics.
13. If Memory Engine is registered and the provider call succeeds, `MemoryWriteStep` writes the current user message and assistant response.
14. `ResponseNormalizationStep` creates `AIOrchestrationResponse` with session id, conversation id, correlation id, scenario, provider, model, usage, durations, state, response id, executed steps, safe Knowledge RAG counters, and safe tool status.
15. The orchestrator marks the context `Completed`, `Failed`, or `Cancelled`.

Knowledge RAG is optional. When disabled, no KnowledgeHub search is performed.

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

## Knowledge RAG flow

1. A caller sets `Knowledge.Enabled = true`.
2. The RAG step uses the explicit query or the current user message, according to `AIKnowledgeOptions`.
3. `IKnowledgeContextRetriever` calls `IKnowledgeSearcher`, which adapts to KnowledgeHub search.
4. The retriever filters, deduplicates, orders, budgets, and cites fragments.
5. `IKnowledgeContextComposer` creates a delimited reference-material message.
6. `PromptConstructionStep` injects the RAG context before the current user message.
7. The response exposes citation ids and counts, not full fragment content.

## Explicit tool flow

1. A trusted application consumer enables `AIOrchestrationRequest.Tool` and supplies a valid lowercase-kebab-case tool id plus structured arguments.
2. `RequestValidationStep` requires the tool id and validates the optional timeout shape.
3. Prompt, Memory, and Knowledge context are prepared through their existing paths.
4. `ToolExecutionStep` creates an execution request with session, correlation, conversation, tenant, user, agent, scenario, timeout, idempotency key, and permissions.
5. The registry resolves one known tool.
6. The authorizer enforces availability, permissions, identity restrictions, scenario, and side-effect ceilings.
7. The argument validator applies invariant conversion, defaults, bounds, allowed values, nullability, and sensitive-value protection.
8. The executor invokes the handler once with a linked caller and timeout token, then normalizes timing and result metadata.
9. Successful output is bounded and composed as untrusted external data before the current user message.
10. The provider is called once and the response exposes tool id, use, success, duration, and safe error code only.

Unknown, unavailable, and unauthorized tools fail closed. Validation or execution failures follow `FailClosed` or `ContinueWithoutTool`; continuation never injects failed output or an exception into the prompt.

## Versioned agent flow

1. A product module creates `AIAgentExecutionRequest` with a canonical agent id, explicit version strategy, scenario, user message, identity, permissions, and optional narrower engine settings.
2. `InMemoryAIAgentRegistry` resolves one immutable definition by `Exact`, `Latest`, or `LatestStable` semantic ordering.
3. `PolicyBasedAIAgentAuthorizer` checks availability, permissions, tenant, user, scenario, requested capabilities, timeout, tool, and side-effect boundaries.
4. `AIAgentPolicyGuard` rejects every override that would enable a forbidden capability, disable a required capability, enlarge a context budget, weaken fail-closed behavior, or add rights.
5. The optional starting hook receives a service-free `AIAgentExecutionContext`.
6. `AIAgentRequestMapper` applies Prompt defaults and effective Memory, Knowledge, Tool, identity, session, conversation, correlation, and metadata settings.
7. `IAIOrchestrator` runs its existing ordered pipeline exactly once and calls the active provider once.
8. `AIAgentResponseMapper` exposes safe content, provider, public citation ids, optional tool id, engine-use flags, state, timing, and token metrics.
9. The optional completion hook runs and the executor returns `AIAgentExecutionResponse`.
10. On error, the optional failure hook is attempted without replacing the primary exception; there is no automatic retry.

An agent timeout uses `TimeProvider` and becomes `AIAgentTimeoutException`. Caller cancellation remains `OperationCanceledException`. Reduced-capability mode applies only to optional Memory, Knowledge, or Tool failures and never to authorization.

## Trading Coach analysis flow

1. A trusted product caller supplies an immutable `TradingJournalAnalysisRequest`, `TradingCoachProfile`, bounded `TradingCoachExecutionOptions`, and cancellation token.
2. The validator rejects unsafe signs, ranges, timestamps, text lengths, unsupported direction, and contradictory explicit risk data without logging values.
3. The normalizer trims text, canonicalizes direction, timeframe, and tags, reports unambiguous derivations, and calculates completeness.
4. The metrics calculator derives only supported historical values and records sources, unavailable metrics, and consistency warnings.
5. The rule analyzer detects documented process breaches, missing fields, behavior terms, and process-versus-result distinctions; the scorer creates deterministic process scores.
6. The service creates an exact `trading-coach` `1.0.0` request. Structured journal content is supplied as delimited JSON prompt data, not as a free-form system instruction.
7. Optional Memory uses a small window and does not automatically save the request or response. Optional Knowledge uses an explicit educational-process query and bounded filters. Neither is required for execution.
8. `IAIAgentExecutor` invokes the existing orchestrator once. Tools are disabled and no tool orchestration registration is required.
9. The response parser accepts one exact JSON schema and assigns analysis identity, time, and version from application-controlled values.
10. The merger restores deterministic metrics, findings, missing information, scores, and disclaimer as authoritative values; AI actions must reference a finding.
11. The final safety filter fails closed on directional trade instructions, order execution language, exact predictions, leverage instructions, guarantees, or promised returns.
12. The service returns an immutable `TradingCoachAnalysis` and logs identifiers, counts, state, and duration only.
