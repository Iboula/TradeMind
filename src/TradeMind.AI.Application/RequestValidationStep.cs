namespace TradeMind.AI.Application;

public sealed class RequestValidationStep : IAIOrchestrationStep
{
    private readonly AIOrchestrationRequestValidator _validator;

    public RequestValidationStep(AIOrchestrationRequestValidator validator)
    {
        _validator = validator;
    }

    public string Name => AIOrchestrationStepNames.RequestValidation;

    public int Order => 100;

    public Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _validator.Validate(context.Request, context.Session.CorrelationId, Name);
        return Task.CompletedTask;
    }
}
