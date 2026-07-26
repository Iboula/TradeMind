# ADR-0015: API Host and Contract Isolation

## Status

Accepted.

## Context

TradeMind V1 Core contains deterministic application modules with domain contracts, EF Core infrastructure and provider-neutral AI abstractions. The next delivery needs an HTTP host without allowing transport concerns, serializers or API clients to leak into those modules. The host also needs consistent validation, errors, correlation, health and command replay behavior.

## Decision

Use the existing `TradeMind.Api` project as a Minimal API composition root. The host registers module composition extensions, accepts immutable API-specific records, delegates to application interfaces and maps results into transport-safe contracts. Route versioning uses `/api/v1`; schema versions are explicit in envelopes where a module contract evolves independently.

Cross-cutting behavior is implemented at the host boundary: RFC-style ProblemDetails, correlation middleware, structured request logging, a TimeProvider-backed request timeout, bounded request bodies, health endpoints and an in-memory idempotency abstraction for selected command-like routes. Infrastructure references are limited to composition and configured dependency health checks. Domain projects remain unaware of ASP.NET Core.

## Consequences

The API is easy to host and test with `WebApplicationFactory`, while module business logic remains reusable outside HTTP. The in-memory idempotency store is single-instance and must be replaced or coordinated with a distributed store before multi-instance command processing. Authentication and authorization remain an explicit future platform concern. Modules without a configured application facade return a structured unsupported-operation response rather than a fabricated result.

## Alternatives considered

- Controllers and a heavy API versioning package were rejected because the repository has no controller convention and route versioning is sufficient for V1.
- Serializing domain results directly was rejected because it couples the public contract to internal aggregates and can leak implementation details.
- A database-backed idempotency store was deferred because Sprint 24 does not add persistence to the host.
