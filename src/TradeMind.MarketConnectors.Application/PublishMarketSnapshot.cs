using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;

namespace TradeMind.MarketConnectors.Application;

public enum PublishMarketSnapshotStatus
{
    Accepted,
    Duplicate,
    Conflict,
    Rejected
}

public sealed record PublishMarketSnapshotReason(string Code, string Message);

public sealed record PublishMarketSnapshotResult(
    PublishMarketSnapshotStatus Status,
    SnapshotId? SnapshotId,
    ConnectorId? ConnectorId,
    PublishMarketSnapshotReason? Reason,
    DateTimeOffset? PersistedReceivedAt);

public sealed record PublishMarketSnapshotCommand(MarketSnapshot? Snapshot)
    : IRequest<PublishMarketSnapshotResult>;

public sealed class PublishMarketSnapshotCommandHandler(
    IMarketSnapshotRepository repository,
    IMarketSnapshotSerializer serializer,
    IMarketSnapshotHasher hasher,
    IOptions<MarketConnectorCoreOptions> options,
    TimeProvider timeProvider,
    ILogger<PublishMarketSnapshotCommandHandler> logger)
    : IRequestHandler<PublishMarketSnapshotCommand, PublishMarketSnapshotResult>
{
    private readonly MarketConnectorCoreOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    public async Task<PublishMarketSnapshotResult> Handle(
        PublishMarketSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Snapshot is null)
        {
            return Reject(null, "MARKET_SNAPSHOT_REQUIRED", "A market snapshot is required.");
        }

        var snapshot = request.Snapshot;
        var validationReason = ValidateMetadata(snapshot);
        if (validationReason is not null)
        {
            return Reject(snapshot, validationReason.Code, validationReason.Message);
        }

        var serialized = serializer.Serialize(snapshot);
        if (serialized.SchemaVersion != _options.SupportedSchemaVersion)
        {
            return Reject(
                snapshot,
                "MARKET_SNAPSHOT_SCHEMA_UNSUPPORTED",
                $"Schema version '{serialized.SchemaVersion}' is not supported.");
        }

        if (serialized.SizeInBytes > _options.MaximumPayloadBytes)
        {
            return Reject(
                snapshot,
                "MARKET_SNAPSHOT_PAYLOAD_TOO_LARGE",
                $"Payload size exceeds the {_options.MaximumPayloadBytes}-byte limit.");
        }

        var contentHash = hasher.ComputeHash(serialized.CanonicalContent);
        var existing = await repository.FindByKeyAsync(
            snapshot.ConnectorId,
            snapshot.Id,
            cancellationToken);
        if (existing is not null)
        {
            return CreateExistingResult(snapshot, contentHash, existing);
        }

        var persistenceRequest = new MarketSnapshotPersistenceRequest(
            snapshot,
            contentHash,
            serialized.CanonicalPayload,
            serialized.SchemaVersion,
            serialized.SizeInBytes,
            timeProvider.GetUtcNow());
        var insertion = await repository.InsertAsync(persistenceRequest, cancellationToken);
        var result = insertion.Status switch
        {
            MarketSnapshotAtomicInsertStatus.Inserted => new PublishMarketSnapshotResult(
                PublishMarketSnapshotStatus.Accepted,
                snapshot.Id,
                snapshot.ConnectorId,
                null,
                insertion.StoredSnapshot.ReceivedAt),
            MarketSnapshotAtomicInsertStatus.Duplicate => Duplicate(snapshot, insertion.StoredSnapshot.ReceivedAt),
            MarketSnapshotAtomicInsertStatus.Conflict => Conflict(snapshot, insertion.StoredSnapshot.ReceivedAt),
            _ => throw new InvalidOperationException("The repository returned an unknown insertion status.")
        };

        LogResult(snapshot, result.Status, contentHash);
        return result;
    }

    private PublishMarketSnapshotReason? ValidateMetadata(MarketSnapshot snapshot)
    {
        if (snapshot.Metadata.Count > _options.MaximumMetadataEntries)
        {
            return new PublishMarketSnapshotReason(
                "MARKET_SNAPSHOT_METADATA_LIMIT",
                $"Metadata contains more than {_options.MaximumMetadataEntries} entries.");
        }

        if (snapshot.Metadata.Keys.Any(key => key.Length > _options.MaximumMetadataKeyLength))
        {
            return new PublishMarketSnapshotReason(
                "MARKET_SNAPSHOT_METADATA_KEY_TOO_LONG",
                $"A metadata key exceeds {_options.MaximumMetadataKeyLength} characters.");
        }

        return snapshot.Metadata.Values.Any(value => value.Length > _options.MaximumMetadataValueLength)
            ? new PublishMarketSnapshotReason(
                "MARKET_SNAPSHOT_METADATA_VALUE_TOO_LONG",
                $"A metadata value exceeds {_options.MaximumMetadataValueLength} characters.")
            : null;
    }

    private PublishMarketSnapshotResult CreateExistingResult(
        MarketSnapshot snapshot,
        string contentHash,
        StoredMarketSnapshotIdentity existing)
    {
        var result = string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal)
            ? Duplicate(snapshot, existing.ReceivedAt)
            : Conflict(snapshot, existing.ReceivedAt);
        LogResult(snapshot, result.Status, contentHash);
        return result;
    }

    private static PublishMarketSnapshotResult Duplicate(
        MarketSnapshot snapshot,
        DateTimeOffset receivedAt) => new(
            PublishMarketSnapshotStatus.Duplicate,
            snapshot.Id,
            snapshot.ConnectorId,
            new PublishMarketSnapshotReason(
                "MARKET_SNAPSHOT_DUPLICATE",
                "The same canonical snapshot has already been accepted."),
            receivedAt);

    private static PublishMarketSnapshotResult Conflict(
        MarketSnapshot snapshot,
        DateTimeOffset receivedAt) => new(
            PublishMarketSnapshotStatus.Conflict,
            snapshot.Id,
            snapshot.ConnectorId,
            new PublishMarketSnapshotReason(
                "MARKET_SNAPSHOT_CONTENT_CONFLICT",
                "The snapshot key is already associated with different canonical content."),
            receivedAt);

    private PublishMarketSnapshotResult Reject(
        MarketSnapshot? snapshot,
        string code,
        string message)
    {
        logger.LogWarning(
            "Market snapshot ingestion rejected. ConnectorId={ConnectorId} SnapshotId={SnapshotId} Instrument={Instrument} Timeframe={Timeframe} AccountReference={AccountReference} CapturedAt={CapturedAt} ReceivedAt={ReceivedAt} ReasonCode={ReasonCode}",
            snapshot?.ConnectorId.Value,
            snapshot?.Id.Value,
            snapshot?.Instrument.Symbol,
            snapshot?.Timeframe.Code,
            snapshot?.Account.Value,
            snapshot?.CapturedAt,
            snapshot?.ReceivedAt,
            code);

        return new PublishMarketSnapshotResult(
            PublishMarketSnapshotStatus.Rejected,
            snapshot?.Id,
            snapshot?.ConnectorId,
            new PublishMarketSnapshotReason(code, message),
            null);
    }

    private void LogResult(
        MarketSnapshot snapshot,
        PublishMarketSnapshotStatus status,
        string contentHash)
    {
        logger.LogInformation(
            "Market snapshot ingestion {IngestionStatus}. ConnectorId={ConnectorId} SnapshotId={SnapshotId} Instrument={Instrument} Timeframe={Timeframe} AccountReference={AccountReference} CapturedAt={CapturedAt} ReceivedAt={ReceivedAt} ContentHashPrefix={ContentHashPrefix}",
            status,
            snapshot.ConnectorId.Value,
            snapshot.Id.Value,
            snapshot.Instrument.Symbol,
            snapshot.Timeframe.Code,
            snapshot.Account.Value,
            snapshot.CapturedAt,
            snapshot.ReceivedAt,
            contentHash[..Math.Min(12, contentHash.Length)]);
    }
}
