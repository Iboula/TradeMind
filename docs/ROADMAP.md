# TradeMind Roadmap

## Sprint 1 — KnowledgeHub MVP

Status: implemented on `agent/knowledge-module`.

- TXT ingestion
- extraction and normalization
- overlapping fragmentation
- deterministic fake embeddings
- PostgreSQL and pgvector persistence
- top-five semantic search
- Docker Compose
- unit and integration tests
- CI pipeline

## Sprint 2 — Production ingestion

- asynchronous ingestion jobs
- PDF and HTML extractors
- source metadata and checksums
- idempotent re-import
- ingestion progress and failure recovery
- object storage abstraction

## Sprint 3 — Knowledge intelligence

- configurable embedding provider
- concepts and relations extraction
- hybrid lexical/vector search
- citations and source traceability
- retrieval quality evaluation

## Sprint 4 — SaaS foundation

- identity and tenant ownership
- authorization policies
- tenant-isolated data access
- quotas, plans and usage metering
- audit trail and observability

## Sprint 5 — Trading intelligence

- market-data ingestion
- strategy knowledge linking
- risk context retrieval
- AI analysis workflows
- human-reviewed recommendations
