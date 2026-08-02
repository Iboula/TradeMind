# Protocol v1

The public wire prefix is `/bridge/v1`. The negotiated protocol is
`BridgeProtocolVersion(1, 0)`. A request is compatible when its major version
matches and its minor version is not newer than the host minor version. A
major mismatch is rejected before command execution.

## Envelope

Every command carries `BridgeRequestMetadata`:

- `requestId` identifies one transport attempt;
- `correlationId` links the request to the caller trace;
- `executionSessionId` scopes a client session;
- `timestampUtc`, `deadlineUtc` and `nonce` control freshness;
- `idempotencyKeyHash` and `normalizedRequestHash` are SHA-256 values.

The response echoes request, correlation and session identifiers in
`BridgeResponseMetadata`. A response contains either a neutral `payloadJson`
or a bounded `BridgeError`; error messages are safe summaries and never raw
terminal packets.

## Commands

Supported operations are health, account, instrument, order, position and
execution queries plus submit, modify, cancel and close. Command fields use
the existing adapter wire shape:

```json
{
  "command": "submit-order",
  "fields": {
    "account_id": "mt5-demo-account",
    "client_order_id": "demo-1",
    "instrument": "EURUSD",
    "side": "Buy",
    "order_type": "Market",
    "quantity": "1",
    "time_in_force": "Day"
  }
}
```

The host limits payload size, command field count, identifier length and
error message length before invoking a gateway. Values crossing the boundary
are neutral records. Native MT5 DTOs cannot be represented by these contracts.

## Error semantics

Errors include a stable `BridgeErrorCode`, a safe message, retryability and
whether the operation is safe to retry. Transport failures, timeouts,
protocol mismatches, authentication failures, replay conflicts and demo-mode
violations are distinct. A submit-order error is never made retryable merely
because the transport failed.
