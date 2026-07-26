# ADR-0002: CQRS

## Status

Accepted as an architectural direction.

## Context

TradeMind will combine write-heavy workflows, read-heavy analysis, and AI-assisted retrieval. In KnowledgeHub, ingestion changes state by creating sources and fragments, while retrieval and semantic search read optimized views of that state.

As the product expands, features such as trading journal analytics, market analysis, risk dashboards, and AI coach memory will likely need read models that differ from write models.

## Decision

TradeMind will use CQRS where it improves clarity or scalability.

Commands should represent state-changing operations. Queries should represent read operations and may use dedicated projections or SQL paths when needed. The current KnowledgeHub service keeps command-like ingestion and query-like lookup/search together because the module is still small. Splitting into explicit command and query handlers is expected when complexity increases.

## Consequences

Positive consequences:

- Clearer separation between mutation and reads.
- Room for optimized projections for search, dashboards, analytics, and AI context.
- Easier reasoning about validation and side effects.

Tradeoffs:

- CQRS should not be applied mechanically to every small method.
- More files and concepts can reduce clarity if introduced too early.
- Eventually consistent read models may be needed for more advanced features.

## Current application

KnowledgeHub currently contains:

- A command-like ingestion path through `KnowledgeHubService.IngestAsync`.
- Query-like source retrieval through `GetAsync`.
- Query-like semantic search through `SearchAsync`.

Future modules should introduce explicit command and query handlers when the use case count, validation rules, or read models justify the separation.
