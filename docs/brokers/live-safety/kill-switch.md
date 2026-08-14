# Kill Switch

Kill switches are keyed by scope: `Global`, `Tenant`, `Broker`, `Account`, `Instrument` or `ExecutionSession`. A key is either `Disabled`, `Enabled` or `EmergencyStopped`. Disabled means that this specific switch is not active; it does not override any other safety condition.

`EmergencyStopped` is monotonic for normal operations. Automatic resume, ordinary enable and ordinary disable cannot clear it. Only an explicit reactivation call with an audit acknowledgement can return it to `Disabled`. The PostgreSQL implementation persists every transition and writes a critical audit entry.

The gate maps an active switch to `Blocked` and an emergency stop to `EmergencyStop`. Operator actions are tenant-scoped, permissioned, idempotent and auditable.
