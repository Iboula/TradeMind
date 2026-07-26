# ADR-0004: AI provider abstraction

## Status

Accepted.

## Context

TradeMind needs AI capabilities for chat, embeddings, KnowledgeHub retrieval, memory, coaching, market analysis, strategy workflows, and future product modules. The project must be able to use OpenAI first while avoiding a design that couples Domain, Application, or KnowledgeHub business logic to one concrete provider SDK.

KnowledgeHub already has an `IEmbeddingGenerator` abstraction that its application service consumes. That contract is kept stable for compatibility, and provider-agnostic AI abstractions are introduced alongside it.

## Decision

TradeMind will define provider-independent AI contracts in `TradeMind.AI.Abstractions`.

The initial contracts are:

- `IChatProvider`
- `IEmbeddingProvider`
- `IAIProviderMetadata`
- chat request and response records
- embedding request and response records
- immutable provider capabilities

Concrete SDKs live only in `TradeMind.AI.Infrastructure`. The first implementation is OpenAI, using the official `OpenAI` NuGet package. No OpenAI type is exposed through the abstractions.

Provider selection is configuration-based through the `AI` section. `AddTradeMindAI(configuration)` binds and validates options, compares provider names case-insensitively, and fails clearly for empty or unknown providers.

KnowledgeHub keeps `IEmbeddingGenerator` as its internal application port. `AIEmbeddingGeneratorAdapter` bridges `IEmbeddingProvider` to `IEmbeddingGenerator` so KnowledgeHub can use provider-agnostic embeddings without referencing `TradeMind.AI.Infrastructure`.

## Consequences

Positive consequences:

- Domain and Application code remain independent of concrete AI SDKs.
- OpenAI can be replaced or complemented later without changing KnowledgeHub business logic.
- Provider capabilities can be inspected without type checks.
- Configuration owns model names and provider selection.
- Tests can validate composition without real network calls or secrets.

Tradeoffs:

- There is one additional abstraction layer.
- The first OpenAI adapter does not implement streaming or tool calling even though the SDK supports those scenarios.
- KnowledgeHub still has its existing `IEmbeddingGenerator` port, so an adapter is needed during the transition.
- OpenAI embedding dimensions are provider and model dependent; KnowledgeHub persistence must be evolved before switching the live pipeline from deterministic 64-dimensional embeddings to larger production embeddings.

## Alternatives rejected

- Referencing the OpenAI SDK directly from KnowledgeHub Application: rejected because it would couple business logic to a provider.
- Creating abstractions for image and audio providers now: rejected because no current feature consumes them.
- Supporting Azure OpenAI, Ollama, Anthropic, Gemini, or other providers in this sprint: rejected to keep the implementation focused.
- Silently defaulting to OpenAI for invalid configuration: rejected because configuration errors must fail clearly.
