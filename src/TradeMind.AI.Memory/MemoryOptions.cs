namespace TradeMind.AI.Memory;

public sealed record MemoryWindowOptions
{
    public MemoryWindowOptions(
        int maxEntries = 20,
        int? maxCharacters = 8000,
        int? maxEstimatedTokens = null,
        bool includeSystemMessages = true,
        bool includeSummary = true,
        int recentUserMessagesMinimum = 1,
        int recentAssistantMessagesMinimum = 1)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "MaxEntries must be positive.");
        }

        if (maxCharacters is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters), "MaxCharacters must be positive when provided.");
        }

        if (maxEstimatedTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEstimatedTokens), "MaxEstimatedTokens must be positive when provided.");
        }

        if (recentUserMessagesMinimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentUserMessagesMinimum), "Recent user minimum cannot be negative.");
        }

        if (recentAssistantMessagesMinimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentAssistantMessagesMinimum), "Recent assistant minimum cannot be negative.");
        }

        if (recentUserMessagesMinimum + recentAssistantMessagesMinimum > maxEntries)
        {
            throw new ArgumentException("Recent minimum messages cannot exceed MaxEntries.");
        }

        MaxEntries = maxEntries;
        MaxCharacters = maxCharacters;
        MaxEstimatedTokens = maxEstimatedTokens;
        IncludeSystemMessages = includeSystemMessages;
        IncludeSummary = includeSummary;
        RecentUserMessagesMinimum = recentUserMessagesMinimum;
        RecentAssistantMessagesMinimum = recentAssistantMessagesMinimum;
    }

    public int MaxEntries { get; }

    public int? MaxCharacters { get; }

    public int? MaxEstimatedTokens { get; }

    public bool IncludeSystemMessages { get; }

    public bool IncludeSummary { get; }

    public int RecentUserMessagesMinimum { get; }

    public int RecentAssistantMessagesMinimum { get; }
}

public sealed record MemoryCompactionOptions
{
    public MemoryCompactionOptions(
        bool enabled = true,
        int summarizeAfterEntryCount = 20,
        int entriesToKeepAfterSummary = 8,
        int summaryMaxCharacters = 1200)
    {
        if (summarizeAfterEntryCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(summarizeAfterEntryCount), "Summary threshold must be positive.");
        }

        if (entriesToKeepAfterSummary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entriesToKeepAfterSummary), "Entries to keep cannot be negative.");
        }

        if (entriesToKeepAfterSummary >= summarizeAfterEntryCount)
        {
            throw new ArgumentException("Entries to keep must be lower than the summary threshold.");
        }

        if (summaryMaxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(summaryMaxCharacters), "Summary max characters must be positive.");
        }

        Enabled = enabled;
        SummarizeAfterEntryCount = summarizeAfterEntryCount;
        EntriesToKeepAfterSummary = entriesToKeepAfterSummary;
        SummaryMaxCharacters = summaryMaxCharacters;
    }

    public bool Enabled { get; }

    public int SummarizeAfterEntryCount { get; }

    public int EntriesToKeepAfterSummary { get; }

    public int SummaryMaxCharacters { get; }
}

public sealed record MemoryRetentionOptions
{
    public MemoryRetentionOptions(
        TimeSpan? defaultTimeToLive = null,
        bool slidingExpiration = false,
        bool removeExpiredOnAccess = true)
    {
        if (defaultTimeToLive is not null && defaultTimeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTimeToLive), "Default TTL must be positive when provided.");
        }

        DefaultTimeToLive = defaultTimeToLive;
        SlidingExpiration = slidingExpiration;
        RemoveExpiredOnAccess = removeExpiredOnAccess;
    }

    public TimeSpan? DefaultTimeToLive { get; }

    public bool SlidingExpiration { get; }

    public bool RemoveExpiredOnAccess { get; }
}

public sealed record MemoryOrchestrationOptions
{
    public MemoryOrchestrationOptions(
        MemoryFailureMode failureMode = MemoryFailureMode.FailClosed,
        MemoryWindowOptions? window = null)
    {
        FailureMode = failureMode;
        Window = window ?? new MemoryWindowOptions();
    }

    public MemoryFailureMode FailureMode { get; }

    public MemoryWindowOptions Window { get; }
}

public enum MemoryFailureMode
{
    FailClosed,
    ContinueWithoutMemory
}
