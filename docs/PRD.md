# TradeMind Product Requirements Document

## 1. Product vision

TradeMind is an AI-assisted trading research and decision-support platform. It helps discretionary traders prepare, validate, execute and review decisions while enforcing explicit risk rules. TradeMind does not promise profitability and does not replace regulated financial advice.

## 2. Target users

- Beginner traders who need structure and risk discipline.
- Intermediate discretionary traders who need journaling and performance feedback.
- Strategy researchers who need repeatable backtesting and experiment tracking.
- Prop-firm traders who must respect account-specific drawdown rules.

## 3. MVP outcomes

The MVP must allow a user to:

1. Define an account and risk profile.
2. Calculate position size and reject trades outside configured limits.
3. Record planned, open and closed trades.
4. Attach a strategy, setup, notes and screenshots to a journal entry.
5. Review performance by symbol, strategy, session and period.
6. Receive AI-generated explanations grounded in the user's own journal and market context.

## 4. Functional scope

### Trading Journal

- Trade lifecycle: draft, planned, open, closed, cancelled.
- Entry, stop-loss, take-profit, size, fees and timestamps.
- Tags, strategy, timeframe, screenshots and psychology notes.
- Computed R-multiple, P&L and rule-compliance status.

### Risk Management

- Account balance and equity snapshots.
- Maximum risk per trade, daily loss and total drawdown.
- Position sizing by stop distance and instrument specification.
- Pre-trade validation and auditable rule violations.

### Market Intelligence

- Economic calendar and market-data ingestion through replaceable providers.
- Watchlists and price alerts.
- AI analysis with source attribution, uncertainty and safety disclaimers.

### Strategy Lab

- Strategy definitions and versioning.
- Backtest runs with immutable inputs and results.
- Metrics including win rate, expectancy, profit factor and maximum drawdown.

## 5. Non-functional requirements

- .NET 9 modular monolith using DDD boundaries.
- PostgreSQL with schema ownership per module.
- No direct database access across modules.
- Idempotent external-data ingestion.
- UTC storage for timestamps and explicit market time zones at boundaries.
- Structured logs, distributed traces, health checks and audit trails.
- Encryption in transit and at rest; secrets never committed.
- Unit, integration, architecture and end-to-end tests.

## 6. Out of scope for MVP

- Autonomous live-trade execution.
- Copy trading or management of third-party funds.
- Guaranteed signals or return promises.
- High-frequency or latency-sensitive execution.

## 7. Success measures

- At least 90% of journaled trades include a risk plan.
- Position-sizing results are reproducible and covered by tests.
- Users can identify their best and worst setups within two minutes.
- AI responses cite their inputs and clearly indicate uncertainty.
