# MT5 Terminal Bridge Decision

## Decision

TradeMind uses a local .NET Terminal Bridge plus an isolated MQL5 Expert
Advisor. No official MetaTrader 5 .NET SDK, native SDK, Python
`MetaTrader5` package or existing EA was available in this repository. A
direct .NET call to `terminal64.exe` is therefore not a supported integration
mechanism.

The .NET service owns the HTTP contract required by
`HttpMT5TerminalTransport`. The EA is attached to the already authenticated
MT5 terminal and polls the service through the MQL5 `WebRequest` API. The
service queues read commands and receives normalized results from the EA.
This keeps MT5 types and native operations inside the adapter boundary.

## Availability and dependencies

The .NET project builds on Windows and runs independently of the terminal.
The real read-only path additionally requires:

- MetaTrader 5 x64 build within the configured support range;
- the EA compiled in MetaEditor and attached to a chart;
- `https://localhost:5001` allowed in MT5 WebRequest settings;
- a Demo account already authenticated by the terminal;
- bridge and agent tokens supplied through local secrets or EA inputs.

The EA source is not compiled by .NET CI. Without the EA, the service remains
healthy as a process but reports `TERMINAL_NOT_READY`; it never fabricates an
account or market response.

## Protocol and security

The four host routes are `POST /terminal/v1/handshake`, `ping`, `execute` and
`disconnect`. Host requests use HTTPS, a bearer token, HMAC-SHA256 over method,
path, timestamp, nonce and body, and bounded timestamp/replay windows. Body
size, concurrency and request timeouts are limited. The EA control plane uses
a separate local agent token and does not expose credentials in responses or
logs.

The default listener is loopback HTTPS on port 5001. Plain HTTP is available
only as an explicit test/demo override. No token, login, password, server,
account number or raw terminal payload is logged or committed.

## Demo-only behavior

The service rejects live environments, non-Demo agent reports, unsupported
protocol/build/architecture values and all write commands. Phase 1 supports
only account, instrument, orders, positions and heartbeat reads. There is no
order submission, modification, cancellation or position close path.

Phase 2 write tests are intentionally not enabled. Any future write test must
require both `MT5_REAL_TESTS=true` and `MT5_REAL_WRITE_TESTS=true`, an explicit
Demo confirmation and deterministic cleanup.

## Windows deployment

Install the EA under the terminal's Experts directory, compile it with
MetaEditor, configure its local agent token, allow the bridge URL in MT5, and
attach it to a chart. Run the .NET service as a separate local process. The
Bridge Host then continues to target the service endpoint configured in its
existing options; no Core project changes are required.
