# ADR-0013: Trading Journal Analytics

- Status: Accepted
- Date: 2026-07-19

## Context

`TradeMind.Trading.Coaching` analyzes one explicitly supplied journal snapshot. That boundary is appropriate for a trade review, but it cannot describe changes across time, repeated behavior, risk drift, streaks, setup cohorts, or score evolution. A historical analysis also has different failure modes: duplicate records can bias counts, missing dates can invalidate period comparisons, small samples can make trends misleading, and an AI provider can overstate association as causation.

The product needs multi-trade educational analytics without introducing journal persistence, market data, broker access, execution behavior, or provider-specific infrastructure. Numeric and categorical results must remain reproducible and testable without an AI provider.

## Decision

Create the provider-agnostic `TradeMind.Trading.Analytics` module. It accepts a bounded immutable collection of `TradingJournalAnalysisRequest` values supplied by a trusted caller and reuses Coaching validation, normalization, metrics, behavior detection, rule analysis, and deterministic process scores.

The module separates single-trade and historical responsibilities:

- Coaching remains authoritative for one trade's validation, metrics, findings, and scores.
- Analytics owns collection validation, date filtering, deduplication, chronology, aggregation, cohorts, trends, streaks, historical R drawdown, data quality, and cross-trade findings.
- Neither module persists journal entries or reads another module's database.

All statistics are descriptive. Mean, median, extrema, rates, duration, cumulative historical R drawdown, streaks, and first-versus-recent comparisons are computed in code with invariant culture. Missing values are omitted only from the metric that requires them; they remain visible in data-quality limitations.

Time cohorts support no grouping, UTC day, UTC week, and UTC month. Weeks begin Monday at `00:00:00Z` and use an exclusive end boundary. Setup cohorts normalize whitespace and case for grouping while retaining deterministic display and ordering. Small cohorts are marked insufficient instead of being labeled profitable or unprofitable.

Risk drift, behavior trends, post-outcome comparisons, and score evolution are deterministic. Post-outcome output uses "observed association" and never makes a causal claim. Process scores come from the existing Coaching engine; profit and loss do not replace those scores.

Data quality is a first-class result based on trade count, field completeness, invalid entries, duplicates, timestamp coverage, and consistency. Confidence is a bounded qualitative label, not a probability that an analysis or future outcome is correct.

An optional `journal-analysis` agent version `1.0.0` can enrich the summary, explanations, linked recommendations, and review checklist. The Prompt Engine counterpart is `trading-journal-analysis` version `1.0` because prompt versions use `major.minor`. The provider receives deterministic aggregate results, not raw journal entries, and is called at most once. Strict JSON parsing, deterministic-priority merging, bounded collections, and final fail-closed safety validation prevent the provider from changing metrics, drawdown, streaks, scores, trends, data quality, or code findings.

Memory and Knowledge are optional. Memory is limited to nonsensitive coaching preferences, goals, prior improvement themes, and review frequency; automatic request and response storage is disabled. Knowledge is limited to bounded educational material about discipline, journaling, psychology, and risk management. Tools and side effects are disabled.

## Safety and absence of prediction

The report must not contain a directional signal, asset recommendation, lot or leverage recommendation, order instruction, future price or return prediction, guarantee, or financial certainty. The shared Trading Coaching safety policy is public and reused by Analytics so both business modules enforce one pattern set. Critical violations fail closed.

Historical R drawdown depends on the supplied chronological sequence. It describes the largest observed decline from a prior cumulative-R peak in that sequence. It is not a forecast, value-at-risk estimate, strategy edge calculation, or expected future drawdown.

## Alternatives rejected

- Prompt-only historical analysis was rejected because counts, trends, streaks, cohorts, and drawdown would not be authoritative or reproducible.
- Adding history logic to Trading Coaching was rejected because one-trade review and collection analytics have different contracts, policies, and complexity.
- Persisting journal history in this increment was rejected because storage ownership, authorization, retention, deletion, and privacy are separate Trading Journal decisions.
- Using an in-memory database or PostgreSQL for these calculations was rejected because this module consumes an explicit snapshot and needs no persistence.
- Correlation, regression, profitability classification, and predictive modeling were rejected because the current sample and product boundary support descriptive process review only.
- Provider-native tools and autonomous agents were rejected because all arithmetic and categorization are deterministic application behavior.

## Consequences

TradeMind gains reproducible multi-trade analysis with explicit sample-size and data-quality limits. The module stays compatible with the existing Agent Framework and Coaching contracts and introduces no concrete AI, database, HTTP, broker, or market dependency.

The caller remains responsible for authorization, source integrity, retention, and mapping a future journal read model into the request. Duplicate detection is intentionally strict and does not merge arbitrary trades. Lexical behavior detection can produce false positives or false negatives. First-versus-recent comparisons identify direction but do not establish statistical significance or causality. Results are only as complete and chronologically accurate as the supplied history.
