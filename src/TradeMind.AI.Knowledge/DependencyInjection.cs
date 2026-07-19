using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradeMind.AI.Application;

namespace TradeMind.AI.Knowledge;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindKnowledgeRag(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IKnowledgeTokenEstimator, CharacterKnowledgeTokenEstimator>();
        services.TryAddTransient<IKnowledgeSearcher, KnowledgeHubServiceSearcher>();
        services.TryAddTransient<IKnowledgeContextRetriever, KnowledgeContextRetriever>();
        services.TryAddTransient<IKnowledgeContextComposer, KnowledgeContextComposer>();
        services.AddTransient<IAIOrchestrationStep, KnowledgeRetrievalStep>();

        return services;
    }
}
