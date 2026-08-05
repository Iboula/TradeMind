# MT5 Terminal Bridge Expert Advisor

`TradeMindTerminalBridge.mq5` is the terminal-side component for the
external .NET Terminal Bridge. Install it manually in the MT5 terminal's
Experts directory and compile it with MetaEditor. It is deliberately outside
the TradeMind .NET solution and never references Core projects.

Configuration is supplied as EA inputs. The agent token must be entered from
a local secret store and must never be committed. The terminal must allow
`https://localhost:5001` in its WebRequest settings.

The EA reports only a demo account and rejects write commands in its current
Phase 1 implementation. The source is provided for an explicit terminal-side
deployment; CI cannot compile or execute MQL5 and therefore does not claim a
real MT5 connection.
