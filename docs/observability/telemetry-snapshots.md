# Telemetry Snapshots

`TelemetrySnapshot` is a provider-neutral immutable comparison model. It captures bounded stage names, durations, failure counts, outcome, tenant ownership and schema version. It defensively copies and sorts stages, then computes a SHA-256 fingerprint from canonical fields.

The current sprint does not persist snapshots. There is therefore no telemetry migration and no claim of durable snapshot reload. Persistence can be added later through an explicit execution-session-owned table and concurrency policy without coupling the analytical domain to OpenTelemetry.

Comparison returns duration delta, failure-count delta, missing stages and schema compatibility. It is deterministic and does not use wall-clock time during comparison.
