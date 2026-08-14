# Configuration

Configuration is bound under `TradeMind:Brokers:MetaTrader5:BridgeHost`.

| Setting | Default | Meaning |
| --- | --- | --- |
| `GatewayMode` | `Simulation` | `Simulation` for CI/local deterministic runs, `Real` for the terminal gateway |
| `DemoOnly` | `true` | mandatory demo-only boundary |
| `AllowLive` | `false` | validated and rejected when true |
| `TerminalPath` | empty | absolute path to the terminal executable in real mode |
| `TerminalBridgeEndpoint` | empty | absolute HTTPS endpoint of the local terminal-side bridge |
| `AutoStartTerminal` | `false` | start only the configured process and stop only a process owned by the host |
| `ExpectedTerminalProtocolVersion` | `1.0` | compatible terminal bridge protocol |
| `SupportedTerminalArchitecture` | `x64` | accepted PE architecture |
| `MinimumSupportedTerminalBuild` | `3000` | lower build boundary |
| `MaximumSupportedTerminalBuild` | `99999` | upper build boundary |
| `TerminalHeartbeatTimeoutSeconds` | `5` | per-heartbeat timeout |
| `ReconnectMaximumAttempts` | `3` | bounded reconnect attempts |
| `ReconnectBackoffMilliseconds` | `250` | sequential retry backoff |

The host exposes `GET /` as a safe diagnostic endpoint and `GET /health/terminal`
as the terminal-gateway health probe. These endpoints contain status, mode,
versions, connection state and demo-account safety flags only. They never return
login, password, server, account number or bridge token values.

For local demo-only development, HTTP requires `AllowInsecureDemoTransport=true`. HTTPS is required for a configured remote endpoint. The bridge token is read through `MT5_TERMINAL_BRIDGE_TOKEN` by default, but the key can be redirected to a vault-backed configuration provider with `TerminalBridgeTokenConfigurationKey`.

Example environment setup in PowerShell:

```powershell
$env:MT5_TERMINAL_BRIDGE_TOKEN = "set-outside-the-repository"
$env:MT5_REAL_TESTS = "true"
```

The example value above is instructional only and must never be committed as a credential.

The normal opt-in real test is read-only. Sprint 32 also defines a separate
Demo write test that requires both `MT5_REAL_TESTS=true` and
`MT5_REAL_WRITE_TESTS=true`, the exact local confirmation
`MT5_REAL_DEMO_CONFIRMATION=I_CONFIRM_ONE_DEMO_ORDER`, and explicit
`EnableWriteTests=true` in both local bridge services. Without every guard it
must not submit or close anything. See [the real Demo order runbook](real-demo-order-smoke-test.md).

## Environment variable names

The preferred options use the following environment-variable equivalents:

```text
TradeMind__Brokers__MetaTrader5__BridgeHost__GatewayMode
TradeMind__Brokers__MetaTrader5__BridgeHost__TerminalPath
TradeMind__Brokers__MetaTrader5__BridgeHost__TerminalBridgeEndpoint
TradeMind__Brokers__MetaTrader5__BridgeHost__AllowLive
TradeMind__Brokers__MetaTrader5__BridgeHost__AutoStartTerminal
MT5_TERMINAL_BRIDGE_TOKEN
MT5_REAL_TESTS
MT5_REAL_WRITE_TESTS
```

The Sprint 30 compatibility section also accepts these legacy names:

```text
TradeMind__Brokers__MetaTrader5__Bridge__Terminal__Mode
TradeMind__Brokers__MetaTrader5__Bridge__Terminal__Enabled
TradeMind__Brokers__MetaTrader5__Bridge__Terminal__AllowLive
TradeMind__Brokers__MetaTrader5__Bridge__Terminal__Login
TradeMind__Brokers__MetaTrader5__Bridge__Terminal__Password
TradeMind__Brokers__MetaTrader5__Bridge__Terminal__Server
TradeMind__Brokers__MetaTrader5__Bridge__Terminal__TerminalPath
TradeMind__Brokers__MetaTrader5__Bridge__Endpoint
TradeMind__Brokers__MetaTrader5__Bridge__Authentication__Mode
TradeMind__Brokers__MetaTrader5__Bridge__Authentication__Token
```

`Mode`, `TerminalPath`, `AutoStart`, `Endpoint` and the authentication token
are mapped by the compatibility layer. `Enabled`, `Login`, `Password`,
`Server`, `Authentication:Mode`, `RealTests:Enabled` and account credentials
are not consumed by the Sprint 31 host. In particular, the host does not log in
to MT5 directly: the external terminal-side bridge owns terminal-account
authentication, while the host authenticates that HTTP boundary with its bridge
token.

## Existing User Secrets compatibility

The host also accepts the Sprint 30 names under `TradeMind:Brokers:MetaTrader5:Bridge`. The terminal values `Terminal:Mode`, `Terminal:TerminalPath` and `Terminal:AutoStart` map to the Sprint 31 gateway options. `Bridge:Endpoint` is used as the terminal-side bridge endpoint, and `Bridge:Authentication:Token` is consumed only through the secret provider. New `BridgeHost` keys take precedence when present.
