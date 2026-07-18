using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class ProviderCapabilityValidationStep : IAIOrchestrationStep
{
    private readonly IAIProviderMetadata _providerMetadata;

    public ProviderCapabilityValidationStep(IAIProviderMetadata providerMetadata)
    {
        _providerMetadata = providerMetadata;
    }

    public int Order => 300;

    public Task ExecuteAsync(
        AIOrchestrationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_providerMetadata.Capabilities.SupportsChat)
        {
            throw new AIProviderCapabilityException(
                _providerMetadata.ProviderName,
                $"AI provider '{_providerMetadata.ProviderName}' does not support chat completion.",
                context.CorrelationId,
                nameof(ProviderCapabilityValidationStep));
        }

        return Task.CompletedTask;
    }
}
