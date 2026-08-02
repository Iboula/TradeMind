# ADR-0021: External MT5 Bridge Boundary

## Status

Accepted for Sprint 30.

## Context

Sprint 29 introduced a provider-neutral `IBrokerConnector` and an isolated
MetaTrader 5 adapter with a deterministic local bridge. The next boundary
must allow the adapter to communicate with a separate host process without
bringing HTTP, credentials, terminal types or a native MT5 SDK into the
analytical core. The first implementation also needs deterministic demo
execution for integration tests.

## Decision

Create three assemblies: neutral immutable bridge contracts, an adapter-side
HTTP/JSON client and an ASP.NET Core bridge host. Version the public paths as
`/bridge/v1` and negotiate `BridgeProtocolVersion(1, 0)`. Carry request,
correlation and execution-session identifiers, timestamps, deadlines, nonces,
idempotency hashes and normalized error values in every transport envelope.

Use mutual TLS or a signed service token for service authentication. Permit
plaintext only with an explicit local demo opt-in. Enforce demo-only mode in
both client and host options, at handshake and at execution. Use a
deterministic simulated terminal gateway in Sprint 30; do not load an MT5 SDK
or connect to a live terminal.

The client owns bounded transport concurrency, deadlines, state and safe query
retries. Submit, modify, cancel and close commands are never automatically
retried. The host owns lifecycle, request validation, replay protection,
idempotency and normalized terminal errors. Existing observability abstractions
are reused for bridge activities and MT5 metrics.

## Alternatives considered

gRPC was considered for stronger schema enforcement, but HTTP/JSON gives the
initial adapter a small inspectable dependency surface and can be replaced by
a transport implementation behind the same contract intent. Embedding a
terminal bridge in the core was rejected because it would violate the
modular, broker-neutral dependency rule. A live terminal was rejected because
this sprint is explicitly demo-only.

## Consequences

The analytical core remains unchanged and broker-neutral. Client and host can
be tested independently with bounded payloads, safe errors, state machines,
timeouts, cancellation, idempotency and replay scenarios. The in-memory
stores are intentionally non-durable, so a future multi-host deployment must
add shared coordination before any live side effect could be considered.
