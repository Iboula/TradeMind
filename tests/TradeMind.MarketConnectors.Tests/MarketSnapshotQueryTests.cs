using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;

namespace TradeMind.MarketConnectors.Tests;

public sealed class MarketSnapshotQueryTests
{
    [Fact]
    public async Task LatestQuery_ReturnsNullWhenRepositoryHasNoCandidate()
    {
        var handler = CreateHandler(new FakeMarketSnapshotRepository());

        var result = await handler.Handle(Query(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LatestQuery_PassesOptionalAccountFilter()
    {
        var repository = RepositoryWith(MarketConnectorTestData.Snapshot());
        var handler = CreateHandler(repository);
        var account = new ExternalAccountReference("Account-A/42");

        await handler.Handle(new GetLatestMarketSnapshotQuery(
            new ConnectorId("sample-connector"),
            account,
            new Instrument("EURUSD.a"),
            Timeframe.M15), CancellationToken.None);

        Assert.Equal(account, repository.LastLookup?.Account);
    }

    [Fact]
    public async Task LatestQuery_ConvertsMaximumAgeToMinimumCapturedAt()
    {
        var repository = RepositoryWith(MarketConnectorTestData.Snapshot());
        var handler = CreateHandler(repository);

        await handler.Handle(new GetLatestMarketSnapshotQuery(
            new ConnectorId("sample-connector"),
            null,
            new Instrument("EURUSD.a"),
            Timeframe.M15,
            TimeSpan.FromMinutes(5)), CancellationToken.None);

        Assert.Equal(MarketConnectorTestData.Now.AddMinutes(-5), repository.LastLookup?.MinimumCapturedAt);
    }

    [Fact]
    public async Task LatestQuery_RecalculatesFreshnessWithInjectedTime()
    {
        var repository = RepositoryWith(MarketConnectorTestData.Snapshot(
            capturedAt: MarketConnectorTestData.Now.AddSeconds(-20)));
        var handler = CreateHandler(repository);

        var result = await handler.Handle(Query(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(SnapshotFreshness.Recent, result.Quality.Freshness);
    }

    [Fact]
    public async Task LatestQuery_RestoresCanonicalSnapshot()
    {
        var source = MarketConnectorTestData.Snapshot();
        var handler = CreateHandler(RepositoryWith(source));

        var result = await handler.Handle(Query(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(source.Id, result.Id);
        Assert.Equal(source.ConnectorId, result.ConnectorId);
        Assert.Equal(source.Account, result.Account);
    }

    [Fact]
    public void LatestQuery_RejectsNegativeMaximumAge()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GetLatestMarketSnapshotQuery(
            new ConnectorId("sample-connector"),
            null,
            new Instrument("EURUSD"),
            Timeframe.M15,
            TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public async Task LatestQuery_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateHandler(new FakeMarketSnapshotRepository()).Handle(Query(), cancellation.Token));
    }

    private static GetLatestMarketSnapshotQueryHandler CreateHandler(FakeMarketSnapshotRepository repository) => new(
        repository,
        new TradeMind.MarketConnectors.Infrastructure.CanonicalMarketSnapshotSerializer(),
        Options.Create(new MarketConnectorCoreOptions()),
        new FixedTimeProvider(MarketConnectorTestData.Now));

    private static FakeMarketSnapshotRepository RepositoryWith(MarketSnapshot snapshot)
    {
        var serialized = MarketConnectorTestData.Serialize(snapshot);
        return new FakeMarketSnapshotRepository
        {
            Latest = new StoredMarketSnapshotPayload(serialized.CanonicalPayload, serialized.SchemaVersion)
        };
    }

    private static GetLatestMarketSnapshotQuery Query() => new(
        new ConnectorId("sample-connector"),
        null,
        new Instrument("EURUSD.a"),
        Timeframe.M15);
}
