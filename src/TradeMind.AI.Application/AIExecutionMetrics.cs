namespace TradeMind.AI.Application;

public sealed record AIExecutionMetrics
{
    public AIExecutionMetrics(DateTimeOffset startedAtUtc)
    {
        if (startedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Execution start date must be UTC.", nameof(startedAtUtc));
        }

        StartedAtUtc = startedAtUtc;
    }

    public DateTimeOffset StartedAtUtc { get; private init; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public TimeSpan? TotalDuration => CompletedAtUtc - StartedAtUtc;

    public TimeSpan? ProviderDuration { get; private set; }

    public TimeSpan? PromptConstructionDuration { get; private set; }

    public int? InputTokens { get; private set; }

    public int? OutputTokens { get; private set; }

    public int? TotalTokens { get; private set; }

    public string? ProviderName { get; private set; }

    public string? ModelName { get; private set; }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        if (CompletedAtUtc is not null)
        {
            throw new InvalidOperationException("Execution metrics cannot be completed twice.");
        }

        if (completedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Execution completion date must be UTC.", nameof(completedAtUtc));
        }

        if (completedAtUtc < StartedAtUtc)
        {
            throw new ArgumentException("Execution completion date cannot be earlier than the start date.", nameof(completedAtUtc));
        }

        CompletedAtUtc = completedAtUtc;
    }

    public void RecordProviderDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Provider duration cannot be negative.");
        }

        ProviderDuration = duration;
    }

    public void RecordPromptConstructionDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Prompt construction duration cannot be negative.");
        }

        PromptConstructionDuration = duration;
    }

    public void RecordProvider(string providerName, string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        ProviderName = providerName;
        ModelName = modelName;
    }

    public void RecordTokens(int? inputTokens, int? outputTokens, int? totalTokens)
    {
        ValidateToken(inputTokens, nameof(inputTokens));
        ValidateToken(outputTokens, nameof(outputTokens));
        ValidateToken(totalTokens, nameof(totalTokens));

        var resolvedTotal = totalTokens;
        if (inputTokens is not null && outputTokens is not null)
        {
            var expectedTotal = inputTokens.Value + outputTokens.Value;
            if (totalTokens is not null && totalTokens.Value != expectedTotal)
            {
                throw new ArgumentException("Total tokens must equal input tokens plus output tokens when all values are known.", nameof(totalTokens));
            }

            resolvedTotal = expectedTotal;
        }

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = resolvedTotal;
    }

    private static void ValidateToken(int? value, string parameterName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Token values cannot be negative.");
        }
    }
}
