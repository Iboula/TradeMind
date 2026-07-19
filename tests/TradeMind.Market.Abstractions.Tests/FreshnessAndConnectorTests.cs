using TradeMind.Market.Abstractions;

namespace TradeMind.Market.Abstractions.Tests;

public sealed class FreshnessAndConnectorTests
{
    private static readonly SnapshotFreshnessPolicy Policy = new(
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2));

    [Theory]
    [InlineData(0, SnapshotFreshness.Live)]
    [InlineData(5, SnapshotFreshness.Live)]
    [InlineData(6, SnapshotFreshness.Recent)]
    [InlineData(30, SnapshotFreshness.Recent)]
    [InlineData(31, SnapshotFreshness.Stale)]
    [InlineData(120, SnapshotFreshness.Stale)]
    [InlineData(121, SnapshotFreshness.Expired)]
    public void Freshness_UsesExactPolicyBoundaries(int ageSeconds, SnapshotFreshness expected)
    {
        var result = SnapshotFreshnessClassifier.Classify(
            MarketTestData.Now,
            MarketTestData.Now.AddSeconds(ageSeconds),
            Policy);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Freshness_RejectsFutureCaptureRelativeToReference()
    {
        Assert.Throws<ArgumentException>(() => SnapshotFreshnessClassifier.Classify(
            MarketTestData.Now,
            MarketTestData.Now.AddTicks(-1),
            Policy));
    }

    [Fact]
    public void FreshnessPolicy_RejectsNegativeLiveThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnapshotFreshnessPolicy(
            TimeSpan.FromTicks(-1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void FreshnessPolicy_RejectsUnorderedRecentThreshold()
    {
        Assert.Throws<ArgumentException>(() => new SnapshotFreshnessPolicy(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void FreshnessPolicy_RejectsUnorderedStaleThreshold()
    {
        Assert.Throws<ArgumentException>(() => new SnapshotFreshnessPolicy(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void ConnectorCapabilities_CombineAsFlags()
    {
        var capabilities = ConnectorCapabilities.Candles
            | ConnectorCapabilities.Quotes
            | ConnectorCapabilities.Streaming;

        Assert.True(capabilities.HasFlag(ConnectorCapabilities.Candles));
        Assert.True(capabilities.HasFlag(ConnectorCapabilities.Quotes));
        Assert.True(capabilities.HasFlag(ConnectorCapabilities.Streaming));
        Assert.False(capabilities.HasFlag(ConnectorCapabilities.Positions));
    }

    [Fact]
    public void Descriptor_ExposesReadOnlyConnectorMetadata()
    {
        var descriptor = new ConnectorDescriptor(
            new ConnectorId("sample"),
            "Sample connector",
            "1.2.3",
            ConnectorCapabilities.Candles | ConnectorCapabilities.HistoricalData,
            ConnectorAcquisitionMode.Hybrid);

        Assert.Equal("sample", descriptor.Id.Value);
        Assert.Equal("Sample connector", descriptor.DisplayName);
        Assert.Equal("1.2.3", descriptor.Version);
        Assert.Equal(ConnectorAcquisitionMode.Hybrid, descriptor.AcquisitionMode);
    }

    [Fact]
    public void Descriptor_RejectsUndefinedCapabilityFlag()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectorDescriptor(
            new ConnectorId("sample"),
            "Sample connector",
            "1.0.0",
            (ConnectorCapabilities)(1 << 20),
            ConnectorAcquisitionMode.Pull));
    }

    [Fact]
    public void SnapshotRequest_AcceptsOptionalAccountAndMaximumAge()
    {
        var request = new MarketSnapshotRequest(
            new ConnectorId("sample"),
            null,
            new Instrument("BTC/USD"),
            Timeframe.H1,
            TimeSpan.Zero);

        Assert.Null(request.Account);
        Assert.Equal(TimeSpan.Zero, request.MaximumAge);
    }

    [Fact]
    public void SnapshotRequest_RejectsNegativeMaximumAge()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketSnapshotRequest(
            new ConnectorId("sample"),
            null,
            new Instrument("BTC/USD"),
            Timeframe.H1,
            TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public async Task Connector_PropagatesCancellation()
    {
        IMarketConnector connector = new FakeConnector();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            connector.GetLatestSnapshotAsync(MarketTestData.Request(), cancellation.Token));
    }

    [Fact]
    public void Registry_ContractSupportsReadOnlyLookup()
    {
        IMarketConnector connector = new FakeConnector();
        IMarketConnectorRegistry registry = new FakeRegistry(connector);

        Assert.True(registry.TryGet(connector.Descriptor.Id, out var resolved));
        Assert.Same(connector, resolved);
        Assert.Single(registry.GetAvailableConnectors());
    }

    private sealed class FakeConnector : IMarketConnector
    {
        public ConnectorDescriptor Descriptor { get; } = new(
            new ConnectorId("fake"),
            "Fake connector",
            "1.0.0",
            ConnectorCapabilities.Candles,
            ConnectorAcquisitionMode.Pull);

        public Task<MarketSnapshot?> GetLatestSnapshotAsync(
            MarketSnapshotRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<MarketSnapshot?>(null);
        }
    }

    private sealed class FakeRegistry(IMarketConnector connector) : IMarketConnectorRegistry
    {
        public IReadOnlyCollection<ConnectorDescriptor> GetAvailableConnectors() =>
            Array.AsReadOnly([connector.Descriptor]);

        public bool TryGet(ConnectorId connectorId, out IMarketConnector resolved)
        {
            if (connector.Descriptor.Id == connectorId)
            {
                resolved = connector;
                return true;
            }

            resolved = null!;
            return false;
        }
    }
}
