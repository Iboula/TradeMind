# MT5 real Demo order smoke test

This runbook describes the only supported write path in Sprint 32. It submits
one EURUSD market order at the broker-reported minimum volume through the
TradeMind broker connector, verifies the resulting order, deal and position,
then closes the position and verifies that no position remains.

The path is deliberately Demo-only. `AllowLive` remains `false` in the
TerminalBridge, Bridge Host, bridge client and MT5 adapter. No pending order,
modification, cancellation, partial close, scaling, averaging or second
position is permitted.

## Preconditions

The following must all be true before the test is allowed to write:

- `MT5_REAL_TESTS=true`;
- `MT5_REAL_WRITE_TESTS=true`;
- `MT5_REAL_DEMO_CONFIRMATION=I_CONFIRM_ONE_DEMO_ORDER`;
- TerminalBridge has `DemoOnly=true`, `AllowLive=false`, and
  `EnableWriteTests=true` in local User Secrets;
- Bridge Host uses `GatewayMode=Real`, `DemoOnly=true`, `AllowLive=false`,
  `EnableWriteTests=true`, and the VT Markets terminal path;
- the EA is attached to the VT Markets Demo terminal and reports a writable
  Demo account;
- `MT5_BRIDGE_HOST_ENDPOINT` points to the local Bridge Host, normally
  `https://localhost:65172` when using the preserved Development launch
  profile;
- the EURUSD account has no open order and no open position before the test;
- the operator has explicitly confirmed that one Demo order may be sent.

The confirmation value is a local runtime guard, not a secret. Do not put it
in source control or CI configuration. Do not print tokens, login values,
passwords, complete account numbers, complete server names or raw MT5
packets.

## Configure local services

Set local User Secrets, never repository files. The complete `BridgeHost`
section is intentional: once one key under this section exists, the host no
longer applies the legacy configuration fallback.

```powershell
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:TerminalBridge:EnableWriteTests" "true" --project .\src\TradeMind.Brokers.MetaTrader5.TerminalBridge
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:GatewayMode" "Real" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:Environment" "Demo" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:AccountEnvironment" "Demo" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:DemoOnly" "true" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:AllowLive" "false" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:RequireTls" "true" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:TerminalPath" "C:\Program Files\VT Markets (Pty) MT5 Terminal\terminal64.exe" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:TerminalBridgeEndpoint" "https://localhost:5001/" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:TerminalBridgeTokenConfigurationKey" "MT5_TERMINAL_BRIDGE_TOKEN" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "TradeMind:Brokers:MetaTrader5:BridgeHost:EnableWriteTests" "true" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
dotnet user-secrets set "MT5_TERMINAL_BRIDGE_TOKEN" "<existing local bridge token>" --project .\src\TradeMind.Brokers.MetaTrader5.TerminalBridge
dotnet user-secrets set "MT5_TERMINAL_AGENT_TOKEN" "<same local value as the EA AgentToken input>" --project .\src\TradeMind.Brokers.MetaTrader5.TerminalBridge
dotnet user-secrets set "MT5_TERMINAL_BRIDGE_TOKEN" "<existing local bridge token>" --project .\src\TradeMind.Brokers.MetaTrader5.Bridge.Host
```

The bridge token must also be available to the Bridge Host through its
configured secret provider. The agent token must be present in the
TerminalBridge configuration and must exactly match the EA input. Keep the
values local and never paste them into the repository. The EA input
`BridgeUrl` remains `https://localhost:5001`, and that URL must be present in
the MT5 WebRequest allow-list. The Host targets the TerminalBridge endpoint;
the TradeMind adapter targets the Bridge Host endpoint.

## Run the real test

Start the TerminalBridge and Bridge Host as separate local processes. Confirm
the read-only health, handshake, ping, account, instrument, orders and
positions checks first. Then, in the same PowerShell session, set the opt-in
flags and run only the dedicated test:

```powershell
$env:MT5_REAL_TESTS = "true"
$env:MT5_REAL_WRITE_TESTS = "true"
$env:MT5_REAL_DEMO_CONFIRMATION = "I_CONFIRM_ONE_DEMO_ORDER"
$env:MT5_BRIDGE_HOST_ENDPOINT = "https://localhost:65172"
dotnet test .\tests\TradeMind.Brokers.MetaTrader5.Tests\TradeMind.Brokers.MetaTrader5.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Opt_in_real_demo_order"
```

The test reads the account and EURUSD instrument before submitting. It uses
the instrument's `MinimumQuantity`, sends a single market order, checks a
filled order and execution, checks one position, closes that position, and
finally reads positions again. A cleanup failure is a critical incident:
stop further execution, preserve the safe error code, and resolve the
remaining Demo position manually before rerunning anything.

The test is a no-op when either real-test flag or the exact confirmation is
missing. It must never be enabled in GitHub Actions or a normal full-suite
run. `AllowLive=true`, a Live account, a read-only account, a missing
heartbeat, missing plan/risk/session/permission/capability guards, a non-market
order, a non-EURUSD symbol, or a non-minimum volume is rejected before the EA
receives a write command.

## Error interpretation

The adapter exposes stable broker-neutral codes including
`OrderRejected`, `InvalidVolume`, `InvalidStops`, `MarketClosed`,
`NoConnection`, `InsufficientMargin`, `DemoOnly`, `LiveForbidden`,
`DuplicateExecution`, `CleanupFailed`, `Timeout` and `Cancelled` through its
existing error model. Identifiers are retained in in-memory execution results
for reconciliation, but never used as metric labels or emitted in routine
logs.

## Rollback and evidence

If the order is accepted but cleanup is not confirmed, do not rerun the test.
Use MT5 manually to confirm the Demo position state, then close it through the
approved Demo-only path. Keep only redacted status codes, counts and UTC
timestamps in the report. Never commit the generated `.ex5`, terminal logs,
tokens or account details.
