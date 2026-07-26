using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.ExpertAgents.Domain;

public sealed record AgentExecutionRequest
{
    public const int CurrentVersion = 1;
    public const int MaximumQuestionLength = 2_000;
    public const int MaximumObjectiveLength = 500;
    public const int MaximumLanguageLength = 32;
    public const int MaximumMetadataEntries = 20;
    public const int MaximumMetadataValueLength = 256;

    public AgentExecutionRequest(
        AgentRunId agentRunId,
        AgentId agentId,
        MarketContextId marketContextId,
        string userId,
        string sessionId,
        string objective,
        AgentAnalysisMode analysisMode = AgentAnalysisMode.StandardAnalysis,
        AgentAnalysisDepth analysisDepth = AgentAnalysisDepth.Standard,
        string language = "en",
        string? question = null,
        TimeSpan? requestedTimeout = null,
        AgentOutputOptions? outputOptions = null,
        string? correlationId = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        int version = CurrentVersion,
        string? tenantId = null,
        IReadOnlyCollection<string>? permissions = null,
        AgentVersionSelection versionSelection = AgentVersionSelection.LatestStable,
        AgentVersion? exactVersion = null)
    {
        ArgumentNullException.ThrowIfNull(agentRunId);
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (versionSelection == AgentVersionSelection.Exact && exactVersion is null)
        {
            throw new ArgumentException("Exact version selection requires an exact version.", nameof(exactVersion));
        }

        if (versionSelection != AgentVersionSelection.Exact && exactVersion is not null)
        {
            throw new ArgumentException("ExactVersion can only be used with exact version selection.", nameof(exactVersion));
        }

        var normalizedQuestion = NormalizeOptional(question);
        if (normalizedQuestion?.Length > MaximumQuestionLength)
        {
            throw new ArgumentException($"Question cannot exceed {MaximumQuestionLength} characters.", nameof(question));
        }

        var normalizedObjective = objective.Trim();
        if (normalizedObjective.Length > MaximumObjectiveLength)
        {
            throw new ArgumentException($"Objective cannot exceed {MaximumObjectiveLength} characters.", nameof(objective));
        }

        var normalizedLanguage = language.Trim();
        if (normalizedLanguage.Length > MaximumLanguageLength)
        {
            throw new ArgumentException($"Language cannot exceed {MaximumLanguageLength} characters.", nameof(language));
        }

        if (requestedTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedTimeout));
        }

        var normalizedMetadata = AgentCollections.CopyDictionary(metadata, StringComparer.OrdinalIgnoreCase);
        if (normalizedMetadata.Count > MaximumMetadataEntries
            || normalizedMetadata.Any(pair => pair.Key.Length > MaximumMetadataValueLength || pair.Value.Length > MaximumMetadataValueLength))
        {
            throw new ArgumentException("Request metadata exceeds the configured safe limits.", nameof(metadata));
        }

        Version = version;
        AgentRunId = agentRunId;
        AgentId = agentId;
        MarketContextId = marketContextId;
        UserId = userId.Trim();
        SessionId = sessionId.Trim();
        Objective = normalizedObjective;
        AnalysisMode = analysisMode;
        AnalysisDepth = analysisDepth;
        Language = normalizedLanguage;
        Question = normalizedQuestion;
        RequestedTimeout = requestedTimeout;
        OutputOptions = outputOptions ?? new AgentOutputOptions();
        CorrelationId = NormalizeOptional(correlationId) ?? Guid.NewGuid().ToString("N");
        Metadata = normalizedMetadata;
        TenantId = NormalizeOptional(tenantId);
        Permissions = AgentCollections.CopyStrings(permissions, StringComparer.OrdinalIgnoreCase);
        VersionSelection = versionSelection;
        ExactVersion = exactVersion;
    }

    public int Version { get; }
    public AgentRunId AgentRunId { get; }
    public AgentId AgentId { get; }
    public MarketContextId MarketContextId { get; }
    public string UserId { get; }
    public string SessionId { get; }
    public string? Question { get; }
    public string Objective { get; }
    public AgentAnalysisMode AnalysisMode { get; }
    public AgentAnalysisDepth AnalysisDepth { get; }
    public string Language { get; }
    public TimeSpan? RequestedTimeout { get; }
    public AgentOutputOptions OutputOptions { get; }
    public string CorrelationId { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public string? TenantId { get; }
    public IReadOnlyList<string> Permissions { get; }
    public AgentVersionSelection VersionSelection { get; }
    public AgentVersion? ExactVersion { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AgentAnalysisResult
{
    public const int CurrentSchemaVersion = 1;

    public AgentAnalysisResult(
        AgentRunId agentRunId,
        AgentId agentId,
        AgentVersion agentVersion,
        MarketContextId marketContextId,
        AgentAnalysisStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        AgentDirectionalBias directionalBias,
        AgentConfidence confidence,
        string? summary = null,
        IReadOnlyCollection<AgentObservation>? observations = null,
        IReadOnlyCollection<AgentEvidence>? evidence = null,
        IReadOnlyCollection<AgentMarketLevel>? marketLevels = null,
        IReadOnlyCollection<AgentScenario>? scenarios = null,
        IReadOnlyCollection<string>? invalidations = null,
        IReadOnlyCollection<string>? risks = null,
        IReadOnlyCollection<AgentWarning>? warnings = null,
        IReadOnlyCollection<string>? limitations = null,
        IReadOnlyCollection<AgentError>? errors = null,
        int schemaVersion = CurrentSchemaVersion,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(agentRunId);
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(agentVersion);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(confidence);
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        AgentRunId = agentRunId;
        AgentId = agentId;
        AgentVersion = agentVersion;
        MarketContextId = marketContextId;
        Status = status;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        DirectionalBias = directionalBias;
        Confidence = confidence;
        Summary = NormalizeOptional(summary);
        Observations = AgentCollections.CopyList(observations);
        Evidence = AgentCollections.CopyList(evidence);
        MarketLevels = AgentCollections.CopyList(marketLevels);
        Scenarios = AgentCollections.CopyList(scenarios);
        Invalidations = AgentCollections.CopyStrings(invalidations);
        Risks = AgentCollections.CopyStrings(risks);
        Warnings = AgentCollections.CopyList(warnings);
        Limitations = AgentCollections.CopyStrings(limitations);
        Errors = AgentCollections.CopyList(errors);
        SchemaVersion = schemaVersion;
        Metadata = AgentCollections.CopyDictionary(metadata, StringComparer.OrdinalIgnoreCase);
    }

    public AgentRunId AgentRunId { get; }
    public AgentId AgentId { get; }
    public AgentVersion AgentVersion { get; }
    public MarketContextId MarketContextId { get; }
    public AgentAnalysisStatus Status { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
    public AgentDirectionalBias DirectionalBias { get; }
    public AgentConfidence Confidence { get; }
    public string? Summary { get; }
    public IReadOnlyList<AgentObservation> Observations { get; }
    public IReadOnlyList<AgentEvidence> Evidence { get; }
    public IReadOnlyList<AgentMarketLevel> MarketLevels { get; }
    public IReadOnlyList<AgentScenario> Scenarios { get; }
    public IReadOnlyList<string> Invalidations { get; }
    public IReadOnlyList<string> Risks { get; }
    public IReadOnlyList<AgentWarning> Warnings { get; }
    public IReadOnlyList<string> Limitations { get; }
    public IReadOnlyList<AgentError> Errors { get; }
    public int SchemaVersion { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    public static AgentAnalysisResult Failure(
        AgentExecutionRequest request,
        AgentVersion agentVersion,
        AgentAnalysisStatus status,
        AgentError error,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc) =>
        new(
            request.AgentRunId,
            request.AgentId,
            agentVersion,
            request.MarketContextId,
            status,
            startedAtUtc,
            completedAtUtc,
            AgentDirectionalBias.InsufficientData,
            AgentConfidence.Unavailable,
            errors: [error]);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
