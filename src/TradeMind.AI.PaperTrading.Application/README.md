# Paper Trading

Paper Trading deterministically replays a neutral bid/ask price path against a
`TradingWorkspaceResult` and its matching `TradingPlanResult`. It produces only
paper artifacts: orders, fills, position snapshots, PnL, equity observations,
journal entries, statistics and an ordered event timeline.

The module has no broker, MT5, HTTP, database, EF Core or AI-provider dependency.
It never derives a new level or quantity: entry, stop, targets and quantity come
from the supplied trading plan. A bid/ask observation is required because a plan
alone contains intent, not the market path needed to simulate fills.

Register the application services at the composition root:

```csharp
services.AddTradeMindPaperTrading(options =>
{
    options.Timeout = TimeSpan.FromSeconds(10);
    options.PointValue = 1m;
});
```

`IPaperTradingSimulator` uses `TimeProvider`, a linked cancellation token and
deterministic identifiers/fingerprints. A stop/target tie uses the configured
`StopLossFirst` policy by default. This is a simulation and does not authorize
or execute live orders.
