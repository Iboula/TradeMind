using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;

namespace TradeMind.MarketConnectors.Infrastructure;

public sealed class MarketSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public required string ConnectorId { get; set; }
    public required string AccountReference { get; set; }
    public required string Instrument { get; set; }
    public required string Timeframe { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public required string Freshness { get; set; }
    public required string ContentHash { get; set; }
    public bool IsComplete { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required MarketSnapshotPayload Payload { get; set; }
}

public sealed class MarketSnapshotPayload
{
    public Guid MarketSnapshotRecordId { get; set; }
    public required string Document { get; set; }
    public int SchemaVersion { get; set; }
    public int PayloadSizeBytes { get; set; }
    public MarketSnapshotRecord? Record { get; set; }
}

public sealed class MarketConnectorsDbContext(DbContextOptions<MarketConnectorsDbContext> options)
    : DbContext(options)
{
    public DbSet<MarketSnapshotRecord> MarketSnapshotRecords => Set<MarketSnapshotRecord>();

    public DbSet<MarketSnapshotPayload> MarketSnapshotPayloads => Set<MarketSnapshotPayload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketSnapshotRecord>(entity =>
        {
            entity.ToTable("market_snapshot_records", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_market_snapshot_records_content_hash",
                    "char_length(content_hash) = 64");
            });
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(record => record.SnapshotId).HasColumnName("snapshot_id").IsRequired();
            entity.Property(record => record.ConnectorId).HasColumnName("connector_id").HasMaxLength(128).IsRequired();
            entity.Property(record => record.AccountReference).HasColumnName("account_reference").HasMaxLength(256).IsRequired();
            entity.Property(record => record.Instrument).HasColumnName("instrument").HasMaxLength(128).IsRequired();
            entity.Property(record => record.Timeframe).HasColumnName("timeframe").HasMaxLength(16).IsRequired();
            entity.Property(record => record.CapturedAt).HasColumnName("captured_at").IsRequired();
            entity.Property(record => record.ReceivedAt).HasColumnName("received_at").IsRequired();
            entity.Property(record => record.Freshness).HasColumnName("freshness").HasMaxLength(16).IsRequired();
            entity.Property(record => record.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(record => record.IsComplete).HasColumnName("is_complete").IsRequired();
            entity.Property(record => record.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(record => new { record.ConnectorId, record.SnapshotId })
                .IsUnique()
                .HasDatabaseName(PostgreSqlMarketSnapshotRepository.IdempotenceConstraintName);
            entity.HasIndex(record => new
            {
                record.ConnectorId,
                record.AccountReference,
                record.Instrument,
                record.Timeframe,
                record.CapturedAt
            })
                .IsDescending(false, false, false, false, true)
                .HasDatabaseName("ix_market_snapshot_records_latest");
            entity.HasIndex(record => new { record.ConnectorId, record.ReceivedAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_market_snapshot_records_health");

            entity.HasOne(record => record.Payload)
                .WithOne(payload => payload.Record)
                .HasForeignKey<MarketSnapshotPayload>(payload => payload.MarketSnapshotRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MarketSnapshotPayload>(entity =>
        {
            entity.ToTable("market_snapshot_payloads", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_market_snapshot_payloads_schema_version",
                    "schema_version > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_market_snapshot_payloads_size",
                    "payload_size_bytes >= 0");
            });
            entity.HasKey(payload => payload.MarketSnapshotRecordId);
            entity.Property(payload => payload.MarketSnapshotRecordId)
                .HasColumnName("market_snapshot_record_id")
                .ValueGeneratedNever();
            entity.Property(payload => payload.Document).HasColumnName("document").HasColumnType("jsonb").IsRequired();
            entity.Property(payload => payload.SchemaVersion).HasColumnName("schema_version").IsRequired();
            entity.Property(payload => payload.PayloadSizeBytes).HasColumnName("payload_size_bytes").IsRequired();
        });
    }
}

public sealed class PostgreSqlMarketSnapshotRepository(MarketConnectorsDbContext dbContext)
    : IMarketSnapshotRepository
{
    public const string IdempotenceConstraintName = "uq_market_snapshot_records_connector_snapshot";

    public async Task<StoredMarketSnapshotIdentity?> FindByKeyAsync(
        ConnectorId connectorId,
        SnapshotId snapshotId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        ArgumentNullException.ThrowIfNull(snapshotId);

        return await dbContext.MarketSnapshotRecords
            .AsNoTracking()
            .Where(record => record.ConnectorId == connectorId.Value && record.SnapshotId == snapshotId.Value)
            .Select(record => new StoredMarketSnapshotIdentity(
                new SnapshotId(record.SnapshotId),
                new ConnectorId(record.ConnectorId),
                record.ContentHash,
                record.ReceivedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<MarketSnapshotAtomicInsertResult> InsertAsync(
        MarketSnapshotPersistenceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = CreateEntity(request);
        await dbContext.MarketSnapshotRecords.AddAsync(entity, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new MarketSnapshotAtomicInsertResult(
                MarketSnapshotAtomicInsertStatus.Inserted,
                ToIdentity(entity));
        }
        catch (DbUpdateException exception) when (IsIdempotenceViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var winner = await FindByKeyAsync(
                request.Snapshot.ConnectorId,
                request.Snapshot.Id,
                cancellationToken);
            if (winner is null)
            {
                throw new InvalidOperationException(
                    "The idempotence constraint was violated but the winning snapshot could not be read.",
                    exception);
            }

            var status = string.Equals(winner.ContentHash, request.ContentHash, StringComparison.Ordinal)
                ? MarketSnapshotAtomicInsertStatus.Duplicate
                : MarketSnapshotAtomicInsertStatus.Conflict;
            return new MarketSnapshotAtomicInsertResult(status, winner);
        }
    }

    public async Task<StoredMarketSnapshotPayload?> GetLatestAsync(
        MarketSnapshotLookup lookup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        var query = dbContext.MarketSnapshotRecords
            .AsNoTracking()
            .Where(record => record.ConnectorId == lookup.ConnectorId.Value)
            .Where(record => record.Instrument == lookup.Instrument.Symbol)
            .Where(record => record.Timeframe == lookup.Timeframe.Code);

        if (lookup.Account is not null)
        {
            query = query.Where(record => record.AccountReference == lookup.Account.Value);
        }

        if (lookup.MinimumCapturedAt is not null)
        {
            query = query.Where(record => record.CapturedAt >= lookup.MinimumCapturedAt.Value);
        }

        return await query
            .OrderByDescending(record => record.CapturedAt)
            .ThenByDescending(record => record.ReceivedAt)
            .ThenByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.Id)
            .Select(record => new StoredMarketSnapshotPayload(
                record.Payload.Document,
                record.Payload.SchemaVersion))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<DateTimeOffset?> GetLatestReceivedAtAsync(
        ConnectorId connectorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        return dbContext.MarketSnapshotRecords
            .AsNoTracking()
            .Where(record => record.ConnectorId == connectorId.Value)
            .Select(record => (DateTimeOffset?)record.ReceivedAt)
            .MaxAsync(cancellationToken);
    }

    private static MarketSnapshotRecord CreateEntity(MarketSnapshotPersistenceRequest request)
    {
        var id = Guid.NewGuid();
        return new MarketSnapshotRecord
        {
            Id = id,
            SnapshotId = request.Snapshot.Id.Value,
            ConnectorId = request.Snapshot.ConnectorId.Value,
            AccountReference = request.Snapshot.Account.Value,
            Instrument = request.Snapshot.Instrument.Symbol,
            Timeframe = request.Snapshot.Timeframe.Code,
            CapturedAt = request.Snapshot.CapturedAt,
            ReceivedAt = request.Snapshot.ReceivedAt,
            Freshness = request.Snapshot.Quality.Freshness.ToString(),
            ContentHash = request.ContentHash,
            IsComplete = request.Snapshot.Quality.IsComplete,
            CreatedAt = request.CreatedAt,
            Payload = new MarketSnapshotPayload
            {
                MarketSnapshotRecordId = id,
                Document = request.CanonicalPayload,
                SchemaVersion = request.SchemaVersion,
                PayloadSizeBytes = request.PayloadSizeBytes
            }
        };
    }

    private static StoredMarketSnapshotIdentity ToIdentity(MarketSnapshotRecord record) => new(
        new SnapshotId(record.SnapshotId),
        new ConnectorId(record.ConnectorId),
        record.ContentHash,
        record.ReceivedAt);

    private static bool IsIdempotenceViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdempotenceConstraintName
        };
}
