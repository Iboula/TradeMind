# Expert Agent SDK

The Expert Agent SDK is the provider-agnostic contract used by future ICT, SMC, Wyckoff, Macro, Risk and other expert agents.

## Boundary

An expert agent receives only an immutable `MarketContext` and an immutable `AgentExecutionRequest`. It returns an immutable, structured `AgentAnalysisResult`. The SDK has no dependency on PostgreSQL, EF Core, HTTP, MT5, KnowledgeHub, Memory Engine or a concrete LLM provider, and it never executes trades or persists state.

The repository also contains a generic `TradeMind.AI.Agents` framework for LLM orchestration. The expert SDK deliberately does not reference that module: its dependency graph must remain free of Memory Engine, KnowledgeHub and concrete model/provider dependencies. The small `AgentId`, `AgentVersion` and `AgentVersionSelection` value-object set is therefore owned by this bounded context and follows the same semantic-version and lowercase-kebab-case invariants. `AgentRunId` is specific to one expert analysis and is also defined here.

## Pipeline

`MarketContext -> registry resolution -> authorization -> compatibility -> bounded timeout/cancellation -> agent -> result validation and deterministic normalization -> AgentAnalysisResult`.

Authorization and compatibility are separate replaceable policies. The default authorization policy is intentionally conservative for disabled and experimental agents and checks permissions, tenant and user allow-lists. The compatibility policy checks instrument, timeframe, analysis mode, context schema, required context categories, quality, freshness and optional size limits.

## Versioning

The registry is built once as an immutable snapshot. Multiple versions of an agent id are supported, duplicate `(AgentId, AgentVersion)` registrations are rejected, and resolution supports exact, latest and latest stable selection. Discovery is ordered by agent id and descending semantic version.

## Results and safety

`AgentAnalysisResult` contains directional bias, explained confidence, observations, evidence, market levels, scenarios, risks, invalidations, warnings, limitations and structured errors. Evidence points to existing `ContextSourceReference` values and the validator rejects unknown references. Collection and text limits are configured through `ExpertAgentOptions`; when truncation is enabled, ordering is preserved and an explicit warning is added. No prompts, full contexts, memory, secrets or technical payloads are logged.

## DI

Call `AddTradeMindExpertAgents` from composition root and register concrete expert agents as `IExpertAgent` services. The registry is a singleton immutable snapshot; the executor is scoped and never captures a scope in a singleton. No agent implementation is shipped by this sprint.
