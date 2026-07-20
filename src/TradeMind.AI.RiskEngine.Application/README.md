# Risk Engine

The Risk Engine evaluates an eligible `TradingDecisionResult` without placing an order. It is deliberately independent from brokers, MT5, HTTP, persistence and concrete AI providers.

The pipeline is deterministic:

`TradingDecisionResult -> eligibility -> effective budget -> stop distance -> R multiples -> position sizing -> exposure reduction -> RiskAssessmentResult`

Risk budgets are expressed in an explicit account currency. The smallest positive remaining constraint wins. Position sizing uses decimal arithmetic and the conservative formula `(StopDistance / TickSize) * TickValue`; quantities are rounded down to the configured quantity step. Tick size, tick value, currencies and exposure metadata are supplied by the caller and are never inferred from an instrument symbol.

Directional decisions require an explicit, directionally coherent entry and stop. Non-directional decisions are rejected without sizing. Drawdown, portfolio and platform constraints are preserved in structured evaluations. Optional portfolio exposure reduction is applied only when the required portfolio and instrument metadata are present; a hard or exhausted constraint rejects the assessment.

The engine returns immutable, versioned results with structured verdicts, constraints, warnings, errors, source traces and preserved decision risks. A timeout produces a `TimedOut` result, while caller cancellation is propagated. No result represents execution, probability or expected gain.

Register it with `AddTradeMindRiskEngine`; override policies through the normal dependency injection registrations when a host needs a different policy.
