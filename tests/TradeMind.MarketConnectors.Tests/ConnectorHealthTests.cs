using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;
using TradeMind.MarketConnectors.Domain;

namespace TradeMind.MarketConnectors.Tests;

public sealed class ConnectorHealthTests
{
    private static readonly ConnectorId ConnectorId = new("sample-connector");
    private static readonly ConnectorHealthPolicy Policy = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2));

    [Fact]
    public void Health_IsNeverConnectedWithoutSnapshot()
    {
        var health = ConnectorHealthClassifier.Classify(
            ConnectorId,
            null,
            MarketConnectorTestData.Now,
            Policy);

        Assert.Equal(ConnectorHealthStatus.NeverConnected, health.Status);
        Assert.Null(health.Age);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void Health_IsConnectedThroughExactBoundary(int ageSeconds)
    {
        Assert.Equal(ConnectorHealthStatus.Connected, Classify(ageSeconds).Status);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(120)]
    public void Health_IsDegradedThroughExactBoundary(int ageSeconds)
    {
        Assert.Equal(ConnectorHealthStatus.Degraded, Classify(ageSeconds).Status);
    }

    [Fact]
    public void Health_IsDisconnectedBeyondCriticalBoundary()
    {
        Assert.Equal(ConnectorHealthStatus.Disconnected, Classify(121).Status);
    }

    [Fact]
    public void Health_UsesReceivedAtRatherThanCapturedAt()
    {
        var lastReceivedAt = MarketConnectorTestData.Now.AddSeconds(-10);
        var health = ConnectorHealthClassifier.Classify(
            ConnectorId,
            lastReceivedAt,
            MarketConnectorTestData.Now,
            Policy);

        Assert.Equal(lastReceivedAt, health.LastReceivedAt);
        Assert.Equal(TimeSpan.FromSeconds(10), health.Age);
    }

    [Fact]
    public void HealthPolicy_RejectsNegativeConnectedThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectorHealthPolicy(
            TimeSpan.FromTicks(-1),
            TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void HealthPolicy_RejectsUnorderedThresholds()
    {
        Assert.Throws<ArgumentException>(() => new ConnectorHealthPolicy(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task HealthQuery_UsesRepositoryAndInjectedTime()
    {
        var repository = new FakeMarketSnapshotRepository
        {
            LastReceivedAt = MarketConnectorTestData.Now.AddSeconds(-31)
        };
        var handler = new GetConnectorHealthQueryHandler(
            repository,
            Options.Create(new MarketConnectorCoreOptions()),
            new FixedTimeProvider(MarketConnectorTestData.Now));

        var health = await handler.Handle(
            new GetConnectorHealthQuery(ConnectorId),
            CancellationToken.None);

        Assert.Equal(ConnectorHealthStatus.Degraded, health.Status);
        Assert.Equal(MarketConnectorTestData.Now, health.EvaluatedAt);
    }

    [Fact]
    public void OptionsValidator_RejectsNonPositiveLimits()
    {
        var options = new MarketConnectorCoreOptions { MaximumPayloadBytes = 0 };

        var result = new MarketConnectorCoreOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("MaximumPayloadBytes", StringComparison.Ordinal));
    }

    private static ConnectorHealthSnapshot Classify(int ageSeconds) =>
        ConnectorHealthClassifier.Classify(
            ConnectorId,
            MarketConnectorTestData.Now.AddSeconds(-ageSeconds),
            MarketConnectorTestData.Now,
            Policy);
}
