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
- `src/TradeMind.AI.Abstractions`: provider-agnostic AI contracts.
- `src/TradeMind.AI.Application`: AI orchestration, prompt construction, validation, capability checks, and response normalization.
- `src/TradeMind.AI.Knowledge`: provider-agnostic KnowledgeHub RAG retrieval, context budgeting, citations, prompt composition, and optional orchestration step.
- `src/TradeMind.AI.Memory`: provider-agnostic conversation memory contracts, in-memory store, window reader, writer, summarizer, and optional orchestration steps.
- `src/TradeMind.AI.Tools`: provider-agnostic tool definitions, registry, discovery, authorization, validation, controlled execution, result composition, and deterministic demonstration tools.
- `src/TradeMind.AI.Infrastructure`: concrete AI provider registration and SDK adapters.
- `tests/TradeMind.AI.Tests`: AI provider registration and orchestration tests.
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

AI product features should compose provider and orchestration registrations together:

```csharp
services.AddTradeMindAI(configuration);
services.AddTradeMindAIOrchestration();
```

`AddTradeMindAIOrchestration()` also registers the Prompt Engine. `AddTradeMindPromptEngine()` can be used independently when a test or future module only needs template rendering.

`AddTradeMindMemory()` registers the in-memory Memory Engine and optional memory orchestration steps. Memory remains opt-in per request through `UseMemory`; callers must provide a conversation id when memory is enabled.

`AddTradeMindKnowledgeRag()` registers optional KnowledgeHub RAG retrieval and composition. Knowledge retrieval remains disabled per request unless `AIOrchestrationRequest.Knowledge.Enabled` is true.

`AddTradeMindAIToolOrchestration()` registers the Tool Engine plus explicit tool execution and result composition steps. Tool invocation remains disabled unless `AIOrchestrationRequest.Tool.Enabled` is true, and the caller must provide a structured tool id and arguments. Development-only tools require explicit local configuration:

```csharp
services.AddTradeMindAIOrchestration();
services.AddTradeMindAIToolOrchestration(options =>
{
    options.EnableDevelopmentTools = true;
});
```

Do not enable development tools in production policy. Do not derive `AIToolInvocationOptions` directly from free-form model or user text. Tests should use deterministic `IAITool` implementations and controlled `TimeProvider` instances; Tool Engine tests require no network, database, broker, or secret.

Unit and composition tests should inject fake `IChatProvider` and `IAIProviderMetadata` implementations rather than requiring external AI calls. Tests that verify AI session timestamps, prompt rendering timestamps, or execution duration should inject a controlled `TimeProvider`.

## Migrations

EF Core migrations live in the Infrastructure project for the module they support. A schema change must be represented by a migration and covered by tests when it affects behavior.

## Common workflow

1. Pull the latest branch.
2. Read the module being changed.
3. Make focused edits.
4. Run restore, build, and tests.
5. Commit one coherent change.
6. Push the branch and let CI validate.
