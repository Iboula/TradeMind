using Microsoft.Extensions.DependencyInjection;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.AI.Knowledge;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;
using TradeMind.KnowledgeHub.Application;

namespace TradeMind.AI.Tests;

public sealed class AIAgentDependencyInjectionTests
{
    [Fact]
    public void DependencyInjection_ResolvesRegistry()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IAIAgentRegistry>());
    }

    [Fact]
    public void DependencyInjection_ResolvesExecutor()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IAIAgentExecutor>());
    }

    [Fact]
    public void DependencyInjection_ResolvesAuthorizer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IAIAgentAuthorizer>());
    }

    [Fact]
    public void DependencyInjection_ResolvesRequestMapper()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IAIAgentRequestMapper>());
    }

    [Fact]
    public void DependencyInjection_ResolvesResponseMapper()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IAIAgentResponseMapper>());
    }

    [Fact]
    public void DependencyInjection_ResolvesGenericAssistant()
    {
        using var provider = CreateProvider();
        Assert.Contains(provider.GetServices<IAIAgent>(), agent => agent.Definition.Id.Value == "generic-assistant");
    }

    [Fact]
    public void DependencyInjection_ResolvesTradingCoach()
    {
        using var provider = CreateProvider();
        Assert.Contains(provider.GetServices<IAIAgent>(), agent => agent.Definition.Id.Value == "trading-coach");
    }

    [Fact]
    public void DependencyInjection_IsCompatibleWithOrchestrator()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IAIOrchestrator>());
    }

    [Fact]
    public async Task DependencyInjection_IsCompatibleWithPromptEngine()
    {
        using var provider = CreateProvider();
        var template = await provider.GetRequiredService<IPromptTemplateRegistry>().GetAsync(
            BuiltInAIAgentPromptTemplates.TradingCoach.Id,
            BuiltInAIAgentPromptTemplates.TradingCoach.Version,
            CancellationToken.None);
        Assert.Equal("Trading Coach Skeleton", template.Name);
    }

    [Fact]
    public void DependencyInjection_IsCompatibleWithMemoryEngine()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IMemoryStore>());
    }

    [Fact]
    public void DependencyInjection_IsCompatibleWithKnowledgeEngine()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IKnowledgeContextRetriever>());
    }

    [Fact]
    public void DependencyInjection_IsCompatibleWithToolEngine()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IAIToolRegistry>());
    }

    [Fact]
    public void AgentFramework_RemainsProviderAgnostic()
    {
        var forbiddenPrefixes = new[] { "OpenAI", "Npgsql", "Pgvector", "Microsoft.EntityFrameworkCore" };
        var references = typeof(IAIAgent).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => forbiddenPrefixes.Any(prefix =>
            reference.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public void Agents_DoNotReceiveServiceProvider()
    {
        var constructors = new[] { typeof(GenericAssistantAgent), typeof(TradingCoachAgent) }
            .SelectMany(type => type.GetConstructors());
        Assert.DoesNotContain(constructors.SelectMany(constructor => constructor.GetParameters()), parameter =>
            parameter.ParameterType == typeof(IServiceProvider));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider, ToolFakeChatProvider>();
        services.AddSingleton<IAIProviderMetadata, ToolFakeProviderMetadata>();
        services.AddSingleton<IKnowledgeSearcher, EmptyKnowledgeSearcher>();
        services.AddTradeMindAIOrchestration();
        services.AddTradeMindMemory();
        services.AddTradeMindKnowledgeRag();
        services.AddTradeMindAIToolOrchestration();
        services.AddTradeMindAIAgents(options => options.EnableDevelopmentAgents = true);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class EmptyKnowledgeSearcher : IKnowledgeSearcher
    {
        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>([]);
        }
    }
}
