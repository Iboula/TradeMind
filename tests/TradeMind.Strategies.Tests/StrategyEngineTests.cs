namespace TradeMind.Strategies.Tests;

public sealed class StrategyEngineTests
{
    [Fact]
    public async Task TrendFollowingStrategy_ShouldProduceBuyCandidate()
    {
        var context = StrategyTestData.Context("bullish");
        var strategy = StrategyTestData.Strategy();

        var result = await strategy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(StrategyEvaluationStatus.BuyCandidate, result.Status);
        Assert.NotNull(result.Candidate);
        Assert.Equal(MarketDirection.Long, result.Candidate.Direction);
        Assert.Equal(2.5d, result.Candidate.RiskRewardRatio, 2);
    }

    [Fact]
    public async Task TrendFollowingStrategy_ShouldProduceSellCandidate()
    {
        var context = StrategyTestData.Context("bearish");
        var features = new DefaultMarketContextFeatureExtractor().Extract(context);
        Assert.Equal(StrategyTrendDirection.Bearish, features.TrendDirection);
        Assert.Equal(PriceStructureState.Bearish, features.PriceStructure);
        Assert.Equal(MomentumState.Bearish, features.Momentum);
        Assert.Equal(VolatilityState.Normal, features.Volatility);
        var result = await StrategyTestData.Strategy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(StrategyEvaluationStatus.SellCandidate, result.Status);
        Assert.Equal(MarketDirection.Short, result.Candidate!.Direction);
        Assert.Equal(2d, result.Candidate.RiskRewardRatio, 2);
        Assert.True(result.Candidate.TargetZone.UpperBound.Value < result.Candidate.EntryZone.LowerBound.Value);
    }

    [Fact]
    public async Task TrendFollowingStrategy_ShouldReturnNoSetupWhenConditionsDisagree()
    {
        var result = await StrategyTestData.Strategy().EvaluateAsync(StrategyTestData.Context("neutral"), CancellationToken.None);

        Assert.Equal(StrategyEvaluationStatus.NoSetup, result.Status);
        Assert.Null(result.Candidate);
        Assert.Contains(result.Warnings, warning => warning.Code == "NO_SETUP");
    }

    [Fact]
    public void Registry_ShouldResolveExactAndLatestStableVersionsDeterministically()
    {
        var stable = StrategyTestData.Strategy(new StrategyVersion(1, 0, 0));
        var newer = StrategyTestData.Strategy(new StrategyVersion(1, 1, 0));
        var experimental = StrategyTestData.Strategy(new StrategyVersion(2, 0, 0, experimental: true));
        var registry = new StrategyRegistry([experimental, stable, newer]);

        Assert.True(registry.TryResolve(TrendFollowingStrategy.StrategyIdentifier, null, out var latestStable));
        Assert.Equal(new StrategyVersion(1, 1, 0), latestStable.Definition.Version);
        Assert.True(registry.TryResolve(TrendFollowingStrategy.StrategyIdentifier, experimental.Definition.Version, out var exact));
        Assert.Equal(experimental.Definition.Version, exact.Definition.Version);
        Assert.Equal(
            registry.GetAvailableDefinitions().Select(definition => definition.Version).ToArray(),
            new[] { new StrategyVersion(2, 0, 0, experimental: true), new StrategyVersion(1, 1, 0), new StrategyVersion(1, 0, 0) });
    }

    [Fact]
    public void Registry_ShouldRejectDuplicateStrategyVersions()
    {
        var strategy = StrategyTestData.Strategy();

        Assert.Throws<ArgumentException>(() => new StrategyRegistry([strategy, StrategyTestData.Strategy()]));
    }

