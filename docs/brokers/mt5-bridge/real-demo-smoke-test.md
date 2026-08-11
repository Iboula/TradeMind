# MT5 real demo read-only smoke test

This guide validates the VT Markets MetaTrader 5 terminal against the local
TradeMind TerminalBridge. It is deliberately limited to demo-account reads.
The bridge keeps `AllowLive=false` and rejects every write command with
`LIVE_MODE_FORBIDDEN`.

## Preconditions

- Windows with the VT Markets MT5 terminal installed.
- A connected VT Markets demo account.
- The local TerminalBridge token configured through User Secrets or an
  environment variable. The token value must never be committed or printed.
- The terminal architecture must be x64 and the terminal build must be within
  the bridge's configured supported range.

The expected installation paths used for the current VT Markets installation
are:

```text
MetaEditor: C:\Program Files\VT Markets (Pty) MT5 Terminal\MetaEditor64.exe
Terminal:   C:\Program Files\VT Markets (Pty) MT5 Terminal\terminal64.exe
Data:       %APPDATA%\MetaQuotes\Terminal\9BB124B7D418C7FB69DF2865535BA9BF
Experts:    %APPDATA%\MetaQuotes\Terminal\9BB124B7D418C7FB69DF2865535BA9BF\MQL5\Experts
```

## Compile and install the EA

The source is isolated at:

```text
src\TradeMind.Brokers.MetaTrader5.TerminalBridge.Mql5\TradeMindTerminalBridge.mq5
```

Copy it to the terminal's `MQL5\Experts` directory and compile it with:

```powershell
$metaEditor = 'C:\Program Files\VT Markets (Pty) MT5 Terminal\MetaEditor64.exe'
$experts = "$env:APPDATA\MetaQuotes\Terminal\9BB124B7D418C7FB69DF2865535BA9BF\MQL5\Experts"
Copy-Item '.\src\TradeMind.Brokers.MetaTrader5.TerminalBridge.Mql5\TradeMindTerminalBridge.mq5' "$experts\TradeMindTerminalBridge.mq5" -Force
Start-Process $metaEditor -ArgumentList "/compile:$experts\TradeMindTerminalBridge.mq5", '/log' -Wait
```

The compiler result is recorded in:

```text
%APPDATA%\MetaQuotes\Terminal\9BB124B7D418C7FB69DF2865535BA9BF\MQL5\Experts\compile.log
```

The expected result is `0 errors, 0 warnings`, with the generated
`TradeMindTerminalBridge.ex5` beside the source. The `.ex5` is a local build
artifact and must not be committed.

## Allow WebRequest

In MT5 open **Tools > Options > Expert Advisors**, enable **Allow WebRequest
for listed URL**, add exactly:

```text
https://127.0.0.1:5001
```

Do not add a wildcard or an HTTP endpoint. Restart the terminal or reload the
EA after changing this setting.

Set the EA `BridgeUrl` to this exact value. The bridge remains HTTPS and
loopback-only; this is not a network exposure or a live-broker endpoint.

## Start and verify the Bridge

Start the service from the repository root. Supply the Bridge and agent tokens
from the local secret store or environment without echoing their values:

```powershell
$env:MT5_TERMINAL_BRIDGE_TOKEN = '<local secret, not committed>'
$env:MT5_TERMINAL_AGENT_TOKEN = '<local secret, not committed>'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project .\src\TradeMind.Brokers.MetaTrader5.TerminalBridge -c Release --no-build
```

The service listens on `https://localhost:5001`; its local binding accepts the
IPv4 loopback used by the EA. Use `https://127.0.0.1:5001` for the EA and
WebRequest allow-list, and `https://localhost:5001` for the .NET Bridge Host
because the development certificate is issued for `localhost`. Verify the
unauthenticated health endpoint first:

```powershell
Invoke-RestMethod https://127.0.0.1:5001/health
```

Handshake and ping use the Host transport contract, including Bearer
authentication, timestamp, nonce and HMAC-SHA256 headers. They should be
verified through the existing `TradeMind.Brokers.MetaTrader5.Bridge.Host`
client or its contract tests, not by copying a secret into a command line.

## Attach the EA

In Navigator > Expert Advisors, attach `TradeMindTerminalBridge` to a demo
chart. Set:

- `BridgeUrl` to `https://127.0.0.1:5001`;
- `AgentToken` to the local agent token through the EA input dialog;
- `PollIntervalSeconds` to `1`.

The Bridge health response must move from `not-ready` to `ready`. The agent
poll establishes the terminal snapshot; the first handshake and ping then
confirm protocol `1.0`, x64 architecture and demo environment.

If the service log shows `401` for `POST /terminal/v1/agent/poll` while health
continues to report `agentConnected=false`, the WebRequest route is working
but the agent token is missing or does not match. Configure the
`MT5_TERMINAL_AGENT_TOKEN` User Secret for the TerminalBridge project with the
same local value entered in the EA `AgentToken` input, restart the service,
and reload the EA. Never print or commit the value.

## Read-only checks

Run through the existing Host client or contract smoke test in this order:

1. `handshake`;
2. `ping`;
3. `get-account`;
4. `get-instrument` for a visible symbol;
5. `get-orders`;
6. `get-positions`;
7. `heartbeat`.

Record only success/error codes, counts and non-sensitive capabilities. Never
record login, password, complete account number, complete server name or raw
MT5 packets.

Any command outside the read-only allow-list, including submit, cancel,
modify or close, must return `LIVE_MODE_FORBIDDEN`. Do not set
`MT5_REAL_WRITE_TESTS`; no write test is part of this smoke test.

## Evidence and limitations

Keep compiler output, health status, handshake/ping codes, read counts and
timestamps in the test report. Redact identifiers before sharing it. A real
terminal smoke test is environment-dependent; normal CI remains deterministic
and performs no live-terminal or write operation.
