using Microsoft.Extensions.Logging.Abstractions;
using TradeMind.AI.Application;
using TradeMind.AI.Knowledge;
using TradeMind.KnowledgeHub.Application;
using TradeMind.KnowledgeHub.Domain;
using TradeMind.KnowledgeHub.Infrastructure;

namespace TradeMind.KnowledgeHub.IntegrationTests;

public sealed class KnowledgeRagIntegrationTests(PostgreSqlKnowledgeHubFixture fixture)
    : IClassFixture<PostgreSqlKnowledgeHubFixture>
{
    [Fact]
    public async Task Retriever_ShouldSearchPostgreSqlPgvectorAndMapCitations()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new PostgreSqlKnowledgeSourceRepository(dbContext);
        var embeddingGenerator = new DeterministicEmbeddingGenerator();
        var source = new KnowledgeSource("Liquidity playbook", KnowledgeSourceType.Text, Hash('R'));
        source.StartProcessing();
        source.AddFragment(
            0,
            "liquidity sweep rules and confirmation checklist",
            6,
            await embeddingGenerator.GenerateAsync("liquidity sweep rules and confirmation checklist", CancellationToken.None));
        source.AddFragment(
            1,
            "risk management notes for maximum daily loss",
            7,
            await embeddingGenerator.GenerateAsync("risk management notes for maximum daily loss", CancellationToken.None));
        source.MarkReady();

        await repository.AddAsync(source, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var service = new KnowledgeHubService(
            new PlainTextExtractor(),
            new SlidingWindowFragmenter(),
            embeddingGenerator,
            repository);
        var retriever = new KnowledgeContextRetriever(
            new KnowledgeHubServiceSearcher(service),
            new CharacterKnowledgeTokenEstimator(),
            TimeProvider.System,
            NullLogger<KnowledgeContextRetriever>.Instance);

        var result = await retriever.RetrieveAsync(
            new KnowledgeContextRequest(
                "liquidity sweep rules",
                "session",
                "corr",
                "Scenario",
                2,
                0,
                2000,
                null,
                KnowledgeOrderingStrategy.RelevanceDescending),
            CancellationToken.None);

        Assert.NotEmpty(result.Fragments);
        Assert.Equal(source.Id, result.Fragments[0].SourceId);
        Assert.Contains("liquidity", result.Fragments[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("K1", result.Citations[0].CitationId);
        Assert.True(result.RetrievalDuration >= TimeSpan.Zero);
    }

    private static string Hash(char value)
    {
        return new string(value, 64);
    }
}
