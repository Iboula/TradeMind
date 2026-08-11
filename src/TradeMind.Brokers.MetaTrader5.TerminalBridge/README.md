# TradeMind MetaTrader 5 Terminal Bridge

This process is the external terminal-side HTTP bridge required by
`HttpMT5TerminalTransport`. It is independent from the analytical Core,
the Bridge Host and persistence. By default it binds to
`https://localhost:5001` and accepts demo-only traffic. The EA uses the
IPv4 loopback spelling `https://127.0.0.1:5001` for its WebRequest allow-list;
the .NET Bridge Host uses `localhost` to match the local development
certificate. Both addresses remain loopback-only.

## Real MT5 mechanism

The repository contains no official MT5 .NET SDK, native SDK, Python
MetaTrader package or existing terminal API. The supported mechanism chosen
for this sprint is an isolated MQL5 Expert Advisor:

1. the EA is attached to a real MT5 terminal chart;
2. the EA polls the local service through `WebRequest`;
3. the service queues read-only commands from the Bridge Host;
4. the EA executes the corresponding native MQL5 read operation;
5. the EA posts the normalized result back to the service.

The EA source is in
`src/TradeMind.Brokers.MetaTrader5.TerminalBridge.Mql5/`. It is not part of
any Domain or Application project and is not compiled by .NET CI.

## Routes

The service exposes the exact routes currently used by
`HttpMT5TerminalTransport`:

- `POST /terminal/v1/handshake`
- `POST /terminal/v1/ping`
- `POST /terminal/v1/execute`
- `POST /terminal/v1/disconnect`

The EA control plane uses:

- `POST /terminal/v1/agent/poll`
- `POST /terminal/v1/agent/result`

For diagnostics, `GET /` and `GET /health` return no account number,
credentials, token or raw terminal payload.

## Security and deployment

The host-to-service boundary uses HTTPS, a bearer token from User Secrets or
an environment variable, a signed HMAC request, a timestamp and a nonce.
Nonces are retained for the replay window and requests are bounded by body
size, concurrency and timeout settings. The default endpoint is loopback only.

The EA control plane uses a separate local agent token. Set both token values
outside the repository. Do not put them in `appsettings.json`, the EA source,
logs or command history. In MetaTrader 5, add the service URL to the allowed
WebRequest list before attaching the EA.

## Scope

Phase 1 implements discovery, demo handshake, account/instrument/order/
position reads, heartbeat and health. All write commands are rejected with
`LIVE_MODE_FORBIDDEN`; no order can be submitted by this service in the
default configuration. Phase 2 write tests are not enabled or implemented in
this sprint.

The service does not start a terminal, select an account, store credentials,
persist data, expose a Core dependency or make live trading possible.
