using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;

namespace TradeMind.MarketConnectors.Infrastructure;

public sealed class Sha256MarketSnapshotHasher : IMarketSnapshotHasher
{
    public string ComputeHash(string canonicalPayload)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));
        return Convert.ToHexStringLower(hash);
    }
}

public sealed class CanonicalMarketSnapshotSerializer : IMarketSnapshotSerializer
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public int CurrentSchemaVersion => SchemaVersion;

    public SerializedMarketSnapshot Serialize(MarketSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var content = ToDto(snapshot);
        var canonicalContent = JsonSerializer.Serialize(content, SerializerOptions);
        var payload = JsonSerializer.Serialize(
            new SnapshotDocumentDto(SchemaVersion, content),
            SerializerOptions);

        return new SerializedMarketSnapshot(
            payload,
            canonicalContent,
            SchemaVersion,
            Encoding.UTF8.GetByteCount(payload));
    }

    public MarketSnapshot Deserialize(string canonicalPayload, int schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayload);
        if (schemaVersion != SchemaVersion)
        {
            throw new UnsupportedMarketSnapshotSchemaVersionException(schemaVersion);
        }

        var document = JsonSerializer.Deserialize<SnapshotDocumentDto>(canonicalPayload, SerializerOptions)
            ?? throw new JsonException("The market snapshot payload is empty.");
        if (document.SchemaVersion != schemaVersion)
        {
            throw new JsonException("The payload schema version does not match its persisted schema version.");
        }

        return FromDto(document.Snapshot);
    }

    private static SnapshotContentDto ToDto(MarketSnapshot snapshot) => new(
        snapshot.Id.Value.ToString("D", CultureInfo.InvariantCulture),
        snapshot.ConnectorId.Value,
        snapshot.Account.Value,
        snapshot.Instrument.Symbol,
        snapshot.Timeframe.Code,
        FormatDate(snapshot.CapturedAt),
        FormatDate(snapshot.ReceivedAt),
        snapshot.Candles.Select(ToDto).ToArray(),
        snapshot.Quote is null ? null : ToDto(snapshot.Quote),
        snapshot.Positions.Select(ToDto).ToArray(),
        snapshot.PendingOrders.Select(ToDto).ToArray(),
        snapshot.Indicators.Select(ToDto).ToArray(),
        snapshot.Drawings.Select(ToDto).ToArray(),
        new QualityDto(
            snapshot.Quality.Freshness,
            snapshot.Quality.AvailableCapabilities,
            snapshot.Quality.MissingCapabilities,
            snapshot.Quality.Warnings
                .Select(warning => new WarningDto(warning.Code, warning.Message))
                .ToArray()),
        Sort(snapshot.Metadata));

    private static CandleDto ToDto(MarketCandle candle) => new(
        FormatDate(candle.OpenTime),
        candle.Open.Value,
        candle.High.Value,
        candle.Low.Value,
        candle.Close.Value,
        candle.TickVolume,
        candle.RealVolume,
        candle.IsClosed);

    private static QuoteDto ToDto(MarketQuote quote) => new(
        quote.Bid.Value,
        quote.Ask.Value,
        quote.Last?.Value,
        FormatDate(quote.Timestamp));

    private static PositionDto ToDto(TradingPosition position) => new(
        position.Id.Value,
        position.Instrument.Symbol,
        position.Direction,
        position.Volume,
        position.EntryPrice.Value,
        position.StopLoss?.Value,
        position.TakeProfit?.Value,
        FormatDate(position.OpenedAt),
        position.UnrealizedProfitLoss);

    private static PendingOrderDto ToDto(PendingOrder order) => new(
        order.Id.Value,
        order.Instrument.Symbol,
        order.Type,
        order.Direction,
        order.Volume,
        order.RequestedPrice.Value,
        order.StopLoss?.Value,
        order.TakeProfit?.Value,
        FormatDate(order.CreatedAt),
        order.ExpiresAt is null ? null : FormatDate(order.ExpiresAt.Value));

    private static IndicatorDto ToDto(ChartIndicator indicator) => new(
        indicator.Name.Value,
        indicator.InstanceId.Value,
        Sort(indicator.Parameters),
        indicator.Series.Select(series => new IndicatorSeriesDto(
            series.Name,
            series.Points.Select(point => new IndicatorPointDto(
                FormatDate(point.Timestamp),
                point.Value)).ToArray())).ToArray());

    private static DrawingDto ToDto(ChartDrawing drawing) => drawing switch
    {
        HorizontalLineDrawing horizontal => new DrawingDto
        {
            Type = horizontal.Type,
            Id = horizontal.Id.Value,
            Label = horizontal.Label,
            Price = horizontal.Price.Value
        },
        VerticalLineDrawing vertical => new DrawingDto
        {
            Type = vertical.Type,
            Id = vertical.Id.Value,
            Label = vertical.Label,
            Time = FormatDate(vertical.Time)
        },
        TrendLineDrawing trend => new DrawingDto
        {
            Type = trend.Type,
            Id = trend.Id.Value,
            Label = trend.Label,
            StartTime = FormatDate(trend.StartTime),
            StartPrice = trend.StartPrice.Value,
            EndTime = FormatDate(trend.EndTime),
            EndPrice = trend.EndPrice.Value
        },
        RectangleDrawing rectangle => new DrawingDto
        {
            Type = rectangle.Type,
            Id = rectangle.Id.Value,
            Label = rectangle.Label,
            StartTime = FormatDate(rectangle.StartTime),
            EndTime = FormatDate(rectangle.EndTime),
            LowerPrice = rectangle.LowerPrice.Value,
            UpperPrice = rectangle.UpperPrice.Value
        },
        TextDrawing text => new DrawingDto
        {
            Type = text.Type,
            Id = text.Id.Value,
            Label = text.Label,
            Time = FormatDate(text.Time),
            Price = text.Price?.Value,
            Text = text.Text
        },
        RiskRewardBoxDrawing riskReward => new DrawingDto
        {
            Type = riskReward.Type,
            Id = riskReward.Id.Value,
            Label = riskReward.Label,
            Direction = riskReward.Direction,
            StartTime = FormatDate(riskReward.StartTime),
            EndTime = FormatDate(riskReward.EndTime),
            EntryPrice = riskReward.EntryPrice.Value,
            StopPrice = riskReward.StopPrice.Value,
            TargetPrice = riskReward.TargetPrice.Value
        },
        _ => throw new NotSupportedException($"Drawing type '{drawing.GetType().Name}' is not supported.")
    };

    private static MarketSnapshot FromDto(SnapshotContentDto snapshot) => new(
        new SnapshotId(Guid.ParseExact(snapshot.Id, "D")),
        new ConnectorId(snapshot.ConnectorId),
        new ExternalAccountReference(snapshot.Account),
        new Instrument(snapshot.Instrument),
        Timeframe.Parse(snapshot.Timeframe),
        ParseDate(snapshot.CapturedAt),
        ParseDate(snapshot.ReceivedAt),
        snapshot.Candles.Select(FromDto).ToArray(),
        snapshot.Quote is null ? null : FromDto(snapshot.Quote),
        snapshot.Positions.Select(FromDto).ToArray(),
        snapshot.PendingOrders.Select(FromDto).ToArray(),
        snapshot.Indicators.Select(FromDto).ToArray(),
        snapshot.Drawings.Select(FromDto).ToArray(),
        new SnapshotQuality(
            snapshot.Quality.Freshness,
            snapshot.Quality.AvailableCapabilities,
            snapshot.Quality.MissingCapabilities,
            snapshot.Quality.Warnings
                .Select(warning => new SnapshotWarning(warning.Code, warning.Message))
                .ToArray()),
        snapshot.Metadata);

    private static MarketCandle FromDto(CandleDto candle) => new(
        ParseDate(candle.OpenTime),
        new Price(candle.Open),
        new Price(candle.High),
        new Price(candle.Low),
        new Price(candle.Close),
        candle.TickVolume,
        candle.RealVolume,
        candle.IsClosed);

    private static MarketQuote FromDto(QuoteDto quote) => new(
        new Price(quote.Bid),
        new Price(quote.Ask),
        quote.Last is null ? null : new Price(quote.Last.Value),
        ParseDate(quote.Timestamp));

    private static TradingPosition FromDto(PositionDto position) => new(
        new ExternalPositionId(position.Id),
        new Instrument(position.Instrument),
        position.Direction,
        position.Volume,
        new Price(position.EntryPrice),
        position.StopLoss is null ? null : new Price(position.StopLoss.Value),
        position.TakeProfit is null ? null : new Price(position.TakeProfit.Value),
        ParseDate(position.OpenedAt),
        position.UnrealizedProfitLoss);

    private static PendingOrder FromDto(PendingOrderDto order) => new(
        new ExternalOrderId(order.Id),
        new Instrument(order.Instrument),
        order.Type,
        order.Direction,
        order.Volume,
        new Price(order.RequestedPrice),
        order.StopLoss is null ? null : new Price(order.StopLoss.Value),
        order.TakeProfit is null ? null : new Price(order.TakeProfit.Value),
        ParseDate(order.CreatedAt),
        order.ExpiresAt is null ? null : ParseDate(order.ExpiresAt));

    private static ChartIndicator FromDto(IndicatorDto indicator) => new(
        new IndicatorName(indicator.Name),
        new IndicatorInstanceId(indicator.InstanceId),
        indicator.Parameters,
        indicator.Series.Select(series => new IndicatorSeries(
            series.Name,
            series.Points.Select(point => new IndicatorPoint(
                ParseDate(point.Timestamp),
                point.Value)).ToArray())).ToArray());

    private static ChartDrawing FromDto(DrawingDto drawing) => drawing.Type switch
    {
        ChartDrawingType.HorizontalLine => new HorizontalLineDrawing(
            new DrawingId(drawing.Id),
            new Price(Required(drawing.Price, nameof(drawing.Price))),
            drawing.Label),
        ChartDrawingType.VerticalLine => new VerticalLineDrawing(
            new DrawingId(drawing.Id),
            ParseDate(Required(drawing.Time, nameof(drawing.Time))),
            drawing.Label),
        ChartDrawingType.TrendLine => new TrendLineDrawing(
            new DrawingId(drawing.Id),
            ParseDate(Required(drawing.StartTime, nameof(drawing.StartTime))),
            new Price(Required(drawing.StartPrice, nameof(drawing.StartPrice))),
            ParseDate(Required(drawing.EndTime, nameof(drawing.EndTime))),
            new Price(Required(drawing.EndPrice, nameof(drawing.EndPrice))),
            drawing.Label),
        ChartDrawingType.Rectangle => new RectangleDrawing(
            new DrawingId(drawing.Id),
            ParseDate(Required(drawing.StartTime, nameof(drawing.StartTime))),
            ParseDate(Required(drawing.EndTime, nameof(drawing.EndTime))),
            new Price(Required(drawing.LowerPrice, nameof(drawing.LowerPrice))),
            new Price(Required(drawing.UpperPrice, nameof(drawing.UpperPrice))),
            drawing.Label),
        ChartDrawingType.Text => new TextDrawing(
            new DrawingId(drawing.Id),
            ParseDate(Required(drawing.Time, nameof(drawing.Time))),
            drawing.Price is null ? null : new Price(drawing.Price.Value),
            Required(drawing.Text, nameof(drawing.Text)),
            drawing.Label),
        ChartDrawingType.RiskRewardBox => new RiskRewardBoxDrawing(
            new DrawingId(drawing.Id),
            Required(drawing.Direction, nameof(drawing.Direction)),
            ParseDate(Required(drawing.StartTime, nameof(drawing.StartTime))),
            ParseDate(Required(drawing.EndTime, nameof(drawing.EndTime))),
            new Price(Required(drawing.EntryPrice, nameof(drawing.EntryPrice))),
            new Price(Required(drawing.StopPrice, nameof(drawing.StopPrice))),
            new Price(Required(drawing.TargetPrice, nameof(drawing.TargetPrice))),
            drawing.Label),
        _ => throw new JsonException($"Drawing type '{drawing.Type}' is not supported.")
    };

    private static SortedDictionary<string, string> Sort(IReadOnlyDictionary<string, string> values)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            sorted.Add(key, value);
        }

        return sorted;
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static T Required<T>(T? value, string propertyName)
        where T : struct =>
        value ?? throw new JsonException($"Drawing property '{propertyName}' is required.");

    private static string Required(string? value, string propertyName) =>
        value ?? throw new JsonException($"Drawing property '{propertyName}' is required.");

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record SnapshotDocumentDto(int SchemaVersion, SnapshotContentDto Snapshot);

    private sealed record SnapshotContentDto(
        string Id,
        string ConnectorId,
        string Account,
        string Instrument,
        string Timeframe,
        string CapturedAt,
        string ReceivedAt,
        IReadOnlyList<CandleDto> Candles,
        QuoteDto? Quote,
        IReadOnlyList<PositionDto> Positions,
        IReadOnlyList<PendingOrderDto> PendingOrders,
        IReadOnlyList<IndicatorDto> Indicators,
        IReadOnlyList<DrawingDto> Drawings,
        QualityDto Quality,
        IReadOnlyDictionary<string, string> Metadata);

    private sealed record CandleDto(
        string OpenTime,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long TickVolume,
        long? RealVolume,
        bool IsClosed);

    private sealed record QuoteDto(decimal Bid, decimal Ask, decimal? Last, string Timestamp);

    private sealed record PositionDto(
        string Id,
        string Instrument,
        MarketDirection Direction,
        decimal Volume,
        decimal EntryPrice,
        decimal? StopLoss,
        decimal? TakeProfit,
        string OpenedAt,
        decimal? UnrealizedProfitLoss);

    private sealed record PendingOrderDto(
        string Id,
        string Instrument,
        PendingOrderType Type,
        MarketDirection Direction,
        decimal Volume,
        decimal RequestedPrice,
        decimal? StopLoss,
        decimal? TakeProfit,
        string CreatedAt,
        string? ExpiresAt);

    private sealed record IndicatorDto(
        string Name,
        string InstanceId,
        IReadOnlyDictionary<string, string> Parameters,
        IReadOnlyList<IndicatorSeriesDto> Series);

    private sealed record IndicatorSeriesDto(string Name, IReadOnlyList<IndicatorPointDto> Points);

    private sealed record IndicatorPointDto(string Timestamp, decimal? Value);

    private sealed class DrawingDto
    {
        public ChartDrawingType Type { get; init; }
        public required string Id { get; init; }
        public string? Label { get; init; }
        public string? Time { get; init; }
        public decimal? Price { get; init; }
        public string? StartTime { get; init; }
        public decimal? StartPrice { get; init; }
        public string? EndTime { get; init; }
        public decimal? EndPrice { get; init; }
        public decimal? LowerPrice { get; init; }
        public decimal? UpperPrice { get; init; }
        public string? Text { get; init; }
        public MarketDirection? Direction { get; init; }
        public decimal? EntryPrice { get; init; }
        public decimal? StopPrice { get; init; }
        public decimal? TargetPrice { get; init; }
    }

    private sealed record QualityDto(
        SnapshotFreshness Freshness,
        ConnectorCapabilities AvailableCapabilities,
        ConnectorCapabilities MissingCapabilities,
        IReadOnlyList<WarningDto> Warnings);

    private sealed record WarningDto(string Code, string Message);
}
