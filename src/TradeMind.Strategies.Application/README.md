# TradeMind Strategies

The Strategies module evaluates an immutable `MarketContext` and produces an
explainable `SetupCandidate`. The module is deterministic and has no broker,
MT5, network, persistence, LLM or order-execution dependency.

`TrendFollowingStrategy` uses explicit context metadata when available and a
deterministic candle/quote fallback otherwise. A candidate is produced only
when trend, structure, momentum, volatility, support/resistance and the
configured risk/reward threshold agree. Otherwise the result is `NoSetup`.

The registry is immutable after construction, rejects duplicate strategy
versions, resolves an exact version when requested and selects the latest
stable version by default. `AddTradeMindStrategies` registers the domain
services; downstream decision and risk modules consume the candidate through
their orchestration boundary. This module never submits an order.
