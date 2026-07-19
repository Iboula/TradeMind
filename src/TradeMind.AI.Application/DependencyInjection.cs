using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TradeMind.AI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindPromptEngine(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        if (!services.Any(IsGenericChatTemplateRegistration))
        {
            services.AddSingleton(BuiltInPromptTemplates.GenericChat);
        }

        services.TryAddSingleton<IPromptTemplateRegistry, InMemoryPromptTemplateRegistry>();
        services.TryAddTransient<IPromptRenderer, PromptRenderer>();

        return services;
    }

    public static IServiceCollection AddTradeMindAIOrchestration(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddTradeMindPromptEngine();
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

    private static bool IsGenericChatTemplateRegistration(ServiceDescriptor descriptor)
    {
        return descriptor.ServiceType == typeof(PromptTemplate)
            && descriptor.ImplementationInstance is PromptTemplate template
            && template.Id == BuiltInPromptTemplates.GenericChat.Id
            && template.Version == BuiltInPromptTemplates.GenericChat.Version;
    }
}
