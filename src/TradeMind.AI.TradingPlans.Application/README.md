# Trading Plan Generator

The Trading Plan Generator assembles a declarative plan from an existing `TradingDecisionResult` and `RiskAssessmentResult`. It does not execute trades, call a broker, recalculate consensus or confidence, recalculate risk or sizing, invent levels, convert currencies, or provide an order payload.

The pipeline is:

`TradingDecisionResult + RiskAssessmentResult -> cross-validation -> eligibility -> plan assembly -> pre-trade checklist -> expiration -> TradingPlanResult`

For a directional decision, the generator copies the selected scenario, explicit entry, explicit stop, source targets, coherent invalidations and the exact Risk Engine quantity. `Approved` produces an `ExecutableCandidate`; a reduced Risk Engine result remains `ExecutableCandidate` with exactly the reduced quantity and an explicit warning. Rejected risk produces `NoTradePlan`. Wait, Monitor, Conflicted and InsufficientData decisions retain their corresponding non-directional plan types.

All models are immutable and versioned. Collections are copied defensively and ordered deterministically. Declarative rules describe activation, invalidation, approved quantity, stop discipline, targets, entry-change re-evaluation, expiration and stale-context handling. The pre-trade checklist is intentionally blocking and always requires explicit user confirmation for any future execution workflow.

The generator uses `TimeProvider` for timeout, freshness and expiration. Register it with `AddTradeMindTradingPlans`; replace policies through dependency injection when a host needs a different eligibility or expiration policy.
