# Paper Trading

Paper Trading replays a fixed `TradingPlanResult` and its compatible
`TradingWorkspaceResult` against explicit UTC bid/ask ticks. It produces only
immutable paper artifacts: orders, fills, executions, position snapshots, PnL,
balance/equity, journal entries, statistics, traces and a timeline.

The module has no MT5, broker, exchange, HTTP, database, EF Core or concrete AI
provider dependency. It never changes the upstream plan or workspace and never
places a live order. Entry, stop, target and quantity values are read from the
plan; the engine does not invent missing levels.

## Registration

```csharp
services.AddTradeMindPaperTrading(options =>
{
    options.Timeout = TimeSpan.FromSeconds(10);
    options.PointValue = 1m;
    options.CommissionPerUnit = 0m;
    options.SlippagePerUnit = 0m;
});
```

Use `IPaperTradingEngine` for the public boundary. `IPaperTradingSimulator` is
kept as the implementation-level compatibility contract. Both are scoped;
neither captures a service scope or uses global locks.

## Deterministic policies

- Ticks are validated for positive ordered bid/ask values, instrument
  compatibility and UTC timestamps, then ordered by timestamp and stable
  sequence. When every sequence is omitted, a deterministic timestamp/price
  ordering supplies the sequence; explicit duplicate sequences are rejected.
- Long entries use ask and long exits use bid. Short entries use bid and short
  exits use ask. Mid-price handling is available only when explicitly selected.
- Commission and slippage default to zero. They are represented as options and
  are included in execution and PnL outputs when configured.
- A tick with optional high/low data that touches both a stop and target uses
  `ConservativeStopFirst` by default. `TargetFirst` and
  `RejectAmbiguousTick` are explicit alternatives; no profitable outcome is
  selected implicitly.
- Target allocations are required for partial exits. Without allocations, the
  first eligible target closes the position. Allocated targets generate
  partial and final exit events without modifying the upstream plan.
- Caller cancellation is propagated as `OperationCanceledException`; the
  configured timeout returns a `TimedOut` structured result.

All generated identifiers, replay fingerprints and ordering are deterministic
for the same simulation input. `TimeProvider` controls result timestamps.
