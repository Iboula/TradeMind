# Observability

TradeMind observability follows the analytical request from the API boundary through application handlers, persistence and execution-session workflows. It is an infrastructure capability, not a domain dependency.

The runtime uses `System.Diagnostics.Activity` and `System.Diagnostics.Metrics` behind provider-neutral interfaces. OpenTelemetry is composed at the API boundary and can export OTLP, Prometheus and development console diagnostics. No collector, cloud vendor or dashboard is required for the application to start.

## Entry points

- `/health/live`: process liveness, without database access.
- `/health/ready`: redacted dependency readiness.
- `/health/startup`: startup initialization state.
- `/metrics`: Prometheus exposition when enabled.
- `TradeMind:Observability`: strongly typed tracing, metrics, logging and health configuration.

## Boundaries

Domain and application contracts do not reference OpenTelemetry, ASP.NET Core, exporters, Grafana or Prometheus server packages. API and infrastructure composition provide instrumentation. Tenant, actor, session and correlation identifiers are trace/log fields only; they are never metric dimensions.

## Documentation map

- [Architecture](architecture.md)
- [Tracing](tracing.md)
- [Metrics](metrics.md)
- [Logging](logging.md)
- [Health checks](health-checks.md)
- [OpenTelemetry configuration](opentelemetry-configuration.md)
- [OTLP](otlp.md)
- [Prometheus](prometheus.md)
- [Grafana](grafana.md)
- [Telemetry snapshots](telemetry-snapshots.md)
- [Performance comparison](performance-comparison.md)
- [Security](security.md)
- [Threat model](threat-model.md)
- [Local stack](local-stack.md)
