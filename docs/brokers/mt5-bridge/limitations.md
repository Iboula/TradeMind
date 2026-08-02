# Sprint 30 Limitations

This sprint establishes the external boundary and a deterministic demo host.
It does not implement live trading, a real terminal process, an MT5 SDK
binding, broker credentials, market data streaming, persistence, a durable
replay store or a production certificate provisioning workflow.

The host idempotency and replay stores are in-memory and process-local. They
are enough for deterministic demo execution and integration tests, but they do
not provide cross-process guarantees. A deployment with multiple hosts must
add shared coordination before any non-demo side effect is permitted.

HTTP/JSON is the selected Sprint 30 transport. A future gRPC transport may be
introduced behind the same neutral contract intent, but it must retain
version negotiation, metadata, bounded payloads, safe errors, cancellation,
demo-only enforcement and no automatic retries for non-idempotent commands.

The simulated terminal uses deterministic sample values. It does not model
real spread, slippage, liquidity, disconnections, broker rules or market
execution conditions.
