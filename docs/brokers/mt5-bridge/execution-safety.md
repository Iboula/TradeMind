# Demo Execution Safety

The real gateway permits order commands only after all of these checks pass:

- host configuration remains `DemoOnly=true` and `AllowLive=false`;
- the terminal handshake reports a demo account, trading enabled and not read-only;
- the connection is `Connected` and a recent heartbeat is valid;
- the request explicitly declares `mode=Demo` and `demo_confirmation=true`;
- risk approval, Trading Plan validity and Execution Session validity are explicitly true;
- permission and capability guards are explicitly true.

The gateway rejects `mode=Live` before transport execution. It does not calculate risk, position size or trading decisions. It consumes the existing neutral command contract and returns normalized safe error categories. Unknown terminal failures become `TERMINAL_FAILURE`; native terminal error codes and messages do not cross the bridge boundary.

Transport failures do not replay order commands. Reconnect is bounded and sequential. Cancellation is propagated to heartbeat, handshake, transport, reconnect delay and process lifecycle operations. There is no static lock or global semaphore.

The Sprint 31 gateway is demo/simulation only. It does not implement live trading, copy trading, Expert Advisors, Strategy Tester or broker-side credentials beyond the terminal-side bridge token.
