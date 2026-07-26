using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradeMind.AI.Application;

namespace TradeMind.AI.Memory;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindMemory(
        this IServiceCollection services,
        MemoryRetentionOptions? retentionOptions = null,
        MemoryCompactionOptions? compactionOptions = null,
        MemoryOrchestrationOptions? orchestrationOptions = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(retentionOptions ?? new MemoryRetentionOptions());
        services.TryAddSingleton(compactionOptions ?? new MemoryCompactionOptions());
        services.TryAddSingleton(orchestrationOptions ?? new MemoryOrchestrationOptions());
        services.TryAddSingleton<ITokenEstimator, CharacterBasedTokenEstimator>();
        services.TryAddSingleton<IMemoryStore, InMemoryMemoryStore>();
        services.TryAddSingleton<IConversationSummarizer, DeterministicConversationSummarizer>();
        services.TryAddTransient<IMemoryReader, MemoryReader>();
        services.TryAddTransient<IMemoryWriter, MemoryWriter>();
        services.AddTransient<IAIOrchestrationStep, MemoryReadStep>();
        services.AddTransient<IAIOrchestrationStep, MemoryWriteStep>();

        return services;
    }
}
