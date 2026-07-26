# TradeMind API

The API host is the HTTP composition root for the deterministic TradeMind core. It owns transport concerns only: request validation, API-specific contracts, correlation, logging, error mapping, idempotency, OpenAPI and health endpoints. Business calculations remain in the application modules.

## Run locally

From the repository root:

```powershell
dotnet run --project src/TradeMind.Api/TradeMind.Api.csproj
```

The host uses the `TradeMind:Api` configuration section. Safe defaults are committed in `src/TradeMind.Api/appsettings.json`; local overrides belong in `appsettings.Local.json`, environment variables or user secrets.

## Endpoints

Versioned routes use `/api/v1`:

- `POST /api/v1/market-context/build`
- `POST /api/v1/experts/dispatch`
- `POST /api/v1/experts/analyze`
- `POST /api/v1/consensus/build`
- `POST /api/v1/trading-decisions/evaluate`
- `POST /api/v1/risk/evaluate`
- `POST /api/v1/trading-plans/generate`
- `POST /api/v1/trading-workspaces/build`
- `POST /api/v1/trading-assistant/ask`
- `POST /api/v1/paper-trading/simulate`

KnowledgeHub source and search routes are also available under `/api/v1/knowledge` when a KnowledgeHub connection is configured. `GET /api/v1/system/version` exposes non-sensitive version metadata.

The pipeline routes are composition endpoints. They accept versioned transport envelopes and invoke the corresponding application facade. A module that is not configured returns a structured `501` response; the host never fabricates a business result.

## OpenAPI and health

OpenAPI is available at `/openapi/v1.json` outside Production when enabled. Swagger UI is intentionally not enabled by default.

- `/health/live` checks only that the process can serve requests.
- `/health/ready` checks configured database dependencies and returns `503` when one is unavailable.

## Cross-cutting behavior

- `X-Correlation-ID` is accepted when it matches the configured safe character policy and is generated otherwise.
- `Idempotency-Key` is required for workspace, assistant and paper-trading commands. The Sprint 24 store is in-memory and therefore single-instance only.
- Request and response bodies are not logged by default.
- Validation and unexpected failures use RFC-style ProblemDetails without stack traces, file paths or connection strings.
- Request bodies are bounded by `TradeMind:Api:PayloadLimits:MaximumBodyBytes`.
- HTTPS redirection is enabled outside the Test environment, HSTS is enabled in Production, and authentication is intentionally deferred to the platform integration phase.

## Architecture boundary

API contracts are immutable records and do not expose EF entities, aggregates or infrastructure models. Endpoint code delegates to application abstractions and maps results into transport-safe envelopes. Domain projects do not reference ASP.NET Core.
