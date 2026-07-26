using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.Memory;

namespace TradeMind.AI.Context.Infrastructure;

public sealed class MemoryContextProvider(
    IMemoryReader memoryReader,
    IOptions<ContextEngineOptions> options) : IContextProvider
{
    public static readonly ContextProviderId ProviderId = new("memory");
    private readonly ContextEngineOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    public ContextProviderDescriptor Descriptor { get; } = new(
        ProviderId,
        ContextProviderCategory.Memory,
        ContextRequirement.Preferred,
        20,
        TimeSpan.FromSeconds(2));

    public async Task<ContextProviderResult> ProvideAsync(
        ContextProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversationId = request.Query.ConversationId ?? request.Query.SessionId;
        var result = await memoryReader.ReadAsync(
            new MemoryReadRequest(
                new ConversationMemoryKey(
                    conversationId,
                    request.Query.TenantId,
                    request.Query.UserId),
                new MemoryWindowOptions(
                    _options.MaximumMemoryItems,
                    _options.MaximumMemoryCharacters,
                    recentUserMessagesMinimum: 0,
                    recentAssistantMessagesMinimum: 0)),
            cancellationToken).ConfigureAwait(false);
        if (result.Summary is null && result.SelectedEntries.Count == 0)
        {
            return ContextProviderResult.Unavailable(
                ProviderId,
                "No conversation memory exists for this session.");
        }

        var items = result.SelectedEntries.Select(entry => new MemoryItem(
            entry.EntryId,
            entry.Role.ToString(),
            entry.Content,
            entry.CreatedAtUtc,
            entry.SequenceNumber)).ToArray();
        var sourceTimestamp = items.Select(item => (DateTimeOffset?)item.CreatedAtUtc)
            .Append(result.Summary?.UpdatedAtUtc)
            .Max();

        return ContextProviderResult.Succeeded(
            new MemoryContextData(new MemoryContext(
                conversationId,
                result.Summary?.Content,
                items,
                result.Truncated,
                result.TotalAvailableEntries)),
            sourceTimestamp,
            items.Length + (result.Summary is null ? 0 : 1),
            "1.0",
            [new ContextSourceReference("conversation", conversationId)]);
    }
}
