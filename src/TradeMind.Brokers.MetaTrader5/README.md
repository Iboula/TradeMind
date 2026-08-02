# TradeMind.Brokers.MetaTrader5

This project is the Sprint 29 MetaTrader 5 adapter boundary. It translates
provider-neutral `IBrokerConnector` calls into an internal, serialized MT5
protocol and translates responses back into `TradeMind.Brokers.Domain`
contracts.

The adapter currently contains a deterministic simulation bridge only. It does
not reference an MT5 SDK, open a network socket, store credentials, or execute
live orders. `Mode=Live` and `AllowLive=true` are rejected during validation and
construction. Demo mode requires `AllowDemo=true`.

## Composition

Hosts opt in with `AddTradeMindMetaTrader5(configuration)`. The host only sees
the existing `IBrokerConnector` registration; MT5 protocol records, mapping
types, connection states, and bridge details remain inside this assembly.

The connection state machine is explicit:

`Disconnected -> Connecting -> Authenticating -> Connected`

Failures enter `Faulted`; recovery enters `Reconnecting`. Disposal enters
`Closed`. There is no public boolean connection flag.

## Configuration

```json
{
  "TradeMind": {
    "Brokers": {
      "MetaTrader5": {
        "Host": "localhost",
        "Port": 0,
        "TimeoutSeconds": 10,
        "HeartbeatSeconds": 30,
        "ReconnectAttempts": 3,
        "Mode": "Simulation",
        "AllowDemo": false,
        "AllowLive": false
      }
    }
  }
}
```

Host, port and protocol settings are deliberately retained as adapter
configuration so a future bridge can be introduced without changing core
contracts. The Sprint 29 bridge remains local and simulation-only.
