namespace TradeMind.Market.Abstractions;

public sealed record IndicatorPoint(DateTimeOffset Timestamp, decimal? Value);

public sealed record IndicatorSeries
{
    public IndicatorSeries(string name, IReadOnlyCollection<IndicatorPoint>? points)
    {
        Name = MarketValueObject.Normalize(name, nameof(name));
        Points = MarketCollections.CopyList(points);
    }

    public string Name { get; }
    public IReadOnlyList<IndicatorPoint> Points { get; }
}

public sealed record ChartIndicator
{
    public ChartIndicator(
        IndicatorName name,
        IndicatorInstanceId instanceId,
        IReadOnlyDictionary<string, string>? parameters,
        IReadOnlyCollection<IndicatorSeries> series)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count == 0)
        {
            throw new ArgumentException("An indicator must contain at least one series.", nameof(series));
        }

        Name = name;
        InstanceId = instanceId;
        Parameters = MarketCollections.CopyDictionary(parameters);
        Series = MarketCollections.CopyList(series);
    }

    public IndicatorName Name { get; }
    public IndicatorInstanceId InstanceId { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
    public IReadOnlyList<IndicatorSeries> Series { get; }
}
