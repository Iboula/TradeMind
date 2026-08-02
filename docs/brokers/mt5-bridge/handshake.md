# Handshake

The client first calls `GET /health/ready`, then posts a handshake containing
the protocol version, adapter version, demo requirement, account environment,
client time, required capabilities and request metadata.

```mermaid
sequenceDiagram
    participant Client
    participant Host
    Client->>Host: GET /health/ready
    Host-->>Client: 200 when demo gateway is ready
    Client->>Host: POST /bridge/v1/handshake
    Host->>Host: authenticate and validate protocol
    Host->>Host: validate clock, demo mode and capabilities
    Host-->>Client: BridgeHandshakeResponse
    Client->>Host: POST /bridge/v1/health or /execute
```

The response reports negotiated protocol, bridge and adapter versions,
environment, account environment, terminal state, terminal version, server
time, clock skew, capabilities and a normalized error when negotiation fails.
Unknown capabilities are rejected rather than silently ignored. A client
cannot use handshake success to enable live mode because both execution
layers enforce demo-only behavior independently.
