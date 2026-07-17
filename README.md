# TradeMind

TradeMind is an AI-assisted trading research and decision-support platform for disciplined market analysis, risk management, strategy validation and trade review.

> TradeMind is educational and decision-support software. It does not guarantee profitability and is not a substitute for regulated financial advice.

## Architecture

The platform starts as a **.NET 9 DDD Modular Monolith**. Each bounded context owns its domain model, use cases, adapters and PostgreSQL schema. Module boundaries are designed so that a capability can later be extracted without redesigning the whole system.

Initial modules:

- Trading Journal
- Risk Management
- Market Data
- Strategy Lab
- Alerts
- AI Analysis
- Identity and Access

## Repository structure

```text
TradeMind/
├── src/
│   ├── BuildingBlocks/
│   ├── Modules/
│   │   ├── TradingJournal/
│   │   └── RiskManagement/
│   └── TradeMind.Api/
├── tests/
│   ├── TradeMind.Architecture.Tests/
│   └── TradeMind.Modules.TradingJournal.UnitTests/
├── docs/
│   ├── adr/
│   ├── DOMAIN_MODEL.md
│   └── PRD.md
├── Directory.Build.props
├── TradeMind.slnx
└── .editorconfig
```

## Technology baseline

- .NET 9 and ASP.NET Core Minimal APIs
- PostgreSQL with one schema per module
- DDD, CQRS-oriented application services and explicit module contracts
- OpenAPI, health checks and Problem Details
- xUnit, FluentAssertions, NetArchTest and architecture tests
- Docker and Kubernetes planned for deployment
- Replaceable market-data and LLM providers

## Getting started

Prerequisites: .NET 9 SDK.

```bash
dotnet restore TradeMind.slnx
dotnet build TradeMind.slnx
dotnet test TradeMind.slnx
dotnet run --project src/TradeMind.Api
```

The API exposes:

- `/` — service metadata
- `/health` — health probe
- `/openapi/v1.json` — OpenAPI document
- `/api/trading-journal/status` — module status
- `/api/risk-management/status` — module status

## Documentation

- [Product Requirements](docs/PRD.md)
- [Domain Model](docs/DOMAIN_MODEL.md)
- [ADR 0001 — DDD Modular Monolith](docs/adr/0001-modular-monolith.md)

## Engineering rules

- Modules do not read each other's database tables.
- Modules do not reference another module's Domain or Infrastructure project.
- Cross-module workflows use explicit contracts and integration events.
- Shared building blocks remain small and stable.
- All timestamps are stored in UTC.
- Risk calculations must be deterministic and covered by tests.

## License

MIT
