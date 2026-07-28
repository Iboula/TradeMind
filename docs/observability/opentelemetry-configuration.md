# OpenTelemetry Configuration

Configuration is bound to `TradeMind:Observability` and validated on startup. Invalid sampling ratios, unsupported protocols, malformed OTLP endpoints, unsafe header lengths and invalid Prometheus paths fail fast before serving traffic.

Tracing and metrics can be independently disabled. OTLP and console exporters default to disabled. Prometheus is enabled by default for local diagnostics and can be disabled in environments where the endpoint is not exposed. Console export is intended for Development and is not automatically enabled in Production.

Sampling supports `ParentBased` and `TraceIdRatio`. The default ratio is `1.0` so local diagnosis is complete; production deployments should select a deliberate ratio while retaining parent-based sampling for distributed consistency.

Exporter headers are configuration secrets. They are never included in `/api/v1/observability/info`, health responses, logs or metrics labels.
