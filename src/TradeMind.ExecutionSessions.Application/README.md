# Execution Sessions Application

The application layer exposes commands, queries and ports for the Execution
Sessions bounded context. MediatR handlers load an aggregate through
`IExecutionSessionRepository`, apply a domain transition, and persist the
aggregate together with an audit entry and transactional outbox message through
`IExecutionSessionUnitOfWork`.

Queries expose DTOs for the session, timeline, search and replay manifest. The
layer does not know EF Core, PostgreSQL, HTTP or a broker. Time comes through
`TimeProvider`, cancellation is passed to every port, and repositories are
responsible for translating optimistic concurrency failures into the explicit
application exception.
