using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.Market.Abstractions;
using TradeMind.Strategies.Domain;

namespace TradeMind.Strategies.Application;

public interface IStrategy
{
    StrategyDefinition Definition { get; }

    Task<StrategyEvaluationResult> EvaluateAsync(MarketContext context, CancellationToken cancellationToken);
}

public interface IStrategyRegistry
{
    IReadOnlyList<StrategyDefinition> GetAvailableDefinitions();

    bool TryResolve(StrategyId id, StrategyVersion? requestedVersion, out IStrategy strategy);
}

public interface IStrategyEvaluator
{
    Task<StrategyEvaluationResult> EvaluateAsync(
        StrategyId strategyId,
        MarketContext context,
        StrategyVersion? requestedVersion,
        CancellationToken cancellationToken);
}

public interface IMarketContextFeatureExtractor
{
    StrategyFeatureSnapshot Extract(MarketContext context);
}

public sealed record StrategyEngineOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(2);
    public double MinimumRiskRewardRatio { get; init; } = 1.5;
    public double MinimumConfidence { get; init; } = 70;

    public void Validate()
    {
        if (Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Timeout));
        if (MinimumRiskRewardRatio <= 0 || double.IsNaN(MinimumRiskRewardRatio) || double.IsInfinity(MinimumRiskRewardRatio))
            throw new ArgumentOutOfRangeException(nameof(MinimumRiskRewardRatio));
        if (MinimumConfidence is < 0 or > 100 || double.IsNaN(MinimumConfidence) || double.IsInfinity(MinimumConfidence))
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
    }
}

public sealed class StrategyRegistry : IStrategyRegistry
{
    private readonly IReadOnlyList<IStrategy> strategies;

    public StrategyRegistry(IEnumerable<IStrategy> registeredStrategies)
    {
        ArgumentNullException.ThrowIfNull(registeredStrategies);
        var values = registeredStrategies.ToArray();
        if (values.Any(strategy => strategy is null))
        {
            throw new ArgumentException("A strategy registry cannot contain null entries.", nameof(registeredStrategies));
        }

        var duplicate = values
            .GroupBy(strategy => (strategy.Definition.Id, strategy.Definition.Version))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Strategy '{duplicate.Key.Id}' version '{duplicate.Key.Version}' is registered more than once.",
                nameof(registeredStrategies));
        }

        strategies = Array.AsReadOnly(values
            .Where(strategy => strategy.Definition.Enabled)
            .OrderBy(strategy => strategy.Definition.Id.Value, StringComparer.Ordinal)
            .ThenByDescending(strategy => strategy.Definition.Version)
            .ToArray());
    }

    public IReadOnlyList<StrategyDefinition> GetAvailableDefinitions() =>
        Array.AsReadOnly(strategies.Select(strategy => strategy.Definition).ToArray());

    public bool TryResolve(StrategyId id, StrategyVersion? requestedVersion, out IStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(id);
        var candidates = strategies.Where(item => item.Definition.Id == id);
        strategy = requestedVersion is null
            ? candidates.FirstOrDefault(item => item.Definition.Version.IsStable)!
            : candidates.FirstOrDefault(item => item.Definition.Version == requestedVersion)!;
        return strategy is not null;
    }
}

