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

For local demo-only development, HTTP requires `AllowInsecureDemoTransport=true`. HTTPS is required for a configured remote endpoint. The bridge token is read through `MT5_TERMINAL_BRIDGE_TOKEN` by default, but the key can be redirected to a vault-backed configuration provider with `TerminalBridgeTokenConfigurationKey`.

Example environment setup in PowerShell:

```powershell
$env:MT5_TERMINAL_BRIDGE_TOKEN = "set-outside-the-repository"
$env:MT5_REAL_TESTS = "true"
```

The example value above is instructional only and must never be committed as a credential.
