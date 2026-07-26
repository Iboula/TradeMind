# ADR-0008: Conversational Memory Engine

## Status

Accepted.

## Context

TradeMind AI sessions need a controlled way to carry conversational continuity across turns. A single prompt can include the current user message, but coaching, journal review, market analysis, and future strategy workflows need recent history, a bounded context window, and a summary of older exchanges without coupling the application layer to a concrete provider, database, web identity system, or KnowledgeHub retrieval.

Conversation memory is different from KnowledgeHub. KnowledgeHub stores and searches user knowledge sources through PostgreSQL and pgvector. Memory Engine stores conversational state scoped to tenant, user, and conversation. It is also different from RAG: it does not search documents, embed content, rank fragments, or call pgvector.

## Decision

TradeMind will add a provider-agnostic Memory Engine in `TradeMind.AI.Memory`.

The module introduces immutable contracts for:

- `ConversationMemoryKey`
- `ConversationMemoryEntry`
- `ConversationMemory`
- `ConversationSummary`
- `MemoryReadRequest`
- `MemoryReadResult`
- `MemoryWriteRequest`
- `MemoryWindowOptions`
- `MemoryRetentionOptions`
- `MemoryCompactionOptions`
- `MemoryOrchestrationOptions`
- `IMemoryStore`
- `IMemoryReader`
- `IMemoryWriter`
- `IConversationSummarizer`
- `ITokenEstimator`

The first implementation is in-memory only. It uses a per-conversation lock strategy, monotonic sequence numbers, immutable snapshots, `TimeProvider`, logical expiration, and a deterministic local summarizer. It is suitable for tests and local development, not for durable production storage or multi-pod deployment.

Memory is scoped by `ConversationMemoryKey`, which contains optional `TenantId`, optional `UserId`, and required `ConversationId`. Tenant and user values are part of the key when provided. There is no fallback from one tenant or user to another.

The context window selects recent entries, preserves chronological order in the final provider request, can include a summary, skips entries already covered by `SummarizedThroughSequence`, and uses a replaceable deterministic token estimator. The default failure mode is `FailClosed`; `ContinueWithoutMemory` is configurable for scenarios where AI execution may continue without memory.

## Orchestrator integration

`TradeMind.AI.Memory` depends on `TradeMind.AI.Application` to provide optional orchestration steps:

1. `ai.memory.read`
2. `ai.memory.write`

The core orchestration project remains independent of the Memory module. `AIOrchestrationRequest.UseMemory` opts a request into memory. Memory read happens before prompt construction and contributes provider-agnostic `ChatMessage` values through `AIExecutionContext.Items`. `PromptConstructionStep` inserts these messages before the current user message, which keeps both the legacy prompt path and Prompt Engine path functional. Memory write happens after a successful provider response and before response normalization.

## Alternatives rejected

- Storing conversation memory in KnowledgeHub: rejected because conversational state and document knowledge have different retention, privacy, and retrieval rules.
- Implementing RAG in this increment: rejected because semantic document retrieval belongs to a separate KnowledgeHub integration.
- Adding PostgreSQL persistence now: rejected because schema, retention, privacy, and migration concerns need their own implementation increment.
- Depending on OpenAI or another tokenizer: rejected because the first sprint needs deterministic provider-agnostic behavior.
- Using a global lock for all conversations: rejected because independent conversations should not block each other.
- Hiding memory failure silently: rejected because memory loss must be explicit through `FailClosed` or configured `ContinueWithoutMemory`.

## Consequences

Positive consequences:

- AI conversations can include recent history and summaries without provider coupling.
- Tenant, user, and conversation isolation is explicit.
- Memory behavior is testable without network, database, or SDK dependencies.
- Future PostgreSQL persistence can implement `IMemoryStore` without changing orchestrator flow.
- Prompt Engine and legacy prompt construction both remain supported.

Limits:

- In-memory storage is lost on restart.
- In-memory storage is not shared across pods.
- The deterministic summarizer is not a production-quality AI summary.
- There is no RAG, pgvector search, trading business logic, UI, HTTP API, Tool Engine, Agent Framework, or Cost Engine in this increment.
