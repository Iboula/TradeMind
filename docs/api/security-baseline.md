# Security Baseline

The API host provides transport and identity boundaries; application modules
remain provider-neutral.

- HTTPS redirection is enabled outside Test and HSTS is enabled in Production.
- Kestrel does not emit its default `Server` header.
- Responses receive `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` and `Referrer-Policy: no-referrer`.
- Request bodies are bounded and idempotency keys are validated.
- No wildcard CORS policy is registered.
- Detailed exception output is disabled for clients.
- API binding uses dedicated DTOs and never binds directly to EF entities or domain aggregates.
- JWT bearer validation and scoped `X-TradeMind-Api-Key` authentication are
  composed only when enabled by identity configuration.
- Secrets are not stored in committed configuration or written to request logs;
  raw API-key material is shown only once on create/rotate.
- Permission policies and tenant ownership filters run before identity and
  execution-session operations.

Development/test authentication is explicit, test-environment-only and rejected
by the Production startup guard. Authentication failures, authorization
failures and rate-limit rejections use ProblemDetails.
