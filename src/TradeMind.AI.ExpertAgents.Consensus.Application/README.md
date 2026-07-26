# Consensus Engine

The Consensus Engine is the deterministic consolidation boundary for the Expert Agent SDK. It accepts immutable `AgentAnalysisResult` values that already belong to one `MarketContext` and produces an immutable, versioned `ConsensusResult`.

The engine does not execute agents, call an LLM, access PostgreSQL, persist data, expose HTTP, or place trades. Agent execution remains the responsibility of `IExpertAgentExecutor` and dispatch remains the responsibility of the Dispatcher module.

## Pipeline

1. Validate the request version, context identity, duplicate run identifiers, result status, confidence, freshness, schema and fatal errors.
2. Calculate a transparent weight from confidence, data coverage, freshness, evidence quality and `PartiallySucceeded` penalty. Contributions are capped per `AgentId`.
3. Consolidate directional votes, keeping `Neutral`, `Mixed`, `InsufficientData` and `NotApplicable` semantically distinct.
4. Detect directional, level, scenario, invalidation and critical-risk conflicts.
5. Merge only compatible levels and equivalent scenarios. Preserve incompatible variants, material minorities, critical risks, invalidations and source references.
6. Apply deterministic limits and warnings, then return a structured result with agreement, disagreement, coverage and uncertainty metrics.

Consensus confidence measures evidence quality, coverage and agreement. It is explicitly not a probability of profit or a promise of a market outcome.

## Registration

```csharp
services.AddTradeMindConsensus(options =>
{
    options.MinimumConfidenceScore = 25;
    options.MaximumMarketLevels = 20;
});
```

The engine is scoped because it is an application operation. Policies are stateless singletons. `TimeProvider` is resolved from dependency injection and can be replaced by a test clock. Cancellation is linked to the caller token and the request timeout; caller cancellation is propagated, while an internal timeout returns a structured `TimedOut` result.
