using System.Security.Cryptography;
using TradeMind.Api.Contracts.ExecutionSessions;
using TradeMind.Api.Contracts.Common;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Application.Commands;
using TradeMind.ExecutionSessions.Application.DTOs;
using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.Api.Application;

public interface IExecutionSessionApiApplication
{
    Task<ExecutionSessionApiResource> StartAsync(CreateExecutionSessionApiRequest request, CancellationToken cancellationToken);
    Task<ExecutionSessionApiResource> LinkArtifactAsync(Guid sessionId, LinkExecutionArtifactApiRequest request, CancellationToken cancellationToken);
    Task<ExecutionSessionApiResource> AdvanceStageAsync(Guid sessionId, AdvanceExecutionStageApiRequest request, CancellationToken cancellationToken);
    Task<ExecutionSessionApiResource> CompleteAsync(Guid sessionId, ExecutionSessionMutationApiRequest request, CancellationToken cancellationToken);
    Task<ExecutionSessionApiResource> FailAsync(Guid sessionId, FailExecutionSessionApiRequest request, CancellationToken cancellationToken);
    Task<ExecutionSessionApiResource> CancelAsync(Guid sessionId, ExecutionSessionMutationApiRequest request, CancellationToken cancellationToken);
    Task<ExecutionSessionApiResource> GetAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionSessionApiTimeline>> GetTimelineAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchApiQuery query, CancellationToken cancellationToken);
    Task<ExecutionSessionReplayManifestApiResponse> GetReplayManifestAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<ExecutionSessionApiResource> LinkPipelineArtifactAsync(Guid sessionId, string stage, string artifactId, byte[] responseBody, CancellationToken cancellationToken);
}

public sealed class ExecutionSessionApiApplication(IExecutionSessionService service, TimeProvider timeProvider) : IExecutionSessionApiApplication
{
    public async Task<ExecutionSessionApiResource> StartAsync(CreateExecutionSessionApiRequest request, CancellationToken cancellationToken)
    {
        ValidateSchema(request.SchemaVersion);
        var trigger = ParseEnum<ExecutionSessionTriggerType>(request.TriggerType, nameof(request.TriggerType));
        var result = await service.StartAsync(new StartExecutionSessionCommand(
            request.SessionId is null ? null : new ExecutionSessionId(request.SessionId.Value), request.CorrelationId, request.Instrument,
            request.Timeframe, trigger, request.Source, request.CoreVersion, request.ApiVersion, request.StartedAtUtc,
            request.Metadata, request.IdempotencyKeyHash, request.TenantId, request.UserId, request.SchemaVersion), cancellationToken).ConfigureAwait(false);
        return Map(result);
    }

    public async Task<ExecutionSessionApiResource> LinkArtifactAsync(Guid sessionId, LinkExecutionArtifactApiRequest request, CancellationToken cancellationToken)
    {
        ValidateSchema(request.SchemaVersion);
        var artifact = new ExecutionSessionArtifactReference(ParseEnum<ExecutionSessionArtifactType>(request.ArtifactType, nameof(request.ArtifactType)),
            new ExecutionSessionArtifactId(request.ArtifactId), ParseEnum<ExecutionSessionStage>(request.Stage, nameof(request.Stage)),
            request.ArtifactSchemaVersion, request.CreatedAtUtc, request.ContentHash, request.StorageReference, request.IsReplayable, request.Metadata);
        return Map(await service.LinkArtifactAsync(new LinkExecutionArtifactCommand(new ExecutionSessionId(sessionId), artifact, request.ExpectedConcurrencyVersion), cancellationToken).ConfigureAwait(false));
    }

    public async Task<ExecutionSessionApiResource> AdvanceStageAsync(Guid sessionId, AdvanceExecutionStageApiRequest request, CancellationToken cancellationToken)
    {
        ValidateSchema(request.SchemaVersion);
        return Map(await service.AdvanceStageAsync(new AdvanceExecutionStageCommand(new ExecutionSessionId(sessionId),
            ParseEnum<ExecutionSessionStage>(request.TargetStage, nameof(request.TargetStage)), request.OccurredAtUtc, request.Metadata,
            request.ExpectedConcurrencyVersion), cancellationToken).ConfigureAwait(false));
    }

