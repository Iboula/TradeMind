namespace TradeMind.AI.Application;

public sealed record AIKnowledgeOptions
{
    public static AIKnowledgeOptions Disabled { get; } = new();

    public AIKnowledgeOptions(
        bool enabled = false,
        string? query = null,
        int maxResults = 5,
        double? minimumScore = null,
        int? maxCharacters = 4000,
        int? maxEstimatedTokens = null,
        bool includeSourceMetadata = false,
        bool includeCitations = true,
        KnowledgeFailureMode failureMode = KnowledgeFailureMode.ContinueWithoutKnowledge,
        KnowledgeOrderingStrategy orderingStrategy = KnowledgeOrderingStrategy.SourceThenSequence,
        IReadOnlyDictionary<string, string>? filters = null,
        bool useCurrentUserMessageAsQuery = true,
        string? contextLabel = null)
    {
        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), "MaxResults must be positive.");
        }

        if (minimumScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore), "MinimumScore must be between 0 and 1 when provided.");
        }

        if (maxCharacters is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters), "MaxCharacters must be positive when provided.");
        }

        if (maxEstimatedTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEstimatedTokens), "MaxEstimatedTokens must be positive when provided.");
        }

        if (enabled && string.IsNullOrWhiteSpace(query) && !useCurrentUserMessageAsQuery)
        {
            throw new ArgumentException("Knowledge query is required when knowledge is enabled and current user message fallback is disabled.", nameof(query));
        }

        Enabled = enabled;
        Query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        MaxResults = maxResults;
        MinimumScore = minimumScore;
        MaxCharacters = maxCharacters;
        MaxEstimatedTokens = maxEstimatedTokens;
        IncludeSourceMetadata = includeSourceMetadata;
        IncludeCitations = includeCitations;
        FailureMode = failureMode;
        OrderingStrategy = orderingStrategy;
        Filters = filters is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(filters, StringComparer.OrdinalIgnoreCase);
        UseCurrentUserMessageAsQuery = useCurrentUserMessageAsQuery;
        ContextLabel = string.IsNullOrWhiteSpace(contextLabel) ? "KnowledgeHub context" : contextLabel.Trim();
    }

    public bool Enabled { get; }

    public string? Query { get; }

    public int MaxResults { get; }

    public double? MinimumScore { get; }

    public int? MaxCharacters { get; }

    public int? MaxEstimatedTokens { get; }

    public bool IncludeSourceMetadata { get; }

    public bool IncludeCitations { get; }

    public KnowledgeFailureMode FailureMode { get; }

    public KnowledgeOrderingStrategy OrderingStrategy { get; }

    public IReadOnlyDictionary<string, string> Filters { get; }

    public bool UseCurrentUserMessageAsQuery { get; }

    public string ContextLabel { get; }
}

public enum KnowledgeFailureMode
{
    FailClosed,
    ContinueWithoutKnowledge
}

public enum KnowledgeOrderingStrategy
{
    RelevanceDescending,
    SourceThenSequence,
    OriginalSearchOrder
}