public sealed class StrategyEvaluationService(
    IStrategyRegistry registry,
    TimeProvider timeProvider,
    IOptions<StrategyEngineOptions> options,
    ILogger<StrategyEvaluationService> logger) : IStrategyEvaluator
{
    private readonly StrategyEngineOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<StrategyEvaluationResult> EvaluateAsync(
        StrategyId strategyId,
        MarketContext context,
        StrategyVersion? requestedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();

        if (!registry.TryResolve(strategyId, requestedVersion, out var strategy))
        {
            var definition = new StrategyDefinition(
                strategyId,
                requestedVersion ?? new StrategyVersion(0, 0, 0, experimental: true),
                "Unavailable strategy",
                "The requested strategy version is not registered.",
                [new StrategyCondition("unavailable", "The requested strategy must be registered.", true, 100)],
                []);
            return Failed(definition, "STRATEGY_NOT_FOUND", "The requested strategy or version is not registered.");
        }

        if (!strategy.Definition.Supports(context.Timeframe))
        {
            return new StrategyEvaluationResult(
                strategy.Definition,
                StrategyEvaluationStatus.NoSetup,
                null,
                [],
                [new StrategyEvaluationError("TIMEFRAME_UNSUPPORTED", "The strategy does not support the context timeframe.")],
                timeProvider.GetUtcNow());
        }

        try
        {
            var result = await strategy.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Strategy {StrategyId} version {StrategyVersion} evaluated context {ContextId} with status {Status}.",
                strategy.Definition.Id,
                strategy.Definition.Version,
                context.Id,
                result.Status);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Strategy {StrategyId} failed for context {ContextId}.", strategy.Definition.Id, context.Id);
            return Failed(strategy.Definition, "STRATEGY_EVALUATION_FAILED", "The strategy evaluation failed.");
        }
    }

    private StrategyEvaluationResult Failed(StrategyDefinition definition, string code, string message) =>
        new(definition, StrategyEvaluationStatus.Failed, null, [], [new StrategyEvaluationError(code, message, true)], timeProvider.GetUtcNow());
}

public sealed class DefaultMarketContextFeatureExtractor : IMarketContextFeatureExtractor
{
    public StrategyFeatureSnapshot Extract(MarketContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = context.MarketSnapshot;
        var candles = snapshot.Candles
            .Where(candle => candle.IsClosed)
            .OrderBy(candle => candle.OpenTime)
            .ThenBy(candle => candle.Close.Value)
            .ToArray();
        var metadata = snapshot.Metadata;
        var current = ReadPrice(metadata, ["strategy.current", "current.price"])
            ?? snapshot.Quote?.Last
            ?? (snapshot.Quote is null ? (Price?)null : new Price((snapshot.Quote.Bid.Value + snapshot.Quote.Ask.Value) / 2m))
            ?? candles.LastOrDefault()?.Close;
        if (current is null || current.Value.Value <= 0)
        {
            throw new InvalidOperationException("A current market price is required for strategy evaluation.");
        }

        var support = ReadPrice(metadata, ["strategy.support", "support.price"])
            ?? (candles.Length == 0 ? (Price?)null : new Price(candles.TakeLast(20).Min(candle => candle.Low.Value)));
        var resistance = ReadPrice(metadata, ["strategy.resistance", "resistance.price"])
            ?? (candles.Length == 0 ? (Price?)null : new Price(candles.TakeLast(20).Max(candle => candle.High.Value)));
        var averageRange = candles.Length == 0 ? 0m : candles.TakeLast(20).Average(candle => candle.High.Value - candle.Low.Value);

        return new StrategyFeatureSnapshot(
            current.Value,
            ReadEnum(metadata, ["strategy.price-structure", "price.structure"], ParseStructure) ?? DeriveStructure(candles),
            ReadEnum(metadata, ["strategy.trend", "trend.direction"], ParseTrend) ?? DeriveTrend(candles),
            ReadEnum(metadata, ["strategy.volatility", "volatility.state"], ParseVolatility) ?? DeriveVolatility(averageRange, current.Value.Value),
            ReadEnum(metadata, ["strategy.momentum", "momentum.state"], ParseMomentum) ?? DeriveMomentum(candles),
            support,
            resistance,
            averageRange);
    }