    public async Task<ExecutionSessionApiResource> CompleteAsync(Guid sessionId, ExecutionSessionMutationApiRequest request, CancellationToken cancellationToken)
    {
        ValidateSchema(request.SchemaVersion);
        return Map(await service.CompleteAsync(new CompleteExecutionSessionCommand(new ExecutionSessionId(sessionId), request.OccurredAtUtc, request.ExpectedConcurrencyVersion), cancellationToken).ConfigureAwait(false));
    }

    public async Task<ExecutionSessionApiResource> FailAsync(Guid sessionId, FailExecutionSessionApiRequest request, CancellationToken cancellationToken)
    {
        ValidateSchema(request.SchemaVersion);
        return Map(await service.FailAsync(new FailExecutionSessionCommand(new ExecutionSessionId(sessionId), new ExecutionSessionFailure(request.Code, request.Message, request.Details),
            request.OccurredAtUtc, request.ExpectedConcurrencyVersion), cancellationToken).ConfigureAwait(false));
    }

    public async Task<ExecutionSessionApiResource> CancelAsync(Guid sessionId, ExecutionSessionMutationApiRequest request, CancellationToken cancellationToken)
    {
        ValidateSchema(request.SchemaVersion);
        return Map(await service.CancelAsync(new CancelExecutionSessionCommand(new ExecutionSessionId(sessionId), request.OccurredAtUtc, request.ExpectedConcurrencyVersion), cancellationToken).ConfigureAwait(false));
    }

    public async Task<ExecutionSessionApiResource> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Map(await service.GetAsync(new ExecutionSessionId(sessionId), cancellationToken).ConfigureAwait(false));

    public async Task<IReadOnlyList<ExecutionSessionApiTimeline>> GetTimelineAsync(Guid sessionId, CancellationToken cancellationToken) =>
        (await service.GetTimelineAsync(new ExecutionSessionId(sessionId), cancellationToken).ConfigureAwait(false)).Select(item => new ExecutionSessionApiTimeline(
            item.TimelineId, item.EventType, item.Stage, item.OccurredAtUtc, item.Metadata)).ToArray();

