# Bridge Security

The bridge is designed for a private service-to-service network. Production
transport requires TLS and mutual TLS by default. Signed service tokens are
also supported through the `Authorization: Bearer` header; token material is
provided at runtime and is not stored in the repository.

Plain HTTP is accepted only when both client and host explicitly opt into
`AllowInsecureDemoTransport` for a local demo. The host still requires a
certificate marker or signed token according to its configured authentication
mode.

## Request protections

- protocol compatibility is checked before execution;
- timestamps must be inside the configured clock-skew window;
- deadlines are checked before queueing and linked to request cancellation;
- nonces are retained for the replay window;
- idempotency keys are compared with the normalized request hash;
- payload and collection limits are enforced by immutable contracts;
- concurrency is bounded per host/client instance.

Logs may contain operation names, status classes and failure type names. They
must not contain passwords, login values, servers, account numbers,
credentials or raw MT5 packets. The implementation logs only normalized
transport/protocol failure categories.

`AllowLive` defaults to false and validation rejects live environments. This
is a demo safety boundary, not an authorization system for future production
trading.
