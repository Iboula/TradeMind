namespace TradeMind.AI.Application;

public sealed record AIIdentityContext
{
    public static AIIdentityContext Empty { get; } = new();

    public AIIdentityContext(
        string? tenantId = null,
        string? userId = null,
        string? agentId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        UserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
        AgentId = string.IsNullOrWhiteSpace(agentId) ? null : agentId;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string? AgentId { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}
