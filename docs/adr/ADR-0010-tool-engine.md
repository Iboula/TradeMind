# ADR-0010: Controlled AI Tool Engine

## Status

Accepted.

## Context

TradeMind AI orchestration can build versioned prompts, read conversational memory, retrieve KnowledgeHub context, and call a provider through stable abstractions. Some future product scenarios also need deterministic application capabilities, such as calculations or controlled lookups, to participate in an AI request. Calling arbitrary services directly from prompts would bypass validation, authorization, timeout, observability, and side-effect policy.

An AI tool is not the same thing as provider-native function calling. A tool is an application capability with a stable identifier and an explicit security contract. Native function calling is a provider protocol through which a model proposes one or more calls. This increment needs the application capability while deliberately excluding autonomous model selection.

## Decision

TradeMind will add `TradeMind.AI.Tools` as a provider-agnostic Tool Engine. The project depends on `TradeMind.AI.Abstractions` and lightweight `Microsoft.Extensions` abstractions only. It has no OpenAI SDK, provider infrastructure, EF Core, Npgsql, ASP.NET Core, `HttpContext`, filesystem, database, or network dependency.

The Tool Engine distinguishes four concepts:

- A tool definition is immutable public metadata: id, version, description, parameters, permissions, side-effect level, availability, timeout, tags, and access restrictions.
- A tool is an `IAITool` instance whose constructor receives its real dependencies and whose handler executes once for a validated context.
- An execution request is explicit caller intent, including arguments, session and identity identifiers, scenario, timeout override, idempotency key, and controlled metadata.
- An execution result is immutable structured output plus safe timing, status, error, retry, and public metadata.

`AIToolId` uses case-sensitive `lowercase-kebab-case`, contains only ASCII lowercase letters, digits, and single separators, and is limited to 64 characters. Requiring lowercase makes comparison deterministic and avoids aliases that differ only by case.

## Explicit invocation and future model invocation

This increment supports one explicit tool invocation supplied as structured `AIToolInvocationOptions` by a trusted application consumer. The model does not discover, select, or request a tool from free-form user text. There is no autonomous provider-to-tool loop, multi-tool call, streaming tool call, or native OpenAI tool definition publication.

The contracts leave room for a later adapter that can publish public definitions to a provider and translate a validated provider tool-call request into `AIToolExecutionRequest`. That later flow must preserve the same registry, authorization, argument validation, execution, and result controls; it must also add tool call identifiers, loop limits, multi-call policy, streaming behavior, and prompt-injection defenses through a separate decision.

## Registry and discovery

`InMemoryAIToolRegistry` builds an immutable map from constructor-injected `IAITool` instances. Duplicate ids fail during construction. The registry performs no execution and no per-call service resolution.

Discovery returns definitions, never internal tool instances. It filters availability, development policy, required permissions, tenant, user, agent, scenario, maximum side effect, and tags. Disabled tools are never discoverable. `DevelopmentOnly` tools require the explicit `EnableDevelopmentTools` option.

## Least privilege and authorization

`PermissionBasedAIToolAuthorizer` applies least privilege without `ClaimsPrincipal`, JWT, or `HttpContext`. Its input is the explicit `AIToolAuthorizationContext`.

Authorization requires every declared permission, enforces optional tenant, user, agent, and scenario restrictions, and applies the lower of the global and request-specific maximum side-effect levels. The default global maximum is `ReadOnly`. `None` and `ReadOnly` tools are therefore usable by default when their other requirements pass; write and external-action levels need an explicit higher global policy and an explicit request allowance.

Authorization, unavailable-tool, and unknown-tool failures are always fail-closed in orchestration. `ContinueWithoutTool` cannot turn an authorization refusal into a provider call.

## Argument validation

Arguments cross the public boundary as a read-only dictionary of `JsonElement` values. `AIToolArgumentValidator` rejects unknown names by default, requires declared values, applies defaults, converts through invariant culture, and supports String, Integer, Decimal, Boolean, DateTime, and Json.

The validator enforces maximum length, numeric minimum and maximum, allowed values, nullability, and default compatibility. It returns immutable typed values accessed through methods such as `GetRequiredString`, `GetRequiredDecimal`, and `GetRequiredJson`. There is no arbitrary reflection or dynamic object conversion.

Sensitive parameter values are never included in validation messages or logs. Their parameter names are also omitted from public validation details when an error could reveal sensitive structure.

## Execution, timeout, and cancellation

