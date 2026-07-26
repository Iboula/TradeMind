using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Agents;
using TradeMind.AI.Application;
using TradeMind.AI.Tools;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradingCoachDependencyInjectionTests
{
    [Fact]
    public void Case121_ResolvesTradingCoachService()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingCoachService>());
    }

    [Fact]
    public void Case122_ResolvesValidator()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalValidator>());
    }

    [Fact]
    public void Case123_ResolvesNormalizer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalNormalizer>());
    }

    [Fact]
    public void Case124_ResolvesMetricsCalculator()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradeMetricsCalculator>());
    }

    [Fact]
    public void Case125_ResolvesRuleAnalyzer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingCoachRuleAnalyzer>());
    }

    [Fact]
    public void Case126_ResolvesResponseParser()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingCoachResponseParser>());
    }

    [Fact]
    public void Case127_ResolvesAnalysisMerger()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingCoachAnalysisMerger>());
    }

    [Fact]
    public void Case128_ResolvesSafetyFilter()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingCoachSafetyFilter>());
    }

    [Fact]
    public async Task Case129_IsCompatibleWithAddTradeMindAiAgents()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindAIAgents();
        services.AddTradeMindTradingCoaching();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var registry = provider.GetRequiredService<IAIAgentRegistry>();
        var agent = await registry.GetAsync(
            new AIAgentId(TradingCoachConstants.AgentId),
            AIAgentVersionSelection.LatestStable,
            null,
            CancellationToken.None);
        Assert.Equal("1.0.0", agent.Definition.Version.ToString());
    }

    [Fact]
    public void Case130_IsCompatibleWithAiOrchestrator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider, FakeChatProvider>();
        services.AddSingleton<IAIProviderMetadata, FakeProviderMetadata>();
        services.AddTradeMindAIOrchestration();
        services.AddTradeMindTradingCoaching();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        Assert.NotNull(provider.GetRequiredService<IAIOrchestrator>());
        Assert.NotNull(provider.GetRequiredService<ITradingCoachService>());
    }

    [Fact]
    public void Case131_WorksWithoutToolEngine()
    {
        using var provider = CreateProvider();
        Assert.Empty(provider.GetServices<IAITool>());
        Assert.NotNull(provider.GetRequiredService<ITradingCoachService>());
    }

    [Fact]
    public void Case132_IsProviderAgnostic()
    {
        var references = typeof(ITradingCoachService).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("OpenAI", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Case133_HasNoHttpContextDependency()
    {
        Assert.DoesNotContain(PublicContractTypes(), type => type.FullName?.Contains("HttpContext", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Case134_HasNoClaimsPrincipalDependency()
    {
        Assert.DoesNotContain(PublicContractTypes(), type => type == typeof(ClaimsPrincipal));
        Assert.DoesNotContain(typeof(TradingCoachService).GetConstructors().SelectMany(item => item.GetParameters()),
            parameter => parameter.ParameterType == typeof(ClaimsPrincipal));
    }

    [Fact]
    public void Case135_HasNoConcreteSdkDependency()
    {
        var forbidden = new[] { "OpenAI", "Npgsql", "Pgvector", "EntityFrameworkCore", "AspNetCore" };
        var references = typeof(ITradingCoachService).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => forbidden.Any(prefix =>
            reference.Name?.Contains(prefix, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAIAgentExecutor>(new StubAgentExecutor());
        services.AddTradeMindTradingCoaching();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IEnumerable<Type> PublicContractTypes() => typeof(ITradingCoachService).Assembly
        .GetExportedTypes()
        .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType)
            .Concat(type.GetProperties().Select(property => property.PropertyType)));

    private sealed class FakeChatProvider : IChatProvider
    {
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ChatResponse("{}", "Fake", "fake-model", null, null, TradingCoachTestData.Now));
    }

    private sealed class FakeProviderMetadata : IAIProviderMetadata
    {
        public string ProviderName => "Fake";
        public AIProviderCapabilities Capabilities { get; } = new(true, false, false, false, false);
    }
}
