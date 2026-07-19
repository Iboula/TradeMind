using TradeMind.AI.Application;

namespace TradeMind.AI.Knowledge;

public sealed record KnowledgeContextRequest
{
    public KnowledgeContextRequest(
        string query,
        string sessionId,
        string correlationId,
        string scenario,
        int maxResults,
        double? minimumScore,
        int? maxCharacters,
        int? maxEstimatedTokens,
        KnowledgeOrderingStrategy orderingStrategy,
        IReadOnlyDictionary<string, string>? filters = null,
        string? tenantId = null,
        string? userId = null,
        bool includeSourceMetadata = false,
        bool includeCitations = true,
        string? contextLabel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

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

        Query = query.Trim();
        TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        UserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        Scenario = scenario;
        MaxResults = maxResults;
        MinimumScore = minimumScore;
        MaxCharacters = maxCharacters;
        MaxEstimatedTokens = maxEstimatedTokens;
        OrderingStrategy = orderingStrategy;
        Filters = filters is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(filters, StringComparer.OrdinalIgnoreCase);
        IncludeSourceMetadata = includeSourceMetadata;
        IncludeCitations = includeCitations;
        ContextLabel = string.IsNullOrWhiteSpace(contextLabel) ? "KnowledgeHub context" : contextLabel.Trim();
    }

    public string Query { get; }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string SessionId { get; }

    public string CorrelationId { get; }

    public string Scenario { get; }

    public int MaxResults { get; }

    public double? MinimumScore { get; }

    public int? MaxCharacters { get; }

    public int? MaxEstimatedTokens { get; }

    public KnowledgeOrderingStrategy OrderingStrategy { get; }

    public IReadOnlyDictionary<string, string> Filters { get; }

    public bool IncludeSourceMetadata { get; }

    public bool IncludeCitations { get; }

    public string ContextLabel { get; }
}

public sealed record KnowledgeContextFragment
{
    public KnowledgeContextFragment(
        Guid fragmentId,
        Guid sourceId,
        string content,
        double score,
        int sequence,
        string? title = null,
        string? sourceType = null,
        string? sourceReference = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        int? estimatedTokenCount = null,
        string? checksum = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (score is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 1.");
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence cannot be negative.");
        }

        if (estimatedTokenCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedTokenCount), "Estimated token count cannot be negative.");
        }

        FragmentId = fragmentId;
        SourceId = sourceId;
        Content = content.Trim();
        Score = score;
        Sequence = sequence;
        Title = string.IsNullOrWhiteSpace(title) ? null : title;
        SourceType = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType;
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        EstimatedTokenCount = estimatedTokenCount;
        Checksum = string.IsNullOrWhiteSpace(checksum) ? null : checksum;
    }

    public Guid FragmentId { get; }

    public Guid SourceId { get; }

    public string Content { get; }

    public double Score { get; }

    public int Sequence { get; }

    public string? Title { get; }

    public string? SourceType { get; }

    public string? SourceReference { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public int? EstimatedTokenCount { get; }

    public string? Checksum { get; }
}

public sealed record KnowledgeCitation
{
    public KnowledgeCitation(
        string citationId,
        Guid fragmentId,
        Guid sourceId,
        double score,
        int ordinal,
        string? title = null,
        string? sourceReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(citationId);

        if (score is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 1.");
        }

        if (ordinal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), "Ordinal must be positive.");
        }

        CitationId = citationId;
        FragmentId = fragmentId;
        SourceId = sourceId;
        Score = score;
        Ordinal = ordinal;
        Title = string.IsNullOrWhiteSpace(title) ? null : title;
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference;
    }

    public string CitationId { get; }

    public Guid FragmentId { get; }

    public Guid SourceId { get; }

    public string? Title { get; }

    public string? SourceReference { get; }

    public double Score { get; }

    public int Ordinal { get; }
}

public sealed record KnowledgeContextResult
{
    public KnowledgeContextResult(
        string query,
        IReadOnlyList<KnowledgeContextFragment> fragments,
        IReadOnlyList<KnowledgeCitation> citations,
        int availableResultCount,
        bool truncated,
        int? estimatedTokenCount,
        int totalCharacters,
        TimeSpan retrievalDuration,
        DateTimeOffset retrievedAtUtc,
        string? searchProvider = null,
        string? embeddingModel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(fragments);
        ArgumentNullException.ThrowIfNull(citations);

        if (availableResultCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableResultCount), "Available result count cannot be negative.");
        }

        if (estimatedTokenCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedTokenCount), "Estimated token count cannot be negative.");
        }

        if (totalCharacters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCharacters), "Total characters cannot be negative.");
        }

        if (retrievalDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retrievalDuration), "Retrieval duration cannot be negative.");
        }

        if (retrievedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Retrieved date must be UTC.", nameof(retrievedAtUtc));
        }

        Query = query.Trim();
        Fragments = fragments.ToArray();
        Citations = citations.ToArray();
        AvailableResultCount = availableResultCount;
        SelectedResultCount = Fragments.Count;
        Truncated = truncated;
        EstimatedTokenCount = estimatedTokenCount;
        TotalCharacters = totalCharacters;
        RetrievalDuration = retrievalDuration;
        SearchProvider = string.IsNullOrWhiteSpace(searchProvider) ? null : searchProvider;
        EmbeddingModel = string.IsNullOrWhiteSpace(embeddingModel) ? null : embeddingModel;
        RetrievedAtUtc = retrievedAtUtc;
    }

    public string Query { get; }

    public IReadOnlyList<KnowledgeContextFragment> Fragments { get; }

    public IReadOnlyList<KnowledgeCitation> Citations { get; }

    public int AvailableResultCount { get; }

    public int SelectedResultCount { get; }

    public bool Truncated { get; }

    public int? EstimatedTokenCount { get; }

    public int TotalCharacters { get; }

    public TimeSpan RetrievalDuration { get; }

    public string? SearchProvider { get; }

    public string? EmbeddingModel { get; }

    public DateTimeOffset RetrievedAtUtc { get; }
}
