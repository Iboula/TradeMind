# ADR-0005: AI orchestration pipeline

## Status

Accepted.

## Context

TradeMind needs more than direct chat and embedding provider calls. AI features such as coaching, journal analysis, market analysis, memory, KnowledgeHub enrichment, risk review, and strategy review need a consistent application-level flow that can validate requests, build prompts, check provider capabilities, call the active provider, normalize responses, and expose operational logs.

ADR-0004 established provider-independent contracts in `TradeMind.AI.Abstractions` and isolated OpenAI in `TradeMind.AI.Infrastructure`. The next layer must stay provider-agnostic. It should depend on abstractions, not SDK types, HTTP clients, EF Core, Npgsql, or KnowledgeHub storage.

## Decision

TradeMind will add `TradeMind.AI.Application` as the application layer for AI orchestration.

The orchestration entry point is `IAIOrchestrator.ExecuteAsync(AIOrchestrationRequest, CancellationToken)`. The request describes a logical scenario, optional system instruction, user message, optional logical model, generation limits, metadata, and correlation id. The response normalizes provider content, provider name, model, token usage, correlation id, total duration, executed pipeline steps, UTC generation date, and optional provider response id.

The pipeline is composed from `IAIOrchestrationStep` registrations loaded through dependency injection and sorted by `Order`. The first increment registers these steps:

1. `RequestValidationStep`
2. `PromptConstructionStep`
3. `ProviderCapabilityValidationStep`
4. `ProviderExecutionStep`
5. `ResponseNormalizationStep`

Steps execute sequentially and receive the caller's `CancellationToken`. The orchestrator records executed step names, stops on failure, and does not swallow exceptions. Provider execution errors are wrapped in `AIOrchestrationException` while preserving the original `InnerException`.

`IPromptBuilder` builds `ChatRequest` instances from `TradeMind.AI.Abstractions`. It preserves message order, ignores absent optional content, and requires at least one user message.

`IAIContextContributor` is introduced as a small extension point. Memory, KnowledgeHub, or other modules may later enrich the AI execution context before prompt construction without coupling the AI application layer to module infrastructure. The pipeline works with no contributors registered.

## Error handling

The first increment uses three orchestration exceptions:

- `AIOrchestrationException` for pipeline and provider execution failures.
- `AIProviderCapabilityException` when the active provider lacks a required capability.
- `AIOrchestrationValidationException` for structural request validation errors.

Exception messages must not include API keys, full prompts, full responses, embeddings, documents, or sensitive trading journal content.

## Extensible behaviors

The pipeline is extensible through DI registration order values, not reflection or static discovery. New steps can be added when a feature needs cross-cutting behavior such as policy checks, context contribution, model routing, retries, or response post-processing.

Provider capabilities remain exposed by `IAIProviderMetadata`. For this increment, the existing active provider registrations are enough, so no `IAIProviderRegistry` is introduced.

## Alternatives rejected

- Calling `IChatProvider` directly from every module: rejected because validation, logging, capabilities, prompt construction, and response normalization would be duplicated.
- Placing orchestration in `TradeMind.AI.Infrastructure`: rejected because orchestration is application behavior and must not depend on OpenAI or provider details.
- Using a MediatR pipeline behavior for AI orchestration: rejected because MediatR is not currently part of the repository and the AI pipeline has its own domain-specific state.
- Discovering steps through reflection: rejected because DI registration is explicit, testable, and simpler.
- Adding persistent memory, RAG, streaming, tool calling, or multi-agent execution now: rejected because they require separate product and architecture decisions.

## Consequences

Positive consequences:

- AI features get one provider-agnostic orchestration surface.
- OpenAI remains isolated in Infrastructure.
- Tests can verify AI flow with fake providers and no network calls.
- Logs include correlation, scenario, provider, model, step count, duration, and failure step without prompt or secret content.
- KnowledgeHub and Memory have a clean extension point without direct coupling.

Tradeoffs:

- There is one more project in the AI module.
- The pipeline introduces a small amount of ceremony before the first product feature consumes it.
- Model routing remains logical and simple until multiple providers or model catalogs exist.
- Provider retries, timeouts, streaming, tool calling, and memory persistence are outside the first increment.
