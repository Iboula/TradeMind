namespace TradeMind.AI.Application;

public sealed class AISessionFactory : IAISessionFactory
{
    private readonly TimeProvider _timeProvider;

    public AISessionFactory(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public AISession Create(AIOrchestrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Scenario))
        {
            throw new AIOrchestrationValidationException(
                ["Scenario is required."],
                CreateIdentifier(request.CorrelationId),
                AIOrchestrationStepNames.RequestValidation);
        }

        var identity = request.Identity ?? AIIdentityContext.Empty;

        return new AISession(
            CreateIdentifier(request.SessionId),
            request.ConversationId,
            CreateIdentifier(request.CorrelationId),
            identity.TenantId,
            identity.UserId,
            identity.AgentId,
            request.Scenario,
            _timeProvider.GetUtcNow(),
            request.Metadata);
    }

    private static string CreateIdentifier(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value;
    }
}
