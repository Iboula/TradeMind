# MT5 Terminal Bridge Expert Advisor

`TradeMindTerminalBridge.mq5` is the terminal-side component for the
external .NET Terminal Bridge. Install it manually in the MT5 terminal's
Experts directory and compile it with MetaEditor. It is deliberately outside
the TradeMind .NET solution and never references Core projects.

Configuration is supplied as EA inputs. The agent token must be entered from
a local secret store and must never be committed. The terminal must allow
`https://127.0.0.1:5001` in its WebRequest settings.

The EA reports only a demo account. Sprint 32 adds one explicitly gated Demo
write path: a single EURUSD market order at the broker-reported minimum
volume and a matching position close for cleanup. Pending orders, modification,
cancellation, partial close, scaling and all Live operations remain rejected.
The source is provided for an explicit terminal-side deployment; CI cannot
compile or execute MQL5 and therefore does not claim a real MT5 connection.
