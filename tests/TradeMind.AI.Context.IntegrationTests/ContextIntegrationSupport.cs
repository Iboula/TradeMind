using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.Context.Infrastructure;
using TradeMind.AI.Knowledge;
using TradeMind.KnowledgeHub.Application;
using TradeMind.KnowledgeHub.Infrastructure;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;
using TradeMind.MarketConnectors.Infrastructure;

namespace TradeMind.AI.Context.IntegrationTests;

internal static class ContextIntegrationSupport
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 23, 0, 0, TimeSpan.Zero);

    public static MarketSnapshot Snapshot(
        string connectorId,
        DateTimeOffset capturedAt,
        decimal close = 1.1m) => new(
            SnapshotId.New(),
            new ConnectorId(connectorId),
            new ExternalAccountReference("account-1"),
            new Instrument("EURUSD"),
            Timeframe.M15,
            capturedAt,
            capturedAt.AddSeconds(1),
            [new MarketCandle(
                capturedAt.AddMinutes(-15),
                new Price(close),
                new Price(close),
                new Price(close),
                new Price(close),
                1,
                1,
                true)],
            null,
            [],
            [],
            [],
            [],
            new SnapshotQuality(
                SnapshotFreshness.Live,
                ConnectorCapabilities.Candles,
                ConnectorCapabilities.None));

    public static async Task PublishAsync(
        MarketConnectorsDbContext dbContext,
        MarketSnapshot snapshot)
    {
        var handler = new PublishMarketSnapshotCommandHandler(
            new PostgreSqlMarketSnapshotRepository(dbContext),
            new CanonicalMarketSnapshotSerializer(),
            new Sha256MarketSnapshotHasher(),
            Options.Create(new MarketConnectorCoreOptions()),
            new FixedIntegrationTimeProvider(Now),
            NullLogger<PublishMarketSnapshotCommandHandler>.Instance);
        var result = await handler.Handle(
            new PublishMarketSnapshotCommand(snapshot),
            CancellationToken.None);
        Assert.Equal(PublishMarketSnapshotStatus.Accepted, result.Status);
    }

    public static MarketSnapshotContextProvider MarketProvider(
        MarketConnectorsDbContext dbContext)
    {
        var handler = new GetLatestMarketSnapshotQueryHandler(
            new PostgreSqlMarketSnapshotRepository(dbContext),
            new CanonicalMarketSnapshotSerializer(),
            Options.Create(new MarketConnectorCoreOptions()),
            new FixedIntegrationTimeProvider(Now));
        return new MarketSnapshotContextProvider(new LatestSnapshotSender(handler));
    }

    public static KnowledgeContextProvider KnowledgeProvider(
        KnowledgeHubDbContext dbContext)
    {
        var repository = new PostgreSqlKnowledgeSourceRepository(dbContext);
        var embedding = new DeterministicEmbeddingGenerator();
        var service = new KnowledgeHubService(
            new PlainTextExtractor(),
            new SlidingWindowFragmenter(),
            embedding,
            repository);
        var retriever = new KnowledgeContextRetriever(
            new KnowledgeHubServiceSearcher(service),
            new CharacterKnowledgeTokenEstimator(),
            new FixedIntegrationTimeProvider(Now),
            NullLogger<KnowledgeContextRetriever>.Instance);
        return new KnowledgeContextProvider(retriever, Options.Create(new ContextEngineOptions()));
    }

    public static MarketContextBuilder Builder(
        IEnumerable<IContextProvider> providers,
        ContextEngineOptions? options = null)
    {
        var configured = Options.Create(options ?? new ContextEngineOptions());
        return new MarketContextBuilder(
            new ContextProviderRegistry(providers),
            new ContextFreshnessPolicy(configured),
            new ContextQualityPolicy(),
            new ContextSizePolicy(configured),
            configured,
            new FixedIntegrationTimeProvider(Now),
            NullLogger<MarketContextBuilder>.Instance);
    }

    public static BuildMarketContextQuery Query(
        string connectorId,
        string userId = "user-1",
        string sessionId = "session-1",
        TimeSpan? maximumAge = null,
        string knowledgeQuery = "liquidity sweep") => new(
            userId,
            sessionId,
            new ConnectorId(connectorId),
            new Instrument("EURUSD"),
            Timeframe.M15,
            conversationId: sessionId,
            correlationId: Guid.NewGuid().ToString("N"),
            knowledgeQuery: knowledgeQuery,
            maximumMarketAge: maximumAge);

    private sealed class LatestSnapshotSender(
        GetLatestMarketSnapshotQueryHandler handler) : ISender
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            if (request is not GetLatestMarketSnapshotQuery query)
            {
                throw new InvalidOperationException("Unexpected MediatR request.");
            }

            return Cast<TResponse>(handler.Handle(query, cancellationToken));
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static async Task<TResponse> Cast<TResponse>(Task<MarketSnapshot?> task)
        {
            var result = await task.ConfigureAwait(false);
            return (TResponse)(object?)result!;
        }
    }
}

internal sealed class FixedIntegrationTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class IntegrationContextProvider(
    ContextProviderDescriptor descriptor,
    Func<ContextProviderRequest, CancellationToken, Task<ContextProviderResult>> handler)
    : IContextProvider
{
    public ContextProviderDescriptor Descriptor { get; } = descriptor;

    public Task<ContextProviderResult> ProvideAsync(
        ContextProviderRequest request,
        CancellationToken cancellationToken) => handler(request, cancellationToken);
}
