using MediatR;
using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Domain;

namespace TradeMind.MarketConnectors.Application;

public sealed record GetLatestMarketSnapshotQuery : IRequest<MarketSnapshot?>
{
    public GetLatestMarketSnapshotQuery(
        ConnectorId connectorId,
        ExternalAccountReference? account,
        Instrument instrument,
        Timeframe timeframe,
        TimeSpan? maximumAge = null)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(timeframe);
        if (maximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAge), "Maximum age cannot be negative.");
        }

        ConnectorId = connectorId;
        Account = account;
        Instrument = instrument;
        Timeframe = timeframe;
        MaximumAge = maximumAge;
    }

    public ConnectorId ConnectorId { get; }
    public ExternalAccountReference? Account { get; }
    public Instrument Instrument { get; }
    public Timeframe Timeframe { get; }
    public TimeSpan? MaximumAge { get; }
}

public sealed class GetLatestMarketSnapshotQueryHandler(
    IMarketSnapshotRepository repository,
    IMarketSnapshotSerializer serializer,
    IOptions<MarketConnectorCoreOptions> options,
    TimeProvider timeProvider)
    : IRequestHandler<GetLatestMarketSnapshotQuery, MarketSnapshot?>
{
    private readonly MarketConnectorCoreOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    public async Task<MarketSnapshot?> Handle(
        GetLatestMarketSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var referenceTime = timeProvider.GetUtcNow();
        var lookup = new MarketSnapshotLookup(
            request.ConnectorId,
            request.Account,
            request.Instrument,
            request.Timeframe,
            request.MaximumAge is null ? null : referenceTime - request.MaximumAge.Value);
        var stored = await repository.GetLatestAsync(lookup, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        if (stored.SchemaVersion != _options.SupportedSchemaVersion)
        {
            throw new UnsupportedMarketSnapshotSchemaVersionException(stored.SchemaVersion);
        }

        var snapshot = serializer.Deserialize(stored.CanonicalPayload, stored.SchemaVersion);
        var freshness = SnapshotFreshnessClassifier.Classify(
            snapshot.CapturedAt,
            referenceTime,
            _options.FreshnessPolicy);
        var quality = new SnapshotQuality(
            freshness,
            snapshot.Quality.AvailableCapabilities,
            snapshot.Quality.MissingCapabilities,
            snapshot.Quality.Warnings);

        return new MarketSnapshot(
            snapshot.Id,
            snapshot.ConnectorId,
            snapshot.Account,
            snapshot.Instrument,
            snapshot.Timeframe,
            snapshot.CapturedAt,
            snapshot.ReceivedAt,
            snapshot.Candles,
            snapshot.Quote,
            snapshot.Positions,
            snapshot.PendingOrders,
            snapshot.Indicators,
            snapshot.Drawings,
            quality,
            snapshot.Metadata);
    }
}

public sealed record GetConnectorHealthQuery(ConnectorId ConnectorId)
    : IRequest<ConnectorHealthSnapshot>
{
    public ConnectorId ConnectorId { get; } = ConnectorId
        ?? throw new ArgumentNullException(nameof(ConnectorId));
}

public sealed class GetConnectorHealthQueryHandler(
    IMarketSnapshotRepository repository,
    IOptions<MarketConnectorCoreOptions> options,
    TimeProvider timeProvider)
    : IRequestHandler<GetConnectorHealthQuery, ConnectorHealthSnapshot>
{
    private readonly MarketConnectorCoreOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    public async Task<ConnectorHealthSnapshot> Handle(
        GetConnectorHealthQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lastReceivedAt = await repository.GetLatestReceivedAtAsync(
            request.ConnectorId,
            cancellationToken);

        return ConnectorHealthClassifier.Classify(
            request.ConnectorId,
            lastReceivedAt,
            timeProvider.GetUtcNow(),
            _options.HealthPolicy);
    }
}
