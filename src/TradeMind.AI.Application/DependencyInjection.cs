using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TradeMind.AI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindAIOrchestration(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<IAISessionFactory, AISessionFactory>();
        services.AddTransient<IAIOrchestrator, AIOrchestrator>();
        services.AddTransient<AIOrchestrationRequestValidator>();
        services.AddTransient<IPromptBuilder, PromptBuilder>();
        services.AddTransient<IAIOrchestrationStep, RequestValidationStep>();
        services.AddTransient<IAIOrchestrationStep, PromptConstructionStep>();
        services.AddTransient<IAIOrchestrationStep, ProviderCapabilityValidationStep>();
        services.AddTransient<IAIOrchestrationStep, ProviderExecutionStep>();
        services.AddTransient<IAIOrchestrationStep, ResponseNormalizationStep>();

        return services;
    }
}
