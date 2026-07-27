# Identity and Authorization

TradeMind identity is a provider-neutral foundation composed of three layers:

- `TradeMind.Identity.Domain` contains immutable users, organizations, tenants,
  actors, permissions and API-key lifecycle rules.
- `TradeMind.Identity.Application` contains current-actor contracts, authorization
  evaluation, API-key use cases, audit ports and configuration options.
- `TradeMind.Identity.Infrastructure` contains PostgreSQL mappings, migrations,
  PBKDF2 credential handling and JWT claim mapping. It is composed by the API host.

The core does not issue tokens, store raw credentials, call a broker, or depend on
ASP.NET. The API host is the resource server: it validates bearer tokens and API
keys, resolves the tenant, applies policies and maps failures to ProblemDetails.

## Documents

- [Architecture](architecture.md)
- [Authentication](authentication.md)
- [API keys](api-keys.md)
- [Authorization](authorization.md)
- [Multi-tenancy](multitenancy.md)
- [Audit and rate limiting](audit-rate-limiting.md)

Identity PostgreSQL migrations are applied through the normal infrastructure
migration process. No seed user, password, token, API key or secret is committed.
