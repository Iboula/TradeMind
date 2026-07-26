using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Application;

namespace TradeMind.AI.ExpertAgents.Dispatch.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindExpertAgentDispatcher(
        this IServiceCollection services,
        Action<AgentDispatchOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTradeMindExpertAgents();
        services.TryAddSingleton(TimeProvider.System);

        var options = services.AddOptions<AgentDispatchOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AgentDispatchOptions>, AgentDispatchOptionsValidator>());
        services.TryAddSingleton<IAnalysisIntentClassifier, RuleBasedAnalysisIntentClassifier>();
        services.TryAddSingleton<IAgentRelevancePolicy, DefaultAgentRelevancePolicy>();
        services.TryAddSingleton<IAgentCostPolicy, DefaultAgentCostPolicy>();
        services.TryAddSingleton<IAgentDispatchBudgetPolicy, DefaultAgentDispatchBudgetPolicy>();
        services.TryAddSingleton<IAgentDispatchVersionPolicy, DefaultAgentDispatchVersionPolicy>();
        services.TryAddSingleton<IAgentDispatchFallbackPolicy, DefaultAgentDispatchFallbackPolicy>();
        services.TryAddScoped<IAgentDispatchPlanner, AgentDispatchPlanner>();
        services.TryAddScoped<IExpertDispatcher, ExpertDispatcher>();
        return services;
    }
}
