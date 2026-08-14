# Production Readiness

The readiness surface reports persistence, idempotency, execution lock, reconciliation, broker, kill switch, recovery, orphan state, outbox and identity. The Live Safety health check is tagged for readiness and reports detailed booleans without tenant, account or session labels. Liveness remains independent.

A broker orphan can therefore leave the process alive while making execution readiness unhealthy or blocked. This separation is intentional: operators retain access to diagnostics and recovery while new writes remain closed.

Production configuration is validated at startup. In particular, `AllowLive=true` cannot be combined with a disabled gate, and the independent confirmation, lock, reconciliation, heartbeat and orphan controls cannot be disabled. The shipped API configuration explicitly uses `AllowLive=false` and `Enabled=false`.
