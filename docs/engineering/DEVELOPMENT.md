# Development

This document describes the local development workflow for TradeMind.

## Prerequisites

- .NET 9 SDK.
- Docker Desktop or another Docker-compatible engine.
- Git.
- Access to the repository branch being worked on.

## Repository layout

- `src/TradeMind.Api`: ASP.NET Core API host.
- `src/TradeMind.KnowledgeHub.Domain`: KnowledgeHub domain model.
- `src/TradeMind.KnowledgeHub.Application`: KnowledgeHub use cases and ports.
- `src/TradeMind.KnowledgeHub.Infrastructure`: KnowledgeHub adapters and persistence.
- `tests/TradeMind.KnowledgeHub.Tests`: unit tests.
- `tests/TradeMind.KnowledgeHub.IntegrationTests`: PostgreSQL/pgvector integration tests.
- `docs`: architecture, engineering, ADR, and product documentation.
- `.github/workflows/ci.yml`: restore, build, and test workflow.

## Local database

The repository includes `docker-compose.yml` with PostgreSQL and pgvector using `pgvector/pgvector:pg17`. The local service creates a `trademind` database with a non-secret development password.

Start the local database with:

```powershell
docker compose up -d postgres
```

The API applies EF Core migrations during startup. Integration tests apply migrations inside their Testcontainers database.

## Running the solution

Restore packages:

```powershell
dotnet restore TradeMind.sln
```

Build in Release mode:

```powershell
dotnet build TradeMind.sln --configuration Release
```

Run tests:

```powershell
dotnet test TradeMind.sln --configuration Release
```

Run the API after PostgreSQL is available:

```powershell
dotnet run --project src/TradeMind.Api/TradeMind.Api.csproj
```

## Configuration

The API expects a `ConnectionStrings:KnowledgeHub` connection string. Local development may use appsettings, user secrets, environment variables, or Docker Compose defaults. Real secrets must not be committed.

AI provider configuration lives under the `AI` section. The checked-in OpenAI API key value is intentionally empty. Use environment variable `AI__OpenAI__ApiKey`, user secrets, or a future secret store for real credentials.

## Migrations

EF Core migrations live in the Infrastructure project for the module they support. A schema change must be represented by a migration and covered by tests when it affects behavior.

## Common workflow

1. Pull the latest branch.
2. Read the module being changed.
3. Make focused edits.
4. Run restore, build, and tests.
5. Commit one coherent change.
6. Push the branch and let CI validate.
