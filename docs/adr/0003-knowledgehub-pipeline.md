# ADR 0003: KnowledgeHub ingestion and semantic-search pipeline

- Status: Accepted
- Date: 2026-07-17

## Context

TradeMind requires a durable, provider-independent memory capability that can ingest many source types, produce embeddings and retrieve relevant knowledge without coupling the domain to an AI vendor.

## Decision

KnowledgeHub is a bounded context centered on `KnowledgeSource`, `KnowledgeFragment`, `Embedding`, `KnowledgeConcept` and `KnowledgeRelation`.

The synchronous MVP pipeline is:

```text
Import → Extract → Normalize → Fragment → Embed → Persist → Index → Search
```

Application ports define all variable behavior. Infrastructure supplies the TXT importer, UTF-8 extractor, sliding-window fragmenter, deterministic fake embedding generator, EF Core persistence and Dapper/pgvector search.

PostgreSQL owns three initial tables: `knowledge_sources`, `knowledge_fragments` and `embeddings`. The vector extension and HNSW cosine index are created by migration. EF Core owns transactional writes; Dapper owns the vector read query.

## Consequences

- The domain remains independent from OpenAI and other providers.
- Fake embeddings make local and CI execution deterministic.
- Provider replacement does not change application use cases.
- Synchronous ingestion is intentionally limited to small TXT files in the MVP.
- Large-scale ingestion will move behind asynchronous jobs while preserving the interfaces and aggregate model.
