# Roadmap

This roadmap describes a coherent product path. It is ordered by dependency and product maturity, not by final release names.

## Phase 1: KnowledgeHub

Build the durable knowledge foundation.

- Ingest text and Markdown sources.
- Persist sources and fragments.
- Store embeddings in PostgreSQL with pgvector.
- Search semantically by cosine distance.
- Add support for additional source types such as PDF, websites, YouTube, podcasts, broker rules, prop firm rules, CSV, Excel, JSON, and images.
- Improve source status, failures, deduplication, and reprocessing.

## Phase 2: AI Providers

Introduce production AI provider integrations behind abstractions.

- Add provider-agnostic chat and embedding contracts.
- Add OpenAI chat and embedding providers behind the AI abstraction layer.
- Add provider configuration without committing secrets.
- Add retry, timeout, rate-limit, and observability patterns.
- Support provider swapping for cost, quality, or compliance.

## Phase 3: Memory Engine

Turn KnowledgeHub retrieval into durable AI memory.

- Build memory records and retrieval policies.
- Rank context by relevance, recency, source trust, and user intent.
- Store AI interaction summaries when approved.
- Keep sensitive trading data governed by explicit retention rules.

## Phase 4: Trading Journal

Capture trading decisions and outcomes.

- Record trade plans, entries, exits, screenshots, notes, and emotions.
- Link journal entries to KnowledgeHub sources and market context.
- Extract recurring mistakes and strengths.
- Prepare analytics for behavior and process quality.

## Phase 5: AI Coach

Use journal and knowledge context to provide coaching.

- Review trades against rules and playbooks.
- Ask reflective questions.
- Highlight risk violations and process drift.
- Avoid pretending to guarantee trade outcomes.

## Phase 6: Market Analysis

Add structured market context.

- Track instruments, sessions, news, events, levels, and scenarios.
- Support AI-assisted summaries with citations to source material.
- Keep analysis separate from execution advice.

## Phase 7: Strategy Builder

Let users define and refine strategies.

- Model setup rules, entry criteria, exit criteria, filters, and risk rules.
- Link strategy rules to KnowledgeHub evidence.
- Version strategies over time.

## Phase 8: Backtesting

Validate strategies with historical data.

- Import market data.
- Execute deterministic backtests.
- Report expectancy, drawdown, win rate, risk-adjusted metrics, and failure modes.
- Compare strategy versions.

## Phase 9: Risk Engine

Make risk constraints explicit.

- Position sizing.
- Daily loss limits.
- Drawdown controls.
- Prop firm rule tracking.
- Portfolio-level exposure.

## Phase 10: Portfolio

Provide account and portfolio views.

- Aggregate performance by instrument, strategy, session, and period.
- Track exposure and correlations.
- Connect trading journal outcomes with risk metrics.

## Phase 11: SaaS

Prepare for hosted multi-user operation.

- Authentication and authorization.
- Tenant isolation.
- Billing and subscriptions.
- Operational monitoring.
- Data export and deletion.

## Phase 12: Enterprise

Support teams and institutional use cases.

- Organization management.
- Roles and permissions.
- Audit logs.
- Compliance controls.
- Admin reporting.
- Private deployment options.
