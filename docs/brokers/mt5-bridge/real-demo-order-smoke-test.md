# MT5 real Demo order smoke test

This runbook describes the only supported write path in Sprint 32. It submits
one market order for the locally selected `MT5_REAL_TEST_SYMBOL` at the
broker-reported minimum volume through the TradeMind broker connector,
verifies the resulting order, deal and position, then closes the position and
verifies that no position remains. The validated local symbol is
`XAUUSD-VIP`; the test never falls back to `EURUSD`.

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
- `MT5_REAL_TEST_SYMBOL=XAUUSD-VIP`;
- the selected symbol has no open position or pending order before the test;
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
`BridgeUrl` is `https://127.0.0.1:5001`, and that exact URL must be present in
the MT5 WebRequest allow-list. The Bridge Host targets
`https://localhost:5001` because the local .NET development certificate is
issued for `localhost`; both endpoints remain loopback-only.
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
$env:MT5_REAL_TEST_SYMBOL = "XAUUSD-VIP"
$env:MT5_BRIDGE_HOST_ENDPOINT = "https://localhost:65172"
dotnet test .\tests\TradeMind.Brokers.MetaTrader5.Tests\TradeMind.Brokers.MetaTrader5.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Opt_in_real_demo_order"
```

The test reads the account and selected instrument before submitting. It
verifies the broker-reported trading specification and free margin, uses the
instrument's `MinimumQuantity`, sends a single market order, checks a filled
order and execution, verifies idempotent replay and conflict handling without
a second external order, closes that position, and finally reads positions
and pending orders again. A cleanup failure is a critical incident:
stop further execution, preserve the safe error code, and resolve the
remaining Demo position manually before rerunning anything.

The test is a no-op when either real-test flag or the exact confirmation is
missing. It must never be enabled in GitHub Actions or a normal full-suite
run. `AllowLive=true`, a Live account, a read-only account, a missing
heartbeat, missing plan/risk/session/permission/capability guards, a non-market
order, an empty symbol, or a non-minimum volume is rejected before the EA
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

## Emergency procedure

This procedure applies when a Demo order was accepted but the expected cleanup
or reconciliation result is missing. It is intentionally operator-led and does
not attempt automatic position closure.

1. Stop all further execution immediately. Do not rerun the test, resubmit the
   order, or start another write-capable process.
2. Preserve the original error code, correlation identifier, execution session
   identifier and UTC timestamps. Redact credentials, server names, account
   numbers, tokens and raw terminal packets.
3. Inspect the Demo terminal manually in read-only mode and determine whether
   the order, deal or position still exists. Record only the classification and
   count in the incident evidence.
4. Keep the broker safety state blocked when a cleanup failure or unknown
   position is reported. Restore it only after an operator has reconciled the
   terminal state and confirmed that no residual position or pending order is
   present.
5. Close a residual Demo position manually through the approved Demo-only
   procedure, then repeat the read-only orders and positions checks. Do not
   enable Live mode as a recovery measure.

The lifecycle service preserves the original reconciliation failure. A cleanup
failure is critical, blocks subsequent writes, emits bounded telemetry and
requires explicit operator review. Orphan detection is read-only and
classifies positions as `Known`, `Orphaned`, `Unreconciled`, `Stale` or
`UnknownExternal`; it never auto-closes a position.

### A. Position remains open

Disable further writes, inspect the Demo terminal manually, close the position
manually if required, verify that the position disappears from a read-only
positions query, and record the incident using redacted evidence.

### B. Heartbeat lost

Stop execution and keep only safe reads where available. Restart the EA or the
bridge, verify a fresh heartbeat, and reconcile orders and positions before
resuming any Demo write.

### C. TerminalBridge unavailable

Do not retry `SubmitOrder` blindly. Wait for the bridge to recover, query by
client order identifier, reconcile the terminal state, and only then decide
whether the original request is already complete.

### D. Cleanup failure

Treat the result as critical, preserve the original failure, block subsequent
writes, and follow the manual intervention procedure in this section. Resume
only after the residual position and pending-order checks are clear.

### E. Unknown external position

Never auto-close it. Mark it `UnknownExternal` or `Unreconciled`, keep writes
blocked, and require explicit operator review before any manual Demo action.
