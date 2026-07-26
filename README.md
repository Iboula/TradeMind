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
- GitHub Actions with centralized package versions, dependency checks, Release build, tests, and coverage.

## Getting Started
The architecture and engineering baseline is documented in [docs/architecture](docs/architecture/README.md).
Start with [the dependency rules](docs/architecture/DEPENDENCY_RULES.md),
[the engineering guide](docs/engineering/ARCHITECTURE.md), and
[the development guide](docs/engineering/DEVELOPMENT.md).

## License
MIT
