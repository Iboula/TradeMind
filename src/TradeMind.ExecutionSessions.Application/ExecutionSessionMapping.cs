using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Application.Commands;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Application.Queries;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application;

public static class ExecutionSessionMapper
{
    public static ExecutionSessionDto ToDto(ExecutionSession session) => new(
        session.Id.ToString(),
        session.CorrelationId.Value,
        session.IdempotencyKeyHash,
        session.TenantId,
        session.UserId,
        session.Instrument,
        session.Timeframe,
        session.StartedAtUtc,
        session.UpdatedAtUtc,
        session.CompletedAtUtc,
        session.Status.ToString(),
        session.CurrentStage.ToString(),
        session.SchemaVersion,
        session.CoreVersion,
        session.ApiVersion,
        session.TriggerType.ToString(),
        session.Source,
        session.Failure is null ? null : new(session.Failure.Code, session.Failure.Message, session.Failure.Details),
        session.Metadata,
        session.ArtifactReferences.Select(ToDto).ToArray(),
        session.TimelineEntries.Select(ToDto).ToArray(),
        session.ConcurrencyVersion);

    public static ExecutionSessionArtifactDto ToDto(ExecutionSessionArtifactReference artifact) => new(
        artifact.ArtifactType.ToString(),
        artifact.ArtifactId.Value,
        artifact.Stage.ToString(),
        artifact.SchemaVersion,
        artifact.CreatedAtUtc,
        artifact.ContentHash,
        artifact.StorageReference,
        artifact.IsReplayable,
        artifact.Metadata);

    public static ExecutionSessionTimelineDto ToDto(ExecutionSessionTimelineEntry timeline) => new(
        timeline.TimelineId,
        timeline.EventType,
        timeline.Stage.ToString(),
        timeline.OccurredAtUtc,
        timeline.Metadata);
}

public sealed class DefaultExecutionSessionIdFactory : IExecutionSessionIdFactory
{
    public ExecutionSessionId Create(string correlationId, string instrument, string timeframe, DateTimeOffset startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var material = string.Join('|', correlationId.Trim(), instrument.Trim(), timeframe.Trim(), startedAtUtc.ToUniversalTime().ToString("O"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var guidBytes = bytes[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new ExecutionSessionId(new Guid(guidBytes));
    }
}

public static class ExecutionSessionReplayManifestBuilder
{
    private static readonly IReadOnlyDictionary<ExecutionSessionStage, ExecutionSessionArtifactType[]> RequiredArtifacts =
        new Dictionary<ExecutionSessionStage, ExecutionSessionArtifactType[]>
        {
            [ExecutionSessionStage.MarketContext] = [ExecutionSessionArtifactType.MarketContext],
            [ExecutionSessionStage.ExpertDispatch] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.ExpertDispatch],
            [ExecutionSessionStage.ExpertAnalysis] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.ExpertDispatch, ExecutionSessionArtifactType.ExpertAnalysis],
            [ExecutionSessionStage.Consensus] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.ExpertAnalysis, ExecutionSessionArtifactType.Consensus],
            [ExecutionSessionStage.TradingDecision] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.Consensus, ExecutionSessionArtifactType.TradingDecision],
            [ExecutionSessionStage.RiskEvaluation] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.TradingDecision, ExecutionSessionArtifactType.RiskAssessment],
            [ExecutionSessionStage.TradingPlan] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.TradingDecision, ExecutionSessionArtifactType.RiskAssessment, ExecutionSessionArtifactType.TradingPlan],
            [ExecutionSessionStage.TradingWorkspace] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.TradingPlan, ExecutionSessionArtifactType.TradingWorkspace],
            [ExecutionSessionStage.TradingAssistant] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.TradingWorkspace, ExecutionSessionArtifactType.TradingAssistantResponse],
            [ExecutionSessionStage.PaperTrading] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.TradingPlan, ExecutionSessionArtifactType.PaperTradingSimulation],
            [ExecutionSessionStage.Completed] = [ExecutionSessionArtifactType.MarketContext, ExecutionSessionArtifactType.TradingPlan, ExecutionSessionArtifactType.TradingWorkspace]
        };

    public static ExecutionSessionReplayManifestDto Build(ExecutionSession session)
    {
        var ordered = session.ArtifactReferences
            .OrderBy(item => item.Stage)
            .ThenBy(item => item.ArtifactType)
            .ThenBy(item => item.ArtifactId.Value, StringComparer.Ordinal)
            .ToArray();
        var required = RequiredArtifacts.TryGetValue(session.CurrentStage, out var types)
            ? types
            : [ExecutionSessionArtifactType.MarketContext];
        var present = ordered.Select(item => item.ArtifactType).ToHashSet();
        var missing = required.Where(type => !present.Contains(type)).Select(type => type.ToString()).ToArray();
        var reasons = new List<string>();
        if (session.Status != ExecutionSessionStatus.Completed) reasons.Add("Session is not completed.");
        if (missing.Length > 0) reasons.Add("Required artifacts are missing.");
        if (ordered.Any(item => !item.IsReplayable)) reasons.Add("One or more artifacts are marked non-replayable.");
        var canonical = string.Join('\n', new[]
        {
            $"core={session.CoreVersion}",
            $"schema={session.SchemaVersion}",
            $"instrument={session.Instrument}",
            $"timeframe={session.Timeframe}"
        }.Concat(ordered.Select(item => string.Join('|', item.ArtifactType, item.ArtifactId.Value, item.Stage, item.SchemaVersion, item.ContentHash, item.IsReplayable ? "1" : "0"))));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new ExecutionSessionReplayManifestDto(
            session.Id.ToString(),
            null,
            session.CoreVersion,
            ordered.Select(item => item.SchemaVersion).Distinct().OrderBy(value => value).ToArray(),
            ordered.Select(ExecutionSessionMapper.ToDto).ToArray(),
            missing,
            reasons,
            ordered.Select(item => item.CreatedAtUtc).ToArray(),
            fingerprint,
            reasons.Count == 0);
    }
}

