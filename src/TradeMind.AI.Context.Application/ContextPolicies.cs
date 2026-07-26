using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Application;

public interface IContextFreshnessPolicy
{
    ContextFreshnessAssessment Evaluate(
        ContextProviderCategory category,
        DateTimeOffset? sourceTimestampUtc,
        DateTimeOffset referenceTimeUtc);

    ContextFreshness Classify(
        ContextProviderCategory category,
        DateTimeOffset? sourceTimestampUtc,
        DateTimeOffset referenceTimeUtc);
}

public sealed class ContextFreshnessPolicy : IContextFreshnessPolicy
{
    private readonly IReadOnlyDictionary<ContextProviderCategory, ContextFreshnessThreshold> _thresholds;

    public ContextFreshnessPolicy(IOptions<ContextEngineOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Value.Validate();
        _thresholds = options.Value.FreshnessThresholds();
    }

    public ContextFreshness Classify(
        ContextProviderCategory category,
        DateTimeOffset? sourceTimestampUtc,
        DateTimeOffset referenceTimeUtc)
    {
        return Evaluate(category, sourceTimestampUtc, referenceTimeUtc).Classification;
    }

    public ContextFreshnessAssessment Evaluate(
        ContextProviderCategory category,
        DateTimeOffset? sourceTimestampUtc,
        DateTimeOffset referenceTimeUtc)
    {
        if (!_thresholds.TryGetValue(category, out var threshold))
        {
            return new ContextFreshnessAssessment(
                ContextFreshness.NotApplicable,
                null,
                null);
        }

        var thresholds = new ContextFreshnessThresholds(
            threshold.FreshMaximumAge,
            threshold.AgingMaximumAge);
        if (sourceTimestampUtc is null || sourceTimestampUtc > referenceTimeUtc)
        {
            return new ContextFreshnessAssessment(
                ContextFreshness.Unknown,
                null,
                thresholds);
        }

        var age = referenceTimeUtc - sourceTimestampUtc.Value;
        if (age <= threshold.FreshMaximumAge)
        {
            return new ContextFreshnessAssessment(
                ContextFreshness.Fresh,
                age,
                thresholds);
        }

        return new ContextFreshnessAssessment(
            age <= threshold.AgingMaximumAge
                ? ContextFreshness.Aging
                : ContextFreshness.Stale,
            age,
            thresholds);
    }
}

public interface IContextQualityPolicy
{
    ContextQuality Evaluate(
        IReadOnlyCollection<ContextProviderDescriptor> descriptors,
        IReadOnlyCollection<ContextSourceTrace> traces);
}

public sealed class ContextQualityPolicy : IContextQualityPolicy
{
    public ContextQuality Evaluate(
        IReadOnlyCollection<ContextProviderDescriptor> descriptors,
        IReadOnlyCollection<ContextSourceTrace> traces)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(traces);
        if (descriptors.Count == 0)
        {
            return new ContextQuality(0, 0, 0, 0, ContextQualityBand.Insufficient);
        }

        var traceByProvider = traces.ToDictionary(trace => trace.ProviderId);
        var totalWeight = descriptors.Sum(descriptor => Weight(descriptor.Requirement));
        var successfulWeight = descriptors
            .Where(descriptor => IsSuccessful(traceByProvider, descriptor.Id))
            .Sum(descriptor => Weight(descriptor.Requirement));
        var completeness = Percent(successfulWeight, totalWeight);

        var successfulDescriptors = descriptors
            .Where(descriptor => IsSuccessful(traceByProvider, descriptor.Id))
            .ToArray();
        var freshnessWeight = successfulDescriptors.Sum(descriptor => Weight(descriptor.Requirement));
        var freshnessPoints = successfulDescriptors.Sum(descriptor =>
            Weight(descriptor.Requirement) * FreshnessFactor(traceByProvider[descriptor.Id].Freshness));
        var freshness = freshnessWeight == 0 ? 0 : Percent(freshnessPoints, freshnessWeight);

