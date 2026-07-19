# ADR-0011: Versioned AI Agent Framework

## Status

Accepted.

## Context

TradeMind has provider-independent chat contracts and separate engines for orchestration, prompts, conversational memory, KnowledgeHub RAG, and controlled tools. Product features need a stable way to combine those engines into named AI behaviors without copying orchestration code or binding a feature to OpenAI.

A prompt alone cannot express availability, permissions, tenant restrictions, supported scenarios, timeouts, memory retention behavior, Knowledge budgets, or tool side-effect limits. The orchestrator cannot own those product-specific choices because it is intentionally generic. A tool is also narrower than an agent: it performs one controlled application capability and does not define the complete AI interaction.

## Decision

TradeMind will add `TradeMind.AI.Agents` as a provider-agnostic composition module. An agent is an immutable, versioned declaration of identity, purpose, capabilities, policies, permissions, scenarios, and execution limits. It is not an autonomous process.

The framework depends on the public contracts of AI Abstractions, AI Application, Prompt Engine, Memory Engine, Knowledge RAG, and Tool Engine. It does not depend on a provider SDK, EF Core, Npgsql, pgvector, ASP.NET Core, `HttpContext`, `ClaimsPrincipal`, filesystem, network, or concrete infrastructure.

The distinction between the main concepts is:

- An agent definition declares which behavior and privileges are allowed for a named version.
- `IAIAgentExecutor` resolves, authorizes, maps, executes, measures, and normalizes one request.
- `IAIOrchestrator` remains the generic pipeline that invokes context engines and one chat provider.
- A prompt template defines provider-independent messages and variables.
- A tool defines one explicit, validated application capability with a side-effect classification.

## Identity and versioning

`AIAgentId` is case-sensitive lowercase-kebab-case ASCII, limited to 64 characters. Lowercase canonical ids avoid aliases that differ only by case.

`AIAgentVersion` implements semantic `major.minor.patch` ordering and supports an optional pre-release suffix. Resolution is always explicit:

- `Exact` requires an exact version.
- `Latest` selects the highest semantic version, including a pre-release.
- `LatestStable` selects the highest version without a pre-release.

No implicit exact version or lexical version comparison is allowed.

## Registry and discovery

`InMemoryAIAgentRegistry` snapshots constructor-injected agent instances into immutable version lists. Duplicate id and version pairs fail during construction. Resolution performs no dynamic `IServiceProvider` lookup and the registry has no global static state.

Discovery returns immutable definitions, never executable instances. It filters disabled and development-only definitions, permissions, tenant, user, scenario, capabilities, tags, availability, and maximum side-effect level. Development agents require both framework configuration and an explicit discovery request.

## Authorization and policies

`PolicyBasedAIAgentAuthorizer` consumes explicit identity and permission values. It does not read ambient HTTP or claims state. Availability, required permissions, tenant and user allowlists, scenario, requested capabilities, side effects, timeout, and all request overrides are checked before orchestration.

An agent policy aggregates:

- prompt template, static instruction, variables, overrides, and character budget;
- optional or required memory, maximum window, save behavior, compaction, and failure mode;
- optional or required Knowledge retrieval, result and context budgets, filters, query policy, citations, and failure mode;
- explicit allowed tools, permissions, scenarios, timeout, idempotency, failure mode, and side-effect ceiling;
- overall timeout, context budget, failure mode, and side-effect ceiling.

Request values may narrow an agent policy but cannot widen it. A request cannot activate a forbidden engine, disable a required engine, enlarge a memory or Knowledge budget, inject an unapproved filter, replace a protected prompt template, add a tool permission, invoke an unlisted tool, weaken a fail-closed policy, exceed a timeout, or increase side effects.

`ContinueWithReducedCapabilities` applies only to optional Memory, Knowledge, or Tool failures through their existing continuation modes. It never bypasses authorization, permissions, tenant restrictions, disabled availability, or side-effect policy.

## Execution cycle

The executor performs one bounded cycle:

1. Resolve one agent version.
2. Authorize the definition and request.
3. Create an execution context.
4. Invoke the optional starting hook.
5. Map effective policies to `AIOrchestrationRequest`.
6. Call `IAIOrchestrator` exactly once.
7. Map the normalized response.
8. Invoke the optional completion hook.
9. Return safe metrics and public response data.

On failure, the framework invokes the optional failure hook when an agent and context exist, preserves the primary exception, and performs no retry. Hooks receive no service provider and do not take control of validation, authorization, mapping, orchestration, security, or metrics.

Timeout priority is request override, agent maximum, then framework default, capped by the framework maximum. A `TimeProvider`-backed token distinguishes agent timeout from caller cancellation. Tool and provider timeout semantics remain owned by their respective layers.

## Composition of existing engines

`AIAgentRequestMapper` converts the effective declaration into the existing orchestration contract. Prompt policy becomes template selection and variables. Memory policy becomes request-scoped window, save, compaction, and failure options while retaining compatibility with the existing `UseMemory` flag. Knowledge policy becomes bounded `AIKnowledgeOptions`. Tool policy becomes one explicit `AIToolInvocationOptions`; the model never chooses a tool.

`AIAgentResponseMapper` exposes agent and execution identifiers, content, provider, public citation ids, safe tool id, state, and metrics. It does not expose a rendered prompt, memory content, Knowledge fragments, tool arguments, tool output, internal metadata, or stack traces.

## Built-in definitions

`generic-assistant` version `1.0.0` is the reference composition. It uses the existing generic prompt and permits optional Memory, Knowledge, and explicit `echo` or `add-numbers` tools. Its maximum side effect is `None`.

`trading-coach` version `0.1.0` is a development-only educational skeleton. It has no Memory, Knowledge, tools, market access, broker access, portfolio logic, trading operation, or side effect. Its prompt explicitly avoids financial advice and buy or sell signals.

## Security and observability

Logs contain stable ids, version, identity ids, scenario, state, booleans, duration, provider, and safe error code only. They exclude user messages, prompts, variables, memory, fragments, citations, tool arguments, tool output, full responses, and secrets.

Definitions, requests, discovery results, policies, and public metadata are copied into read-only collections. Sensitive-looking metadata keys are filtered at public boundaries.

## Alternatives rejected

- Putting agent behavior in the orchestrator was rejected because it would mix generic pipeline mechanics with product policy.
- Treating prompts as agents was rejected because prompts do not carry authorization, version resolution, context, or side-effect policy.
- Autonomous model loops were rejected because planning, repeated calls, termination, budget control, and prompt-injection defenses require a separate design.
- Provider-native assistants and tool calling were rejected because they couple behavior to one provider and bypass the current explicit invocation boundary.
- Per-request service-provider resolution was rejected because it hides dependencies and creates a service locator.
- Persisting agent definitions was rejected because lifecycle, tenancy, signing, approval, and marketplace rules are not yet defined.
- Automatic retry was rejected because provider and tool retries need explicit idempotency and cost policy.

## Consequences and limits

Positive consequences:

- Product code can target stable, discoverable, versioned agent identities.
- Existing Prompt, Memory, Knowledge, Tool, and provider abstractions remain the execution engines.
- Security policy is evaluated before any provider call.
- Tests run deterministically without network, database, market data, broker, or secrets.

Tradeoffs and current limits:

- Definitions are registered in memory at process startup.
- The framework performs one orchestration call and one optional explicit tool call.
- Timeout enforcement is cooperative.
- There is no streaming, planner, autonomous loop, multi-agent coordination, persistence, marketplace, HTTP API, UI, market data, broker, portfolio, or real trading behavior.
