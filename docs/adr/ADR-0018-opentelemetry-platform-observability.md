# ADR-0018: OpenTelemetry-Based Platform Observability

## Status

Accepted for Sprint 27.

## Context

TradeMind now has a multi-stage analytical pipeline, durable execution sessions, tenant ownership and API authentication. Operators need one correlated view from HTTP through application handlers without adding vendor SDKs to domain modules or exposing sensitive trading and identity data.

## Decision

Use `System.Diagnostics.Activity` and `System.Diagnostics.Metrics` behind `TradeMind.Observability.Abstractions`. Compose OpenTelemetry only in the infrastructure/API layer. Use W3C trace context for distributed traces, a bounded immutable `TelemetryContext` for application correlation, and an allow-listed metric dimension set. Keep OTLP and console exporters optional, and expose Prometheus only when configured.

Execution-session IDs remain durable business correlation, not trace identity. Multiple traces can reference one session. Asynchronous outbox/replay work uses ActivityLink when a direct parent is not valid.

Health endpoints return redacted status. Telemetry snapshots are currently immutable comparison models and are not persisted; no migration is introduced in this sprint.

## Consequences

Domain assemblies remain independent from OpenTelemetry, ASP.NET Core, Serilog and Grafana. The API gains composition dependencies and configuration validation. Exporter delivery, backend retention and access control remain deployment responsibilities. The repository can test activities and metrics in-process without requiring a collector.
