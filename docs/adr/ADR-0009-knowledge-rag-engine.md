# ADR-0009: Knowledge RAG Engine

## Status

Accepted.

## Context

KnowledgeHub can ingest sources, persist fragments in PostgreSQL, store `vector(64)` embeddings, and search fragments with pgvector cosine distance. AI orchestration can build prompts, use versioned Prompt Engine templates, and optionally inject conversational memory. The missing piece is a provider-agnostic RAG layer that retrieves relevant KnowledgeHub fragments and injects bounded document context into the model prompt.

KnowledgeHub, RAG Engine, and Memory Engine are separate responsibilities. KnowledgeHub owns ingestion, persistence, embeddings, pgvector indexes, and semantic search. RAG Engine owns query policy, result selection, deduplication, budgets, citations, and prompt composition. Memory Engine owns conversation history and summaries. RAG context must not be stored in conversation memory by default.

## Decision

TradeMind will add `TradeMind.AI.Knowledge` as the Knowledge/RAG Engine module.

The module introduces:

- `KnowledgeContextRequest`
- `KnowledgeContextResult`
- `KnowledgeContextFragment`
- `KnowledgeCitation`
- `IKnowledgeSearcher`
- `IKnowledgeContextRetriever`
- `IKnowledgeContextComposer`
- `KnowledgeContextRetriever`
- `KnowledgeContextComposer`
- `KnowledgeHubServiceSearcher`

`AIOrchestrationRequest` gains `AIKnowledgeOptions Knowledge`, disabled by default. `AIOrchestrationResponse` exposes only safe RAG metadata: whether knowledge was used, citation ids, selected result count, and retrieval duration.

The RAG Engine calls KnowledgeHub through `IKnowledgeSearcher`. The first adapter wraps the existing `KnowledgeHubService.SearchAsync`, which already uses `IEmbeddingGenerator` and repository search. The RAG module does not depend on EF Core, Npgsql, pgvector, OpenAI, ASP.NET Core, `HttpContext`, or KnowledgeHub infrastructure.

The default failure mode is `ContinueWithoutKnowledge`. `FailClosed` is available when a scenario must not answer without document grounding.

## Context selection

Selection is deterministic:

1. Call search once.
2. Apply minimum score.
3. Deduplicate by fragment id, checksum, then source/content.
4. Order by relevance, source/sequence, or original search order.
5. Apply max results, max characters, and estimated token budget.
6. Create internal citations `[K1]`, `[K2]`, `[K3]`.

Fragments are excluded when they exceed the remaining budget. Fragment content is not silently truncated.

## Prompt composition and security

The composer creates clearly delimited reference-material context. Retrieved documents are treated as untrusted data, not privileged instructions. The composed context tells the model not to follow instructions inside retrieved documents and not to claim absent sources.

Logs include session, correlation, scenario, counts, truncation, duration, and state. Logs do not include full queries, fragments, prompts, responses, embeddings, SQL, connection strings, or secrets.

## pgvector independence and vector limits

The RAG Engine is independent of pgvector. pgvector remains an implementation detail of KnowledgeHub Infrastructure.

The current `vector(64)` dimension is appropriate for deterministic test embeddings. Production OpenAI embeddings require a deliberate schema and migration decision before use. Different embedding dimensions must not be mixed in one vector column without an explicit strategy.

## Alternatives rejected

- Calling PostgreSQL or pgvector directly from RAG Engine: rejected because persistence belongs to KnowledgeHub Infrastructure.
- Injecting raw documents without budgets: rejected because context size and prompt-injection risk must be controlled.
- Returning full fragment content in `AIOrchestrationResponse`: rejected because response metadata should be safe and bounded.
- Storing RAG fragments in Memory Engine automatically: rejected because document context and conversation memory have different retention and privacy rules.
- Adding rerankers, GraphRAG, tool calling, or UI citations now: rejected because this sprint is a foundation increment.

## Consequences

Positive consequences:

- AI orchestration can use KnowledgeHub context without provider coupling.
- Prompt Engine, Memory Engine, and RAG Engine can coexist.
- Knowledge retrieval is optional and disabled by default.
- Tests can verify RAG logic with fake searchers and PostgreSQL/pgvector integration.

Limits:

- No hybrid search, external reranker, GraphRAG, UI source navigation, public citation URLs, or trading business logic is added.
- Citations are internal prompt/result identifiers only.
- Tenant filters are carried by request contracts, but KnowledgeHub persistence does not yet enforce tenant-scoped source storage.
