using System.Collections.ObjectModel;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Dispatch.Domain;

public sealed record DispatchId
{
    public DispatchId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Dispatch id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static DispatchId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum AnalysisIntentKind
{
    Unknown,
    MarketOverview,
    TechnicalAnalysis,
    RiskAssessment,
    EducationalExplanation,
    Validation,
    MacroAnalysis,
    Custom
}

public sealed record AnalysisIntent
{
    public const int MaximumKeyLength = 64;

    public AnalysisIntent(
        string key,
        AnalysisIntentKind kind = AnalysisIntentKind.Custom,
        IReadOnlyCollection<string>? keywords = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumKeyLength || !IsLowercaseKebabCase(normalized))
        {
            throw new ArgumentException("Intent keys must use lowercase kebab-case and be 64 characters or fewer.", nameof(key));
        }

        if (normalized[0] == '-' || normalized[^1] == '-' || normalized.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Intent keys must use lowercase kebab-case.", nameof(key));
        }

        Key = normalized;
        Kind = kind;
        Keywords = CopyStrings(keywords);
    }

    public string Key { get; }
    public AnalysisIntentKind Kind { get; }
    public IReadOnlyList<string> Keywords { get; }

    public static AnalysisIntent Unknown { get; } = new("unknown", AnalysisIntentKind.Unknown);

    public override string ToString() => Key;

    private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());

    private static bool IsLowercaseKebabCase(string value) =>
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
        && value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal);
}

public enum AgentDispatchFallbackMode
{
    None,
    UnknownIntent,
    GenericSpecialty
}

public enum AgentDispatchRequirement
{
    Required,
    Preferred,
    Optional
}

public enum DispatchRejectionCode
{
    InvalidRequest,
    UnknownIntent,
    AmbiguousIntent,
    AgentNotFound,
    VersionNotFound,
    Disabled,
    ExperimentalExcluded,
    ExplicitlyExcluded,
    Unauthorized,
    Incompatible,
    ContextStale,
    ContextQualityInsufficient,
    BudgetExceeded,
    RequiredBudgetExceeded,
    MaximumAgentsExceeded,
    MinimumAgentsNotReached,
    NoEligibleAgent
}

public enum DispatchExecutionStatus
{
    Succeeded,
    PartiallySucceeded,
    Failed,
    TimedOut,
    Cancelled,
    Rejected
}

public sealed record AgentDispatchBudget
{
    public AgentDispatchBudget(decimal maximumUnits, int maximumAgents)
    {
        if (maximumUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumUnits));
        }

        if (maximumAgents <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAgents));
        }

        MaximumUnits = maximumUnits;
        MaximumAgents = maximumAgents;
    }

    public decimal MaximumUnits { get; }
    public int MaximumAgents { get; }

    public static AgentDispatchBudget Default { get; } = new(100, 8);
}

public sealed record AgentCost
{
    public AgentCost(decimal units, TimeSpan estimatedDuration)
    {
        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        if (estimatedDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDuration));
        }

        Units = units;
        EstimatedDuration = estimatedDuration;
    }

    public decimal Units { get; }
    public TimeSpan EstimatedDuration { get; }
}

public sealed record AgentRelevanceFactor
{
    public AgentRelevanceFactor(string name, int points, string explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        if (points == 0)
        {
            throw new ArgumentException("A relevance factor must have a non-zero contribution.", nameof(points));
        }

        Name = name.Trim();
        Points = points;
        Explanation = explanation.Trim();
    }

    public string Name { get; }
    public int Points { get; }
    public string Explanation { get; }
}

public sealed record AgentRelevanceScore
{
    public AgentRelevanceScore(int score, IReadOnlyCollection<AgentRelevanceFactor>? factors = null)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        Score = score;
        Factors = Array.AsReadOnly((factors ?? [])
            .Where(factor => factor is not null)
            .ToArray());
    }

    public int Score { get; }
    public IReadOnlyList<AgentRelevanceFactor> Factors { get; }
}

public sealed record AnalysisIntentClassificationResult
{
    public AnalysisIntentClassificationResult(
        AnalysisIntent intent,
        int confidence,
        bool isAmbiguous,
        bool needsClarification,
        bool usedFallback,
        IReadOnlyCollection<string>? reasons = null,
        IReadOnlyCollection<AnalysisIntent>? alternatives = null)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (confidence is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        Intent = intent;
        Confidence = confidence;
        IsAmbiguous = isAmbiguous;
        NeedsClarification = needsClarification;
        UsedFallback = usedFallback;
        Reasons = CopyStrings(reasons);
        Alternatives = Array.AsReadOnly((alternatives ?? []).ToArray());
    }

