# Code Style

TradeMind uses modern C# on .NET 9 with nullable reference types, implicit usings, and warnings treated as errors.

## C# conventions

- Use clear names that reflect the domain language.
- Prefer small classes with focused responsibilities.
- Use records for immutable DTO-style contracts.
- Keep domain entities responsible for their invariants.
- Prefer expression-bodied members only when they remain easy to read.
- Keep comments rare and useful.

## Async

- Use async APIs for I/O, database access, streams, and provider calls.
- Avoid blocking on async work with `.Result`, `.Wait()`, or sync-over-async patterns.
- Return `Task` or `Task<T>` for asynchronous operations.

## CancellationToken

- Accept `CancellationToken` on application and infrastructure methods that perform I/O or potentially long-running work.
- Pass the token through to EF Core, stream, provider, and command APIs.
- Check cancellation explicitly in CPU-bound provider logic when appropriate.

## Dependency injection

- Register module services through module-level extension methods.
- Keep DI composition in API or Infrastructure, not in Domain.
- Prefer scoped repositories and DbContexts.
- Prefer singleton stateless services for deterministic utilities.

## Logging

Logging should be added when behavior needs operational visibility. Do not log secrets, raw credentials, full embeddings, or sensitive trading journal content. Prefer structured logging with meaningful event context.

## Exceptions

- Use domain or standard exceptions to protect invariants.
- Use validation results at API boundaries where user input is invalid.
- Preserve useful exception messages without leaking secrets.
- Do not swallow exceptions unless the application state is deliberately updated to represent failure.

## SQL

- Prefer EF Core LINQ for ordinary persistence.
- Raw SQL is allowed for PostgreSQL-specific features such as pgvector search.
- Always parameterize values.
- Quote identifiers when the EF migration created case-sensitive column names.
- Keep SQL local to Infrastructure.

## EF Core

- Keep DbContext and mappings in Infrastructure.
- Use migrations for schema changes.
- Use PostgreSQL-specific extension configuration where required, including pgvector provider registration.
- Use `AsNoTracking()` for read-only queries.
- Keep cascade and uniqueness rules explicit when they represent domain constraints.

## Tests

- Unit tests should be fast and independent of external services.
- Integration tests should use Testcontainers for PostgreSQL behavior.
- Tests should name the behavior under test.
- Avoid tests that depend on execution order.
- Keep test data deterministic.
