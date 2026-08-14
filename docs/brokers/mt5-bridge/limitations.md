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

## Sprint 32 technical debt

- The default in-memory idempotency and execution-safety stores are
  process-local. PostgreSQL-backed idempotency is available in the broker
  infrastructure, but a production deployment still needs one documented
  durable coordination choice for every write-capable process.
- Real Demo writes remain an explicit, operator-confirmed smoke path and are
  excluded from CI. Live execution is unsupported and `AllowLive=false` is
  enforced by configuration and adapter guards.
- The complete Release coverage run currently reports 86.7% line coverage,
  67.6% branch coverage and 80.3% method coverage from 72 Cobertura inputs
  across 36 test projects. The method figure is calculated from the merged
  Cobertura method entries because the local ReportGenerator edition does not
  publish a method percentage in its Markdown summary.
- Under high parallel test load, a pre-existing Coaching suite can
  intermittently hit `RegexMatchTimeoutException`. This is a test-harness
  stability issue outside the broker adapter; the affected suite is rerun
  sequentially and the incident is reported separately from broker
  regressions.
- MetaEditor/terminal installations, local development certificates and
  generated `.ex5` files are intentionally outside source control. The real
  smoke procedure depends on the operator's local VT Markets Demo terminal.
