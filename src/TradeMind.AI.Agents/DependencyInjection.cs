using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Application;

namespace TradeMind.AI.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindAIAgents(
        this IServiceCollection services,
        Action<AIAgentFrameworkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);

        var optionsBuilder = services.AddOptions<AIAgentFrameworkOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AIAgentFrameworkOptions>, AIAgentFrameworkOptionsValidator>());

        if (!services.Any(IsTradingCoachTemplateRegistration))
        {
            services.AddSingleton(BuiltInAIAgentPromptTemplates.TradingCoach);
        }

        services.AddTradeMindPromptEngine();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIAgent, GenericAssistantAgent>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIAgent, TradingCoachAgent>());
        services.TryAddSingleton<IAIAgentRegistry, InMemoryAIAgentRegistry>();
        services.TryAddSingleton<IAIAgentAuthorizer, PolicyBasedAIAgentAuthorizer>();
        services.TryAddSingleton<IAIAgentRequestMapper, AIAgentRequestMapper>();
        services.TryAddSingleton<IAIAgentResponseMapper, AIAgentResponseMapper>();
        services.TryAddTransient<IAIAgentExecutor, AIAgentExecutor>();

        return services;
    }

    private static bool IsTradingCoachTemplateRegistration(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(PromptTemplate)
        && descriptor.ImplementationInstance is PromptTemplate template
        && template.Id == BuiltInAIAgentPromptTemplates.TradingCoach.Id
        && template.Version == BuiltInAIAgentPromptTemplates.TradingCoach.Version;
}