    [Fact]
    public void SetupCandidate_ShouldValidateDirectionalZones()
    {
        Assert.Throws<ArgumentException>(() => new SetupCandidate(
            new StrategyId("test"),
            new StrategyVersion(1, 0, 0),
            new Instrument("EURUSD"),
            Timeframe.H1,
            MarketDirection.Long,
            new EntryZone(new Price(1.10m), new Price(1.10m)),
            new InvalidationZone(new Price(1.11m), new Price(1.11m)),
            new TargetZone(new Price(1.20m), new Price(1.20m)),
            2,
            new SetupConfidence(80, SetupConfidenceBand.High),
            [new SetupEvidence("evidence", "test", "test", 80)],
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SetupCandidate_ShouldDefensivelyCopyEvidence()
    {
        var evidence = new List<SetupEvidence> { new("b", "b", "test", 80), new("a", "a", "test", 90) };
        var candidate = StrategyTestData.Candidate(evidence);
        evidence.Clear();

        Assert.Equal(["a", "b"], candidate.Evidence.Select(item => item.Code).ToArray());
        Assert.Throws<NotSupportedException>(() => ((IList<SetupEvidence>)candidate.Evidence)[0] = new SetupEvidence("x", "x", "test", 1));
    }

    [Fact]
    public async Task Evaluator_ShouldResolveStrategyAndPreserveTradingDecisionDirectionContract()
    {
        var services = new ServiceCollection()
            .AddTradeMindStrategies()
            .BuildServiceProvider();
        var evaluator = services.GetRequiredService<IStrategyEvaluator>();

        var result = await evaluator.EvaluateAsync(
            TrendFollowingStrategy.StrategyIdentifier,
            StrategyTestData.Context("bullish"),
            null,
            CancellationToken.None);

        Assert.Equal(StrategyEvaluationStatus.BuyCandidate, result.Status);
        Assert.Equal(MarketDirection.Long, result.Candidate!.Direction);
        Assert.Equal(TradingDecisionType.LongSetup, result.Candidate.Direction == MarketDirection.Long ? TradingDecisionType.LongSetup : TradingDecisionType.ShortSetup);
        Assert.True(result.Candidate.RiskRewardRatio >= 1.5);
    }

    [Fact]
    public async Task Evaluator_ShouldReturnFailureForUnknownStrategy()
    {
        var services = new ServiceCollection().AddTradeMindStrategies().BuildServiceProvider();
        var result = await services.GetRequiredService<IStrategyEvaluator>().EvaluateAsync(
            new StrategyId("unknown"), StrategyTestData.Context("bullish"), null, CancellationToken.None);

        Assert.Equal(StrategyEvaluationStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "STRATEGY_NOT_FOUND");
    }

    [Fact]
    public async Task Strategy_ShouldBeDeterministicForSameContext()
    {
        var context = StrategyTestData.Context("bullish");
        var strategy = StrategyTestData.Strategy();

        var first = await strategy.EvaluateAsync(context, CancellationToken.None);
        var second = await strategy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Candidate!.RiskRewardRatio, second.Candidate!.RiskRewardRatio);
        Assert.Equal(first.Candidate.Evidence, second.Candidate.Evidence);
    }

    [Fact]
    public async Task Strategy_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            StrategyTestData.Strategy().EvaluateAsync(StrategyTestData.Context("bullish"), cancellation.Token));
    }
}

internal static class StrategyTestData
{
    public static TrendFollowingStrategy Strategy(StrategyVersion? version = null) =>
        new(
            new DefaultMarketContextFeatureExtractor(),
            TimeProvider.System,
            Options(new StrategyEngineOptions { MinimumRiskRewardRatio = 1.5, MinimumConfidence = 70 }),
            version);

    public static SetupCandidate Candidate(IReadOnlyCollection<SetupEvidence> evidence) => new(
        new StrategyId("test"),
        new StrategyVersion(1, 0, 0),
        new Instrument("EURUSD"),
        Timeframe.H1,
        MarketDirection.Long,
        new EntryZone(new Price(1.10m), new Price(1.10m)),
        new InvalidationZone(new Price(1.08m), new Price(1.08m)),
        new TargetZone(new Price(1.15m), new Price(1.15m)),
        2.5,
        new SetupConfidence(80, SetupConfidenceBand.High),
        evidence,
        DateTimeOffset.UtcNow);

    public static MarketContext Context(string mode)
    {
        var bullish = mode == "bullish";
        var bearish = mode == "bearish";
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["strategy.current"] = "1.1000",
            ["strategy.support"] = bearish ? "1.0000" : "1.0800",
            ["strategy.resistance"] = "1.1500",
            ["strategy.trend"] = bullish ? "bullish" : bearish ? "bearish" : "neutral",
            ["strategy.price-structure"] = bullish ? "bullish" : bearish ? "bearish" : "range",
            ["strategy.volatility"] = "normal",
            ["strategy.momentum"] = bullish ? "bullish" : bearish ? "bearish" : "neutral"
        };
        var now = DateTimeOffset.UtcNow;
        var snapshot = new MarketSnapshot(
            SnapshotId.New(),
            new ConnectorId("test"),
            new ExternalAccountReference("account"),
            new Instrument("EURUSD"),
            Timeframe.H1,
            now,
            now,
            [],
            new MarketQuote(new Price(1.0999m), new Price(1.1001m), new Price(1.1000m), now),
            [],
            [],
            [],
            [],
            new SnapshotQuality(SnapshotFreshness.Live, ConnectorCapabilities.Quotes, ConnectorCapabilities.None),
            metadata);
        return new MarketContext(
            MarketContextId.New(),
            MarketContext.CurrentVersion,
            "user",
            "session",
            new Instrument("EURUSD"),
            Timeframe.H1,
            now,
            MarketContextBuildStatus.Succeeded,
            snapshot,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            new ContextQuality(100, 100, 100, 100, ContextQualityBand.Excellent));
    }

    private static Microsoft.Extensions.Options.IOptions<StrategyEngineOptions> Options(StrategyEngineOptions value) =>
        Microsoft.Extensions.Options.Options.Create(value);
}
