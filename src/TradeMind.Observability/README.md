# TradeMind Observability

This project composes the provider-neutral telemetry contracts with OpenTelemetry at the platform boundary. It owns stable `ActivitySource` and `Meter` registrations, context flow, safe tag enrichment, logging scopes, MediatR pipeline instrumentation, EF Core tracing, options validation and health reporting.

Analytical domain and application contracts do not reference this project. API and infrastructure composition may register it through dependency injection. Exporters are optional: OTLP and console output are disabled by default, while Prometheus is exposed only through the configured API mapping.

Sensitive payloads, credentials, SQL text and unbounded identity values are excluded from telemetry. Metric dimensions pass through an allow-list and trace/log metadata is bounded before it is emitted.
