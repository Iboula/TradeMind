namespace TradeMind.AI.Application;

public class AIOrchestrationException : InvalidOperationException
{
    public AIOrchestrationException(
        string message,
        string? correlationId = null,
        string? stepName = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        CorrelationId = correlationId;
        StepName = stepName;
    }

    public string? CorrelationId { get; }

    public string? StepName { get; }
}

public sealed class AIProviderCapabilityException : AIOrchestrationException
{
    public AIProviderCapabilityException(
        string providerName,
        string message,
        string? correlationId = null,
        string? stepName = null)
        : base(message, correlationId, stepName)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }
}

public sealed class AIOrchestrationValidationException : AIOrchestrationException
{
    public AIOrchestrationValidationException(
        IReadOnlyList<string> errors,
        string? correlationId = null,
        string? stepName = null)
        : base("The AI orchestration request is invalid.", correlationId, stepName)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