    private static Price? ReadPrice(IReadOnlyDictionary<string, string> metadata, IReadOnlyCollection<string> keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value)
                && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                && parsed > 0)
            {
                return new Price(parsed);
            }
        }

        return null;
    }

    private static T? ReadEnum<T>(IReadOnlyDictionary<string, string> metadata, IReadOnlyCollection<string> keys, Func<string, T?> parser)
        where T : struct
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                var parsed = parser(value);
                if (parsed.HasValue) return parsed;
            }
        }

        return null;
    }

    private static PriceStructureState? ParseStructure(string value) => value.Trim().ToLowerInvariant() switch
    {
        "bullish" or "uptrend" => PriceStructureState.Bullish,
        "bearish" or "downtrend" => PriceStructureState.Bearish,
        "range" or "ranging" => PriceStructureState.Range,
        _ => null
    };

    private static StrategyTrendDirection? ParseTrend(string value) => value.Trim().ToLowerInvariant() switch
    {
        "bullish" or "long" or "up" => StrategyTrendDirection.Bullish,
        "bearish" or "short" or "down" => StrategyTrendDirection.Bearish,
        "neutral" or "flat" => StrategyTrendDirection.Neutral,
        _ => null
    };

    private static VolatilityState? ParseVolatility(string value) => value.Trim().ToLowerInvariant() switch
    {
        "low" => VolatilityState.Low,
        "normal" or "medium" => VolatilityState.Normal,
        "high" => VolatilityState.High,
        _ => null
    };

    private static MomentumState? ParseMomentum(string value) => value.Trim().ToLowerInvariant() switch
    {
        "bullish" or "positive" => MomentumState.Bullish,
        "bearish" or "negative" => MomentumState.Bearish,
        "neutral" or "flat" => MomentumState.Neutral,
        _ => null
    };

    private static StrategyTrendDirection DeriveTrend(IReadOnlyList<MarketCandle> candles) => candles.Count < 2
        ? StrategyTrendDirection.Unknown
        : candles[^1].Close.Value > candles[0].Close.Value
            ? StrategyTrendDirection.Bullish
            : candles[^1].Close.Value < candles[0].Close.Value
                ? StrategyTrendDirection.Bearish
                : StrategyTrendDirection.Neutral;

    private static PriceStructureState DeriveStructure(IReadOnlyList<MarketCandle> candles) => candles.Count < 3
        ? PriceStructureState.Unknown
        : candles[^1].High.Value >= candles[^2].High.Value && candles[^1].Low.Value >= candles[^2].Low.Value
            ? PriceStructureState.Bullish
            : candles[^1].High.Value <= candles[^2].High.Value && candles[^1].Low.Value <= candles[^2].Low.Value
                ? PriceStructureState.Bearish
                : PriceStructureState.Range;

    private static MomentumState DeriveMomentum(IReadOnlyList<MarketCandle> candles) => candles.Count < 2
        ? MomentumState.Unknown
        : candles[^1].Close.Value > candles[^2].Close.Value
            ? MomentumState.Bullish
            : candles[^1].Close.Value < candles[^2].Close.Value
                ? MomentumState.Bearish
                : MomentumState.Neutral;

    private static VolatilityState DeriveVolatility(decimal averageRange, decimal currentPrice)
    {
        if (averageRange <= 0 || currentPrice <= 0) return VolatilityState.Unknown;
        var normalized = averageRange / currentPrice;
        return normalized <= 0.002m ? VolatilityState.Low : normalized <= 0.01m ? VolatilityState.Normal : VolatilityState.High;
    }
}

