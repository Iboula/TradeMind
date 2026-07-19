# AI Orchestration

TradeMind AI orchestration is the provider-agnostic application pipeline that turns a logical product request into a normalized chat response.

It lives in `TradeMind.AI.Application` and depends on `TradeMind.AI.Abstractions` plus the provider-agnostic Tool Engine contracts used by its optional explicit-tool adapter. It does not depend on OpenAI, provider infrastructure, EF Core, Npgsql, HTTP clients, or KnowledgeHub infrastructure.

## Objective

The orchestration layer gives future AI features one common flow for:

- structural request validation;
- prompt construction;
- provider capability checks;
- provider execution through `IChatProvider`;
- response normalization;
- structured operational logging;
- optional extension by modules such as Memory, Knowledge RAG, and controlled explicit tools.

The layer does not implement provider-native tool calling, autonomous tool selection, streaming, multi-agent behavior, or trading business rules.

## Public contracts

`IAIOrchestrator` exposes:

```csharp
Task<AIOrchestrationResponse> ExecuteAsync(
    AIOrchestrationRequest request,
    CancellationToken cancellationToken);
```

`AIOrchestrationRequest` carries an optional system instruction, user message, logical scenario, optional logical model, optional temperature, optional token limit, read-only metadata, optional session id, optional conversation id, optional correlation id, optional identity context, optional prompt template selection, an opt-in `UseMemory` flag, disabled-by-default `AIKnowledgeOptions`, and disabled-by-default explicit `AIToolInvocationOptions`.

`AIOrchestrationResponse` carries session id, optional conversation id, correlation id, scenario, provider, model, text content, token usage, total duration, optional provider duration, executed steps, UTC completion date, execution state, optional response id, safe Knowledge metadata, and safe tool metadata. It never exposes complete tool arguments or output.

## Pipeline

Pipeline steps implement `IAIOrchestrationStep`. They are registered through dependency injection, sorted by `Order`, and executed sequentially.

```mermaid
flowchart LR
    Request["AIOrchestrationRequest"] --> Validation["RequestValidationStep"]
    Validation --> MemoryRead["MemoryReadStep optional"]
    MemoryRead --> Knowledge["KnowledgeRetrievalStep optional"]
    Knowledge --> Prompt["PromptConstructionStep"]
    Prompt --> ToolExecution["ToolExecutionStep optional explicit call"]
    ToolExecution --> ToolComposition["ToolResultCompositionStep optional"]
    ToolComposition --> Capabilities["ProviderCapabilityValidationStep"]
    Capabilities --> Provider["ProviderExecutionStep"]
    Provider --> MemoryWrite["MemoryWriteStep optional"]
    MemoryWrite --> Normalize["ResponseNormalizationStep"]
    Normalize --> Response["AIOrchestrationResponse"]
```

## Steps

`RequestValidationStep` validates non-empty user message, non-empty scenario, temperature range, positive token limit, and reasonable correlation id length.

`PromptConstructionStep` runs registered `IAIContextContributor` instances, renders a prompt template through `IPromptRenderer` when one is requested, otherwise uses the legacy `IPromptBuilder` path, and attaches safe session, correlation, scenario, conversation, tenant, user, and agent metadata when present.

When Memory Engine is registered and the request opts in, `MemoryReadStep` runs before prompt construction. It contributes provider-agnostic memory messages through `AIExecutionContext.Items`; `PromptConstructionStep` inserts them before the current user message. `MemoryWriteStep` runs after provider success and writes the current user message plus assistant response.

When Knowledge RAG Engine is registered and `Knowledge.Enabled` is true, `KnowledgeRetrievalStep` runs before prompt construction. It retrieves KnowledgeHub fragments, composes a delimited context message, and stores safe citation ids and counts for response normalization.

When Tool Engine orchestration is registered and `Tool.Enabled` is true, `ToolExecutionStep` executes the explicitly named tool after prompt construction. `ToolResultCompositionStep` inserts only successful structured output before the current user message as bounded, untrusted external data. The model does not select the tool. Authorization failures always stop provider invocation; configured non-security failures may continue without a tool result.

`ProviderCapabilityValidationStep` checks `IAIProviderMetadata.Capabilities.SupportsChat`. It does not inspect concrete provider types.

`ProviderExecutionStep` calls `IChatProvider.CompleteAsync`, passes the caller's `CancellationToken`, measures provider duration, and stores the normalized provider response.

`ResponseNormalizationStep` copies provider response content, provider name, model, usage, response id, execution identifiers, durations, state, executed step names, Knowledge counters, and safe tool status into `AIOrchestrationResponse`.

## Context

`AIExecutionContext` is the per-request working state. It contains `AISession`, the original request, `ChatRequest`, `ChatResponse`, final response, metrics, executed step list, state, optional safe error, optional tool result and safe tool status, and a controlled typed `Items` dictionary for extension data.

The context does not contain services and must not become a service locator.

## Prompt Builder

`IPromptBuilder` builds `ChatRequest` values for the legacy path. `PromptBuilder` supports system, user, assistant, and context messages, preserves insertion order, ignores absent optional content, and requires at least one user message.

The Prompt Engine is the preferred path for reusable prompt definitions. It renders versioned templates into provider-independent messages before `PromptConstructionStep` adapts them to `ChatRequest`.

Prompt construction does not include trading-specific rules. Product modules should supply those rules through request content or later dedicated context contributors.

## Provider Selection

Provider selection remains in `TradeMind.AI.Infrastructure` through `AddTradeMindAI(configuration)`. The orchestration layer consumes the active `IChatProvider` and `IAIProviderMetadata` already registered by DI.

No `IAIProviderRegistry` is currently added because the application layer only needs the active chat provider and metadata. A registry can be introduced later when multiple active providers, model routing, or per-scenario provider selection become real requirements.

## Capabilities

Capabilities are checked through `IAIProviderMetadata`. Chat orchestration requires `SupportsChat`. Future embedding, vision, streaming, or tool-calling flows should add explicit checks before invoking provider capabilities.

## Logging

The orchestrator logs session creation, start, step start, step completion, provider execution, completion, cancellation, and failure using structured logs. Logs include session id, correlation id, conversation id, tenant id, user id, agent id, scenario, logical model, step count, provider, state, total duration, and failed step where relevant.

Logs must not include full prompts, user messages, responses, API keys, document content, embeddings, or secrets.

## Errors

Validation errors throw `AIOrchestrationValidationException`.

Capability failures throw `AIProviderCapabilityException`.

Provider execution failures are wrapped in `AIOrchestrationException` while preserving `InnerException`.

## Extension Points

`IAIContextContributor` allows future modules to enrich orchestration context before prompt construction. The pipeline operates normally when no contributors are registered.

Potential contributors include:

- Memory Engine context retrieval;
- KnowledgeHub semantic context;
- risk policy hints;
- journal summaries;
- strategy review evidence.

## Memory And Knowledge Integration

Memory Engine is optional and lives in `TradeMind.AI.Memory`. `AddTradeMindAIOrchestration()` remains usable without memory. `AddTradeMindMemory()` adds memory read/write steps and in-memory services.

Knowledge RAG is optional and lives in `TradeMind.AI.Knowledge`. `AddTradeMindKnowledgeRag()` adds retrieval and composition without requiring KnowledgeHub Infrastructure from AI Application.

## Tool Engine Integration

Tool Engine is optional and lives in `TradeMind.AI.Tools`. `AddTradeMindAIToolOrchestration()` registers the core engine and the two application pipeline steps. Disabled requests do not access the registry or executor. See [Tool Engine](TOOL_ENGINE.md) for authorization, validation, timeout, result composition, and security policy.
