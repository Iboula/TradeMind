# Risk Guards

Risk guards are represented by `VersionedRiskGuardPolicy` and `RiskGuardLimits`. Limits are supplied by policy configuration or a policy provider; they are not embedded in the execution algorithm. The policy evaluates daily loss, weekly loss, drawdown, trades per day, consecutive losses, concurrent positions, instrument exposure, portfolio exposure, notional and order quantity.

The comparison is deterministic and inclusive at the configured boundary: a measurement equal to a limit is accepted, while a measurement above it is rejected with a stable code. Rejections are sorted by code and remain structured. A rejected risk guard must be projected to the Live gate as a blocking cause.

The default DI registration is intentionally fail-closed: without an explicitly configured versioned policy, the risk policy returns `RISK_POLICY_NOT_CONFIGURED`.
