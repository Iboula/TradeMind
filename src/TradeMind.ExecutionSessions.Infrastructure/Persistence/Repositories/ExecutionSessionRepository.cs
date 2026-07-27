using Microsoft.EntityFrameworkCore;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;
using TradeMind.ExecutionSessions.Infrastructure.Persistence.Entities;

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence.Repositories;

public sealed class ExecutionSessionRepository(ExecutionSessionsDbContext dbContext, IExecutionSessionAccessScope? accessScope = null) : IExecutionSessionRepository
{
    private readonly IExecutionSessionAccessScope _accessScope = accessScope ?? new UnrestrictedExecutionSessionAccessScope();
    public async Task<ExecutionSession?> GetAsync(ExecutionSessionId id, CancellationToken cancellationToken)
    {
        var entity = await ApplyScope(dbContext.ExecutionSessions.AsNoTracking())
            .AsSplitQuery()
            .Include(item => item.Artifacts)
            .Include(item => item.Timeline)
            .SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ExecutionSessionEntityMapper.ToDomain(entity);
    }

    public async Task AddAsync(ExecutionSession session, CancellationToken cancellationToken)
    {
        await dbContext.ExecutionSessions.AddAsync(ExecutionSessionEntityMapper.ToEntity(session), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ExecutionSession session, long expectedConcurrencyVersion, CancellationToken cancellationToken)
    {
        var current = await ApplyScope(dbContext.ExecutionSessions)
            .Include(item => item.Artifacts)
            .Include(item => item.Timeline)
            .SingleOrDefaultAsync(item => item.Id == session.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (current is null) throw new ExecutionSessionNotFoundException(session.Id);

        current.CorrelationId = session.CorrelationId.Value;
        current.IdempotencyKeyHash = session.IdempotencyKeyHash;
        current.TenantId = session.TenantId;
        current.UserId = session.UserId;
        current.OrganizationId = session.OrganizationId;
        current.CreatedByActorId = session.CreatedByActorId;
        current.CreatedByActorType = session.CreatedByActorType;
        current.Instrument = session.Instrument;
        current.Timeframe = session.Timeframe;
        current.StartedAtUtc = session.StartedAtUtc;
        current.UpdatedAtUtc = session.UpdatedAtUtc;
        current.CompletedAtUtc = session.CompletedAtUtc;
        current.Status = session.Status.ToString();
        current.CurrentStage = session.CurrentStage.ToString();
        current.SchemaVersion = session.SchemaVersion;
        current.CoreVersion = session.CoreVersion;
        current.ApiVersion = session.ApiVersion;
        current.TriggerType = session.TriggerType.ToString();
        current.Source = session.Source;
        current.FailureCode = session.Failure?.Code;
        current.FailureMessage = session.Failure?.Message;
        current.FailureDetails = session.Failure?.Details;
        current.MetadataJson = ExecutionSessionSerialization.Serialize(session.Metadata);
        current.ConcurrencyVersion = session.ConcurrencyVersion;
        dbContext.Entry(current).Property(item => item.ConcurrencyVersion).OriginalValue = expectedConcurrencyVersion;

        var artifactKeys = current.Artifacts
            .Select(item => (item.ArtifactType, item.ArtifactId))
            .ToHashSet();
        foreach (var artifact in session.ArtifactReferences)
        {
            if (artifactKeys.Contains((artifact.ArtifactType.ToString(), artifact.ArtifactId.Value))) continue;
            var entity = ExecutionSessionEntityMapper.ToEntity(artifact, session.Id.Value);
            current.Artifacts.Add(entity);
            dbContext.Entry(entity).State = EntityState.Added;
        }

        var timelineIds = current.Timeline.Select(item => item.Id).ToHashSet();
        foreach (var timeline in session.TimelineEntries)
        {
            if (timelineIds.Contains(timeline.TimelineId)) continue;
            var entity = ExecutionSessionEntityMapper.ToEntity(timeline, session.Id.Value);
            current.Timeline.Add(entity);
            dbContext.Entry(entity).State = EntityState.Added;
        }
    }

    public async Task<ExecutionSessionTimelineDto[]> GetTimelineAsync(ExecutionSessionId id, CancellationToken cancellationToken)
    {
        var exists = await ApplyScope(dbContext.ExecutionSessions).AnyAsync(item => item.Id == id.Value, cancellationToken).ConfigureAwait(false);
        if (!exists) throw new ExecutionSessionNotFoundException(id);
        var rows = await dbContext.Timeline.AsNoTracking()
            .Where(item => item.SessionId == id.Value)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.EventType, item.Stage, item.OccurredAtUtc, item.MetadataJson })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(item => new ExecutionSessionTimelineDto(item.Id, item.EventType, item.Stage, item.OccurredAtUtc,
            ExecutionSessionSerialization.Deserialize(item.MetadataJson))).ToArray();
    }

