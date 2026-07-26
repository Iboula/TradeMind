# AI Providers

TradeMind isolates AI providers behind provider-agnostic contracts.

## Architecture

The AI foundation has two projects:

- `TradeMind.AI.Abstractions`: public contracts with no provider SDK dependency.
- `TradeMind.AI.Application`: provider-agnostic orchestration, prompt construction, validation, capability checks, and response normalization.
- `TradeMind.AI.Infrastructure`: concrete provider adapters, options, validation, logging, and SDK integration.

Domain projects must not reference AI SDKs. Application code should depend on abstractions or existing module ports. Infrastructure owns concrete clients and provider-specific mapping.

## Interfaces

The current abstraction surface is intentionally small:

- `IChatProvider` completes chat requests.
- `IEmbeddingProvider` generates embeddings for one or more texts.
- `IAIProviderMetadata` exposes provider name and capabilities.

The contract model includes `ChatRole`, `ChatMessage`, `ChatRequest`, `ChatResponse`, `ChatUsage`, `EmbeddingRequest`, `EmbeddingResponse`, and `AIProviderCapabilities`.

No OpenAI type appears in these contracts.

## Infrastructure role

`TradeMind.AI.Infrastructure` contains:

- `OpenAIChatProvider`
- `OpenAIEmbeddingProvider`
- `OpenAIProviderMetadata`
- `AIOptions`
- `OpenAIOptions`
- `AIConfigurationException`
- `AIProviderException`
- `AddTradeMindAI`

The OpenAI SDK is isolated inside this project. Providers log provider name, model, request shape, and success or failure without logging prompts, responses, or API keys by default.

## Provider selection

Provider selection uses configuration:

```json
{
  "AI": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "",
      "ChatModel": "gpt-4.1-mini",
      "EmbeddingModel": "text-embedding-3-small"
    }
  }
}
```

`AddTradeMindAI(configuration)` validates the section and registers:

- `IChatProvider -> OpenAIChatProvider`
- `IEmbeddingProvider -> OpenAIEmbeddingProvider`
- `IAIProviderMetadata -> OpenAIProviderMetadata`

Provider names are compared without case sensitivity. Empty or unknown provider values fail explicitly.

## Orchestration usage

AI product features should call `IAIOrchestrator` rather than calling `IChatProvider` directly. The orchestrator builds a `ChatRequest`, validates chat capability through `IAIProviderMetadata`, calls the active provider, and returns `AIOrchestrationResponse`.

Composition can register providers and orchestration together:

```csharp
services.AddTradeMindAI(configuration);
services.AddTradeMindAIOrchestration();
```

The orchestration layer does not select concrete SDK clients. It consumes the provider already registered by Infrastructure.

## Capabilities

Capabilities are represented by `AIProviderCapabilities`:

- `SupportsChat`
- `SupportsEmbeddings`
- `SupportsStreaming`
- `SupportsToolCalling`
- `SupportsVision`

Consumers should use capabilities rather than checking concrete provider types.

## KnowledgeHub integration

KnowledgeHub keeps `IEmbeddingGenerator` as the application port consumed by `KnowledgeHubService`. The bridge to the new abstraction is `AIEmbeddingGeneratorAdapter`, which implements `IEmbeddingGenerator` and delegates to `IEmbeddingProvider`.

The default KnowledgeHub registration still uses deterministic 64-dimensional embeddings. This preserves current tests and the existing PostgreSQL `vector(64)` schema. Switching the live KnowledgeHub pipeline to OpenAI embeddings requires a deliberate persistence decision because OpenAI embedding dimensions differ from the current schema.

## Adding a provider

To add another provider:

1. Implement only the interfaces the provider supports.
2. Add provider-specific options in Infrastructure.
3. Extend provider selection by configuration.
4. Add metadata capabilities.
5. Add DI and validation tests.
6. Keep SDK types out of `TradeMind.AI.Abstractions`, Domain, and Application projects.

Do not add empty provider classes for providers that are not being integrated.

## Secret management

API keys must not be committed. The OpenAI API key can be supplied through:

- environment variable `AI__OpenAI__ApiKey`
- user secrets
- future Kubernetes secrets
- future secret-management infrastructure

The empty value in checked-in configuration is only a safe local override target and must not be treated as a valid credential.
