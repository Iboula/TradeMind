# TradeMind Domain Model

## Bounded contexts

| Context | Responsibility | Core aggregates |
|---|---|---|
| Identity and Access | Users, organizations, roles and permissions | User, Membership |
| Trading Journal | Planned and completed trade records | Trade, JournalEntry |
| Risk Management | Account constraints and pre-trade controls | TradingAccount, RiskProfile, RiskAssessment |
| Market Data | Instruments, candles, quotes and economic events | Instrument, MarketDataSubscription |
| Strategy Lab | Strategy versions, experiments and backtests | Strategy, BacktestRun |
| Alerts | User-defined conditions and delivery | AlertRule, Notification |
| AI Analysis | Grounded analysis requests and generated insights | AnalysisSession |

## Core relationships

- A user owns one or more trading accounts.
- A trading account has one active risk profile and historical versions.
- A trade belongs to one account and may reference one strategy version.
- A pre-trade risk assessment evaluates a proposed trade against the active risk profile and current account state.
- A closed trade produces performance facts consumed by journal analytics and AI analysis.
- A backtest run uses an immutable strategy version, market-data snapshot and parameter set.

## Trading Journal aggregate

`Trade` is the consistency boundary for a single trading decision.

Key invariants:

- Symbol, direction, positive entry price and positive quantity are required.
- A trade cannot close twice.
- Closing time cannot precede opening time.
- Closed-trade financial metrics are derived from immutable execution facts.
- Notes and tags enrich a trade but never alter execution facts.

## Risk Management aggregate

`RiskProfile` contains account-level limits.

Key invariants:

- Account balance is positive.
- Percentage limits are greater than zero and at most 100.
- A proposed trade is approved only when its calculated risk is within every active rule.
- Rule violations are retained as auditable facts rather than overwritten.

## Domain events

Initial events include:

- TradePlanned
- TradeOpened
- TradeClosed
- RiskAssessmentCompleted
- RiskLimitBreached
- StrategyVersionPublished
- BacktestCompleted
- AlertTriggered
- AnalysisCompleted

Events are raised by aggregates. Cross-module integration events are produced through an outbox after the originating transaction commits.

## Ubiquitous language

- **R-multiple:** Profit or loss divided by initial planned risk.
- **Risk amount:** Maximum currency amount lost if stop-loss is executed as planned.
- **Drawdown:** Decline from an equity peak to current equity.
- **Setup:** Repeatable market conditions used to classify a trade.
- **Strategy version:** Immutable rules and parameters used for a trade or backtest.
- **Risk assessment:** Point-in-time evaluation of a proposed trade against active constraints.
