# Incident Response

1. Stop new execution with the smallest applicable kill switch scope, or use the global scope for uncertainty.
2. Keep the system alive and inspect readiness. A blocked execution readiness is not a liveness failure.
3. Run the read-only recovery service and reconciliation. Compare non-terminal executions, broker orders, broker positions and ownership.
4. Quarantine ambiguous executions and preserve `ExecutionUnknown` outcomes. Do not resubmit an unknown request.
5. Treat `UnknownExternal` positions as manual-review items. Do not auto-close them.
6. Record the operator decision and evidence in the append-only audit trail.
7. Reactivate an emergency stop only after the incident is reviewed and the explicit audit acknowledgement is recorded.

Credentials, passwords, complete account identifiers and raw connector packets must not appear in logs or audit metadata.
