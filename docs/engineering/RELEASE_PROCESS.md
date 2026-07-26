# Release Process

TradeMind is early in its product lifecycle, but releases should already follow a repeatable path.

## Release readiness

A branch is release-ready when:

- The intended scope is merged into the release branch.
- CI restore, build, and tests pass.
- Database migrations have been reviewed.
- Documentation reflects operationally relevant changes.
- No secrets are present in the repository.

## Versioning

Until public packages or external API contracts are introduced, release versions can follow product milestones. When API consumers or deployable packages depend on versioning, use semantic versioning:

- Major for breaking API or data contract changes.
- Minor for backward-compatible product capability.
- Patch for fixes.

## Database migrations

Migrations are part of the release artifact. A release that changes persistence must describe:

- The migration files included.
- Whether the migration is backward compatible.
- Expected data volume or index creation impact.
- Rollback considerations.

## Deployment validation

At minimum, deployment validation should include:

- API startup.
- Database connectivity.
- EF Core migration application.
- Health endpoint response.
- KnowledgeHub ingestion and search smoke checks when the release affects KnowledgeHub.

## Rollback

Rollback planning must consider code and database state together. Destructive migrations require special review and a recovery plan before release.

## Release notes

Release notes should summarize product changes, engineering changes, migration impact, and known limitations.