    public async Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchFilter filter, CancellationToken cancellationToken)
    {
        var query = ApplyScope(dbContext.ExecutionSessions.AsNoTracking().AsQueryable());
        if (filter.Status is not null) query = query.Where(item => item.Status == filter.Status.Value.ToString());
        if (!string.IsNullOrWhiteSpace(filter.Instrument)) query = query.Where(item => item.Instrument == filter.Instrument.Trim());
        if (filter.StartedFromUtc is not null) query = query.Where(item => item.StartedAtUtc >= filter.StartedFromUtc.Value);
        if (filter.StartedToUtc is not null) query = query.Where(item => item.StartedAtUtc <= filter.StartedToUtc.Value);
        if (filter.Stage is not null) query = query.Where(item => item.CurrentStage == filter.Stage.Value.ToString());
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId)) query = query.Where(item => item.CorrelationId == filter.CorrelationId.Trim());
        if (filter.TriggerType is not null) query = query.Where(item => item.TriggerType == filter.TriggerType.Value.ToString());
        if (!string.IsNullOrWhiteSpace(filter.ArtifactId)) query = query.Where(item => item.Artifacts.Any(artifact => artifact.ArtifactId == filter.ArtifactId.Trim()));

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(item => item.StartedAtUtc).ThenBy(item => item.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(item => new ExecutionSessionSearchItem(item.Id.ToString(), item.CorrelationId, item.Instrument, item.Timeframe,
                item.Status, item.CurrentStage, item.TriggerType, item.StartedAtUtc, item.UpdatedAtUtc, item.Artifacts.Count))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ExecutionSessionSearchPage(items, filter.Page, filter.PageSize, total);
    }

    private IQueryable<ExecutionSessionEntity> ApplyScope(IQueryable<ExecutionSessionEntity> query) =>
        !_accessScope.IsRestricted
            ? query
            : string.IsNullOrWhiteSpace(_accessScope.OrganizationId) || string.IsNullOrWhiteSpace(_accessScope.TenantId)
                ? query.Where(_ => false)
                : query.Where(item => item.OrganizationId == _accessScope.OrganizationId && item.TenantId == _accessScope.TenantId);
}

internal static class ExecutionSessionEntityMapper
{
    public static ExecutionSessionEntity ToEntity(ExecutionSession session) => new()
    {
        Id = session.Id.Value,
        CorrelationId = session.CorrelationId.Value,
        IdempotencyKeyHash = session.IdempotencyKeyHash,
        TenantId = session.TenantId,
        UserId = session.UserId,
        OrganizationId = session.OrganizationId,
        CreatedByActorId = session.CreatedByActorId,
        CreatedByActorType = session.CreatedByActorType,
        Instrument = session.Instrument,
        Timeframe = session.Timeframe,
        StartedAtUtc = session.StartedAtUtc,
        UpdatedAtUtc = session.UpdatedAtUtc,
        CompletedAtUtc = session.CompletedAtUtc,
        Status = session.Status.ToString(),
        CurrentStage = session.CurrentStage.ToString(),
        SchemaVersion = session.SchemaVersion,
        CoreVersion = session.CoreVersion,
        ApiVersion = session.ApiVersion,
        TriggerType = session.TriggerType.ToString(),
        Source = session.Source,
        FailureCode = session.Failure?.Code,
        FailureMessage = session.Failure?.Message,
        FailureDetails = session.Failure?.Details,
        MetadataJson = ExecutionSessionSerialization.Serialize(session.Metadata),
        ConcurrencyVersion = session.ConcurrencyVersion,
        Artifacts = session.ArtifactReferences.Select(item => ToEntity(item, session.Id.Value)).ToList(),
        Timeline = session.TimelineEntries.Select(item => ToEntity(item, session.Id.Value)).ToList()
    };

