using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.Context.Infrastructure;
using TradeMind.KnowledgeHub.Domain;
using TradeMind.KnowledgeHub.Infrastructure;
using TradeMind.AI.Memory;

namespace TradeMind.AI.Context.IntegrationTests;

public sealed class PostgreSqlContextEngineTests(PostgreSqlContextFixture fixture)
    : IClassFixture<PostgreSqlContextFixture>
{
    [Fact]
    public async Task MarketProvider_ShouldBuildFromPersistedSnapshot()
    {
        var connector = Unique("market");
        await using var marketDb = fixture.CreateMarketDbContext();
        var snapshot = ContextIntegrationSupport.Snapshot(
            connector,
            ContextIntegrationSupport.Now.AddSeconds(-10));
        await ContextIntegrationSupport.PublishAsync(marketDb, snapshot);

        var result = await ContextIntegrationSupport.Builder([
            ContextIntegrationSupport.MarketProvider(marketDb)
        ]).BuildAsync(ContextIntegrationSupport.Query(connector), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Succeeded, result.Status);
        Assert.Equal(snapshot.Id, result.Context?.MarketSnapshot.Id);
    }

    [Fact]
    public async Task MarketProvider_ShouldReturnLatestPersistedSnapshot()
    {
        var connector = Unique("latest");
        await using var marketDb = fixture.CreateMarketDbContext();
        var older = ContextIntegrationSupport.Snapshot(
            connector,
            ContextIntegrationSupport.Now.AddMinutes(-2),
            1.1m);
        var latest = ContextIntegrationSupport.Snapshot(
            connector,
            ContextIntegrationSupport.Now.AddSeconds(-5),
            1.2m);
        await ContextIntegrationSupport.PublishAsync(marketDb, older);
        await ContextIntegrationSupport.PublishAsync(marketDb, latest);

        var result = await ContextIntegrationSupport.Builder([
            ContextIntegrationSupport.MarketProvider(marketDb)
        ]).BuildAsync(ContextIntegrationSupport.Query(connector), CancellationToken.None);

        Assert.Equal(latest.Id, result.Context?.MarketSnapshot.Id);
    }

    [Fact]
    public async Task MarketProvider_ShouldHonorMaximumAgeAgainstPostgreSql()
    {
        var connector = Unique("age");
        await using var marketDb = fixture.CreateMarketDbContext();
        await ContextIntegrationSupport.PublishAsync(
            marketDb,
            ContextIntegrationSupport.Snapshot(
                connector,
                ContextIntegrationSupport.Now.AddMinutes(-10)));

        var result = await ContextIntegrationSupport.Builder([
            ContextIntegrationSupport.MarketProvider(marketDb)
        ]).BuildAsync(
            ContextIntegrationSupport.Query(connector, maximumAge: TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.MissingRequiredSource);
    }

    [Fact]
    public async Task Builder_ShouldCombinePersistedMarketAndPgvectorKnowledge()
    {
        var connector = Unique("full");
        await using var marketDb = fixture.CreateMarketDbContext();
        await using var knowledgeDb = fixture.CreateKnowledgeDbContext();
        await ContextIntegrationSupport.PublishAsync(
            marketDb,
            ContextIntegrationSupport.Snapshot(
                connector,
                ContextIntegrationSupport.Now.AddSeconds(-5)));
        var source = await PersistKnowledgeAsync(
            knowledgeDb,
            "liquidity sweep confirmation checklist");

        var result = await ContextIntegrationSupport.Builder([
            ContextIntegrationSupport.MarketProvider(marketDb),
            ContextIntegrationSupport.KnowledgeProvider(knowledgeDb)
        ]).BuildAsync(
            ContextIntegrationSupport.Query(connector, knowledgeQuery: "liquidity sweep confirmation checklist"),
            CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Succeeded, result.Status);
        Assert.Equal(source.Id.ToString("D"), result.Context?.Knowledge?.Chunks[0].SourceId);
        Assert.Equal(2, result.Traces.Count);
    }

    [Fact]
    public async Task Builder_ShouldReturnPartialContextWhenKnowledgeIsUnavailable()
    {
        var connector = Unique("partial");
        await using var marketDb = fixture.CreateMarketDbContext();
        await ContextIntegrationSupport.PublishAsync(
            marketDb,
            ContextIntegrationSupport.Snapshot(
                connector,
                ContextIntegrationSupport.Now.AddSeconds(-5)));
        var knowledge = new IntegrationContextProvider(
            new ContextProviderDescriptor(
                new ContextProviderId("knowledge"),
                ContextProviderCategory.Knowledge,
                ContextRequirement.Preferred,
                20,
                TimeSpan.FromSeconds(1)),
            (_, _) => Task.FromResult(ContextProviderResult.Unavailable(
                new ContextProviderId("knowledge"),
                "KnowledgeHub unavailable")));

        var result = await ContextIntegrationSupport.Builder([
            ContextIntegrationSupport.MarketProvider(marketDb),
            knowledge
        ]).BuildAsync(ContextIntegrationSupport.Query(connector), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.PartiallySucceeded, result.Status);
        Assert.NotNull(result.Context?.MarketSnapshot);
        Assert.Null(result.Context?.Knowledge);
    }

    [Fact]
    public async Task Builder_ShouldCancelAndObserveRunningProvider()
    {
        var completed = false;
        var provider = new IntegrationContextProvider(
            new ContextProviderDescriptor(
                new ContextProviderId("market"),
                ContextProviderCategory.MarketSnapshot,
                ContextRequirement.Required,
                10,
                TimeSpan.FromSeconds(5)),
            async (_, cancellationToken) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    throw new InvalidOperationException("Unreachable.");
                }
                finally
                {
                    completed = true;
                }
            });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ContextIntegrationSupport.Builder([provider]).BuildAsync(
                ContextIntegrationSupport.Query(Unique("cancel")),
                cancellation.Token));

        Assert.True(completed);
    }

    [Fact]
    public async Task Builder_ShouldApplyRealProviderTimeoutWithoutOrphanTask()
    {
        var completed = false;
        var provider = new IntegrationContextProvider(
            new ContextProviderDescriptor(
                new ContextProviderId("market"),
                ContextProviderCategory.MarketSnapshot,
                ContextRequirement.Required,
                10,
                TimeSpan.FromMilliseconds(50)),
            async (_, cancellationToken) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    throw new InvalidOperationException("Unreachable.");
                }
                finally
                {
                    completed = true;
                }
            });

        var result = await ContextIntegrationSupport.Builder([provider]).BuildAsync(
            ContextIntegrationSupport.Query(Unique("timeout")),
            CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.True(completed);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.ProviderTimeout);
    }

    [Fact]
    public async Task Builder_ShouldKeepUserAndSessionContextsIsolated()
    {
        var connector = Unique("isolation");
        await using var marketDb = fixture.CreateMarketDbContext();
        await ContextIntegrationSupport.PublishAsync(
            marketDb,
            ContextIntegrationSupport.Snapshot(
                connector,
                ContextIntegrationSupport.Now.AddSeconds(-5)));
        var builder = ContextIntegrationSupport.Builder([
            ContextIntegrationSupport.MarketProvider(marketDb)
        ]);

        var first = await builder.BuildAsync(
            ContextIntegrationSupport.Query(connector, "user-a", "session-a"),
            CancellationToken.None);
        var second = await builder.BuildAsync(
            ContextIntegrationSupport.Query(connector, "user-b", "session-b"),
            CancellationToken.None);

        Assert.NotEqual(first.Context?.Id, second.Context?.Id);
        Assert.Equal("user-a", first.Context?.UserId);
        Assert.Equal("session-b", second.Context?.SessionId);
    }

    [Fact]
    public async Task MemoryProvider_ShouldNotLeakMemoryBetweenUsers()
    {
        var connector = Unique("memory-isolation");
        await using var marketDb = fixture.CreateMarketDbContext();
        await ContextIntegrationSupport.PublishAsync(
            marketDb,
            ContextIntegrationSupport.Snapshot(
                connector,
                ContextIntegrationSupport.Now.AddSeconds(-5)));

        var memoryStore = new InMemoryMemoryStore(
            new MemoryRetentionOptions(),
            new FixedIntegrationTimeProvider(ContextIntegrationSupport.Now));
        var sharedConversation = "shared-session";
        await memoryStore.AppendAsync(
            new MemoryWriteRequest(
                new ConversationMemoryKey(sharedConversation, userId: "user-a"),
                ConversationMemoryRole.User,
                "private user-a memory",
                sessionId: sharedConversation,
                allowCompaction: false),
            CancellationToken.None);
        var memoryProvider = new MemoryContextProvider(
            new MemoryReader(memoryStore, new CharacterBasedTokenEstimator()),
            Options.Create(new ContextEngineOptions()));
        var builder = ContextIntegrationSupport.Builder([
            ContextIntegrationSupport.MarketProvider(marketDb),
            memoryProvider
        ]);

        var first = await builder.BuildAsync(
            ContextIntegrationSupport.Query(connector, "user-a", sharedConversation),
            CancellationToken.None);
        var second = await builder.BuildAsync(
            ContextIntegrationSupport.Query(connector, "user-b", sharedConversation),
            CancellationToken.None);

        Assert.Equal("private user-a memory", first.Context?.Memory?.Items.Single().Content);
        Assert.Null(second.Context?.Memory);
        Assert.Equal(MarketContextBuildStatus.PartiallySucceeded, second.Status);
    }

    [Fact]
    public async Task ContextEngine_ShouldNotCreatePersistenceTables()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name LIKE '%context%'
            """;

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(0, count);
    }

    private static async Task<KnowledgeSource> PersistKnowledgeAsync(
        KnowledgeHubDbContext dbContext,
        string content)
    {
        var embedding = new DeterministicEmbeddingGenerator();
        var source = new KnowledgeSource(
            $"Context source {Guid.NewGuid():N}",
            KnowledgeSourceType.Text,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")))));
        source.StartProcessing();
        source.AddFragment(
            0,
            content,
            content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            await embedding.GenerateAsync(content, CancellationToken.None));
        source.MarkReady();
        var repository = new PostgreSqlKnowledgeSourceRepository(dbContext);
        await repository.AddAsync(source, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        return source;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
