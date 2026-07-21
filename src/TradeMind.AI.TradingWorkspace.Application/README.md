# Trading Workspace

Trading Workspace is a deterministic read-only projection of the current AI trading pipeline.
It accepts a `MarketContext`, optional dispatch and agent results, `ConsensusResult`,
`TradingDecisionResult`, `RiskAssessmentResult` and `TradingPlanResult`.

The builder performs cross-source consistency checks, freshness checks, progressive
pipeline projection, bounded summaries, a deterministic timeline, alerts, blockers,
declarative next actions and source traces. It does not invoke any engine, recalculate
any business result, alter a plan, persist data, call an HTTP/LLM provider or execute trades.

Register it with `AddTradeMindTradingWorkspace()`. The builder is scoped, policies are
stateless, and `TimeProvider` is injectable for deterministic tests.
