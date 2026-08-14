# Live Safety Gate

The gate is the final policy boundary before a broker execution request. It receives a complete immutable `LiveTradingSafetyRequest` and returns a versioned `LiveTradingSafetyDecision`. A caller must inspect the decision status and causes; there is no global `CanTradeLive` boolean.

The evaluation is deterministic. Emergency stop has highest priority, configuration and account restrictions deny the request, quarantine and reconciliation issues require reconciliation, confirmation issues require confirmation, and all other missing controls block execution. An Allowed result is possible only when every hard Live enablement condition is true.

The gate does not call MT5, PostgreSQL, an LLM or an HTTP endpoint. It is a policy component. Infrastructure supplies durable kill switch, quarantine, ownership, audit and lock implementations through interfaces.

The `BrokerExecutionValidator` invokes the gate for a Live request. It supplies the known execution facts and leaves independent workspace, heartbeat, reconciliation and distributed-lock controls closed unless a future orchestrator explicitly provides verified evidence. This keeps the current product unable to send Live orders.
