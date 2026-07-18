# ADR 0007: Automatic migrations for the MVP

- Status: Accepted
- Date: 2026-07-17

## Context

The first demonstrable MVP must start through Docker Compose without a separate database administration step.

## Decision

The API applies KnowledgeHub EF Core migrations during startup after PostgreSQL becomes healthy. Docker Compose waits for the database health check before starting the API.

## Consequences

Local startup is simple and reproducible. Before production deployment, migrations will move to a dedicated deployment job so multiple API replicas cannot race during schema changes.
