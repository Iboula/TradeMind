# ADR 0005: AI provider boundary

- Status: Accepted
- Date: 2026-07-17

## Context

TradeMind will use multiple AI capabilities over its lifetime, including embeddings, retrieval, analysis and strategy assistance. Coupling domain code to a specific provider would make the platform expensive to test and difficult to evolve.

## Decision

AI providers are infrastructure adapters. Application use cases depend on narrow interfaces such as `IEmbeddingGenerator`. Domain projects contain no provider SDK references, model names, API keys or transport concerns.

The KnowledgeHub MVP uses a deterministic `FakeEmbeddingGenerator`. Production adapters will be selected through dependency injection and configuration.

## Consequences

The platform remains testable without network access or credentials. Replacing a provider does not change domain behavior or public APIs. Persisted embeddings must include provider and model metadata before multiple embedding models are supported, because vectors from different models are not directly comparable.