        var reliableWeight = descriptors
            .Where(descriptor => traceByProvider.TryGetValue(descriptor.Id, out var trace)
                && trace.Status is ContextProviderExecutionStatus.Succeeded
                    or ContextProviderExecutionStatus.Unavailable
                    or ContextProviderExecutionStatus.NotConfigured)
            .Sum(descriptor => Weight(descriptor.Requirement));
        var reliability = Percent(reliableWeight, totalWeight);
        var score = Round((completeness * 0.5) + (freshness * 0.3) + (reliability * 0.2));
        var band = score switch
        {
            >= 85 => ContextQualityBand.Excellent,
            >= 70 => ContextQualityBand.Good,
            >= 45 => ContextQualityBand.Limited,
            _ => ContextQualityBand.Insufficient
        };

        return new ContextQuality(score, completeness, freshness, reliability, band);
    }

    private static bool IsSuccessful(
        IReadOnlyDictionary<ContextProviderId, ContextSourceTrace> traces,
        ContextProviderId id) =>
        traces.TryGetValue(id, out var trace)
        && trace.Status == ContextProviderExecutionStatus.Succeeded;

    private static int Weight(ContextRequirement requirement) => requirement switch
    {
        ContextRequirement.Required => 5,
        ContextRequirement.Preferred => 3,
        ContextRequirement.Optional => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(requirement))
    };

    private static double FreshnessFactor(ContextFreshness freshness) => freshness switch
    {
        ContextFreshness.Fresh => 1,
        ContextFreshness.Aging => 0.7,
        ContextFreshness.Stale => 0.25,
        ContextFreshness.Unknown => 0.5,
        ContextFreshness.NotApplicable => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(freshness))
    };

    private static double Percent(double value, double total) =>
        total == 0 ? 0 : Round((value / total) * 100);

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public interface IContextSizePolicy
{
    ContextNormalizationResult Normalize(IReadOnlyCollection<ContextData> data);
}

public sealed record ContextNormalizationResult(
    MarketSnapshotContextData? Market,
    KnowledgeContextData? Knowledge,
    MemoryContextData? Memory,
    TraderProfileContextData? TraderProfile,
    WorkspaceContextData? Workspace,
    NewsContextData? News,
    EconomicCalendarContextData? EconomicCalendar,
    IReadOnlyList<ContextBuildWarning> Warnings);

public sealed class ContextSizePolicy : IContextSizePolicy
{
    private readonly ContextEngineOptions _options;

    public ContextSizePolicy(IOptions<ContextEngineOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Value.Validate();
        _options = options.Value;
    }

    public ContextNormalizationResult Normalize(IReadOnlyCollection<ContextData> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var warnings = new List<ContextBuildWarning>();
        var market = First<MarketSnapshotContextData>(data);
        var knowledge = NormalizeKnowledge(First<KnowledgeContextData>(data), warnings);
        var memory = NormalizeMemory(First<MemoryContextData>(data), warnings);
        var news = NormalizeNews(First<NewsContextData>(data), warnings);
        var calendar = NormalizeCalendar(First<EconomicCalendarContextData>(data), warnings);

        return new ContextNormalizationResult(
            market,
            knowledge,
            memory,
            First<TraderProfileContextData>(data),
            First<WorkspaceContextData>(data),
            news,
            calendar,
            Array.AsReadOnly(warnings.Take(_options.MaximumWarnings).ToArray()));
    }