    public AnalysisIntent Intent { get; }
    public int Confidence { get; }
    public bool IsAmbiguous { get; }
    public bool NeedsClarification { get; }
    public bool UsedFallback { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyList<AnalysisIntent> Alternatives { get; }

    private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
}

public sealed record AgentDispatchRejection
{
    public AgentDispatchRejection(
        DispatchRejectionCode code,
        string message,
        AgentId? agentId = null,
        AgentVersion? version = null,
        AgentDispatchRequirement requirement = AgentDispatchRequirement.Optional,
        bool explicitRequest = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message.Trim();
        AgentId = agentId;
        Version = version;
        Requirement = requirement;
        ExplicitRequest = explicitRequest;
    }

    public DispatchRejectionCode Code { get; }
    public string Message { get; }
    public AgentId? AgentId { get; }
    public AgentVersion? Version { get; }
    public AgentDispatchRequirement Requirement { get; }
    public bool ExplicitRequest { get; }
}

public sealed record AgentDispatchCandidate
{
    public AgentDispatchCandidate(
        AgentDescriptor descriptor,
        AgentDispatchRequirement requirement,
        AgentRelevanceScore relevance,
        AgentCost cost,
        AgentExecutionRequest executionRequest,
        bool explicitlyIncluded)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(relevance);
        ArgumentNullException.ThrowIfNull(cost);
        ArgumentNullException.ThrowIfNull(executionRequest);
        if (executionRequest.AgentId != descriptor.Id
            || executionRequest.ExactVersion != descriptor.Version
            || executionRequest.VersionSelection != AgentVersionSelection.Exact)
        {
            throw new ArgumentException("Candidate execution must target its selected agent version.", nameof(executionRequest));
        }

        Descriptor = descriptor;
        Requirement = requirement;
        Relevance = relevance;
        Cost = cost;
        ExecutionRequest = executionRequest;
        ExplicitlyIncluded = explicitlyIncluded;
    }

    public AgentDescriptor Descriptor { get; }
    public AgentDispatchRequirement Requirement { get; }
    public AgentRelevanceScore Relevance { get; }
    public AgentCost Cost { get; }
    public AgentExecutionRequest ExecutionRequest { get; }
    public bool ExplicitlyIncluded { get; }
}

public sealed record AgentDispatchGroup
{
    public AgentDispatchGroup(int order, AgentDispatchRequirement requirement, IReadOnlyCollection<AgentDispatchCandidate> candidates)
    {
        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order));
        }

        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("A dispatch group cannot be empty.", nameof(candidates));
        }

        Order = order;
        Requirement = requirement;
        Candidates = Array.AsReadOnly(candidates.ToArray());
    }

    public int Order { get; }
    public AgentDispatchRequirement Requirement { get; }
    public IReadOnlyList<AgentDispatchCandidate> Candidates { get; }
}

public sealed record AgentDispatchPlan
{
    public const int CurrentVersion = 1;

    public AgentDispatchPlan(
        DispatchId dispatchId,
        MarketContextId marketContextId,
        AnalysisIntentClassificationResult classification,
        IReadOnlyCollection<AgentDispatchCandidate> candidates,
        IReadOnlyCollection<AgentDispatchRejection> rejections,
        IReadOnlyCollection<AgentDispatchGroup> executionGroups,
        AgentCost totalCost,
        bool isExecutable,
        DateTimeOffset createdAtUtc,
        IReadOnlyCollection<string>? warnings = null,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(dispatchId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rejections);
        ArgumentNullException.ThrowIfNull(executionGroups);
        ArgumentNullException.ThrowIfNull(totalCost);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        var candidateArray = candidates.ToArray();
        if (candidateArray.GroupBy(candidate => (candidate.Descriptor.Id, candidate.Descriptor.Version)).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A dispatch plan cannot contain duplicate agent versions.", nameof(candidates));
        }

        if (executionGroups.SelectMany(group => group.Candidates).Except(candidateArray).Any())
        {
            throw new ArgumentException("Execution groups must contain selected plan candidates.", nameof(executionGroups));
        }

        DispatchId = dispatchId;
        MarketContextId = marketContextId;
        Classification = classification;
        Candidates = Array.AsReadOnly(candidateArray);
        Rejections = Array.AsReadOnly(rejections.ToArray());
        ExecutionGroups = Array.AsReadOnly(executionGroups.OrderBy(group => group.Order).ToArray());
        TotalCost = totalCost;
        IsExecutable = isExecutable;
        CreatedAtUtc = createdAtUtc;
        Warnings = CopyStrings(warnings);
        Version = version;
    }

