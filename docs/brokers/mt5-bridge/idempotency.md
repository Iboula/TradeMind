# Idempotency and Replay

The client creates a fresh request/correlation/session identifier and nonce
for each command. It hashes the command payload and a stable operation key
with SHA-256. Safe query retries reuse the exact transport envelope, so a
retry cannot accidentally become a different logical request.

The host keeps bounded in-memory replay and idempotency records for the
current demo process. A matching idempotency key and request hash returns the
previous normalized response. Reusing the key with a different hash returns
`Conflict`; reusing a nonce with a different request returns
`ReplayDetected`. A duplicate nonce for the same request returns
`DuplicateRequest`.

The demo store is intentionally process-local and non-durable. A future
multi-instance deployment must replace it with a shared store or gateway-side
idempotency mechanism before using the bridge for any operation with live
side effects. This sprint does not claim cross-host durability.

```mermaid
flowchart TD
    Request --> Key{Known idempotency key?}
    Key -->|No| Nonce{Nonce reserved?}
    Key -->|Same hash| Replay[Return stored response]
    Key -->|Different hash| Conflict[409 Conflict]
    Nonce -->|Conflict| Rejected[409 ReplayDetected]
    Nonce -->|New| Execute[Execute demo command]
    Execute --> Store[Store normalized response]
