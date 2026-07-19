using System.Text;

namespace TradeMind.AI.Memory;

public sealed class DeterministicConversationSummarizer : IConversationSummarizer
{
    private readonly ITokenEstimator _tokenEstimator;
    private readonly TimeProvider _timeProvider;

    public DeterministicConversationSummarizer(
        ITokenEstimator tokenEstimator,
        TimeProvider timeProvider)
    {
        _tokenEstimator = tokenEstimator;
        _timeProvider = timeProvider;
    }

    public Task<ConversationSummary> SummarizeAsync(
        ConversationSummaryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Entries.Count == 0)
        {
            throw new MemoryValidationException("At least one entry is required to summarize.", request.Key);
        }

        if (request.MaxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Summary max characters must be positive.");
        }

        var orderedEntries = request.Entries.OrderBy(entry => entry.SequenceNumber).ToArray();
        var content = BuildSummaryContent(request.PreviousSummary, orderedEntries, request.MaxCharacters);
        var now = _timeProvider.GetUtcNow();

        return Task.FromResult(new ConversationSummary(
            content,
            orderedEntries[^1].SequenceNumber,
            request.PreviousSummary?.CreatedAtUtc ?? now,
            now,
            orderedEntries.Length,
            _tokenEstimator.EstimateTokens(content),
            modelName: "deterministic-local",
            providerName: "TradeMind",
            version: (request.PreviousSummary?.Version ?? 0) + 1));
    }

    private static string BuildSummaryContent(
        ConversationSummary? previousSummary,
        IReadOnlyList<ConversationMemoryEntry> entries,
        int maxCharacters)
    {
        var builder = new StringBuilder();
        if (previousSummary is not null)
        {
            builder.Append("Previous summary: ");
            builder.Append(previousSummary.Content);
            builder.AppendLine();
        }

        builder.Append("Conversation summary through sequence ");
        builder.Append(entries[^1].SequenceNumber);
        builder.Append(": ");
        builder.AppendJoin(
            " | ",
            entries.Select(entry => $"{entry.Role}: {Clip(entry.Content, 80)}"));

        var summary = builder.ToString();
        return summary.Length <= maxCharacters
            ? summary
            : summary[..maxCharacters];
    }

    private static string Clip(string value, int maxCharacters)
    {
        return value.Length <= maxCharacters
            ? value
            : value[..maxCharacters];
    }
}
