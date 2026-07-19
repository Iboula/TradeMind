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
- Add provider-agnostic AI orchestration for validation, prompt construction, capability checks, execution, response normalization, and logging.
- Add AI session and execution context tracking for identity propagation, execution state, durations, tokens, and safe errors.
- Add a versioned Prompt Engine for reusable provider-agnostic prompt templates.
- Add a Knowledge RAG Engine for bounded KnowledgeHub context retrieval and internal citations.
- Add a controlled provider-agnostic Tool Engine for explicit, authorized, validated, timeout-bound application capabilities.
- Add a versioned, provider-agnostic Agent Framework for declarative agent identity, discovery, authorization, engine policy composition, and safe execution.
- Use `generic-assistant` as the reference agent, retain `trading-coach` `0.1.0` as a development skeleton, and publish the bounded educational `trading-coach` `1.0.0` business-agent MVP.
- Design autonomous loops and multi-agent coordination separately, after step budgets, delegation, repeated authorization, termination, idempotency, and audit rules are approved.
- Add provider-native tool calling only after loop limits, model-proposed argument policy, confirmations, and multi-call security are defined.
- Provide context contribution extension points for future KnowledgeHub and Memory enrichment.
- Add provider configuration without committing secrets.
- Add retry, timeout, rate-limit, and observability patterns.
- Support provider swapping for cost, quality, or compliance.

## Phase 3: Memory Engine

Build conversation memory for AI sessions.

- Manage conversation history by tenant, user, and conversation.
- Add bounded context windows for recent user and assistant messages.
- Add conversation summaries for older exchanges.
- Keep Memory Engine separate from RAG and KnowledgeHub document retrieval.
- Replace in-memory storage with PostgreSQL persistence when retention and privacy rules are finalized.
- Keep sensitive trading data governed by explicit retention rules.

## Phase 4: Trading Journal

Capture trading decisions and outcomes.

- Record trade plans, entries, exits, screenshots, notes, and emotions.
- Link journal entries to KnowledgeHub sources and market context.
- Extract recurring mistakes and strengths.
- Prepare analytics for behavior and process quality.
- Map an authorized journal read model into the existing immutable Coaching and Analytics contracts when persistence ownership, retention, and deletion policies are approved.

## Phase 5: AI Coach

Use journal and knowledge context to provide coaching.

- Review trades against rules and playbooks.
- Ask reflective questions.
- Highlight risk violations and process drift.
- Avoid pretending to guarantee trade outcomes.
- Deliver the first journal-analysis MVP with deterministic validation, metrics, rules, process scoring, strict JSON, linked actions, and final safety filtering.
- Keep the MVP limited to explicitly supplied journal snapshots until Trading Journal persistence and cross-trade retention policies are approved.
- Add cross-trade pattern history only through an authorized journal read model; do not give the coach direct database access.
- Deliver deterministic multi-trade journal analytics for descriptive aggregates, historical R drawdown, streaks, UTC cohorts, setup consistency, behavior trends, risk drift, observed post-outcome associations, score evolution, and data quality.
- Offer `journal-analysis` `1.0.0` only as optional narrative enrichment over deterministic analytics, with no prediction, causality, signal, market access, broker access, or Tool Engine.

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
