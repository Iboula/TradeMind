# ADR-0022: Live Trading Safety Gate and Production Readiness

## Status

Accepted for Sprint 33.

## Context

TradeMind already has broker-neutral execution, durable idempotency, reconciliation, execution sessions and an MT5 Demo adapter. A future Live capability would create a materially larger failure domain: configuration drift, stale market state, duplicate submission, broker ambiguity, orphan positions, crash recovery and multi-pod concurrency could independently make an execution unsafe.

## Decision

Introduce a broker-application safety boundary made of independent, structured policies. `ILiveTradingSafetyGate` evaluates all hard enablement conditions and returns a decision status with explicit causes. Kill switches are scoped and durable when PostgreSQL is configured. Risk limits are supplied by versioned policies. Ambiguous executions become `ExecutionUnknown` and are never retried automatically. Problematic executions are quarantined. Recovery produces a read-only plan. PostgreSQL advisory locks serialize the same tenant, broker, account and instrument scope across pods. Critical transitions are append-audited and exposed through bounded-cardinality metrics and readiness checks.

The current runtime remains fail-closed: `AllowLive=false` is explicit in configuration and the execution validator invokes the gate with independent evidence closed. No Live order path, broker SDK, credential handling or Live smoke test is introduced.

## Consequences

The safety controls can evolve independently of the analytical Core and future broker adapters. Operators receive structured reasons and deterministic recovery plans. PostgreSQL becomes required for durable multi-pod safety guarantees. The in-memory fallbacks are deliberately local-only and cannot be used as a distributed guarantee.

The design adds operational state and migration work, but it avoids a single mutable flag and prevents automatic remediation when broker state is ambiguous.

## Rejected alternatives

- A single `CanTradeLive` boolean would hide which independent control failed.
- A process-local lock or `SemaphoreSlim` would not protect multiple pods.
- Automatic retry after a timeout could duplicate an order when the broker accepted the original.
- Automatic closing of an unknown position could create an unauthorized trade.
