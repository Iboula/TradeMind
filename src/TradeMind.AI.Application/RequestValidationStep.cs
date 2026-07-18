namespace TradeMind.AI.Application;

public sealed class RequestValidationStep : IAIOrchestrationStep
{
    private readonly AIOrchestrationRequestValidator _validator;

    public RequestValidationStep(AIOrchestrationRequestValidator validator)
    {
        _validator = validator;
    }

    public int Order => 100;

    public Task ExecuteAsync(
        AIOrchestrationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _validator.Validate(context.Request, context.CorrelationId, nameof(RequestValidationStep));
        return Task.CompletedTask;
    }
}
