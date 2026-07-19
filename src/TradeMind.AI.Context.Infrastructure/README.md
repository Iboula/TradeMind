# TradeMind Context Engine

The Context Engine builds one immutable, versioned `MarketContext` for downstream AI agents. The public domain model contains market data and neutral context sections only; it does not expose EF Core, PostgreSQL, HTTP, MT5, KnowledgeHub, or provider SDK types.

## Pipeline

```text
BuildMarketContextQuery
        |
        v
Provider registry -> dependency validation -> deterministic DAG levels
        |
        v
[Market] [Knowledge] [Memory]   independent providers run in parallel
        |
        v
dependency-aware provider levels -> freshness traces -> size normalization
        |
        v
quality policy -> Succeeded | PartiallySucceeded | Failed
        |
        v
immutable MarketContext v1
```

`MarketSnapshotContextProvider` delegates to the existing `GetLatestMarketSnapshotQuery`. `KnowledgeContextProvider` delegates to `IKnowledgeContextRetriever`, and `MemoryContextProvider` delegates to `IMemoryReader`. Each adapter maps the source module result into a neutral Context Engine contract before returning it.

`BuildMarketContextQuery` keeps connector and account selection optional. When no connector is supplied, the market adapter returns `NotConfigured` as a structured result; the default required market policy then makes the build fail without throwing for normal absence. The knowledge adapter searches with instrument, timeframe, and the requested question or intent. Memory is keyed by conversation, tenant, and user so a context build cannot cross user boundaries.

Providers declare a stable id, category, requirement, priority, timeout, and dependencies. The registry rejects duplicate ids, unknown dependencies, and cycles. Providers in the same topological level run concurrently. A provider timeout and the global build timeout are linked with caller cancellation; every started provider task is included in `Task.WhenAll`, so the builder never returns while a started task remains unobserved.

The build result exposes its correlation id, total duration, executed providers, skipped providers, structured errors, warnings, and the optional context. Every source trace records the provider status, request/completion instants, source timestamp, source age when known, applied freshness thresholds, item count, provider version, non-sensitive references, and a structured error when applicable.

## Policies

Required source failure fails the build. Preferred source failure produces a partial context. Normal absence of an optional source produces a warning without failing the build. Absence is represented by provider results, not exceptions; unexpected technical exceptions remain visible to the caller.

Freshness is calculated from the source timestamp and injected `TimeProvider` using category-specific Fresh and Aging thresholds. Older data is Stale, missing or future timestamps are Unknown, and non-temporal categories are NotApplicable. The trace keeps both the classification and the policy details used for it.

Quality is deterministic. Requirement weights are Required 5, Preferred 3, and Optional 1. The final score is 50% completeness, 30% freshness, and 20% reliability, rounded to two decimals. Knowledge, memory, news, calendar, and warning limits are applied in a stable order and emit explicit truncation warnings.
