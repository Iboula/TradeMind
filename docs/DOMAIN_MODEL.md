# TradeMind Domain Model

## Bounded contexts

| Context | Responsibility | Core aggregates/entities |
|---|---|---|
| KnowledgeHub | Ingest, structure, embed, index and retrieve knowledge | KnowledgeSource, KnowledgeFragment, Embedding, KnowledgeConcept, KnowledgeRelation |
| Identity and Access | Users, organizations, roles and permissions | User, Membership |
| Trading Journal | Planned and completed trade records | Trade, JournalEntry |
| Risk Management | Account constraints and pre-trade controls | TradingAccount, RiskProfile, RiskAssessment |
| Market Data | Instruments, candles, quotes and economic events | Instrument, MarketDataSubscription |
| Strategy Lab | Strategy versions, experiments and backtests | Strategy, BacktestRun |
| Alerts | User-defined conditions and delivery | AlertRule, Notification |
| AI Analysis | Grounded analysis requests and generated insights | AnalysisSession |

## KnowledgeHub aggregate

`KnowledgeSource` is the consistency boundary for an imported source and its ingestion state.

Key invariants:

- A source has a non-empty file name and media type.
- Extracted text cannot be empty.
- A fragmented source contains at least one non-empty fragment.
- Fragment positions are unique and ordered within a source.
- A fragment must have an embedding before indexing.
- The domain stores embedding values but has no dependency on the provider that generated them.

`KnowledgeFragment` is an entity owned by `KnowledgeSource`. `Embedding` is a one-to-one entity owned by a fragment. `KnowledgeConcept` and `KnowledgeRelation` are part of the target model and will be populated by later intelligence pipelines.

## Core relationships

- A KnowledgeSource owns ordered KnowledgeFragments.
- A KnowledgeFragment owns one Embedding.
- KnowledgeConcepts may be supported by many fragments.
- KnowledgeRelations connect concepts with an explicit relation type.
- AI Analysis retrieves grounded fragments through KnowledgeHub contracts rather than accessing its tables.
- A user owns one or more trading accounts.
- A trading account has one active risk profile and historical versions.
- A trade belongs to one account and may reference one strategy version.

## Trading Journal aggregate

`Trade` is the consistency boundary for a single trading decision.

- Symbol, direction, positive entry price and positive quantity are required.
- A trade cannot close twice.
- Closing time cannot precede opening time.
- Closed-trade financial metrics are derived from immutable execution facts.

## Risk Management aggregate

`RiskProfile` contains account-level limits.

- Account balance is positive.
- Percentage limits are greater than zero and at most 100.
- A proposed trade is approved only when calculated risk is within every active rule.
- Rule violations are retained as auditable facts.

## Integration rules

Events are raised by aggregates. Cross-module integration events will be persisted through an outbox after the originating transaction commits. Modules exchange identifiers and contracts, never domain entities or direct database access.

## Ubiquitous language

- **Knowledge source:** Original imported unit of knowledge.
- **Knowledge fragment:** Searchable normalized text segment belonging to one source.
- **Embedding:** Numeric representation used for semantic similarity.
- **Knowledge concept:** Canonical idea extracted from one or more fragments.
- **Knowledge relation:** Typed connection between two concepts.
- **R-multiple:** Profit or loss divided by initial planned risk.
- **Drawdown:** Decline from an equity peak to current equity.
- **Risk assessment:** Point-in-time evaluation of a proposed trade against active constraints.
