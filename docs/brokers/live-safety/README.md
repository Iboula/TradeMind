# Live Safety and Production Readiness

Sprint 33 prepares the broker boundary for a future Live approval without enabling Live today. The safety controls are deliberately independent: configuration, account environment, tenant authorization, actor permission, risk, plan, workspace, heartbeat, reconciliation, idempotency, distributed locking, kill switches and operator confirmation must all agree before a future Live operation could be considered.

`AllowLive=false` remains the repository default and the application configuration sets it explicitly. No Live account, credential, order or smoke test is part of this sprint. The existing MT5 Demo path remains the only real-terminal path and is not changed here.

## Components

- `ILiveTradingSafetyGate` returns a structured `Allowed`, `Denied`, `Blocked`, `RequiresConfirmation`, `RequiresReconciliation` or `EmergencyStop` decision.
- Kill switches are scoped to global, tenant, broker, account, instrument and execution session. Emergency stop recovery requires an explicit audited action.
- Versioned risk guard policies evaluate loss, drawdown, trade-count, position-count, exposure, notional and quantity limits.
- Quarantine prevents a problematic execution from being retried until an operator has reconciled it.
- Crash recovery compares durable TradeMind state with broker observations and produces a read-only `RecoveryPlan`.
- PostgreSQL advisory locks serialize the same tenant/broker/account/instrument execution scope across pods.
- Durable idempotency records `ExecutionUnknown`; an unknown broker outcome is never retried automatically.

See the individual guides for operations and incident handling.
