# TradeMind Market Abstractions

`TradeMind.Market.Abstractions` defines the provider-agnostic, read-only market language used by TradeMind. It has no infrastructure, persistence, transport, or broker SDK dependency.

`MarketSnapshot` is an immutable observation of one account, instrument, and timeframe at a precise capture time. It can contain candles, a quote, open positions, pending orders, generic indicators, chart drawings, quality information, and simple metadata. A connector maps external provider data into this canonical model so downstream modules never depend on provider-specific types or naming.

The V1 contract is strictly read-only. It retrieves observations and exposes no operation to create, modify, cancel, or execute an order.

## Minimal fake connector

```csharp
using TradeMind.Market.Abstractions;

public sealed class SampleConnector : IMarketConnector
{
    public ConnectorDescriptor Descriptor { get; } = new(
        new ConnectorId("sample"),
        "Sample connector",
        "1.0.0",
        ConnectorCapabilities.Candles | ConnectorCapabilities.Quotes,
        ConnectorAcquisitionMode.Pull);

    public Task<MarketSnapshot?> GetLatestSnapshotAsync(
        MarketSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<MarketSnapshot?>(null);
    }
}
```
