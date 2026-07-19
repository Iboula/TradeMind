namespace TradeMind.AI.Application;

public sealed class AIOrchestrationRequestValidator
{
    private const int MaximumCorrelationIdLength = 128;
    private const float MinimumTemperature = 0;
    private const float MaximumTemperature = 2;

    public void Validate(AIOrchestrationRequest request, string correlationId, string stepName)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.UserMessage))
        {
            errors.Add("UserMessage is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Scenario))
        {
            errors.Add("Scenario is required.");
        }

        if (request.Temperature is < MinimumTemperature or > MaximumTemperature)
        {
            errors.Add("Temperature must be between 0 and 2 when provided.");
        }

        if (request.MaxOutputTokens is <= 0)
        {
            errors.Add("MaxOutputTokens must be greater than 0 when provided.");
        }

        if (request.CorrelationId is { Length: > MaximumCorrelationIdLength })
        {
            errors.Add($"CorrelationId must be {MaximumCorrelationIdLength} characters or fewer.");
        }

        if (request.SessionId is { Length: > MaximumCorrelationIdLength })
        {
            errors.Add($"SessionId must be {MaximumCorrelationIdLength} characters or fewer.");
        }

        if (request.ConversationId is { Length: > MaximumCorrelationIdLength })
        {
            errors.Add($"ConversationId must be {MaximumCorrelationIdLength} characters or fewer.");
        }

        if (errors.Count > 0)
        {
            throw new AIOrchestrationValidationException(errors, correlationId, stepName);
        }
    }
}
