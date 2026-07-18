# AGENTS

This guide defines how Codex and other automated contributors should work in TradeMind.

## Operating principles

TradeMind is a .NET 9 modular monolith. Work should preserve the current architecture unless a requested change explicitly requires a different structure. Read the relevant module before editing. Prefer the existing style, naming, dependency direction, and test patterns over introducing new conventions.

The current implemented module is KnowledgeHub. It contains a Domain project, an Application project, an Infrastructure project, an API host, unit tests, and PostgreSQL/pgvector integration tests. Future modules should follow the same separation.

## Allowed work style

1. Inspect the current branch and working tree before changing files.
2. Understand the module boundary and the layer being edited.
3. Make the smallest change that satisfies the request.
4. Keep production code, tests, migrations, and documentation changes separate unless the task requires them together.
5. Run the relevant validation commands before finishing.
6. Report the exact files changed, commands run, test results, and residual risks.

## Conventions

- Use .NET 9 and C# with nullable reference types enabled.
- Keep `TreatWarningsAsErrors` enabled.
- Respect Domain-Driven Design boundaries.
- Keep domain entities free of infrastructure dependencies.
- Express application behavior through services and interfaces in the Application layer.
- Put database, file, provider, and framework details in Infrastructure.
- Use EF Core migrations for schema changes.
- Use PostgreSQL and pgvector for persistent semantic search.
- Use Testcontainers for integration tests that require PostgreSQL.
- Keep OpenAI and other AI providers behind abstractions. Do not couple domain logic to a concrete AI SDK.

## Prohibitions

- Do not replace PostgreSQL with an in-memory database for integration behavior.
- Do not remove pgvector when semantic search is part of the behavior.
- Do not introduce secrets, API keys, passwords, or tokens into the repository.
- Do not weaken warning policies or remove validation.
- Do not move code across module boundaries without a clear architectural reason.
- Do not change namespaces, public contracts, migrations, or tests as drive-by cleanup.
- Do not add broad abstractions before a concrete need exists.
- Do not edit unrelated user changes.

## Definition of Done

A change is done when:

- The requested behavior or documentation exists.
- The solution restores successfully.
- `dotnet build TradeMind.sln --configuration Release` succeeds with zero warnings.
- `dotnet test TradeMind.sln --configuration Release` succeeds.
- Integration tests use real PostgreSQL when database behavior is being verified.
- The CI workflow can execute the same validation path.
- Documentation is updated when the change affects architecture, workflow, or operations.
- The final report includes changed files, commands, outcomes, and remaining risks.

## Sprint workflow

1. Clarify the sprint goal and the module affected.
2. Identify the relevant domain concepts and current implementation.
3. Add or update tests that describe the expected behavior.
4. Implement the smallest production change.
5. Run local validation.
6. Update documentation or ADRs when the decision has lasting impact.
7. Commit one coherent unit of work.
8. Push the branch and leave the PR ready for review.

## Git workflow

- Work on feature branches such as `feature/knowledgehub-mvp`.
- Keep commits atomic and named with conventional prefixes such as `feat:`, `fix:`, `test:`, `docs:`, or `refactor:`.
- Stage only files that belong to the task.
- Do not merge PRs from automation.
- Push to the feature branch and let GitHub Actions validate restore, build, and tests.

## Architecture rules

- Domain has no dependency on Application, Infrastructure, API, EF Core, Npgsql, or external providers.
- Application depends on Domain and defines ports such as repositories, extractors, fragmenters, and embedding generators.
- Infrastructure depends on Application and Domain and implements external concerns.
- API composes modules through dependency injection and exposes HTTP endpoints.
- Tests may reference the layers needed to verify behavior, but unit tests should avoid external infrastructure.
- Integration tests may use Docker and Testcontainers to verify database behavior.
