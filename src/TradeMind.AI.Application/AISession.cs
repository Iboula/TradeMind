namespace TradeMind.AI.Application;

public sealed record AISession
{
    public AISession(
        string sessionId,
        string? conversationId,
        string correlationId,
        string? tenantId,
        string? userId,
        string? agentId,
        string scenario,
        DateTimeOffset createdAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Session creation date must be UTC.", nameof(createdAtUtc));
        }

        SessionId = sessionId;
        ConversationId = string.IsNullOrWhiteSpace(conversationId) ? null : conversationId;
        CorrelationId = correlationId;
        TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        UserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
        AgentId = string.IsNullOrWhiteSpace(agentId) ? null : agentId;
        Scenario = scenario;
        CreatedAtUtc = createdAtUtc;
        Metadata = CopyMetadata(metadata);
    }

    public string SessionId { get; }

    public string? ConversationId { get; }

    public string CorrelationId { get; }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string? AgentId { get; }

    public string Scenario { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        return metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }
}
