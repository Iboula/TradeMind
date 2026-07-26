namespace TradeMind.Api.Contracts.MarketContext;

/// <summary>API request mapped to the Market Context application query.</summary>
public sealed record BuildMarketContextApiRequest
{
    public BuildMarketContextApiRequest(
        int schemaVersion,
        string userId,
        string sessionId,
        string instrument,
        string timeframe,
        string? connectorId = null,
        string? account = null,
        string? conversationId = null,
        string? knowledgeQuery = null,
        int? maximumMarketAgeSeconds = null,
        IReadOnlyCollection<string>? requestedCategories = null)
    {
        SchemaVersion = schemaVersion;
        UserId = userId;
        SessionId = sessionId;
        Instrument = instrument;
        Timeframe = timeframe;
        ConnectorId = connectorId;
        Account = account;
        ConversationId = conversationId;
        KnowledgeQuery = knowledgeQuery;
        MaximumMarketAgeSeconds = maximumMarketAgeSeconds;
        RequestedCategories = requestedCategories ?? [];
    }

    public int SchemaVersion { get; }
    public string UserId { get; }
    public string SessionId { get; }
    public string Instrument { get; }
    public string Timeframe { get; }
    public string? ConnectorId { get; }
    public string? Account { get; }
    public string? ConversationId { get; }
    public string? KnowledgeQuery { get; }
    public int? MaximumMarketAgeSeconds { get; }
    public IReadOnlyCollection<string> RequestedCategories { get; }
}

/// <summary>Transport-safe Market Context result with source traces and quality metadata.</summary>
public sealed record MarketContextApiResponse(
    string Status,
    string? ContextId,
    int? ContextVersion,
    string? Instrument,
    string? Timeframe,
    DateTimeOffset? BuiltAtUtc,
    string? QualityBand,
    double QualityScore,
    IReadOnlyList<ApiContextTrace> Traces,
    IReadOnlyList<ApiContextWarning> Warnings,
    IReadOnlyList<ApiContextError> Errors);

/// <summary>Trace reference for one context provider.</summary>
public sealed record ApiContextTrace(
    string ProviderId,
    string Status,
    IReadOnlyList<string> References,
    DateTimeOffset? SourceTimestampUtc);

/// <summary>Non-blocking Market Context warning.</summary>
public sealed record ApiContextWarning(string Code, string Message);
/// <summary>Blocking or diagnostic Market Context error.</summary>
public sealed record ApiContextError(string Code, string Message);
