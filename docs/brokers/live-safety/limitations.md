# Limitations

Sprint 33 does not authorize Live trading, create a Live account, handle Live credentials, send an order, or add a broker-specific Live adapter. The hard gate intentionally receives closed evidence for workspace, heartbeat, reconciliation and distributed locking from the current execution validator, so the existing runtime remains Demo/Simulation only.

The in-memory kill switch, quarantine, ownership and audit implementations are single-process fallbacks. They are suitable for tests and local operation only; multi-pod operation requires the PostgreSQL persistence configuration. The PostgreSQL advisory lock is session-bound and must always be disposed as a lease.

Recovery is read-only and does not repair positions. A future release must add a reviewed operator workflow, complete risk-policy configuration, durable readiness providers and a separately approved Live enablement process before any Live adapter can be considered.
