namespace TradeMind.AI.Application;

public sealed record AIMemoryWindowOptions
{
    public AIMemoryWindowOptions(
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

        if (recentUserMessagesMinimum < 0 || recentAssistantMessagesMinimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentUserMessagesMinimum), "Recent message minimums cannot be negative.");
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

public sealed record AIMemoryOptions
{
    public static AIMemoryOptions Disabled { get; } = new();

    public AIMemoryOptions(
        bool enabled = false,
        AIMemoryWindowOptions? window = null,
        bool saveUserMessage = true,
        bool saveAssistantResponse = true,
        bool compactionEnabled = true,
        AIMemoryFailureMode failureMode = AIMemoryFailureMode.FailClosed)
    {
        Enabled = enabled;
        Window = window ?? new AIMemoryWindowOptions();
        SaveUserMessage = saveUserMessage;
        SaveAssistantResponse = saveAssistantResponse;
        CompactionEnabled = compactionEnabled;
        FailureMode = failureMode;
    }

    public bool Enabled { get; }

    public AIMemoryWindowOptions Window { get; }

    public bool SaveUserMessage { get; }

    public bool SaveAssistantResponse { get; }

    public bool CompactionEnabled { get; }

    public AIMemoryFailureMode FailureMode { get; }
}

public enum AIMemoryFailureMode
{
    FailClosed,
    ContinueWithoutMemory
}
