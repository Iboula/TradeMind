using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.ExpertAgents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindExpertAgents(
        this IServiceCollection services,
        Action<ExpertAgentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);

        var options = services.AddOptions<ExpertAgentOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ExpertAgentOptions>, ExpertAgentOptionsValidator>());
        services.TryAddSingleton<IExpertAgentRegistry, ImmutableExpertAgentRegistry>();
        services.TryAddSingleton<IAgentCompatibilityPolicy, DefaultAgentCompatibilityPolicy>();
        services.TryAddSingleton<IExpertAgentAuthorizationPolicy, DefaultExpertAgentAuthorizationPolicy>();
        services.TryAddSingleton<IAgentAnalysisResultValidator, AgentAnalysisResultValidator>();
        services.TryAddScoped<IExpertAgentExecutor, ExpertAgentExecutor>();
        return services;
    }
}
