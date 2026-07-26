using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class ProviderCapabilityValidationStep : IAIOrchestrationStep
{
    private readonly IAIProviderMetadata _providerMetadata;

    public ProviderCapabilityValidationStep(IAIProviderMetadata providerMetadata)
    {
        _providerMetadata = providerMetadata;
    }

    public string Name => AIOrchestrationStepNames.ProviderCapabilityValidation;

    public int Order => 300;

    public Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_providerMetadata.Capabilities.SupportsChat)
        {
            throw new AIProviderCapabilityException(
                _providerMetadata.ProviderName,
                $"AI provider '{_providerMetadata.ProviderName}' does not support chat completion.",
                context.Session.CorrelationId,
                Name);
        }

        return Task.CompletedTask;
    }
}
