using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Tests;

public sealed class ContextSizeAndImmutabilityTests
{
    [Fact]
    public void SizePolicy_ShouldLimitKnowledgeChunkCount()
    {
        var result = Policy(new ContextEngineOptions { MaximumKnowledgeChunks = 2 }).Normalize([
            KnowledgeData("one", "two", "three")
        ]);

        Assert.Equal(2, result.Knowledge?.Context.Chunks.Count);
    }

    [Fact]
    public void SizePolicy_ShouldLimitTotalKnowledgeText()
    {
        var result = Policy(new ContextEngineOptions { MaximumKnowledgeCharacters = 5 }).Normalize([
            KnowledgeData("abcd", "efgh")
        ]);

        Assert.Equal(5, result.Knowledge?.Context.Chunks.Sum(chunk => chunk.Content.Length));
        Assert.Equal("e", result.Knowledge?.Context.Chunks[1].Content);
    }

    [Fact]
    public void SizePolicy_ShouldKeepNewestMemoryWithinLimit()
    {
        var memory = new MemoryContextData(new MemoryContext(
            "conversation",
            null,
            [
                Item(1, "one"),
                Item(2, "two"),
                Item(3, "three")
            ],
            false,
            3));

        var result = Policy(new ContextEngineOptions { MaximumMemoryItems = 2 }).Normalize([memory]);

        Assert.Equal([2L, 3L], result.Memory?.Context.Items.Select(item => item.Sequence));
    }

    [Fact]
    public void SizePolicy_ShouldLimitMemoryCharacters()
    {
        var memory = new MemoryContextData(new MemoryContext(
            "conversation",
            null,
            [Item(1, "abcd"), Item(2, "efgh")],
            false,
            2));

        var result = Policy(new ContextEngineOptions { MaximumMemoryCharacters = 5 }).Normalize([memory]);

        Assert.Equal(5, result.Memory?.Context.Items.Sum(item => item.Content.Length));
    }

    [Fact]
    public void SizePolicy_ShouldIncludeSummaryInMemoryCharacterLimit()
    {
        var memory = new MemoryContextData(new MemoryContext(
            "conversation",
            "summary-text",
            [Item(1, "entry")],
            false,
            1));

        var result = Policy(new ContextEngineOptions { MaximumMemoryCharacters = 7 }).Normalize([memory]);

        Assert.Equal("summary", result.Memory?.Context.Summary);
        Assert.Empty(result.Memory?.Context.Items ?? []);
        Assert.True(result.Memory?.Context.Truncated);
    }

    [Fact]
    public void SizePolicy_ShouldEmitDeterministicTruncationWarnings()
    {
        var result = Policy(new ContextEngineOptions { MaximumKnowledgeChunks = 1 }).Normalize([
            KnowledgeData("one", "two")
        ]);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal("knowledge-truncated", warning.Code);
        Assert.True(result.Knowledge?.Context.Truncated);
    }

    [Fact]
    public void KnowledgeContext_ShouldDefensivelyCopyChunks()
    {
        var chunks = new List<KnowledgeChunk>
        {
            new("fragment", "source", "content", 1, 0)
        };
        var context = new KnowledgeContext("query", chunks, ContextTestData.Now, false, 1);

        chunks.Clear();

        Assert.Single(context.Chunks);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<KnowledgeChunk>)context.Chunks).Add(new KnowledgeChunk("x", "y", "z", 1, 0)));
    }

    [Fact]
    public async Task MarketContext_ShouldExposeImmutableCollections()
    {
        var result = await ContextTestData.Builder([ContextTestData.MarketProvider()])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);
        var context = Assert.IsType<MarketContext>(result.Context);

        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContextSourceTrace>)context.Traces).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContextBuildWarning>)context.Warnings).Add(
                new ContextBuildWarning("x", "y")));
    }

    [Fact]
    public void DomainAssembly_ShouldNotReferenceTechnicalModules()
    {
        var references = typeof(MarketContext).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("KnowledgeHub", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("MarketConnectors", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Http", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NeutralSourceContracts_ShouldReturnNormalAbsence()
    {
        Assert.Equal(typeof(Task<TraderProfileContext?>),
            typeof(ITraderProfileContextSource).GetMethod("GetAsync")?.ReturnType);
        Assert.Equal(typeof(Task<WorkspaceContext?>),
            typeof(IWorkspaceContextSource).GetMethod("GetAsync")?.ReturnType);
        Assert.Equal(typeof(Task<NewsContext?>),
            typeof(INewsContextSource).GetMethod("GetAsync")?.ReturnType);
        Assert.Equal(typeof(Task<EconomicCalendarContext?>),
            typeof(IEconomicCalendarContextSource).GetMethod("GetAsync")?.ReturnType);
    }

    [Fact]
    public void SizePolicy_ShouldRespectMaximumWarningCount()
    {
        var result = Policy(new ContextEngineOptions
        {
            MaximumKnowledgeChunks = 1,
            MaximumWarnings = 1
        }).Normalize([
            KnowledgeData("one", "two", "three"),
            new MemoryContextData(new MemoryContext(
                "conversation",
                null,
                [Item(1, "one"), Item(2, "two")],
                false,
                2))
        ]);

        Assert.Single(result.Warnings);
    }

    [Fact]
    public void SizePolicy_ShouldLimitNewsAndCalendarItems()
    {
        var result = Policy(new ContextEngineOptions
        {
            MaximumNewsItems = 1,
            MaximumCalendarItems = 1
        }).Normalize([
            new NewsContextData(new NewsContext([
                new NewsItem("older", "Older", ContextTestData.Now.AddMinutes(-1)),
                new NewsItem("newer", "Newer", ContextTestData.Now)
            ])),
            new EconomicCalendarContextData(new EconomicCalendarContext([
                new EconomicCalendarEvent("late", "Late", ContextTestData.Now.AddHours(1)),
                new EconomicCalendarEvent("early", "Early", ContextTestData.Now)
            ]))
        ]);

        Assert.Equal("newer", result.News?.Context.Items.Single().Id);
        Assert.Equal("early", result.EconomicCalendar?.Context.Events.Single().Id);
        Assert.Contains(result.Warnings, warning => warning.Code == "news-truncated");
        Assert.Contains(result.Warnings, warning => warning.Code == "calendar-truncated");
    }

    [Fact]
    public void ApplicationAssembly_ShouldNotReferenceTechnicalSourceModules()
    {
        var references = typeof(MarketContextBuilder).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("KnowledgeHub", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("MarketConnectors", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Http", StringComparison.OrdinalIgnoreCase));
    }

    private static ContextSizePolicy Policy(ContextEngineOptions options) =>
        new(Options.Create(options));

    private static KnowledgeContextData KnowledgeData(params string[] values) => new(
        new KnowledgeContext(
            "query",
            values.Select((value, index) => new KnowledgeChunk(
                $"fragment-{index}",
                "source",
                value,
                1,
                index)).ToArray(),
            ContextTestData.Now,
            false,
            values.Length));

    private static MemoryItem Item(long sequence, string content) => new(
        sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "User",
        content,
        ContextTestData.Now,
        sequence);
}
