# Demo Mode

The host starts `DeterministicSimulatedTerminalGateway`, an in-process gateway
that does not open a socket, load a native library or connect to a terminal.
It returns stable identifiers derived from SHA-256 input and stores orders and
positions only for the lifetime of the host process.

The demo gateway models the normalized order lifecycle needed by the adapter:
submit, modify, cancel and close. It also exposes health, accounts,
instruments, orders and positions. It is intended for integration tests,
local development and protocol demonstrations.

The following independent checks reject live behavior:

1. client options reject `AllowLive=true` and live transport settings;
2. host options reject `AllowLive=true` and live environments;
3. handshake rejects a live account or a client that does not require demo;
4. execute rejects a command field with `mode=Live`;
5. the existing MT5 adapter rejects live execution before using the bridge.

No demo response should be interpreted as evidence that a real broker order
was placed. The gateway is not a broker simulator with market data; it is a
deterministic protocol test double.
