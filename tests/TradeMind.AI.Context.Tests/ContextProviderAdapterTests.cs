using MediatR;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.Context.Infrastructure;
using TradeMind.AI.Knowledge;
using TradeMind.AI.Memory;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;

namespace TradeMind.AI.Context.Tests;

public sealed class ContextProviderAdapterTests
{
    [Fact]
    public async Task MarketProvider_ShouldDelegateToLatestSnapshotQuery()
    {
        var sender = new CapturingSender(ContextTestData.Snapshot());
        var provider = new MarketSnapshotContextProvider(sender);
        var query = ContextTestData.Query();

        var result = await provider.ProvideAsync(Request(query), CancellationToken.None);

        Assert.Equal(ContextProviderExecutionStatus.Succeeded, result.Status);
        var sent = Assert.IsType<GetLatestMarketSnapshotQuery>(sender.Request);
        Assert.Equal(query.Instrument, sent.Instrument);
        Assert.Equal(query.Timeframe, sent.Timeframe);
    }

    [Fact]
    public async Task MarketProvider_ShouldRepresentMissingSnapshotWithoutException()
    {
        var provider = new MarketSnapshotContextProvider(new CapturingSender(null));

        var result = await provider.ProvideAsync(Request(), CancellationToken.None);

        Assert.Equal(ContextProviderExecutionStatus.Unavailable, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task MarketProvider_ShouldRepresentMissingConnectorAsNotConfigured()
    {
        var sender = new CapturingSender(ContextTestData.Snapshot());
        var provider = new MarketSnapshotContextProvider(sender);
        var query = new BuildMarketContextQuery(
            "user-1",
            "session-1",
            null,
            new Instrument("EURUSD"),
            Timeframe.M15);

        var result = await provider.ProvideAsync(Request(query), CancellationToken.None);

        Assert.Equal(ContextProviderExecutionStatus.NotConfigured, result.Status);
        Assert.Null(sender.Request);
    }

    [Fact]
    public async Task KnowledgeProvider_ShouldMapExistingRagContract()
    {
        var retriever = new StubKnowledgeRetriever(new KnowledgeContextResult(
            "liquidity",
            [new KnowledgeContextFragment(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "liquidity sweep",
                0.9,
                0)],
            [],
            1,
            false,
            4,
            15,
            TimeSpan.FromMilliseconds(1),
            ContextTestData.Now));
        var provider = new KnowledgeContextProvider(
            retriever,
            Options.Create(new ContextEngineOptions()));

        var result = await provider.ProvideAsync(Request(), CancellationToken.None);

        var data = Assert.IsType<KnowledgeContextData>(result.Data);
        Assert.Single(data.Context.Chunks);
        Assert.Equal("MarketContext", retriever.Request?.Scenario);
        Assert.Contains("EURUSD", retriever.Request?.Query, StringComparison.Ordinal);
        Assert.Contains("M15", retriever.Request?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KnowledgeProvider_ShouldRepresentEmptyRagResultAsUnavailable()
    {
        var retriever = new StubKnowledgeRetriever(new KnowledgeContextResult(
            "query",
            [],
            [],
            0,
            false,
            0,
            0,
            TimeSpan.Zero,
            ContextTestData.Now));
        var provider = new KnowledgeContextProvider(
            retriever,
            Options.Create(new ContextEngineOptions()));

        var result = await provider.ProvideAsync(Request(), CancellationToken.None);

        Assert.Equal(ContextProviderExecutionStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task MemoryProvider_ShouldMapExistingMemoryReaderContract()
    {
        var key = new ConversationMemoryKey("conversation-1", userId: "user-1");
        var memory = new MemoryReadResult(
            key,
            null,
            [new ConversationMemoryEntry(
                "entry-1",
                ConversationMemoryRole.User,
                "respect risk",
                ContextTestData.Now,
                1)],
            1,
            false,
            1,
            1,
            3);
        var reader = new StubMemoryReader(memory);
        var provider = new MemoryContextProvider(
            reader,
            Options.Create(new ContextEngineOptions()));

        var result = await provider.ProvideAsync(Request(), CancellationToken.None);

        var data = Assert.IsType<MemoryContextData>(result.Data);
        Assert.Single(data.Context.Items);
        Assert.Equal("conversation-1", reader.Request?.Key.ConversationId);
    }

    [Fact]
    public async Task MemoryProvider_ShouldRepresentEmptyMemoryAsUnavailable()
    {
        var key = new ConversationMemoryKey("conversation-1", userId: "user-1");
        var provider = new MemoryContextProvider(
            new StubMemoryReader(new MemoryReadResult(key, null, [], 0, false, null, null, 0)),
            Options.Create(new ContextEngineOptions()));

        var result = await provider.ProvideAsync(Request(), CancellationToken.None);

        Assert.Equal(ContextProviderExecutionStatus.Unavailable, result.Status);
    }

    private static ContextProviderRequest Request(BuildMarketContextQuery? query = null) =>
        new(query ?? ContextTestData.Query(), new Dictionary<ContextProviderId, ContextProviderResult>());

    private sealed class CapturingSender(MarketSnapshot? snapshot) : ISender
    {
        public object? Request { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult((TResponse)(object?)snapshot!);
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult<object?>(snapshot);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubKnowledgeRetriever(KnowledgeContextResult result)
        : IKnowledgeContextRetriever
    {
        public KnowledgeContextRequest? Request { get; private set; }

        public Task<KnowledgeContextResult> RetrieveAsync(
            KnowledgeContextRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class StubMemoryReader(MemoryReadResult result) : IMemoryReader
    {
        public MemoryReadRequest? Request { get; private set; }

        public Task<MemoryReadResult> ReadAsync(
            MemoryReadRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult(result);
        }
    }
}
