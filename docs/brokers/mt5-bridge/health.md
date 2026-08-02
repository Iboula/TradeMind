# Health and Recovery

The host exposes liveness, readiness and startup probes. `BridgeHealthResponse`
also reports host and terminal state, protocol version, bridge version,
demo-only flag, heartbeat age, reconnect count, latency and last successful
handshake time.

The client keeps a health snapshot and distinguishes `Ready`, `Degraded`,
`Reconnecting`, `Faulted` and `Closed`. A failed heartbeat marks the client
degraded. Reopening a degraded client moves through reconnecting and counts
the reconnect. A circuit breaker prevents repeated transport calls after the
configured failure threshold.

Cancellation remains meaningful: external cancellation is propagated to the
caller, while an expired request deadline is normalized to `Timeout`. No
background retry or heartbeat task is created by the client, so disposal
cannot leave orphaned work behind.

The current demo gateway reports deterministic latency and terminal versions.
Production monitoring should connect the existing `ITradeMindTelemetry` and
`ITradeMindMetrics` abstractions to OpenTelemetry in the hosting process.
