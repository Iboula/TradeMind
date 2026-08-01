# TradeMind

TradeMind is an AI-assisted trading research and decision-support platform focused on helping traders analyze markets, validate strategies, and improve risk management.

## Vision
Build a modular platform that combines market data, AI reasoning, backtesting, and trade journaling to support disciplined trading.

## Planned Features
- AI market analysis
- Trading journal
- Strategy backtesting
- Risk management dashboard
- Economic calendar integration
- Alerts and notifications
- Performance analytics

## Current platform
- .NET 9 modular monolith with Clean Architecture, DDD, and CQRS boundaries.
- PostgreSQL with EF Core and pgvector for KnowledgeHub persistence and semantic search.
- Docker Compose for local infrastructure and Testcontainers for PostgreSQL integration tests.
- Provider-agnostic AI contracts; concrete providers stay outside core modules.
- Provider-neutral identity and authorization foundations with JWT resource-server validation,
  scoped PostgreSQL API keys, tenant resolution and repository-level ownership filters.
- Provider-neutral OpenTelemetry observability with correlated traces, bounded metrics,
  redacted health probes, optional OTLP export, Prometheus exposition and local Grafana assets.
- Provider-neutral broker execution contracts with simulation-only in-memory execution,
  PostgreSQL idempotency, audit and read-only reconciliation; no live connector is included.
- GitHub Actions with centralized package versions, dependency checks, Release build, tests, and coverage.

## Getting Started
The architecture and engineering baseline is documented in [docs/architecture](docs/architecture/README.md).
Start with [the dependency rules](docs/architecture/DEPENDENCY_RULES.md),
[the engineering guide](docs/engineering/ARCHITECTURE.md), and
[the development guide](docs/engineering/DEVELOPMENT.md).

Durable analytical correlation is documented in [Persistence and Execution
Sessions](docs/persistence/README.md), including PostgreSQL migrations,
optimistic concurrency, audit, outbox, durable idempotency and replay manifests.

### Run the API

```powershell
dotnet run --project src/TradeMind.Api/TradeMind.Api.csproj
```

The versioned analytical routes are documented in [docs/api](docs/api/README.md). OpenAPI is served at `/openapi/v1.json` outside Production when enabled and declares JWT bearer plus `X-TradeMind-Api-Key` schemes. Process liveness is available at `/health/live`; configured dependency readiness is available at `/health/ready`. Identity operations and their security boundaries are documented in [docs/identity](docs/identity/README.md). Test authentication is available only in the Test environment and is disabled by default.

Observability conventions, exporters, health probes and local dashboards are documented in [docs/observability](docs/observability/README.md). Prometheus metrics are exposed at `/metrics` when enabled; OTLP export remains optional and disabled by default.

Broker contracts, execution safety, idempotency and future adapter boundaries are documented in [docs/brokers](docs/brokers/README.md). Sprint 28 ships no broker SDK, credentials or live order execution.

## License
MIT
