namespace TradeMind.AI.Memory;

public sealed record ConversationMemoryKey
{
    public ConversationMemoryKey(
        string conversationId,
        string? tenantId = null,
        string? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        ConversationId = conversationId.Trim();
        TenantId = NormalizeOptional(tenantId);
        UserId = NormalizeOptional(userId);
    }

    public string ConversationId { get; }

    public string? TenantId { get; }

    public string? UserId { get; }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
