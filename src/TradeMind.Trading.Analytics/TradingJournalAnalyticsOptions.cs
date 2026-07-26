using Microsoft.Extensions.Options;

namespace TradeMind.Trading.Analytics;

public sealed class TradingJournalAnalyticsOptions
{
    public const string SectionName = "TradingJournalAnalytics";

    public int MaximumTradesPerAnalysis { get; set; } = 500;
    public int MinimumTradesForTrend { get; set; } = 5;
    public int MinimumTradesPerGroup { get; set; } = 3;
    public int MaximumGroups { get; set; } = 52;
    public int MaximumSetups { get; set; } = 20;
    public int MaximumBehaviorPatterns { get; set; } = 9;
    public TimeSpan AnalysisTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool IncludeAIInterpretation { get; set; }
    public bool IncludeKnowledge { get; set; }
    public bool IncludeMemory { get; set; }
    public bool FailOnInvalidTrade { get; set; } = true;
    public bool ExcludeInvalidTradesFromAggregates { get; set; } = true;
    public decimal MinimumDataCompleteness { get; set; } = 50m;
    public int MaximumRecommendations { get; set; } = 8;
    public int MaximumChecklistItems { get; set; } = 8;
}

public sealed class TradingJournalAnalyticsSafetyOptions
{
    public const string SectionName = "TradingJournalAnalytics:Safety";

    public bool FailClosed { get; set; } = true;
}

internal sealed class TradingJournalAnalyticsOptionsValidator : IValidateOptions<TradingJournalAnalyticsOptions>
{
    public ValidateOptionsResult Validate(string? name, TradingJournalAnalyticsOptions options)
    {
        var failures = new List<string>();
        AddRangeFailure(options.MaximumTradesPerAnalysis, 1, 10_000, nameof(options.MaximumTradesPerAnalysis), failures);
        AddRangeFailure(options.MinimumTradesForTrend, 2, 10_000, nameof(options.MinimumTradesForTrend), failures);
        AddRangeFailure(options.MinimumTradesPerGroup, 1, 10_000, nameof(options.MinimumTradesPerGroup), failures);
        AddRangeFailure(options.MaximumGroups, 1, 1_000, nameof(options.MaximumGroups), failures);
        AddRangeFailure(options.MaximumSetups, 1, 1_000, nameof(options.MaximumSetups), failures);
        AddRangeFailure(options.MaximumBehaviorPatterns, 1, 100, nameof(options.MaximumBehaviorPatterns), failures);
        AddRangeFailure(options.MaximumRecommendations, 1, 100, nameof(options.MaximumRecommendations), failures);
        AddRangeFailure(options.MaximumChecklistItems, 1, 100, nameof(options.MaximumChecklistItems), failures);

        if (options.MinimumDataCompleteness is < 0 or > 100)
        {
            failures.Add("MinimumDataCompleteness must be between 0 and 100.");
        }

        if (options.AnalysisTimeout < TimeSpan.FromSeconds(1) || options.AnalysisTimeout > TimeSpan.FromMinutes(2))
        {
            failures.Add("AnalysisTimeout must be between one second and two minutes.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void AddRangeFailure(int value, int minimum, int maximum, string name, ICollection<string> failures)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add($"{name} must be between {minimum} and {maximum}.");
        }
    }
}