internal static class ExecutionSessionOperationSupport
{
    public static async Task<ExecutionSession> LoadAsync(
        IExecutionSessionRepository repository,
        ExecutionSessionId sessionId,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return session ?? throw new ExecutionSessionNotFoundException(sessionId);
    }

    public static ExecutionSessionAuditEntry Audit(
        ExecutionSession session,
        ExecutionSessionAuditEventType eventType,
        DateTimeOffset occurredAtUtc,
        ExecutionSessionStatus? previousStatus,
        ExecutionSessionStage? previousStage,
        string? artifactId = null,
        IReadOnlyDictionary<string, string>? metadata = null) => new(
        Guid.NewGuid(),
        session.Id,
        eventType,
        occurredAtUtc,
        session.CorrelationId,
        ExecutionSessionActorType.System,
        null,
        previousStatus,
        session.Status,
        previousStage,
        session.CurrentStage,
        artifactId,
        metadata ?? new Dictionary<string, string>(),
        session.SchemaVersion);

    public static ExecutionSessionOutboxMessage Outbox(
        ExecutionSession session,
        string eventType,
        DateTimeOffset occurredAtUtc) => new(
        Guid.NewGuid(),
        session.Id,
        eventType,
        JsonSerializer.Serialize(new
        {
            sessionId = session.Id.ToString(),
            correlationId = session.CorrelationId.Value,
            eventType,
            schemaVersion = session.SchemaVersion,
            concurrencyVersion = session.ConcurrencyVersion
        }),
        occurredAtUtc,
        ConcurrencyVersion: session.ConcurrencyVersion);
}

