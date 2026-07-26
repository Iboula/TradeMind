# Security Baseline

Sprint 24 provides a transport baseline, not full identity security.

- HTTPS redirection is enabled outside Test and HSTS is enabled in Production.
- Kestrel does not emit its default `Server` header.
- Responses receive `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` and `Referrer-Policy: no-referrer`.
- Request bodies are bounded and idempotency keys are validated.
- No wildcard CORS policy is registered.
- Detailed exception output is disabled for clients.
- API binding uses dedicated DTOs and never binds directly to EF entities or domain aggregates.
- Secrets are not stored in committed configuration or written to request logs.

Authentication and authorization are deliberately not faked. The planned platform integration phase will add identity, policy evaluation, tenant isolation and audit requirements at the host boundary before production exposure.
