# MetaTrader 5 Demo Terminal Gateway

This project hosts the process boundary between TradeMind and a local MetaTrader 5 demo terminal. The default gateway is `Simulation`. The `Real` gateway is opt-in and connects only to a terminal-side bridge using a neutral local HTTP(S) protocol. The repository does not contain a native MT5 SDK, MQL5 code, a broker login implementation, or a live-trading path.

## Boundary

`RealMT5TerminalGateway` is selected by `TradeMind:Brokers:MetaTrader5:BridgeHost:GatewayMode`. It owns discovery, optional process start, demo-account validation, heartbeat, reconnect and normalization of terminal failures. `HttpMT5TerminalTransport` is the only transport implementation. No MT5 DTO or native error is emitted by the bridge contracts or the analytical core.

The existing `TradeMind.Brokers.MetaTrader5` adapter remains responsible for mapping neutral bridge fields to the normalized broker contracts. Account, instrument, order, position, execution, capability and error mappers are not duplicated in this host.

## Lifecycle

The real gateway uses the explicit states `Disconnected`, `Connecting`, `Authenticating`, `Connected`, `Reconnecting`, `Faulted` and `Closed`. A connection is usable only in `Connected`. Startup validates the executable path, version/build range, PE architecture, bridge protocol, demo account, write access and trading permission before the host becomes ready.

The gateway performs a bounded heartbeat before each command. A lost heartbeat triggers a bounded, sequential reconnect loop with configured backoff. Order commands are never replayed after a transport failure. All operations await their child tasks and propagate the caller cancellation token.

## Terminal-side bridge contract

The configured endpoint provides these neutral JSON operations:

| Endpoint | Purpose |
| --- | --- |
| `POST /terminal/v1/handshake` | protocol, terminal, account and capability validation |
| `POST /terminal/v1/ping` | heartbeat and latency |
| `POST /terminal/v1/execute` | demo query/order command using normalized fields |
| `POST /terminal/v1/disconnect` | graceful bridge disconnect |

The terminal-side component is intentionally external to this repository. It must enforce the same demo-only contract and return normalized response fields. The host never logs request bodies, credentials, account identifiers, terminal packets or remote failure details.

## Configuration and secrets

Use `appsettings.example.json` as a shape reference. Use .NET User Secrets, environment variables or a configuration provider backed by a vault for `MT5_TERMINAL_BRIDGE_TOKEN`. No credential belongs in source control. `AllowLive` is rejected by validation and cannot enable live execution.

Real-terminal tests run only when `MT5_REAL_TESTS=true` and the terminal path, endpoint and token environment variables are present. CI remains terminal-free and uses the deterministic simulation gateway.