public sealed class StartExecutionSessionHandler(
    IExecutionSessionRepository repository,
    IExecutionSessionUnitOfWork unitOfWork,
    IExecutionSessionClock clock,
    IExecutionSessionIdFactory idFactory,
    ILogger<StartExecutionSessionHandler> logger)
    : IRequestHandler<StartExecutionSessionCommand, ExecutionSessionDto>
{
    public async Task<ExecutionSessionDto> Handle(StartExecutionSessionCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = request.StartedAtUtc ?? clock.UtcNow;
        var id = request.SessionId ?? idFactory.Create(request.CorrelationId, request.Instrument, request.Timeframe, startedAt);
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var session = ExecutionSession.Start(id, new ExecutionCorrelationId(request.CorrelationId), request.Instrument, request.Timeframe,
                request.TriggerType, request.Source, request.CoreVersion, request.ApiVersion, startedAt, request.Metadata,
                request.IdempotencyKeyHash, request.TenantId, request.UserId, request.SchemaVersion);
            var createdAudit = ExecutionSessionOperationSupport.Audit(session, ExecutionSessionAuditEventType.SessionCreated,
                startedAt, null, null);
            session.Begin(startedAt);
            await repository.AddAsync(session, token).ConfigureAwait(false);
            await unitOfWork.AddAuditAsync(createdAudit, token).ConfigureAwait(false);
            await unitOfWork.AddAuditAsync(ExecutionSessionOperationSupport.Audit(session, ExecutionSessionAuditEventType.SessionStarted,
                startedAt, ExecutionSessionStatus.Created, ExecutionSessionStage.MarketContext), token).ConfigureAwait(false);
            await unitOfWork.AddOutboxAsync(ExecutionSessionOperationSupport.Outbox(session, "ExecutionSessionStarted", startedAt), token).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            logger.LogInformation("Execution session created. SessionId={SessionId}, CorrelationId={CorrelationId}, Instrument={Instrument}, Timeframe={Timeframe}",
                session.Id, session.CorrelationId, session.Instrument, session.Timeframe);
            return ExecutionSessionMapper.ToDto(session);
        }, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class LinkExecutionArtifactHandler(
    IExecutionSessionRepository repository,
    IExecutionSessionUnitOfWork unitOfWork,
    IExecutionSessionClock clock,
    ILogger<LinkExecutionArtifactHandler> logger)
    : IRequestHandler<LinkExecutionArtifactCommand, ExecutionSessionDto>
{
    public async Task<ExecutionSessionDto> Handle(LinkExecutionArtifactCommand request, CancellationToken cancellationToken) =>
        await MutateAsync(request.SessionId.Value, request.ExpectedConcurrencyVersion, cancellationToken, async session =>
        {
            var now = clock.UtcNow;
            session.LinkArtifact(request.Artifact, now);
            await unitOfWork.AddAuditAsync(ExecutionSessionOperationSupport.Audit(session, ExecutionSessionAuditEventType.ArtifactLinked,
                now, ExecutionSessionStatus.Running, session.CurrentStage, request.Artifact.ArtifactId.Value), cancellationToken);
            await unitOfWork.AddOutboxAsync(ExecutionSessionOperationSupport.Outbox(session, "ExecutionArtifactLinked", now), cancellationToken);
            logger.LogInformation("Execution artifact linked. SessionId={SessionId}, ArtifactType={ArtifactType}, ArtifactId={ArtifactId}",
                session.Id, request.Artifact.ArtifactType, request.Artifact.ArtifactId);
        }).ConfigureAwait(false);

    private async Task<ExecutionSessionDto> MutateAsync(Guid id, long expected, CancellationToken token, Func<ExecutionSession, Task> action)
    {
        return await unitOfWork.ExecuteAsync(async innerToken =>
        {
            var session = await ExecutionSessionOperationSupport.LoadAsync(repository, new ExecutionSessionId(id), innerToken);
            await action(session).ConfigureAwait(false);
            await repository.UpdateAsync(session, expected, innerToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(innerToken).ConfigureAwait(false);
            return ExecutionSessionMapper.ToDto(session);
        }, token).ConfigureAwait(false);
    }
}

public sealed class AdvanceExecutionStageHandler(
    IExecutionSessionRepository repository,
    IExecutionSessionUnitOfWork unitOfWork,
    IExecutionSessionClock clock,
    ILogger<AdvanceExecutionStageHandler> logger)
    : IRequestHandler<AdvanceExecutionStageCommand, ExecutionSessionDto>
{
    public Task<ExecutionSessionDto> Handle(AdvanceExecutionStageCommand request, CancellationToken cancellationToken) => MutateAsync(request, cancellationToken);

    private async Task<ExecutionSessionDto> MutateAsync(AdvanceExecutionStageCommand request, CancellationToken token) =>
        await unitOfWork.ExecuteAsync(async innerToken =>
        {
            var session = await ExecutionSessionOperationSupport.LoadAsync(repository, request.SessionId, innerToken);
            var previous = session.CurrentStage;
            var now = request.OccurredAtUtc ?? clock.UtcNow;
            session.AdvanceStage(request.TargetStage, now, request.Metadata);
            await unitOfWork.AddAuditAsync(ExecutionSessionOperationSupport.Audit(session, ExecutionSessionAuditEventType.StageAdvanced,
                now, session.Status, previous, metadata: request.Metadata), innerToken);
            await unitOfWork.AddOutboxAsync(ExecutionSessionOperationSupport.Outbox(session, "ExecutionStageAdvanced", now), innerToken);
            await repository.UpdateAsync(session, request.ExpectedConcurrencyVersion, innerToken);
            await unitOfWork.SaveChangesAsync(innerToken);
            logger.LogInformation("Execution session stage advanced. SessionId={SessionId}, Stage={Stage}", session.Id, session.CurrentStage);
            return ExecutionSessionMapper.ToDto(session);
        }, token).ConfigureAwait(false);
}

public sealed class CompleteExecutionSessionHandler(
    IExecutionSessionRepository repository,
    IExecutionSessionUnitOfWork unitOfWork,
    IExecutionSessionClock clock,
    ILogger<CompleteExecutionSessionHandler> logger)
    : IRequestHandler<CompleteExecutionSessionCommand, ExecutionSessionDto>
{
    public async Task<ExecutionSessionDto> Handle(CompleteExecutionSessionCommand request, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteAsync(async token =>
        {
            var session = await ExecutionSessionOperationSupport.LoadAsync(repository, request.SessionId, token);
            var previous = session.Status;
            var now = request.CompletedAtUtc ?? clock.UtcNow;
            session.Complete(now);
            await unitOfWork.AddAuditAsync(ExecutionSessionOperationSupport.Audit(session, ExecutionSessionAuditEventType.SessionCompleted,
                now, previous, ExecutionSessionStage.Completed), token);
            await unitOfWork.AddOutboxAsync(ExecutionSessionOperationSupport.Outbox(session, "ExecutionSessionCompleted", now), token);
            await repository.UpdateAsync(session, request.ExpectedConcurrencyVersion, token);
            await unitOfWork.SaveChangesAsync(token);
            logger.LogInformation("Execution session completed. SessionId={SessionId}, CorrelationId={CorrelationId}", session.Id, session.CorrelationId);
            return ExecutionSessionMapper.ToDto(session);
        }, cancellationToken).ConfigureAwait(false);
}

public sealed class FailExecutionSessionHandler(
    IExecutionSessionRepository repository,
    IExecutionSessionUnitOfWork unitOfWork,
    IExecutionSessionClock clock,
    ILogger<FailExecutionSessionHandler> logger)
    : IRequestHandler<FailExecutionSessionCommand, ExecutionSessionDto>
{
    public async Task<ExecutionSessionDto> Handle(FailExecutionSessionCommand request, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteAsync(async token =>
        {
            var session = await ExecutionSessionOperationSupport.LoadAsync(repository, request.SessionId, token);
            var previous = session.Status;
            var now = request.OccurredAtUtc ?? clock.UtcNow;
            session.Fail(request.Failure, now);
            await unitOfWork.AddAuditAsync(ExecutionSessionOperationSupport.Audit(session, ExecutionSessionAuditEventType.SessionFailed,
                now, previous, session.CurrentStage, metadata: new Dictionary<string, string> { ["failureCode"] = request.Failure.Code }), token);
            await unitOfWork.AddOutboxAsync(ExecutionSessionOperationSupport.Outbox(session, "ExecutionSessionFailed", now), token);
            await repository.UpdateAsync(session, request.ExpectedConcurrencyVersion, token);
            await unitOfWork.SaveChangesAsync(token);
            logger.LogWarning("Execution session failed. SessionId={SessionId}, FailureCode={FailureCode}", session.Id, request.Failure.Code);
            return ExecutionSessionMapper.ToDto(session);
        }, cancellationToken).ConfigureAwait(false);
}

public sealed class CancelExecutionSessionHandler(
    IExecutionSessionRepository repository,
    IExecutionSessionUnitOfWork unitOfWork,
    IExecutionSessionClock clock,
    ILogger<CancelExecutionSessionHandler> logger)
    : IRequestHandler<CancelExecutionSessionCommand, ExecutionSessionDto>
{
    public async Task<ExecutionSessionDto> Handle(CancelExecutionSessionCommand request, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteAsync(async token =>
        {
            var session = await ExecutionSessionOperationSupport.LoadAsync(repository, request.SessionId, token);
            var previous = session.Status;
            var now = request.OccurredAtUtc ?? clock.UtcNow;
            session.Cancel(now);
            await unitOfWork.AddAuditAsync(ExecutionSessionOperationSupport.Audit(session, ExecutionSessionAuditEventType.SessionCancelled,
                now, previous, session.CurrentStage), token);
            await unitOfWork.AddOutboxAsync(ExecutionSessionOperationSupport.Outbox(session, "ExecutionSessionCancelled", now), token);
            await repository.UpdateAsync(session, request.ExpectedConcurrencyVersion, token);
            await unitOfWork.SaveChangesAsync(token);
            logger.LogInformation("Execution session cancelled. SessionId={SessionId}", session.Id);
            return ExecutionSessionMapper.ToDto(session);
        }, cancellationToken).ConfigureAwait(false);
}
