using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.ExpertAgents.Consensus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindConsensus(
        this IServiceCollection services,
        Action<ConsensusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var options = services.AddOptions<ConsensusOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ConsensusOptions>, ConsensusOptionsValidator>());
        services.TryAddSingleton<IConsensusEligibilityPolicy, DefaultConsensusEligibilityPolicy>();
        services.TryAddSingleton<IConsensusWeightingPolicy, DefaultConsensusWeightingPolicy>();
        services.TryAddSingleton<IConsensusConflictPolicy, DefaultConsensusConflictPolicy>();
        services.TryAddScoped<IConsensusEngine, ConsensusEngine>();
        return services;
    }
}
