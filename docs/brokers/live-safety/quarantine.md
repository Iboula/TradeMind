# Execution Quarantine

An execution is quarantined after an ambiguous timeout, broker state mismatch, orphan position, cleanup failure, duplicate fill, stale reconciliation, reconnect during submit or unknown broker response. A quarantine record is tenant-scoped and contains only broker-neutral identifiers and a safe explanation.

Quarantined executions cannot be retried by the safety layer. Release is an explicit operator action after reconciliation and is append-audited. PostgreSQL stores the state durably; the in-memory implementation is used only when persistence is not configured and is not a multi-pod guarantee.
