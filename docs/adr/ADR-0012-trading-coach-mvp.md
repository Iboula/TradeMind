# ADR-0012: Trading Coach Analysis MVP

## Status

Accepted.

## Context

TradeMind has provider-independent chat contracts, an orchestration pipeline, versioned prompts, optional conversational Memory, optional KnowledgeHub RAG, controlled Tools, and a versioned Agent Framework. The existing `trading-coach` `0.1.0` definition is intentionally a non-functional development skeleton. The product now needs its first useful business agent without adding market data, brokerage, order execution, or a persisted Trading Journal module prematurely.

Trading journal content can contain financial and behavioral information. A free-form model response could invent calculations, suppress rule violations, emit a directional instruction, or expose sensitive input through logs and errors. Process coaching therefore needs deterministic boundaries before and after the provider call.

## Decision

TradeMind will add `TradeMind.Trading.Coaching` as a provider-agnostic business module and publish `trading-coach` version `1.0.0`. The agent is enabled because its functional path is bounded, fail-closed, and covered by deterministic tests. It analyzes only journal data explicitly supplied by the caller. It is educational process coaching, not financial advice.

The module depends on AI Agents and AI Application contracts plus Microsoft dependency injection, options, and logging abstractions. It does not depend on a provider SDK, EF Core, Npgsql, pgvector, ASP.NET Core, ambient identity, filesystem, network, market data, or broker APIs.

## Input and validation

`TradingJournalAnalysisRequest` is an immutable, optional-field snapshot. It can describe prices, risk, result, setup, plan, execution, emotions, rules, mistakes, lessons, tags, and UTC timing without requiring every field. Collections are copied defensively.

Validation rejects invalid signs and ranges, non-UTC or reversed dates, unsupported direction values, overlong text, unreasonable R multiples, and contradictory explicit risk percentages. Errors expose stable codes and field names, never field values. Contradictory explicit values are not silently replaced.

Normalization trims text, canonicalizes direction, timeframe, and tags, and derives only unambiguous actual-risk and realized-R values when the caller omitted them. Every derivation is reported. Completeness is a deterministic 0-100 measure of supplied review fields.

## Metrics and scoring

The module deterministically calculates risk percentage, planned reward-to-risk, realized R, duration, stop and target distance, result relative to balance, and planned-versus-actual risk delta when inputs permit. Sources, warnings, and unavailable metrics remain explicit. It does not calculate future probability, future price, unsupported expectancy, or recommended lot size.

Process scores cover plan adherence, risk discipline, execution quality, emotional control, journal completeness, and overall process quality. Scores are bounded, explained, and identified as rule-based or AI-originated. Profit or loss alone cannot improve a process score and no score predicts profitability.

## Rule analysis and transparency

The deterministic analyzer identifies risk-limit breaches, missing stop or plan data, missing entry or exit rationale, reported rule breaches, incomplete lessons or mistakes, strong emotions, R inconsistencies, and simple French or English textual patterns such as FOMO, revenge trading, overtrading, hesitation, impulsive entry, moved stop, early exit, ignored plan, and oversized position.

Text matching is deliberately simple and transparent. It can produce false positives when a term is quoted, negated, or used in another context, and false negatives when a behavior is described indirectly. Findings are educational review cues, not clinical or financial conclusions.

## Prompt and structured output

The agent uses `trading-coach-analysis` template version `1.0`. Prompt template versions currently use `major.minor`; this template is the prompt counterpart of agent semantic version `1.0.0`. Structured journal data, metrics, rule findings, profile, language, output schema, and safety rules are supplied as declared variables inside explicit data delimiters. Journal text is treated as untrusted data, not as a system instruction.

The provider must return exactly one JSON object matching the documented schema. The parser rejects malformed JSON, duplicate or unknown properties, missing critical fields, invalid severities, invalid confidence, and out-of-range scores. Provider-controlled metadata cannot set the analysis id, generation time, agent version, deterministic metrics, deterministic scores, or final disclaimer.

## Merge and safety

Deterministic calculations and findings take priority. AI output may enrich an explanation but cannot remove a code finding, alter a calculated metric, hide missing information, or replace rule-based scores. Recommendations must reference an existing finding and are bounded by execution options.

A final safety filter rejects directional trading instructions, exact future price predictions, order instructions, maximum-leverage instructions, guaranteed outcomes, and return promises in common English and French variants. Fail-closed is the default. This keyword and pattern filter is defense in depth and does not claim perfect safety.

## Memory, Knowledge, and Tools

Memory is optional and continues without memory on an optional-engine failure. The agent uses a small window for coaching preferences or improvement themes and disables automatic saving of the raw user message and provider response. It does not automatically retain the full journal, prices, balance, position size, profit and loss, or sensitive notes.

Knowledge is optional and continues without Knowledge on failure. Its explicit query is limited to educational process material, and its allowed filter names are `contentPurpose`, `topic`, and `language`. It must not retrieve signals, predictions, asset recommendations, or regulated advice.

Tools are disabled. The definition permits no tool id, no side effect, no provider-native tool calling, and no autonomous execution. Deterministic business calculators perform all numeric work.

## Errors and observability

Public failures use only `TradingJournalValidationException`, `TradingCoachAnalysisException`, `TradingCoachResponseParsingException`, `TradingCoachSafetyException`, and `TradingCoachTimeoutException`. They carry a stable error code, analysis id, optional correlation id, and safe field names where applicable.

Logs contain lifecycle phase, identifiers, counts, completeness, duration, state, and safe error classification. They exclude journal text, emotions, prices, balance, position size, result, prompts, and provider responses.

## Alternatives rejected

- A prompt-only implementation was rejected because it cannot make calculations authoritative or protect deterministic findings.
- Provider-native JSON mode was not required because current provider abstractions do not expose it and the module must remain provider-agnostic.
- Provider-native tools and model-selected calculators were rejected because arithmetic and policy checks are deterministic business behavior.
- Persisting journal entries in this increment was rejected because the Trading Journal storage aggregate and retention policy are separate product decisions.
- Market data, broker access, signals, and order execution were rejected as outside educational coaching and outside the approved security boundary.
- Storing complete journal content in conversational Memory was rejected because retention, privacy, and deletion rules are not established.

## Consequences

The product has a functional business-agent pipeline with deterministic validation, calculations, findings, scores, strict JSON parsing, bounded merging, and final safety enforcement. It can run with any provider that implements existing abstractions and can run without Memory, Knowledge, or the Tool Engine.

The MVP remains limited to one explicitly supplied journal snapshot per analysis. Behavioral matching is lexical, optional Knowledge filters depend on the registered retrieval adapter, and provider compliance still requires parsing and safety enforcement. There is no persisted journal, cross-trade trend engine, UI, HTTP endpoint, market context, backtesting, portfolio logic, broker connection, or autonomous loop.
