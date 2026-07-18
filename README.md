# TradeMind

TradeMind is an enterprise-grade AI-assisted trading intelligence platform designed to learn from large knowledge collections, support traders, analyze markets, evaluate risk, improve strategies and evolve into a multi-user SaaS product.

> TradeMind is educational and decision-support software. It does not guarantee profitability and is not a substitute for regulated financial advice.

## Architecture

TradeMind uses **.NET 9**, DDD, CQRS with MediatR and a Modular Monolith. Each bounded context owns its model, use cases, persistence and public contracts. AI providers are always accessed through interfaces; the domain never depends on OpenAI or another vendor.

Current modules:

- KnowledgeHub
- Trading Journal
- Risk Management

## KnowledgeHub MVP

KnowledgeHub implements this pipeline:

```text
KnowledgeSource → Import → Extract → Normalize → Fragment → Embedding → Index → Semantic Search
```

The MVP supports:

- `.txt` upload up to 10 MB
- UTF-8 extraction and normalization
- overlapping text fragmentation
- deterministic 64-dimension fake embeddings
- PostgreSQL persistence
- pgvector HNSW cosine index
- semantic search returning the five nearest fragments

## Run with Docker

```bash
docker compose up --build
```

The API is available at `http://localhost:8080` and PostgreSQL/pgvector at `localhost:5432`.

Upload a source:

```bash
curl -F "file=@knowledge.txt;type=text/plain" http://localhost:8080/knowledge/sources
```

Read a source:

```bash
curl http://localhost:8080/knowledge/sources/{id}
```

Search:

```bash
curl "http://localhost:8080/knowledge/search?query=risk%20management"
```

## Local development

Prerequisites: .NET 9 SDK, Docker and Docker Compose.

```bash
docker compose up -d postgres
dotnet restore TradeMind.slnx
dotnet build TradeMind.slnx
dotnet test TradeMind.slnx
dotnet run --project src/TradeMind.Api
```

Database migrations run automatically when the API starts.

## Repository structure

```text
src/
├── BuildingBlocks/
├── Modules/
│   ├── KnowledgeHub/
│   ├── RiskManagement/
│   └── TradingJournal/
└── TradeMind.Api/
tests/
docs/
```

## Documentation

- [Product Requirements](docs/PRD.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Domain Model](docs/DOMAIN_MODEL.md)
- [Roadmap](docs/ROADMAP.md)
- [ADR 0001 — DDD Modular Monolith](docs/adr/0001-modular-monolith.md)
- [ADR 0003 — KnowledgeHub pipeline](docs/adr/0003-knowledgehub-pipeline.md)

## Engineering rules

- Modules do not read each other's tables.
- Modules do not reference another module's Domain or Infrastructure project.
- Cross-module workflows use contracts or integration events.
- AI dependencies are replaceable infrastructure adapters.
- All timestamps are UTC.
- Pushes and pull requests must restore, build and pass all tests in GitHub Actions.

## License

MIT
