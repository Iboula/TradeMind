# Architecture Decision Records

TradeMind uses Architecture Decision Records to capture decisions that shape the system over time.

An ADR should be added or updated when a decision affects architecture, persistence, module boundaries, deployment, security, or provider strategy. ADRs are not task notes; they explain durable choices and their consequences.

## Current ADRs

- [ADR-0001: Modular monolith](ADR-0001-modular-monolith.md)
- [ADR-0002: CQRS](ADR-0002-cqrs.md)
- [ADR-0003: PostgreSQL and pgvector](ADR-0003-postgresql-pgvector.md)
- [ADR-0004: AI provider abstraction](ADR-0004-ai-provider-abstraction.md)
- [ADR-0005: AI orchestration pipeline](ADR-0005-ai-orchestration-pipeline.md)
- [ADR-0006: AI session and execution context](ADR-0006-ai-session-execution-context.md)
- [ADR-0007: Prompt Engine](ADR-0007-prompt-engine.md)
- [ADR-0008: Conversational Memory Engine](ADR-0008-memory-engine.md)
- [ADR-0009: Knowledge RAG Engine](ADR-0009-knowledge-rag-engine.md)
- [ADR-0010: Controlled AI Tool Engine](ADR-0010-tool-engine.md)
- [ADR-0011: Versioned AI Agent Framework](ADR-0011-agent-framework.md)
- [ADR-0012: Trading Coach Analysis MVP](ADR-0012-trading-coach-mvp.md)
- [ADR-0013: Trading Journal Analytics](ADR-0013-trading-journal-analytics.md)
- [ADR-0014: Core Stabilization and Quality Gates](ADR-0014-core-stabilization.md)
- [ADR-0015: API Host and Contract Isolation](ADR-0015-api-host-contract-isolation.md)
- [ADR-0016: Execution Sessions and Durable Persistence](ADR-0016-execution-sessions-and-durable-persistence.md)

## ADR format

Each ADR should include:

- Status.
- Context.
- Decision.
- Consequences.
- Alternatives considered when useful.

The repository also contains earlier module-specific notes such as `0002-knowledge-source-model.md`. New foundational ADRs use the `ADR-0000-name.md` naming convention.
