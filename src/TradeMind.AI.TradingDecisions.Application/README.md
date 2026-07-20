# Trading Decision Model

The Trading Decision Model is a deterministic projection of `ConsensusResult`. It evaluates eligibility, selects a primary scenario, validates level coherence and returns a structured `TradingDecisionResult`.

It does not execute trades, calculate lots or sizing, calculate drawdown, access MT5, persist data, expose HTTP, call an LLM or implement a Risk Engine. It only describes whether the supplied evidence supports a coherent setup, waiting, monitoring, no-trade or insufficient-data outcome.

## Decision rules

- Strong Bullish/Bearish consensus can produce `LongSetup`/`ShortSetup` only when a matching scenario, explicit invalidation, explicit entry and coherent stop exist.
- Weak consensus produces `Wait`; neutral consensus produces `Monitor`.
- Critical conflicts produce `Conflicted` and remain visible in the result.
- Explicit levels are projected only when they already exist in the consensus. Long stops must be below entry and targets above entry; short stops must be above entry and targets below entry.
- Critical risks, invalidations, alternatives and source references are preserved and deterministically truncated only at configured limits.

## Registration

```csharp
services.AddTradeMindTradingDecisions(options =>
{
    options.MinimumConsensusConfidence = 60;
    options.MaximumTargets = 5;
});
```

The engine is scoped, policies are stateless singletons, and `TimeProvider` is resolved through dependency injection. Caller cancellation is propagated; an internal timeout returns a structured `TimedOut` result.
