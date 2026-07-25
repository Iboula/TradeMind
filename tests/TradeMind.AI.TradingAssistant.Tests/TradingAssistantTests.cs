using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace TradeMind.AI.TradingAssistant.Tests;

public sealed class TradingAssistantTests
{
    private readonly DeterministicTradingAssistantIntentClassifier _classifier = new();

    [Theory]
    [InlineData("What is the risk?", TradingAssistantIntent.Risk)]
    [InlineData("Quel est le risque?", TradingAssistantIntent.Risk)]
    [InlineData("Montre l'entrée et le stop", TradingAssistantIntent.Plan)]
    [InlineData("Explique la décision", TradingAssistantIntent.Decision)]
    [InlineData("What should I do next?", TradingAssistantIntent.NextActions)]
    [InlineData("Show warnings and errors", TradingAssistantIntent.Issues)]
    [InlineData("Pourquoi ce setup est-il bloqué?", TradingAssistantIntent.Explain)]
    [InlineData("Give me a summary", TradingAssistantIntent.Summary)]
    public void Classify_WhenQuestionProvided_ReturnsExpectedIntent(
        string question,
        TradingAssistantIntent expected)
    {
        var result = _classifier.Classify(question);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Classify_WhenQuestionIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => _classifier.Classify(" "));
    }

    [Fact]
    public void AddTradingAssistant_RegistersPublicContracts()
    {
        var services = new ServiceCollection();
        services.AddTradingAssistant();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<DeterministicTradingAssistantIntentClassifier>(
            provider.GetRequiredService<ITradingAssistantIntentClassifier>());
        Assert.IsType<DeterministicTradingAssistant>(
            provider.GetRequiredService<ITradingAssistant>());
        Assert.NotNull(provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void Request_without_correlation_id_is_deterministic()
    {
        var workspace = TradingAssistantTestData.Workspace;

        var first = new TradingAssistantRequest(workspace, "Give me a summary", "en");
        var second = new TradingAssistantRequest(workspace, "Give me a summary", "en");

        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(32, first.CorrelationId.Length);
    }

    [Fact]
    public async Task Answer_uses_fixed_time_workspace_facts_and_citations()
    {
        var now = TradingAssistantTestData.Now;
        var assistant = new DeterministicTradingAssistant(
            _classifier,
            NullLogger<DeterministicTradingAssistant>.Instance,
            new FixedTimeProvider(now));
        var response = await assistant.AnswerAsync(
            new TradingAssistantRequest(TradingAssistantTestData.Workspace, "Give me a summary", "en"));

        Assert.Equal(TradingAssistantIntent.Summary, response.Intent);
        Assert.Equal(now, response.GeneratedAtUtc);
        Assert.Contains("Workspace summary", response.Answer, StringComparison.Ordinal);
        Assert.Contains(response.Citations, citation => citation.Source == "workspace-trace");
        Assert.Contains(response.Citations, citation => citation.Source == "workspace");
        Assert.Contains("non-executable", response.Disclaimer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answer_supports_french_and_preserves_explicit_correlation()
    {
        var assistant = new DeterministicTradingAssistant(
            _classifier,
            NullLogger<DeterministicTradingAssistant>.Instance,
            new FixedTimeProvider(TradingAssistantTestData.Now));
        var response = await assistant.AnswerAsync(
            new TradingAssistantRequest(TradingAssistantTestData.Workspace, "Quel est le risque?", "fr", "corr-1"));

        Assert.Equal(TradingAssistantIntent.Risk, response.Intent);
        Assert.Equal("corr-1", response.CorrelationId);
        Assert.Contains("Évaluation du risque", response.Answer, StringComparison.Ordinal);
        Assert.Contains("Réponse informative", response.Disclaimer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answer_propagates_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new DeterministicTradingAssistant(
                _classifier,
                NullLogger<DeterministicTradingAssistant>.Instance,
                new FixedTimeProvider(TradingAssistantTestData.Now))
            .AnswerAsync(
                new TradingAssistantRequest(TradingAssistantTestData.Workspace, "summary"),
                cancellation.Token));
    }

    [Fact]
    public void Response_collections_are_defensively_copied()
    {
        var facts = new List<string> { "fact" };
        var recommendations = new List<TradingAssistantRecommendation>
        {
            new("NEXT", "next", false)
        };
        var citations = new List<TradingAssistantCitation>
        {
            new("source", "value")
        };
        var response = new TradingAssistantResponse(
            TradingAssistantIntent.Summary,
            "answer",
            facts,
            recommendations,
            citations,
            "disclaimer",
            "corr",
            TradingAssistantTestData.Now);

        facts.Clear();
        recommendations.Clear();
        citations.Clear();

        Assert.Single(response.Facts);
        Assert.Single(response.Recommendations);
        Assert.Single(response.Citations);
    }

    [Fact]
    public void Assembly_has_no_broker_http_or_llm_dependency()
    {
        var names = typeof(ITradingAssistant).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(names, name => name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Http", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("MarketConnector", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal static class TradingAssistantTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
    public static readonly TradingWorkspaceResult Workspace = BuildWorkspace();

    private static TradingWorkspaceResult BuildWorkspace() => new(
        new WorkspaceId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        TradingWorkspaceStatus.Succeeded,
        TradingWorkspaceState.PlanReady,
        TradingWorkspaceBuildMode.Snapshot,
        null,
        null,
        null,
        new WorkspaceContextSummary(null),
        new WorkspaceAnalysisSummary(null, null, []),
        new WorkspaceConsensusSummary(null),
        new WorkspaceDecisionSummary(null),
        new WorkspaceRiskSummary(null),
        new WorkspacePlanSummary(null, Now),
        new WorkspacePipelineProgress([]),
        new WorkspaceCompleteness(0, 1, 0, []),
        new WorkspaceFreshness(Now, null, null, null, null, null, null, null, null, false, false, false, false, false, false),
        new WorkspaceConsistency([]),
        [],
        [],
        [],
        [],
        [],
        [new WorkspaceTraceReference("workspace-trace", "trace-1", "fact", "value", Now)],
        [],
        [],
        [],
        "Synthetic workspace summary.",
        Now,
        Now);
}