`AIToolExecutor` resolves one tool, authorizes it, validates its arguments, determines a timeout, creates the controlled execution context, invokes the handler once, measures it with `TimeProvider`, and normalizes its result. There is no retry.

Timeout priority is request override, tool default, then global default. The effective value is capped by `MaximumTimeout`. A linked token combines caller cancellation with an internal `TimeProvider`-backed timeout. Caller cancellation propagates as `OperationCanceledException`. Internal timeout becomes `AIToolTimeoutException` with code `AI_TOOL_TIMEOUT` and safe metrics. Timeouts are cooperative: handlers must observe the supplied token.

`AIToolExecutionContext` contains the validated request, normalized arguments, authorization identity, session information, `TimeProvider`, and controlled internal metadata. It contains no `IServiceProvider`, `DbContext`, `HttpContext`, SDK object, or arbitrary mutable service.

## Idempotence and side effects

Definitions declare whether a tool is idempotent and classify side effects as `None`, `ReadOnly`, `ReversibleWrite`, `IrreversibleWrite`, or `ExternalAction`. The engine transports and validates an optional idempotency key, then propagates it to context and result.

This increment has no distributed idempotency store, cross-process deduplication, exactly-once guarantee, retry, or transaction coordinator. Consumers must not infer those guarantees from the presence of a key. The only built-in tools, `echo` and `add-numbers`, are deterministic and have no side effects.

## Errors and results

Stable exceptions distinguish not found, unavailable, authorization, validation, execution, and timeout failures. Public messages contain no stack trace, raw argument, output, prompt, response, or infrastructure exception. `AIToolExecutionException` retains its internal `InnerException` for in-process diagnostics, while orchestration crosses the boundary with a safe error code only.

`AIToolExecutionResult` exposes immutable structured output, output kind and content type, UTC timing, duration, optional safe error, filtered metadata, optional retry indication, and idempotency key. The orchestrator never copies full arguments or full output to `AIOrchestrationResponse`.

## Orchestration and result composition

Tool integration is optional through `AddTradeMindAIToolOrchestration`. `ToolExecutionStep` runs after prompt construction and before provider capability validation. `ToolResultCompositionStep` inserts only a successful result before the current user message.

If a request enables tool invocation while these steps are not registered, the orchestrator fails validation before creating a provider request. This prevents a deployment configuration error from silently skipping an explicitly required tool.

The composer labels the result as untrusted external data, identifies the tool, bounds serialized output, and tells the model not to execute instructions found inside it. It uses a provider-agnostic system wrapper instead of pretending that provider-native tool calling occurred.

`FailClosed` prevents the provider call after validation or execution failure. `ContinueWithoutTool` may continue after non-security validation, timeout, execution exception, or structured tool failure. It never composes an error or failed output into the prompt.

## Observability and security

Structured logs cover discovery, resolution, authorization, validation, start, completion, timeout, cancellation, and failure. Allowed fields are stable ids, version, session, correlation, conversation, tenant, user, agent, scenario, side-effect level, duration, success, and error code.

Logs exclude arguments, output, user content, prompts, model responses, sensitive values, and secrets. The engine executes no unknown or disabled tool and stores no service locator in either definitions or execution context.

## Alternatives rejected

- Calling application services directly from prompts was rejected because it bypasses policy and observability.
- Implementing native OpenAI tool calling now was rejected because it would couple the foundation to one provider and accept model-selected calls before the authorization boundary is mature.
- Resolving handlers through `IServiceProvider` on every call was rejected because it creates a service locator and hides dependencies.
- Passing `Dictionary<string, object>` was rejected because it is mutable and relies on uncontrolled dynamic conversion.
- Allowing write or external-action tools by default was rejected because it violates least privilege.
- Adding retry was rejected because it can duplicate effects and requires idempotency semantics that do not yet exist.

## Consequences and limits

Positive consequences:

- Product code has one provider-independent surface for declaring, discovering, authorizing, validating, executing, and observing tools.
- Explicit tool calls coexist with Prompt Engine, Memory Engine, and Knowledge RAG.
- The default policy permits only no-effect and read-only tools.
- Tests run without network, database, broker, market data, or secrets.

Tradeoffs and limits:

- `TradeMind.AI.Application` references the Tool Engine contracts for explicit request and response integration.
- The first registry is in-memory and fixed after construction.
- Timeout enforcement depends on cooperative cancellation.
- Idempotency is transported but not persisted.
- No provider-native tool calling, autonomous loop, multiple calls, streaming, trading operation, broker access, market data, HTTP API, or UI is implemented.