    private KnowledgeContextData? NormalizeKnowledge(
        KnowledgeContextData? data,
        ICollection<ContextBuildWarning> warnings)
    {
        if (data is null)
        {
            return null;
        }

        var selected = new List<KnowledgeChunk>();
        var remainingCharacters = _options.MaximumKnowledgeCharacters;
        foreach (var chunk in data.Context.Chunks.Take(_options.MaximumKnowledgeChunks))
        {
            if (remainingCharacters == 0)
            {
                break;
            }

            var content = chunk.Content.Length <= remainingCharacters
                ? chunk.Content
                : chunk.Content[..remainingCharacters];
            selected.Add(new KnowledgeChunk(
                chunk.FragmentId,
                chunk.SourceId,
                content,
                chunk.Score,
                chunk.Sequence,
                chunk.Title,
                chunk.SourceReference));
            remainingCharacters -= content.Length;
        }

        var truncated = data.Context.Truncated
            || selected.Count < data.Context.Chunks.Count
            || selected.Sum(chunk => chunk.Content.Length) < data.Context.Chunks.Sum(chunk => chunk.Content.Length);
        if (truncated)
        {
            warnings.Add(new ContextBuildWarning(
                "knowledge-truncated",
                "Knowledge context was deterministically truncated to configured limits."));
        }

        return new KnowledgeContextData(new KnowledgeContext(
            data.Context.Query,
            selected,
            data.Context.RetrievedAtUtc,
            truncated,
            data.Context.AvailableChunkCount));
    }

    private MemoryContextData? NormalizeMemory(
        MemoryContextData? data,
        ICollection<ContextBuildWarning> warnings)
    {
        if (data is null)
        {
            return null;
        }

        var candidates = data.Context.Items
            .OrderByDescending(item => item.Sequence)
            .Take(_options.MaximumMemoryItems)
            .ToArray();
        var selected = new List<MemoryItem>();
        var remainingCharacters = _options.MaximumMemoryCharacters;
        var summary = data.Context.Summary;
        var summaryTruncated = false;
        if (summary is not null)
        {
            if (summary.Length > remainingCharacters)
            {
                summary = summary[..remainingCharacters];
                summaryTruncated = true;
            }

            remainingCharacters -= summary.Length;
        }

        foreach (var item in candidates)
        {
            if (remainingCharacters == 0)
            {
                break;
            }

            var content = item.Content.Length <= remainingCharacters
                ? item.Content
                : item.Content[..remainingCharacters];
            selected.Add(new MemoryItem(item.Id, item.Role, content, item.CreatedAtUtc, item.Sequence));
            remainingCharacters -= content.Length;
        }

        selected.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
        var truncated = data.Context.Truncated
            || summaryTruncated
            || selected.Count < data.Context.Items.Count
            || selected.Sum(item => item.Content.Length) < data.Context.Items.Sum(item => item.Content.Length);
        if (truncated)
        {
            warnings.Add(new ContextBuildWarning(
                "memory-truncated",
                "Memory context was deterministically truncated to configured limits."));
        }

        return new MemoryContextData(new MemoryContext(
            data.Context.ConversationId,
            summary,
            selected,
            truncated,
            data.Context.AvailableItemCount));
    }

    private NewsContextData? NormalizeNews(
        NewsContextData? data,
        ICollection<ContextBuildWarning> warnings)
    {
        if (data is null || data.Context.Items.Count <= _options.MaximumNewsItems)
        {
            return data;
        }

        warnings.Add(new ContextBuildWarning("news-truncated", "News context was truncated to configured limits."));
        return new NewsContextData(new NewsContext(data.Context.Items
            .OrderByDescending(item => item.PublishedAtUtc)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Take(_options.MaximumNewsItems)
            .ToArray()));
    }

    private EconomicCalendarContextData? NormalizeCalendar(
        EconomicCalendarContextData? data,
        ICollection<ContextBuildWarning> warnings)
    {
        if (data is null || data.Context.Events.Count <= _options.MaximumCalendarItems)
        {
            return data;
        }

        warnings.Add(new ContextBuildWarning(
            "calendar-truncated",
            "Economic calendar context was truncated to configured limits."));
        return new EconomicCalendarContextData(new EconomicCalendarContext(data.Context.Events
            .OrderBy(item => item.ScheduledAtUtc)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Take(_options.MaximumCalendarItems)
            .ToArray()));
    }

    private static T? First<T>(IEnumerable<ContextData> data) where T : ContextData =>
        data.OfType<T>().FirstOrDefault();
}
