using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Application;

public sealed class ContextEngineOptions
{
    public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaximumKnowledgeChunks { get; set; } = 8;
    public int MaximumKnowledgeCharacters { get; set; } = 12_000;
    public int MaximumMemoryItems { get; set; } = 20;
    public int MaximumMemoryCharacters { get; set; } = 8_000;
    public int MaximumNewsItems { get; set; } = 20;
    public int MaximumCalendarItems { get; set; } = 20;
    public int MaximumWarnings { get; set; } = 20;
    public ContextFreshnessThreshold MarketFreshness { get; set; } =
        new(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));
    public ContextFreshnessThreshold KnowledgeFreshness { get; set; } =
        new(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
    public ContextFreshnessThreshold MemoryFreshness { get; set; } =
        new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(24));
    public ContextFreshnessThreshold NewsFreshness { get; set; } =
        new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(2));
    public ContextFreshnessThreshold CalendarFreshness { get; set; } =
        new(TimeSpan.FromHours(1), TimeSpan.FromHours(24));

    internal void Validate()
    {
        if (GlobalTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(GlobalTimeout), "Global timeout must be positive.");
        }

        ValidatePositive(MaximumKnowledgeChunks, nameof(MaximumKnowledgeChunks));
        ValidatePositive(MaximumKnowledgeCharacters, nameof(MaximumKnowledgeCharacters));
        ValidatePositive(MaximumMemoryItems, nameof(MaximumMemoryItems));
        ValidatePositive(MaximumMemoryCharacters, nameof(MaximumMemoryCharacters));
        ValidatePositive(MaximumNewsItems, nameof(MaximumNewsItems));
        ValidatePositive(MaximumCalendarItems, nameof(MaximumCalendarItems));
        ValidatePositive(MaximumWarnings, nameof(MaximumWarnings));
        _ = FreshnessThresholds();
    }

    internal IReadOnlyDictionary<ContextProviderCategory, ContextFreshnessThreshold> FreshnessThresholds()
    {
        MarketFreshness.Validate(nameof(MarketFreshness));
        KnowledgeFreshness.Validate(nameof(KnowledgeFreshness));
        MemoryFreshness.Validate(nameof(MemoryFreshness));
        NewsFreshness.Validate(nameof(NewsFreshness));
        CalendarFreshness.Validate(nameof(CalendarFreshness));

        return new Dictionary<ContextProviderCategory, ContextFreshnessThreshold>
        {
            [ContextProviderCategory.MarketSnapshot] = MarketFreshness,
            [ContextProviderCategory.Knowledge] = KnowledgeFreshness,
            [ContextProviderCategory.Memory] = MemoryFreshness,
            [ContextProviderCategory.News] = NewsFreshness,
            [ContextProviderCategory.EconomicCalendar] = CalendarFreshness
        };
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "Context size limits must be positive.");
        }
    }
}

public sealed record ContextFreshnessThreshold
{
    public ContextFreshnessThreshold(TimeSpan freshMaximumAge, TimeSpan agingMaximumAge)
    {
        FreshMaximumAge = freshMaximumAge;
        AgingMaximumAge = agingMaximumAge;
        Validate(nameof(freshMaximumAge));
    }

    public TimeSpan FreshMaximumAge { get; }
    public TimeSpan AgingMaximumAge { get; }

    internal void Validate(string parameterName)
    {
        if (FreshMaximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Fresh threshold cannot be negative.");
        }

        if (AgingMaximumAge <= FreshMaximumAge)
        {
            throw new ArgumentException("Aging threshold must be greater than the fresh threshold.", parameterName);
        }
    }
}
