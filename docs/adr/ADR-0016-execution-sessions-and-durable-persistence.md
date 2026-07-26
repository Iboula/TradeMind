# ADR-0016: Execution Sessions and Durable Persistence

## Status

Accepted

## Context

TradeMind has a deterministic analytical pipeline made of independent modules.
The API needs one durable correlation boundary that can connect a request to
its context, analyses, consensus, decision, risk evaluation, plan, workspace,
assistant response and paper-trading result. The boundary must survive process
restarts and coordinate multiple API pods without process-local locks.

The same boundary must support audit, event publication, replay diagnostics and
idempotent HTTP commands. Storing full business results inside one aggregate
would couple module schemas and make the session row unbounded.

## Decision

Introduce a dedicated Execution Sessions bounded context with Domain,
Application and Infrastructure projects. The domain owns a versioned,
immutable-by-observation aggregate with explicit lifecycle and stage invariants.
The application layer owns commands, queries and persistence ports. The
infrastructure layer owns EF Core, Npgsql, migrations, the repository and
transactional outbox.

Use PostgreSQL optimistic concurrency with a unique idempotency key. Store typed
artifact references and content hashes instead of embedding analytical
payloads. Write the session, audit and outbox rows in one transaction. Capture
successful API pipeline responses after the business endpoint has completed and
link them as replayable artifacts when a known stage exists.

## Consequences

- Sessions remain queryable after restarts and across pods.
- Conflicts are explicit HTTP 409 responses instead of lost updates.
- Audit and outbox records cannot be committed independently of a session
  transition.
- Replay manifests are deterministic, bounded and honest about missing data.
- API response completion and artifact linking are separate operations; a link
  failure is logged and recoverable, but does not roll back the response.
- A future outbox worker, payload store and authentication boundary are still
  required for full platform operation.

## Alternatives rejected

- In-memory session state: loses history on restart and cannot coordinate pods.
- One shared EF context: breaks module ownership and makes migrations coupled.
- Process-local locks: cannot guarantee correctness across replicas.
- Embedding every result in the session row: creates schema coupling and
  unbounded rows.
- A distributed transaction around the analytical pipeline: unavailable across
  independent modules and external providers.
