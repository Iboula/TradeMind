# ADR-0006: AI session and execution context

## Status

Accepted.

## Context

AI orchestration now has a provider-agnostic pipeline, but future features need more than a single request and response. Coaching, journal analysis, Memory Engine, KnowledgeHub context enrichment, cost tracking, tenant-aware SaaS behavior, and future agents all need a common way to carry identity, scenario, timing, provider, model, token, step, and error information through one execution.

The system must prepare these concepts without adding persistence, conversation memory, RAG, streaming, tool execution, or trading business logic in this sprint.

## Decision

TradeMind will introduce explicit AI session and execution context objects in `TradeMind.AI.Application`.

`AISession` represents the logical identity of one AI interaction. It contains session id, optional conversation id, correlation id, optional tenant id, optional user id, optional agent id, scenario, UTC creation date, and read-only metadata. `SessionId` and `CorrelationId` are strings so external identifiers can be supported later without forcing GUIDs.

`AIIdentityContext` carries tenant, user, and agent identifiers without depending on `HttpContext`, `ClaimsPrincipal`, JWT, Zitadel, ASP.NET Core, or any identity provider. API mapping from HTTP identity is deliberately deferred.

`AIExecutionContext` replaces the previous orchestration context as the mutable per-execution working state. It contains the session, request, chat request, chat response, final response, metrics, executed steps, state, optional safe error, and controlled internal items. Mutations are performed through explicit methods such as `Start`, `MarkStepStarted`, `SetChatRequest`, `SetChatResponse`, `SetFinalResponse`, `Complete`, `Fail`, and `Cancel`.

`AIExecutionMetrics` tracks UTC start and completion times, total duration, provider duration, prompt construction duration, optional token counts, provider name, and model name. Token values cannot be negative. When input and output tokens are known, total tokens must match their sum.

`AIExecutionError` records a safe public error shape: code, non-sensitive message, optional step name, optional provider name, UTC occurrence date, optional transient flag, exception type, and read-only safe metadata. Stack traces, prompts, responses, documents, secrets, and sensitive personal data are not stored in this contract.

`AIExecutionState` uses controlled transitions between `Created`, `Running`, `Completed`, `Failed`, and `Cancelled`. Terminal states cannot be restarted or completed again.

`IAISessionFactory` creates sessions from `AIOrchestrationRequest`. It generates missing session and correlation identifiers, propagates conversation and identity values, and uses `TimeProvider` for UTC timestamps.

## Session, request, and execution context

The request is caller intent: message, scenario, optional system instruction, model controls, optional identifiers, and metadata.

The session is logical identity: it names the interaction and carries tenant, user, agent, conversation, scenario, and correlation values.

The execution context is runtime state: it is mutable during pipeline execution and records steps, provider result, metrics, state, and error information.

## Identifier propagation

`AIOrchestrationRequest` now supports optional `SessionId`, `ConversationId`, `CorrelationId`, and `AIIdentityContext`. When absent, the session factory generates session and correlation identifiers. Tenant, user, and agent values are copied from `AIIdentityContext` to `AISession` and into provider request metadata where appropriate.

## Metrics and TimeProvider

`TimeProvider` is registered in DI as `TimeProvider.System` and injected into the session factory, orchestrator, and duration-sensitive steps. This keeps timestamps and elapsed-time behavior testable without introducing a custom clock abstraction.

No price or cost calculation is introduced. The metrics shape is prepared so a future Cost Engine can consume provider, model, duration, and token values.

## Metadata confidentiality

Metadata remains read-only at public boundaries. It may carry safe identifiers and operational context, but it must not contain API keys, prompts, full responses, KnowledgeHub documents, certificates, or sensitive personal data. Logs use structured identifiers and durations rather than raw prompt or response content.

## Persistence

No database table, EF Core mapping, event store, memory record, or durable conversation state is added in this sprint. Sessions and execution contexts are in-process runtime objects only.

## Consequences

Positive consequences:

- AI executions now have stable session, correlation, conversation, tenant, user, and agent identifiers.
- Pipeline state and metrics are centralized in one context.
- Completion, failure, and cancellation are distinguished.
- Tests can control session time through `TimeProvider`.
- Future Memory Engine, Cost Engine, and SaaS identity work have a clean integration point.

Tradeoffs:

- AI Application has more contracts before persistent AI features exist.
- The response contract is broader because it now reports session and execution state.
- The context is mutable internally, so mutations must stay controlled by methods and tests.

## Alternatives rejected

- Keeping identifiers only in metadata: rejected because identifiers become too easy to miss, overwrite, or log incorrectly.
- Using `HttpContext` or `ClaimsPrincipal` directly: rejected because AI Application must remain independent of ASP.NET Core and identity providers.
- Persisting sessions now: rejected because storage, retention, privacy, and tenancy rules need their own decision.
- Reusing the previous orchestration context alongside `AIExecutionContext`: rejected because two contexts would duplicate state and create drift.
- Forcing GUID identifiers: rejected because future SaaS or enterprise integrations may provide external string identifiers.
