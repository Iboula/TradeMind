namespace TradeMind.AI.Application;

public sealed record AIExecutionError
{
    public AIExecutionError(
        string errorCode,
        string message,
        DateTimeOffset occurredAtUtc,
        string? stepName = null,
        string? providerName = null,
        bool? isTransient = null,
        string? exceptionType = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Error date must be UTC.", nameof(occurredAtUtc));
        }

        ErrorCode = errorCode;
        Message = message;
        StepName = string.IsNullOrWhiteSpace(stepName) ? null : stepName;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? null : providerName;
        OccurredAtUtc = occurredAtUtc;
        IsTransient = isTransient;
        ExceptionType = string.IsNullOrWhiteSpace(exceptionType) ? null : exceptionType;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    public string ErrorCode { get; }

    public string Message { get; }

    public string? StepName { get; }

    public string? ProviderName { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public bool? IsTransient { get; }

    public string? ExceptionType { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public static AIExecutionError FromException(
        Exception exception,
        DateTimeOffset occurredAtUtc,
        string? stepName,
        string? providerName = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new AIExecutionError(
            exception is AIOrchestrationValidationException ? "AI_VALIDATION_FAILED" : "AI_EXECUTION_FAILED",
            exception.Message,
            occurredAtUtc,
            stepName,
            providerName,
            exception is AIProviderCapabilityException ? false : null,
            exception.GetType().Name);
    }
}