    public Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchApiQuery query, CancellationToken cancellationToken)
    {
        var filter = new ExecutionSessionSearchFilter(
            ParseOptionalEnum<ExecutionSessionStatus>(query.Status, nameof(query.Status)), query.Instrument, query.StartedFromUtc, query.StartedToUtc,
            ParseOptionalEnum<ExecutionSessionStage>(query.Stage, nameof(query.Stage)), query.CorrelationId, query.ArtifactId,
            ParseOptionalEnum<ExecutionSessionTriggerType>(query.TriggerType, nameof(query.TriggerType)), query.Page, query.PageSize);
        return service.SearchAsync(filter, cancellationToken);
    }

    public async Task<ExecutionSessionReplayManifestApiResponse> GetReplayManifestAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await service.GetReplayManifestAsync(new ExecutionSessionId(sessionId), cancellationToken).ConfigureAwait(false);
        return new(result.SessionId, result.SourceSessionId, result.CoreVersion, result.SchemaVersions,
            result.OrderedArtifacts.Select(Map).ToArray(), result.MissingArtifacts, result.NonReplayableReasons,
            result.OriginalTimestamps, result.DeterministicFingerprint, result.IsReplayable,
            new ApiResponseMetadata("", "", timeProvider.GetUtcNow(), "1.0", [], [], []));
    }

    public async Task<ExecutionSessionApiResource> LinkPipelineArtifactAsync(Guid sessionId, string stage, string artifactId, byte[] responseBody, CancellationToken cancellationToken)
    {
        var current = await service.GetAsync(new ExecutionSessionId(sessionId), cancellationToken).ConfigureAwait(false);
        var parsedStage = ParseEnum<ExecutionSessionStage>(stage, nameof(stage));
        var hash = Convert.ToHexString(SHA256.HashData(responseBody)).ToLowerInvariant();
        var type = parsedStage switch
        {
            ExecutionSessionStage.MarketContext => ExecutionSessionArtifactType.MarketContext,
            ExecutionSessionStage.ExpertDispatch => ExecutionSessionArtifactType.ExpertDispatch,
            ExecutionSessionStage.ExpertAnalysis => ExecutionSessionArtifactType.ExpertAnalysis,
            ExecutionSessionStage.Consensus => ExecutionSessionArtifactType.Consensus,
            ExecutionSessionStage.TradingDecision => ExecutionSessionArtifactType.TradingDecision,
            ExecutionSessionStage.RiskEvaluation => ExecutionSessionArtifactType.RiskAssessment,
            ExecutionSessionStage.TradingPlan => ExecutionSessionArtifactType.TradingPlan,
            ExecutionSessionStage.TradingWorkspace => ExecutionSessionArtifactType.TradingWorkspace,
            ExecutionSessionStage.TradingAssistant => ExecutionSessionArtifactType.TradingAssistantResponse,
            ExecutionSessionStage.PaperTrading => ExecutionSessionArtifactType.PaperTradingSimulation,
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
        return await LinkArtifactAsync(sessionId, new LinkExecutionArtifactApiRequest(1, type.ToString(), artifactId, parsedStage.ToString(), 1,
            timeProvider.GetUtcNow(), hash, null, true, new Dictionary<string, string> { ["source"] = "api-pipeline" }, current.ConcurrencyVersion), cancellationToken).ConfigureAwait(false);
    }

    private static ExecutionSessionApiResource Map(ExecutionSessionDto result) => new(result.Id, result.CorrelationId, result.IdempotencyKeyHash,
        result.TenantId, result.UserId, result.Instrument, result.Timeframe, result.StartedAtUtc, result.UpdatedAtUtc, result.CompletedAtUtc,
        result.Status, result.CurrentStage, result.SchemaVersion, result.CoreVersion, result.ApiVersion, result.TriggerType, result.Source,
        result.Failure is null ? null : new(result.Failure.Code, result.Failure.Message, result.Failure.Details), result.Metadata,
        result.ArtifactReferences.Select(Map).ToArray(), result.TimelineEntries.Select(item => new ExecutionSessionApiTimeline(item.TimelineId, item.EventType,
            item.Stage, item.OccurredAtUtc, item.Metadata)).ToArray(), result.ConcurrencyVersion);

    private static ExecutionSessionApiArtifact Map(ExecutionSessionArtifactDto artifact) => new(artifact.ArtifactType, artifact.ArtifactId, artifact.Stage,
        artifact.SchemaVersion, artifact.CreatedAtUtc, artifact.ContentHash, artifact.StorageReference, artifact.IsReplayable, artifact.Metadata);

    private static void ValidateSchema(int schemaVersion)
    {
        if (schemaVersion != 1) throw new ApiRequestValidationException([new("UNSUPPORTED_SCHEMA_VERSION", "Schema version 1 is required.", "schemaVersion")]);
    }

    private static T ParseEnum<T>(string value, string field) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : throw new ApiRequestValidationException([new("INVALID_VALUE", $"{field} is invalid.", field)]);

    private static T? ParseOptionalEnum<T>(string? value, string field) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : ParseEnum<T>(value, field);
}

public sealed class ExecutionSessionNotConfiguredApiApplication : IExecutionSessionApiApplication
{
    private static Task<T> NotConfigured<T>() => Task.FromException<T>(new ExecutionSessionNotConfiguredException());
    public Task<ExecutionSessionApiResource> StartAsync(CreateExecutionSessionApiRequest request, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
    public Task<ExecutionSessionApiResource> LinkArtifactAsync(Guid sessionId, LinkExecutionArtifactApiRequest request, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
    public Task<ExecutionSessionApiResource> AdvanceStageAsync(Guid sessionId, AdvanceExecutionStageApiRequest request, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
    public Task<ExecutionSessionApiResource> CompleteAsync(Guid sessionId, ExecutionSessionMutationApiRequest request, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
    public Task<ExecutionSessionApiResource> FailAsync(Guid sessionId, FailExecutionSessionApiRequest request, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
    public Task<ExecutionSessionApiResource> CancelAsync(Guid sessionId, ExecutionSessionMutationApiRequest request, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
    public Task<ExecutionSessionApiResource> GetAsync(Guid sessionId, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
    public Task<IReadOnlyList<ExecutionSessionApiTimeline>> GetTimelineAsync(Guid sessionId, CancellationToken cancellationToken) => NotConfigured<IReadOnlyList<ExecutionSessionApiTimeline>>();
    public Task<ExecutionSessionSearchPage> SearchAsync(ExecutionSessionSearchApiQuery query, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionSearchPage>();
    public Task<ExecutionSessionReplayManifestApiResponse> GetReplayManifestAsync(Guid sessionId, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionReplayManifestApiResponse>();
    public Task<ExecutionSessionApiResource> LinkPipelineArtifactAsync(Guid sessionId, string stage, string artifactId, byte[] responseBody, CancellationToken cancellationToken) => NotConfigured<ExecutionSessionApiResource>();
}
