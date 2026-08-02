# ADR-0020: MetaTrader 5 Adapter Boundary

## Status

Accepted for Sprint 29.

## Context

TradeMind needs its first concrete broker adapter while preserving the
provider-neutral analytical core and the existing `IBrokerConnector` contract.
The release must not introduce a live trading path, credentials, network
coupling, or MT5 SDK types into core modules.

## Decision

Create `TradeMind.Brokers.MetaTrader5` as an adapter assembly. It owns the MT5
configuration, bridge, protocol, serializer, connection state machine, mapping,
retry, health, and telemetry components. Hosts register it explicitly through
`AddTradeMindMetaTrader5`; the host and core consume only `IBrokerConnector`.

Sprint 29 uses a deterministic simulation bridge. `Mode=Live` and
`AllowLive=true` are rejected. Demo mode is opt-in. No external MT5 SDK or
network transport is included.

## Consequences

The adapter can evolve its bridge and wire protocol without changing the
analytical modules or normalized broker contracts. Connection failures,
timeouts, reconnects, heartbeats, and normalized broker errors are testable
without a terminal. A future real bridge remains a separate implementation
inside this adapter boundary and must preserve the same live-execution policy.