    public int Version { get; }
    public DispatchId DispatchId { get; }
    public MarketContextId MarketContextId { get; }
    public AnalysisIntentClassificationResult Classification { get; }
    public IReadOnlyList<AgentDispatchCandidate> Candidates { get; }
    public IReadOnlyList<AgentDispatchRejection> Rejections { get; }
    public IReadOnlyList<AgentDispatchGroup> ExecutionGroups { get; }
    public AgentCost TotalCost { get; }
    public bool IsExecutable { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<string> Warnings { get; }

    private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
}

public sealed record AgentDispatchExecutionResult
{
    public AgentDispatchExecutionResult(
        AgentDispatchPlan plan,
        DispatchExecutionStatus status,
        IReadOnlyCollection<AgentAnalysisResult> results,
        DateTimeOffset completedAtUtc,
        IReadOnlyCollection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(results);
        Plan = plan;
        Status = status;
        Results = Array.AsReadOnly(results.ToArray());
        CompletedAtUtc = completedAtUtc;
        Warnings = Array.AsReadOnly((warnings ?? []).ToArray());
    }

    public AgentDispatchPlan Plan { get; }
    public DispatchExecutionStatus Status { get; }
    public IReadOnlyList<AgentAnalysisResult> Results { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public IReadOnlyList<string> Warnings { get; }
}

public sealed record AgentDispatchRequest
{
    public const int CurrentVersion = 1;
    public const int MaximumObjectiveLength = 500;
    public const int MaximumQuestionLength = 2_000;

    public AgentDispatchRequest(
        DispatchId dispatchId,
        MarketContextId marketContextId,
        string userId,
        string sessionId,
        string objective,
        string? question = null,
        AnalysisIntent? explicitIntent = null,
        AgentSpecialty? specialty = null,
        IReadOnlyCollection<AgentId>? includedAgentIds = null,
        IReadOnlyCollection<AgentId>? excludedAgentIds = null,
        IReadOnlyDictionary<AgentId, AgentDispatchRequirement>? requirements = null,
        IReadOnlyDictionary<AgentId, AgentVersion>? exactVersions = null,
        AgentVersionSelection versionSelection = AgentVersionSelection.LatestStable,
        AgentDispatchFallbackMode fallbackMode = AgentDispatchFallbackMode.None,
        AgentDispatchBudget? budget = null,
        int minimumAgents = 1,
        int maximumAgents = 8,
        TimeSpan? globalTimeout = null,
        TimeSpan? perAgentTimeout = null,
        AgentAnalysisMode analysisMode = AgentAnalysisMode.StandardAnalysis,
        AgentAnalysisDepth analysisDepth = AgentAnalysisDepth.Standard,
        string language = "en",
        string? tenantId = null,
        IReadOnlyCollection<string>? permissions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool allowExperimentalAgents = false,
        AgentOutputOptions? outputOptions = null,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(dispatchId);
        ArgumentNullException.ThrowIfNull(marketContextId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        var normalizedObjective = objective.Trim();
        if (normalizedObjective.Length > MaximumObjectiveLength)
        {
            throw new ArgumentException($"Objective cannot exceed {MaximumObjectiveLength} characters.", nameof(objective));
        }

        var normalizedQuestion = NormalizeOptional(question);
        if (normalizedQuestion?.Length > MaximumQuestionLength)
        {
            throw new ArgumentException($"Question cannot exceed {MaximumQuestionLength} characters.", nameof(question));
        }

        if (minimumAgents <= 0 || maximumAgents <= 0 || minimumAgents > maximumAgents)
        {
            throw new ArgumentException("Dispatch agent limits must be positive and minimumAgents cannot exceed maximumAgents.", nameof(minimumAgents));
        }

        if (globalTimeout is { } global && global <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(globalTimeout));
        }

        if (perAgentTimeout is { } individual && individual <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(perAgentTimeout));
        }

        IncludedAgentIds = CopyAgents(includedAgentIds);
        ExcludedAgentIds = CopyAgents(excludedAgentIds);
        if (IncludedAgentIds.Intersect(ExcludedAgentIds).Any())
        {
            throw new ArgumentException("An agent cannot be included and excluded in the same dispatch.", nameof(excludedAgentIds));
        }

        Requirements = CopyRequirements(requirements);
        ExactVersions = CopyVersions(exactVersions);
        if (versionSelection == AgentVersionSelection.Exact && ExactVersions.Count == 0)
        {
            throw new ArgumentException("Exact version selection requires at least one exact version.", nameof(exactVersions));
        }

