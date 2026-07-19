using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.Tools;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindAITools(
        this IServiceCollection services,
        Action<AIToolEngineOptions>? configure = null)
    {
        services.TryAddSingleton(TimeProvider.System);

        var optionsBuilder = services.AddOptions<AIToolEngineOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AIToolEngineOptions>, AIToolEngineOptionsValidator>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAITool, EchoAITool>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAITool, AddNumbersAITool>());
        services.TryAddSingleton<IAIToolRegistry, InMemoryAIToolRegistry>();
        services.TryAddSingleton<IAIToolAuthorizer, PermissionBasedAIToolAuthorizer>();
        services.TryAddSingleton<IAIToolArgumentValidator, AIToolArgumentValidator>();
        services.TryAddSingleton<IAIToolExecutor, AIToolExecutor>();
        services.TryAddSingleton<IAIToolResultComposer, AIToolResultComposer>();

        return services;
    }
}
