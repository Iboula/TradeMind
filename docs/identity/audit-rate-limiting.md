# Audit and Rate Limiting

Identity mutations write structured audit entries containing event type,
timestamp, actor, organization, tenant, resource, outcome and permission
context. Audit metadata is intentionally constrained: keys containing secret,
token or credential markers are rejected. Audit writing is an application port
and the PostgreSQL adapter persists it without exposing credential material.

The API host provides a fixed-window rate-limit foundation. Partitions use the
API-key identifier, JWT subject, or remote address fallback. Limits and window
are strongly typed configuration values. Rejections return HTTP 429
ProblemDetails and a `Retry-After` header. The rate limiter is operational
protection, not an authorization decision and does not replace per-resource
permissions.

Operational logs use correlation identifiers and must never record bearer
tokens, raw API keys, password values or authorization headers. Readiness
checks report only configured dependency state; sensitive configuration values
are not returned.