        var explicitIds = IncludedAgentIds.Concat(Requirements.Keys).Distinct().ToArray();
        if (ExactVersions.Keys.Any(id => !explicitIds.Contains(id)))
        {
            throw new ArgumentException("Exact versions can only be requested for explicitly included agents.", nameof(exactVersions));
        }

        Version = version;
        DispatchId = dispatchId;
        MarketContextId = marketContextId;
        UserId = userId.Trim();
        SessionId = sessionId.Trim();
        Objective = normalizedObjective;
        Question = normalizedQuestion;
        ExplicitIntent = explicitIntent;
        Specialty = specialty;
        VersionSelection = versionSelection;
        FallbackMode = fallbackMode;
        Budget = budget ?? AgentDispatchBudget.Default;
        MinimumAgents = minimumAgents;
        MaximumAgents = maximumAgents;
        GlobalTimeout = globalTimeout;
        PerAgentTimeout = perAgentTimeout;
        AnalysisMode = analysisMode;
        AnalysisDepth = analysisDepth;
        Language = language.Trim();
        TenantId = NormalizeOptional(tenantId);
        Permissions = CopyStrings(permissions);
        Metadata = CopyDictionary(metadata);
        AllowExperimentalAgents = allowExperimentalAgents;
        OutputOptions = outputOptions ?? new AgentOutputOptions();
    }

    public int Version { get; }
    public DispatchId DispatchId { get; }
    public MarketContextId MarketContextId { get; }
    public string UserId { get; }
    public string SessionId { get; }
    public string Objective { get; }
    public string? Question { get; }
    public AnalysisIntent? ExplicitIntent { get; }
    public AgentSpecialty? Specialty { get; }
    public IReadOnlyList<AgentId> IncludedAgentIds { get; }
    public IReadOnlyList<AgentId> ExcludedAgentIds { get; }
    public IReadOnlyDictionary<AgentId, AgentDispatchRequirement> Requirements { get; }
    public IReadOnlyDictionary<AgentId, AgentVersion> ExactVersions { get; }
    public AgentVersionSelection VersionSelection { get; }
    public AgentDispatchFallbackMode FallbackMode { get; }
    public AgentDispatchBudget Budget { get; }
    public int MinimumAgents { get; }
    public int MaximumAgents { get; }
    public TimeSpan? GlobalTimeout { get; }
    public TimeSpan? PerAgentTimeout { get; }
    public AgentAnalysisMode AnalysisMode { get; }
    public AgentAnalysisDepth AnalysisDepth { get; }
    public string Language { get; }
    public string? TenantId { get; }
    public IReadOnlyList<string> Permissions { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public bool AllowExperimentalAgents { get; }
    public AgentOutputOptions OutputOptions { get; }

    public AgentDispatchRequirement RequirementFor(AgentId id) =>
        Requirements.TryGetValue(id, out var requirement)
            ? requirement
            : IncludedAgentIds.Contains(id)
                ? AgentDispatchRequirement.Required
                : AgentDispatchRequirement.Optional;

    private static IReadOnlyList<AgentId> CopyAgents(IEnumerable<AgentId>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => value is not null)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray());

    private static IReadOnlyDictionary<AgentId, AgentDispatchRequirement> CopyRequirements(
        IReadOnlyDictionary<AgentId, AgentDispatchRequirement>? values)
    {
        var copy = new Dictionary<AgentId, AgentDispatchRequirement>();
        if (values is not null)
        {
            foreach (var pair in values)
            {
                ArgumentNullException.ThrowIfNull(pair.Key);
                if (!copy.TryAdd(pair.Key, pair.Value))
                {
                    throw new ArgumentException("Dispatch requirements contain duplicate agent ids.", nameof(values));
                }
            }
        }

        return new ReadOnlyDictionary<AgentId, AgentDispatchRequirement>(copy);
    }

    private static IReadOnlyDictionary<AgentId, AgentVersion> CopyVersions(
        IReadOnlyDictionary<AgentId, AgentVersion>? values)
    {
        var copy = new Dictionary<AgentId, AgentVersion>();
        if (values is not null)
        {
            foreach (var pair in values)
            {
                ArgumentNullException.ThrowIfNull(pair.Key);
                ArgumentNullException.ThrowIfNull(pair.Value);
                if (!copy.TryAdd(pair.Key, pair.Value))
                {
                    throw new ArgumentException("Exact versions contain duplicate agent ids.", nameof(values));
                }
            }
        }

        return new ReadOnlyDictionary<AgentId, AgentVersion>(copy);
    }

    private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values) =>
        Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private static IReadOnlyDictionary<string, string> CopyDictionary(IReadOnlyDictionary<string, string>? values) =>
        new ReadOnlyDictionary<string, string>(
            values is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
