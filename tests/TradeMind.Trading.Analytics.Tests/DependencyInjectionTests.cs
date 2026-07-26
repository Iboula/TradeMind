using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using TradeMind.AI.Agents;
using TradeMind.AI.Tools;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void Case151_ResolvesAnalyticsService()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalAnalyticsService>());
    }

    [Fact]
    public void Case152_ResolvesValidator()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalAnalyticsValidator>());
    }

    [Fact]
    public void Case153_ResolvesNormalizer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalCollectionNormalizer>());
    }

    [Fact]
    public void Case154_ResolvesStatisticsCalculator()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingStatisticsCalculator>());
    }

    [Fact]
    public void Case155_ResolvesBehaviorAnalyzer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingBehaviorTrendAnalyzer>());
    }

    [Fact]
    public void Case156_ResolvesRiskDriftAnalyzer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IRiskDriftAnalyzer>());
    }

    [Fact]
    public void Case157_ResolvesPostOutcomeAnalyzer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<IPostOutcomeBehaviorAnalyzer>());
    }

    [Fact]
    public void Case158_ResolvesRuleAnalyzer()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalRuleAnalyzer>());
    }

    [Fact]
    public void Case159_ResolvesParser()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalAnalyticsResponseParser>());
    }

    [Fact]
    public void Case160_ResolvesMerger()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingJournalAnalyticsMerger>());
    }

    [Fact]
    public async Task Case161_IsCompatibleWithAgents()
    {
        using var provider = CreateProvider();
        var agent = await provider.GetRequiredService<IAIAgentRegistry>().GetAsync(
            new AIAgentId(TradingJournalAnalyticsConstants.AgentId),
            AIAgentVersionSelection.LatestStable,
            null,
            CancellationToken.None);
        Assert.Equal("1.0.0", agent.Definition.Version.ToString());
    }

    [Fact]
    public void Case162_IsCompatibleWithCoaching()
    {
        using var provider = CreateProvider();
        Assert.NotNull(provider.GetRequiredService<ITradingCoachService>());
        Assert.NotNull(provider.GetRequiredService<ITradingJournalAnalyticsService>());
    }

    [Fact]
    public void Case163_WorksWithoutTools()
    {
        using var provider = CreateProvider();
        Assert.Empty(provider.GetServices<IAITool>());
        Assert.NotNull(provider.GetRequiredService<ITradingJournalAnalyticsService>());
    }

    [Fact]
    public void Case164_IsProviderAgnostic()
    {
        var references = typeof(ITradingJournalAnalyticsService).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("OpenAI", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Case165_HasNoHttpContextDependency()
    {
        Assert.DoesNotContain(PublicContractTypes(), type => type.FullName?.Contains("HttpContext", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Case166_HasNoClaimsPrincipalDependency()
    {
        Assert.DoesNotContain(PublicContractTypes(), type => type == typeof(ClaimsPrincipal));
        Assert.DoesNotContain(typeof(TradingJournalAnalyticsService).GetConstructors().SelectMany(item => item.GetParameters()),
            parameter => parameter.ParameterType == typeof(ClaimsPrincipal));
    }

    [Fact]
    public void Case167_HasNoConcreteSdkDependency()
    {
        var forbidden = new[] { "OpenAI", "Npgsql", "Pgvector", "EntityFrameworkCore", "AspNetCore" };
        var references = typeof(ITradingJournalAnalyticsService).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => forbidden.Any(prefix =>
            reference.Name?.Contains(prefix, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAIAgentExecutor>(new StubAgentExecutor());
        services.AddTradeMindTradingAnalytics();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IEnumerable<Type> PublicContractTypes() => typeof(ITradingJournalAnalyticsService).Assembly
        .GetExportedTypes()
        .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType)
            .Concat(type.GetProperties().Select(property => property.PropertyType)));
}
