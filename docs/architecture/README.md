# Architecture Documentation

This directory describes the current TradeMind architecture.

TradeMind is a .NET 9 modular monolith. KnowledgeHub is exposed through an ASP.NET Core API and backed by PostgreSQL with pgvector. Provider-agnostic AI foundations cover providers, orchestration, sessions, prompts, conversational memory, Knowledge RAG, and controlled tools.

## Documents

- [System Context](SYSTEM_CONTEXT.md): users, external systems, and boundaries.
- [Containers](CONTAINERS.md): deployable/runtime units and storage.
- [Components](COMPONENTS.md): internal projects and layer responsibilities.
- [Data Flow](DATA_FLOW.md): ingestion, retrieval, semantic search, startup, and test flows.
- [AI Orchestration](AI_ORCHESTRATION.md): provider-agnostic request pipeline and extension steps.
- [Prompt Engine](PROMPT_ENGINE.md): versioned templates and deterministic rendering.
- [Memory Engine](MEMORY_ENGINE.md): bounded conversational context and in-memory storage.
- [Knowledge RAG Engine](KNOWLEDGE_RAG_ENGINE.md): retrieval, budgeting, citations, and context composition.
- [Tool Engine](TOOL_ENGINE.md): controlled discovery, authorization, validation, execution, and result composition.
- [Agent Framework](AGENT_FRAMEWORK.md): versioned agent definitions, policy, authorization, and bounded execution.
- [Trading Coach](TRADING_COACH.md): deterministic journal analysis, structured coaching, and safety boundaries.
- [Trading Journal Analytics](TRADING_JOURNAL_ANALYTICS.md): deterministic multi-trade statistics, trends, data quality, and bounded interpretation.

## Current architectural shape

The system currently contains:

- ASP.NET Core API host.
- KnowledgeHub Domain, Application, and Infrastructure projects.
- PostgreSQL database with pgvector extension.
- EF Core migrations.
- Docker Compose for local PostgreSQL.
- GitHub Actions for restore, build, and tests.
- Testcontainers for PostgreSQL/pgvector integration tests.
- Provider-agnostic AI Application, Memory, Knowledge RAG, and Tool Engine projects.
- Provider-agnostic versioned Agent Framework.
- Trading Coaching business module with the educational `trading-coach` `1.0.0` agent.
- Trading Analytics business module with deterministic historical analysis and the optional `journal-analysis` `1.0.0` agent.

Future modules should be added as modules inside the monolith before any service extraction is considered.
