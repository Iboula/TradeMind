# Operator Actions

The supported actions are `Resume`, `Reconcile`, `Quarantine`, `Acknowledge`, `EmergencyStop`, `RequestManualClose` and `MarkExternallyResolved`. Actions carry an idempotency identifier, tenant, actor, required permission, target and reason.

Actions are tenant-scoped and reject an explicit target belonging to another tenant. Repeating the same action identifier replays the original result. Emergency stop is routed to the scoped kill switch. This sprint provides contracts and policy behavior, not a UI workflow; a future dual-control workflow can map a Trader proposal to a Risk Manager or Administrator approval.