    public static ExecutionSessionArtifactEntity ToEntity(ExecutionSessionArtifactReference artifact, Guid sessionId) => new()
    {
        Id = Guid.NewGuid(), SessionId = sessionId, ArtifactType = artifact.ArtifactType.ToString(), ArtifactId = artifact.ArtifactId.Value,
        Stage = artifact.Stage.ToString(), SchemaVersion = artifact.SchemaVersion, CreatedAtUtc = artifact.CreatedAtUtc,
        ContentHash = artifact.ContentHash, StorageReference = artifact.StorageReference, IsReplayable = artifact.IsReplayable,
        MetadataJson = ExecutionSessionSerialization.Serialize(artifact.Metadata)
    };

    public static ExecutionSessionTimelineEntity ToEntity(ExecutionSessionTimelineEntry timeline, Guid sessionId) => new()
    {
        Id = timeline.TimelineId, SessionId = sessionId, EventType = timeline.EventType, Stage = timeline.Stage.ToString(),
        OccurredAtUtc = timeline.OccurredAtUtc, MetadataJson = ExecutionSessionSerialization.Serialize(timeline.Metadata)
    };

    public static ExecutionSession ToDomain(ExecutionSessionEntity entity)
    {
        var artifacts = entity.Artifacts.Select(item => new ExecutionSessionArtifactReference(
            Enum.Parse<ExecutionSessionArtifactType>(item.ArtifactType), new ExecutionSessionArtifactId(item.ArtifactId),
            Enum.Parse<ExecutionSessionStage>(item.Stage), item.SchemaVersion, item.CreatedAtUtc, item.ContentHash,
            item.StorageReference, item.IsReplayable, ExecutionSessionSerialization.Deserialize(item.MetadataJson)));
        var timeline = entity.Timeline.Select(item => new ExecutionSessionTimelineEntry(item.Id, item.EventType,
            Enum.Parse<ExecutionSessionStage>(item.Stage), item.OccurredAtUtc, ExecutionSessionSerialization.Deserialize(item.MetadataJson)));
        var failure = entity.FailureCode is null ? null : new ExecutionSessionFailure(entity.FailureCode, entity.FailureMessage ?? "Execution session failed.", entity.FailureDetails);
        return ExecutionSession.Rehydrate(new ExecutionSessionId(entity.Id), new ExecutionCorrelationId(entity.CorrelationId), entity.IdempotencyKeyHash,
            entity.TenantId, entity.UserId, entity.Instrument, entity.Timeframe, entity.StartedAtUtc, entity.UpdatedAtUtc,
            entity.CompletedAtUtc, Enum.Parse<ExecutionSessionStatus>(entity.Status), Enum.Parse<ExecutionSessionStage>(entity.CurrentStage),
            entity.SchemaVersion, entity.CoreVersion, entity.ApiVersion, Enum.Parse<ExecutionSessionTriggerType>(entity.TriggerType),
            entity.Source, failure, ExecutionSessionSerialization.Deserialize(entity.MetadataJson), artifacts, timeline, entity.ConcurrencyVersion,
            entity.OrganizationId, entity.CreatedByActorId, entity.CreatedByActorType);
    }
}
