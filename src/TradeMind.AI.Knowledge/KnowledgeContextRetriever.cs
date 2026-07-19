using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TradeMind.AI.Application;
using TradeMind.KnowledgeHub.Application;

namespace TradeMind.AI.Knowledge;

public sealed class KnowledgeContextRetriever : IKnowledgeContextRetriever
{
    private readonly IKnowledgeSearcher _searcher;
    private readonly IKnowledgeTokenEstimator _tokenEstimator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KnowledgeContextRetriever> _logger;

    public KnowledgeContextRetriever(
        IKnowledgeSearcher searcher,
        IKnowledgeTokenEstimator tokenEstimator,
        TimeProvider timeProvider,
        ILogger<KnowledgeContextRetriever> logger)
    {
        _searcher = searcher;
        _tokenEstimator = tokenEstimator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<KnowledgeContextResult> RetrieveAsync(
        KnowledgeContextRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var startTimestamp = _timeProvider.GetTimestamp();

        _logger.LogInformation(
            "Knowledge retrieval started for session {SessionId}, correlation {CorrelationId}, scenario {Scenario}, max results {MaxResults}, tenant {TenantId}, and user {UserId}",
            request.SessionId,
            request.CorrelationId,
            request.Scenario,
            request.MaxResults,
            request.TenantId,
            request.UserId);

        try
        {
            var rawResults = await _searcher
                .SearchAsync(request.Query, request.MaxResults, cancellationToken)
                .ConfigureAwait(false);
            var candidates = rawResults.Select(MapResult).ToArray();
            var filtered = candidates
                .Where(fragment => request.MinimumScore is null || fragment.Score >= request.MinimumScore)
                .ToArray();
            var deduplicated = Deduplicate(filtered);
            var ordered = Order(deduplicated, request.OrderingStrategy);
            var selected = ApplyBudgets(ordered, request);
            var citations = request.IncludeCitations
                ? CreateCitations(selected)
                : [];
            var duration = _timeProvider.GetElapsedTime(startTimestamp);

            _logger.LogInformation(
                "Knowledge retrieval completed for session {SessionId}, correlation {CorrelationId}, available {AvailableResultCount}, selected {SelectedResultCount}, truncated {Truncated}, and duration {DurationMs} ms",
                request.SessionId,
                request.CorrelationId,
                filtered.Length,
                selected.Count,
                selected.Count < filtered.Length,
                duration.TotalMilliseconds);

            return new KnowledgeContextResult(
                request.Query,
                selected,
                citations,
                filtered.Length,
                selected.Count < filtered.Length,
                selected.Sum(fragment => fragment.EstimatedTokenCount ?? _tokenEstimator.EstimateTokens(fragment.Content)),
                selected.Sum(fragment => fragment.Content.Length),
                duration,
                _timeProvider.GetUtcNow(),
                searchProvider: "KnowledgeHub");
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not KnowledgeRetrievalException)
        {
            throw new KnowledgeRetrievalException("Knowledge context retrieval failed.", request.CorrelationId, exception);
        }
    }

    private KnowledgeContextFragment MapResult(KnowledgeSearchResult result)
    {
        return new KnowledgeContextFragment(
            result.FragmentId,
            result.SourceId,
            result.Content,
            ClampScore(result.Score),
            result.Sequence,
            title: result.SourceTitle,
            checksum: Checksum(result.Content),
            estimatedTokenCount: _tokenEstimator.EstimateTokens(result.Content));
    }

    private static IReadOnlyList<KnowledgeContextFragment> Deduplicate(
        IReadOnlyList<KnowledgeContextFragment> fragments)
    {
        var keyToGroup = new Dictionary<string, int>(StringComparer.Ordinal);
        var groups = new List<(KnowledgeContextFragment Fragment, int Index)>();
        for (var index = 0; index < fragments.Count; index++)
        {
            var fragment = fragments[index];
            var keys = DeduplicationKeys(fragment);
            var existingKey = keys.FirstOrDefault(keyToGroup.ContainsKey);
            if (existingKey is null)
            {
                var groupIndex = groups.Count;
                groups.Add((fragment, index));
                foreach (var key in keys)
                {
                    keyToGroup[key] = groupIndex;
                }

                continue;
            }

            var existingGroupIndex = keyToGroup[existingKey];
            var existing = groups[existingGroupIndex];
            if (fragment.Score > existing.Fragment.Score)
            {
                groups[existingGroupIndex] = (fragment, existing.Index);
                foreach (var key in keys)
                {
                    keyToGroup[key] = existingGroupIndex;
                }
            }
        }

        return groups
            .OrderBy(item => item.Index)
            .Select(item => item.Fragment)
            .ToArray();
    }

    private static string[] DeduplicationKeys(KnowledgeContextFragment fragment)
    {
        var normalizedContent = string.Join(
            ' ',
            fragment.Content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return
        [
            $"fragment:{fragment.FragmentId:N}",
            string.IsNullOrWhiteSpace(fragment.Checksum) ? $"content:{fragment.SourceId:N}:{normalizedContent}" : $"checksum:{fragment.Checksum}",
            $"content:{fragment.SourceId:N}:{normalizedContent}"
        ];
    }

    private static IReadOnlyList<KnowledgeContextFragment> Order(
        IReadOnlyList<KnowledgeContextFragment> fragments,
        KnowledgeOrderingStrategy orderingStrategy)
    {
        return orderingStrategy switch
        {
            KnowledgeOrderingStrategy.RelevanceDescending => fragments
                .OrderByDescending(fragment => fragment.Score)
                .ThenBy(fragment => fragment.SourceId)
                .ThenBy(fragment => fragment.Sequence)
                .ToArray(),
            KnowledgeOrderingStrategy.SourceThenSequence => fragments
                .OrderBy(fragment => fragment.SourceId)
                .ThenBy(fragment => fragment.Sequence)
                .ThenByDescending(fragment => fragment.Score)
                .ToArray(),
            KnowledgeOrderingStrategy.OriginalSearchOrder => fragments.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(orderingStrategy), "Unsupported knowledge ordering strategy.")
        };
    }

    private IReadOnlyList<KnowledgeContextFragment> ApplyBudgets(
        IReadOnlyList<KnowledgeContextFragment> fragments,
        KnowledgeContextRequest request)
    {
        var selected = new List<KnowledgeContextFragment>();
        var characters = 0;
        var tokens = 0;

        foreach (var fragment in fragments)
        {
            if (selected.Count >= request.MaxResults)
            {
                break;
            }

            var fragmentTokens = fragment.EstimatedTokenCount ?? _tokenEstimator.EstimateTokens(fragment.Content);
            var fitsCharacters = request.MaxCharacters is null
                || characters + fragment.Content.Length <= request.MaxCharacters.Value;
            var fitsTokens = request.MaxEstimatedTokens is null
                || tokens + fragmentTokens <= request.MaxEstimatedTokens.Value;

            if (!fitsCharacters || !fitsTokens)
            {
                continue;
            }

            selected.Add(fragment);
            characters += fragment.Content.Length;
            tokens += fragmentTokens;
        }

        return selected.ToArray();
    }

    private static IReadOnlyList<KnowledgeCitation> CreateCitations(
        IReadOnlyList<KnowledgeContextFragment> fragments)
    {
        return fragments
            .Select((fragment, index) => new KnowledgeCitation(
                $"K{index + 1}",
                fragment.FragmentId,
                fragment.SourceId,
                fragment.Score,
                index + 1,
                fragment.Title,
                fragment.SourceReference))
            .ToArray();
    }

    private static double ClampScore(double score)
    {
        return Math.Clamp(score, 0, 1);
    }

    private static string Checksum(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
