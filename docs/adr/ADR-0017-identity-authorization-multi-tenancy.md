# ADR-0017: Identity, Authorization and Multi-Tenant Foundations

- Status: Accepted
- Date: 2026-07-27
- Scope: Sprint 26

## Context

The analytical core now has an API host and durable execution sessions. It needs
a resource-server security boundary without coupling domain or application
modules to ASP.NET, EF Core, JWT libraries or a concrete identity provider.
Tenant ownership must also be enforced at repository boundaries rather than by
trusting route values.

## Decision

Introduce a provider-neutral Identity module with immutable actor, organization,
tenant, permission and API-key contracts. Compose JWT bearer validation and a
scoped `X-TradeMind-Api-Key` handler in the API host. Persist API-key hashes,
permissions, status, expiry, usage and concurrency data in PostgreSQL. Keep
authorization policy-based and map roles to an allow-listed permission set.
Resolve tenant scope from validated identity, and pass a provider-neutral
access scope into execution-session repositories.

Raw API-key secrets are transient and returned once on create/rotate. PBKDF2
with a random salt and fixed-time comparison is the baseline implementation.
Production fails fast when real identity is disabled or development
authentication is enabled. Test authentication is explicit and test-only.

## Consequences

The API host owns authentication middleware and ProblemDetails, while the core
remains reusable by non-HTTP hosts. PostgreSQL receives a new identity schema
and an execution-session ownership migration. Cross-tenant reads and writes
become not-found operations. A future provider can implement the existing
Application ports without changing domain policy.

The design intentionally does not provide token issuance, passwords, broker
execution, or a vendor-specific identity SDK. Rate limiting and audit are
foundations; a future platform phase may add external policy stores, distributed
quotas and richer operational telemetry.