public sealed class TrendFollowingStrategy(
    IMarketContextFeatureExtractor featureExtractor,
    TimeProvider timeProvider,
    IOptions<StrategyEngineOptions> options,
    StrategyVersion? version = null) : IStrategy
{
    public static StrategyId StrategyIdentifier { get; } = new("trend-following");
    public static StrategyVersion StrategyRelease { get; } = new(1, 0, 0);

    public StrategyDefinition Definition { get; } = new(
        StrategyIdentifier,
        version ?? StrategyRelease,
        "Trend Following",
        "Deterministically identifies a trend-aligned setup from explicit market features and levels.",
        [
            new StrategyCondition("trend-confirmed", "Trend direction agrees with the setup direction.", true, 20),
            new StrategyCondition("structure-confirmed", "Price structure agrees with the setup direction.", true, 20),
            new StrategyCondition("momentum-confirmed", "Momentum agrees with the setup direction.", true, 15),
            new StrategyCondition("volatility-acceptable", "Volatility is not high for the setup.", true, 10),
            new StrategyCondition("levels-coherent", "Support, resistance and current price form coherent zones.", true, 25),
            new StrategyCondition("risk-reward-acceptable", "The explicit levels meet the minimum risk/reward ratio.", true, 10)
        ],
        [Timeframe.M1, Timeframe.M5, Timeframe.M15, Timeframe.M30, Timeframe.H1, Timeframe.H4, Timeframe.D1, Timeframe.W1, Timeframe.MN1]);

    public Task<StrategyEvaluationResult> EvaluateAsync(MarketContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var features = featureExtractor.Extract(context);
        var settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        settings.Validate();
        var direction = DetermineDirection(features);
        if (direction is null)
        {
            return Task.FromResult(NoSetup(features));
        }

        var (entry, invalidation, target, ratio) = direction == MarketDirection.Long
            ? BuildLongZones(features)
            : BuildShortZones(features);
        var confidence = Confidence(features, ratio, settings);
        if (ratio < settings.MinimumRiskRewardRatio || confidence.Score < settings.MinimumConfidence)
        {
            return Task.FromResult(new StrategyEvaluationResult(
                Definition,
                StrategyEvaluationStatus.NoSetup,
                null,
                [new StrategyEvaluationWarning("THRESHOLD_NOT_MET", $"The deterministic confidence or risk/reward threshold was not met (ratio={ratio:0.##}, confidence={confidence.Score:0.##}, requiredRatio={settings.MinimumRiskRewardRatio:0.##}, requiredConfidence={settings.MinimumConfidence:0.##}).")],
                [],
                timeProvider.GetUtcNow()));
        }

        var evidence = Evidence(features, ratio);
        var candidate = new SetupCandidate(
            Definition.Id,
            Definition.Version,
            context.Instrument,
            context.Timeframe,
            direction.Value,
            entry,
            invalidation,
            target,
            ratio,
            confidence,
            evidence,
            timeProvider.GetUtcNow());
        return Task.FromResult(new StrategyEvaluationResult(
            Definition,
            direction == MarketDirection.Long ? StrategyEvaluationStatus.BuyCandidate : StrategyEvaluationStatus.SellCandidate,
            candidate,
            [],
            [],
            candidate.GeneratedAtUtc));
    }

    private static MarketDirection? DetermineDirection(StrategyFeatureSnapshot features)
    {
        if (features.Volatility == VolatilityState.High || features.Support is null || features.Resistance is null)
            return null;
        if (features.TrendDirection == StrategyTrendDirection.Bullish
            && features.PriceStructure == PriceStructureState.Bullish
            && features.Momentum == MomentumState.Bullish
            && features.Support.Value.Value < features.CurrentPrice.Value
            && features.Resistance.Value.Value > features.CurrentPrice.Value)
            return MarketDirection.Long;
        if (features.TrendDirection == StrategyTrendDirection.Bearish
            && features.PriceStructure == PriceStructureState.Bearish
            && features.Momentum == MomentumState.Bearish
            && features.Support.Value.Value < features.CurrentPrice.Value
            && features.Resistance.Value.Value > features.CurrentPrice.Value)
            return MarketDirection.Short;
        return null;
    }

    private static (EntryZone Entry, InvalidationZone Invalidation, TargetZone Target, double Ratio) BuildLongZones(StrategyFeatureSnapshot features)
    {
        var entry = new EntryZone(features.CurrentPrice, features.CurrentPrice);
        var invalidation = new InvalidationZone(features.Support!.Value, features.Support.Value);
        var target = new TargetZone(features.Resistance!.Value, features.Resistance.Value);
        return (entry, invalidation, target, CalculateRatio(entry, invalidation, target));
    }

    private static (EntryZone Entry, InvalidationZone Invalidation, TargetZone Target, double Ratio) BuildShortZones(StrategyFeatureSnapshot features)
    {
        var entry = new EntryZone(features.CurrentPrice, features.CurrentPrice);
        var invalidation = new InvalidationZone(features.Resistance!.Value, features.Resistance.Value);
        var target = new TargetZone(features.Support!.Value, features.Support.Value);
        return (entry, invalidation, target, CalculateRatio(entry, invalidation, target));
    }

    private static double CalculateRatio(EntryZone entry, InvalidationZone invalidation, TargetZone target)
    {
        var risk = Math.Abs(entry.Midpoint.Value - invalidation.Midpoint.Value);
        var reward = Math.Abs(target.Midpoint.Value - entry.Midpoint.Value);
        return risk <= 0 ? 0 : (double)(reward / risk);
    }

    private static SetupConfidence Confidence(StrategyFeatureSnapshot features, double ratio, StrategyEngineOptions options)
    {
        var score = 0d;
        score += features.TrendDirection is StrategyTrendDirection.Bullish or StrategyTrendDirection.Bearish ? 20 : 0;
        score += features.PriceStructure is PriceStructureState.Bullish or PriceStructureState.Bearish ? 20 : 0;
        score += features.Momentum is MomentumState.Bullish or MomentumState.Bearish ? 15 : 0;
        score += features.Volatility == VolatilityState.Low ? 15 : features.Volatility == VolatilityState.Normal ? 10 : 0;
        score += features.Support is not null && features.Resistance is not null ? 20 : 0;
        score += ratio >= options.MinimumRiskRewardRatio ? 10 : 0;
        score = Math.Min(100, score);
        var band = score >= 90 ? SetupConfidenceBand.VeryHigh : score >= 80 ? SetupConfidenceBand.High : score >= 60 ? SetupConfidenceBand.Medium : SetupConfidenceBand.Low;
        return new SetupConfidence(score, band, ["trend", "price-structure", "momentum", "volatility", "support-resistance", "risk-reward"]);
    }

    private static IReadOnlyCollection<SetupEvidence> Evidence(StrategyFeatureSnapshot features, double ratio) =>
    [
        new("trend-confirmed", $"Trend is {features.TrendDirection}.", "MarketContext", 85),
        new("structure-confirmed", $"Price structure is {features.PriceStructure}.", "MarketContext", 85),
        new("momentum-confirmed", $"Momentum is {features.Momentum}.", "MarketContext", 80),
        new("volatility-acceptable", $"Volatility is {features.Volatility}.", "MarketContext", 75),
        new("levels-coherent", "Support, resistance and current price form directional zones.", "MarketContext", 90),
        new("risk-reward", $"Risk/reward ratio is {ratio:0.##}.", "DerivedLevels", 90)
    ];

    private StrategyEvaluationResult NoSetup(StrategyFeatureSnapshot features) =>
        new(
            Definition,
            StrategyEvaluationStatus.NoSetup,
            null,
            [new StrategyEvaluationWarning("NO_SETUP", $"Trend-following conditions are not aligned: trend={features.TrendDirection}, structure={features.PriceStructure}, momentum={features.Momentum}, volatility={features.Volatility}.")],
            [],
            timeProvider.GetUtcNow());
}

public static class StrategiesDependencyInjection
{
    public static IServiceCollection AddTradeMindStrategies(
        this IServiceCollection services,
        Action<StrategyEngineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var options = services.AddOptions<StrategyEngineOptions>();
        if (configure is not null) options.Configure(configure);
        options.Validate(optionsValue => { optionsValue.Validate(); return true; }, "Strategy engine options are invalid.");
        services.TryAddSingleton<IMarketContextFeatureExtractor, DefaultMarketContextFeatureExtractor>();
        services.TryAddSingleton<IStrategy, TrendFollowingStrategy>();
        services.TryAddSingleton<IStrategyRegistry, StrategyRegistry>();
        services.TryAddScoped<IStrategyEvaluator, StrategyEvaluationService>();
        return services;
    }
}
