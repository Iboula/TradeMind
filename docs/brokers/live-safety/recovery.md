# Crash Recovery

`IBrokerExecutionRecoveryService` reads non-terminal executions, broker orders, broker positions, ownership records and the last reconciliation timestamp. It compares the two views and creates a deterministic `RecoveryPlan` containing manual-review mismatches.

Recovery is read-only. It never submits, cancels, modifies or closes an order or position. Unknown external positions remain unknown and are never auto-closed. A stale or missing reconciliation timestamp is itself a mismatch. Recovery runs and mismatch counts are auditable and measurable.
