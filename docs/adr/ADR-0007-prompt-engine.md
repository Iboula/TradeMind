# ADR-0007: Prompt Engine

## Status

Accepted.

## Context

The AI orchestration pipeline can build chat prompts directly from a system instruction and user message. That is enough for the first provider call, but future AI features need repeatable prompt definitions that can be versioned, validated, rendered, tested, and selected without coupling the application layer to a concrete provider SDK.

Prompt definitions must not be hidden inside orchestration code. They need explicit variables, safe validation, stable versioning, and deterministic rendering. This sprint does not add prompt persistence, a prompt editor, a user interface, RAG, Memory Engine, Tool Engine, or trading-specific templates.

## Decision

TradeMind will add a provider-agnostic Prompt Engine inside `TradeMind.AI.Application`.

The first increment introduces:

- `PromptTemplate`
- `PromptTemplateId`
- `PromptTemplateVersion`
- `PromptMessageTemplate`
- `PromptMessageRole`
- `PromptVariableDefinition`
- `PromptVariableType`
- `PromptRenderRequest`
- `PromptRenderResult`
- `IPromptTemplateRegistry`
- `InMemoryPromptTemplateRegistry`
- `IPromptRenderer`
- `PromptRenderer`

Templates use a minimal deterministic variable syntax: `{{variableName}}`. Variables must be declared before they can be rendered. Unknown variables are rejected. Missing required variables are rejected. Unresolved placeholders are rejected after rendering.

Prompt templates are versioned with `major.minor`, such as `1.0`, `1.1`, and `2.0`. The registry supports explicit version lookup and latest active version lookup. The first registry is in-memory, thread-safe for reads, and populated through dependency injection.

The Prompt Engine remains independent of OpenAI, infrastructure, EF Core, Npgsql, ASP.NET Core, file storage, and databases. It renders provider-independent prompt messages. `PromptConstructionStep` adapts rendered prompt messages to `ChatRequest` only when building the provider request.

## Prompt, template, variables, and rendered messages

A prompt is the actual message sequence sent to a chat provider.

A template is a named and versioned definition for producing a prompt.

Variables are declared inputs with type, required/default behavior, sensitivity, and optional length limit.

Rendered messages are provider-independent role/content values produced after validation and substitution.

## Security

Sensitive variable values are used for rendering but are not exposed in `PromptRenderResult.VariablesUsed`, exception variable details, or structured logs. Logs include template id, version, scenario, correlation id, duration, message count, and variable count, but never rendered content or variable values.

This engine does not claim to solve business prompt injection. Prompt injection controls will be a separate layer when product flows need them.

## Orchestrator integration

`AIOrchestrationRequest` can optionally carry `PromptTemplateId`, `PromptTemplateVersion`, and prompt variables. When a template id is provided, `PromptConstructionStep` renders it through `IPromptRenderer` and adapts the rendered messages into `ChatRequest`. When no template id is provided, the previous system-instruction and user-message path remains available.

The first built-in template is `generic-chat` version `1.0` for scenario `GenericChat`.

## Alternatives rejected

- Keeping prompts only in code: rejected because versioning, validation, and reuse would be weak.
- Adding Razor, Scriban, Liquid, or another templating engine now: rejected because a small deterministic syntax is enough for the first increment and avoids executable templates.
- Storing templates in files or a database now: rejected because persistence and authoring workflows need separate decisions.
- Adding trading-specific templates now: rejected because this sprint is a foundation increment.
- Letting templates depend on `ChatRequest` or OpenAI types: rejected because templates must stay provider-agnostic.

## Consequences

Positive consequences:

- Prompts can be named, versioned, validated, rendered, and tested.
- Orchestration can gradually move away from direct prompt construction.
- Unknown variables and unresolved placeholders fail early.
- Sensitive variables have safer logging and result behavior.
- The first generic template is available through DI without persistence.

Tradeoffs and limits:

- The in-memory registry is not a management or persistence solution.
- No prompt authoring UI exists.
- No prompt injection policy is implemented.
- No business templates are introduced.
- The renderer supports a small variable syntax only.
