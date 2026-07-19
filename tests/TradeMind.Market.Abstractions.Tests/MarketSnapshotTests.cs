using System.Reflection;
using TradeMind.Market.Abstractions;

namespace TradeMind.Market.Abstractions.Tests;

public sealed class MarketSnapshotTests
{
    [Fact]
    public void Snapshot_PreservesCanonicalIdentityAndTiming()
    {
        var snapshot = MarketTestData.Snapshot();

        Assert.Equal("sample-connector", snapshot.ConnectorId.Value);
        Assert.Equal("Account-A/42", snapshot.Account.Value);
        Assert.Equal("EURUSD.A", snapshot.Instrument.Symbol);
        Assert.Equal(Timeframe.M15, snapshot.Timeframe);
        Assert.Equal(MarketTestData.Now, snapshot.CapturedAt);
        Assert.Equal(MarketTestData.Now.AddMilliseconds(25), snapshot.ReceivedAt);
    }

    [Fact]
    public void Snapshot_CopiesAllCollectionsAndMetadata()
    {
        var candles = new List<MarketCandle> { MarketTestData.Candle() };
        var positions = new List<TradingPosition> { MarketTestData.Position() };
        var orders = new List<PendingOrder> { MarketTestData.PendingOrder() };
        var indicators = new List<ChartIndicator> { MarketTestData.Indicator() };
        var drawings = new List<ChartDrawing> { MarketTestData.Drawing() };
        var metadata = new Dictionary<string, string> { ["source"] = "original" };
        var snapshot = MarketTestData.Snapshot(
            candles,
            positions,
            orders,
            indicators,
            drawings,
            metadata: metadata);

        candles.Clear();
        positions.Clear();
        orders.Clear();
        indicators.Clear();
        drawings.Clear();
        metadata["source"] = "changed";

        Assert.Single(snapshot.Candles);
        Assert.Single(snapshot.Positions);
        Assert.Single(snapshot.PendingOrders);
        Assert.Single(snapshot.Indicators);
        Assert.Single(snapshot.Drawings);
        Assert.Equal("original", snapshot.Metadata["source"]);
    }

    [Fact]
    public void Snapshot_CollectionsCannotBeMutatedThroughPublicViews()
    {
        var snapshot = MarketTestData.Snapshot();

        Assert.Throws<NotSupportedException>(() => ((ICollection<MarketCandle>)snapshot.Candles).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<TradingPosition>)snapshot.Positions).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<PendingOrder>)snapshot.PendingOrders).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<ChartIndicator>)snapshot.Indicators).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<ChartDrawing>)snapshot.Drawings).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)snapshot.Metadata).Clear());
    }

    [Fact]
    public void Snapshot_NullCollectionsBecomeEmptyReadOnlyCollections()
    {
        var snapshot = MarketTestData.Snapshot([], [], [], [], []);

        Assert.Empty(snapshot.Candles);
        Assert.Empty(snapshot.Positions);
        Assert.Empty(snapshot.PendingOrders);
        Assert.Empty(snapshot.Indicators);
        Assert.Empty(snapshot.Drawings);
    }

    [Fact]
    public void SnapshotQuality_RepresentsMissingCapabilities()
    {
        var quality = new SnapshotQuality(
            SnapshotFreshness.Recent,
            ConnectorCapabilities.Candles,
            ConnectorCapabilities.Quotes | ConnectorCapabilities.Positions,
            [new SnapshotWarning("partial", "Quote and positions are unavailable.")]);
        var snapshot = MarketTestData.Snapshot(quality: quality);

        Assert.False(snapshot.Quality.IsComplete);
        Assert.True(snapshot.Quality.MissingCapabilities.HasFlag(ConnectorCapabilities.Quotes));
        Assert.True(snapshot.Quality.MissingCapabilities.HasFlag(ConnectorCapabilities.Positions));
        Assert.Single(snapshot.Quality.Warnings);
    }

    [Fact]
    public void SnapshotQuality_IsCompleteWhenNoCapabilitiesAreMissing()
    {
        Assert.True(MarketTestData.Quality().IsComplete);
    }

    [Fact]
    public void SnapshotQuality_RejectsOverlappingCapabilities()
    {
        Assert.Throws<ArgumentException>(() => new SnapshotQuality(
            SnapshotFreshness.Live,
            ConnectorCapabilities.Candles,
            ConnectorCapabilities.Candles));
    }

    [Fact]
    public void SnapshotQuality_CopiesWarnings()
    {
        var warnings = new List<SnapshotWarning> { new("delayed", "Feed is delayed.") };
        var quality = new SnapshotQuality(
            SnapshotFreshness.Stale,
            ConnectorCapabilities.Candles,
            ConnectorCapabilities.Quotes,
            warnings);

        warnings.Clear();

        Assert.Single(quality.Warnings);
        Assert.Throws<NotSupportedException>(() => ((ICollection<SnapshotWarning>)quality.Warnings).Clear());
    }

    [Fact]
    public void AbstractionsAssembly_HasNoProviderOrInfrastructureDependencies()
    {
        var forbiddenFragments = new[]
        {
            "EntityFramework",
            "Npgsql",
            "AspNetCore",
            "MediatR",
            "MetaTrader",
            "MT5"
        };
        var references = typeof(IMarketConnector).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference =>
            forbiddenFragments.Any(fragment =>
                reference.Name?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public void ConnectorContracts_ExposeNoTradingWriteOperations()
    {
        var publicMethods = new[] { typeof(IMarketConnector), typeof(IMarketConnectorRegistry) }
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Select(method => method.Name)
            .ToArray();
        var forbiddenVerbs = new[] { "Execute", "Place", "Create", "Modify", "Update", "Cancel", "Delete", "Register" };

        Assert.DoesNotContain(publicMethods, method =>
            forbiddenVerbs.Any(verb => method.Contains(verb, StringComparison.OrdinalIgnoreCase)));
    }
}
