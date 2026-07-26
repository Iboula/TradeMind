# Vision

TradeMind is an AI-assisted trading research and decision-support platform for disciplined traders.

The product helps traders capture knowledge, analyze markets, validate strategies, understand risk, review decisions, and improve execution habits. It should not replace trader responsibility. It should make evidence, context, and tradeoffs easier to see.

## Product principles

- Preserve the trader's reasoning trail.
- Favor explainable analysis over opaque recommendations.
- Separate research, planning, execution review, and coaching.
- Treat risk management as a first-class product area.
- Keep AI providers abstract so the platform can evolve with model quality, cost, and compliance needs.

## Current foundation

The current implemented foundation is KnowledgeHub. It ingests text and Markdown sources, fragments content, generates deterministic embeddings through an abstraction, stores source and fragment data in PostgreSQL with pgvector, and supports semantic search.

This creates the base for memory, AI coach context, strategy research, and domain-specific retrieval.

## Long-term direction

TradeMind should become a modular trading intelligence workspace:

- KnowledgeHub for trusted source material.
- Memory Engine for durable AI context.
- Trading Journal for decisions and outcomes.
- AI Coach for reflective feedback.
- Market Analysis for structured context.
- Strategy Builder and Backtesting for validation.
- Risk Engine and Portfolio views for capital discipline.
- SaaS and Enterprise capabilities for teams, permissions, billing, audit, and compliance.
