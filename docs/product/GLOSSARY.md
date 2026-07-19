# Glossary

## AI Coach

The product capability for educational review of trades, journal entries, rules, and knowledge sources. Its first implementation is the bounded `trading-coach` `1.0.0` journal-analysis MVP.

## AI Provider

A concrete model provider such as OpenAI. Providers must remain behind application abstractions.

## Backtesting

Running a strategy against historical market data to estimate behavior, risk, and failure modes.

## CQRS

Command Query Responsibility Segregation. Commands change state. Queries read state.

## DDD

Domain-Driven Design. A modeling approach that keeps business language, boundaries, and invariants explicit.

## EF Core

Entity Framework Core, the ORM used for PostgreSQL persistence and migrations.

## Embedding

A numeric vector representation of text or other content used for semantic comparison.

## HNSW

Hierarchical Navigable Small World, an approximate nearest-neighbor index type used by pgvector.

## KnowledgeFragment

A chunk of extracted source content with token count, sequence, source id, and embedding.

## KnowledgeHub

The module that stores trusted source material and makes it searchable for AI-assisted workflows.

## KnowledgeSource

A domain object representing imported knowledge such as notes, Markdown, PDFs, broker rules, prop firm rules, research, or future media types.

## Memory Engine

A future capability that retrieves, stores, and ranks durable context for AI workflows.

## Modular monolith

A single deployable application organized internally into well-defined modules.

## pgvector

The PostgreSQL extension used to store vectors and perform similarity search.

## PostgreSQL

The primary relational database for TradeMind.

## Risk Engine

A future capability for position sizing, drawdown control, account rules, and exposure management.

## Strategy Builder

A future capability for defining, versioning, and validating trading strategies.

## Testcontainers

A testing library used to run disposable Docker containers for integration tests.

## Trading Coach

A provider-agnostic educational agent that analyzes explicitly supplied journal data. It prioritizes deterministic metrics and process findings, emits strict structured output, and cannot access market data, a broker, orders, or trading tools.

## Process Score

A bounded and explained review score for plan adherence, risk discipline, execution quality, emotional control, journal completeness, or overall process quality. It is not a prediction of profitability.

## Rule-Based Finding

A deterministic coaching observation produced by application code from supplied journal facts. A provider can enrich its explanation but cannot remove it or change its calculated inputs.
