using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.Context.Tests;

internal static class ContextTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 22, 0, 0, TimeSpan.Zero);

    public static BuildMarketContextQuery Query(
        int version = MarketContext.CurrentVersion,
        IReadOnlyCollection<ContextProviderCategory>? categories = null,
        string userId = "user-1",
        string sessionId = "session-1") => new(
            userId,
            sessionId,
            new ConnectorId("test-connector"),
            new Instrument("EURUSD"),
            Timeframe.M15,
            conversationId: "conversation-1",
            correlationId: "correlation-1",
            contextVersion: version,
            requestedCategories: categories);

    public static MarketSnapshot Snapshot(DateTimeOffset? capturedAt = null) => new(
        SnapshotId.New(),
        new ConnectorId("test-connector"),
        new ExternalAccountReference("account-1"),
        new Instrument("EURUSD"),
        Timeframe.M15,
        capturedAt ?? Now.AddSeconds(-10),
        Now.AddSeconds(-9),
        [],
        null,
        [],
        [],
        [],
        [],
        new SnapshotQuality(
            SnapshotFreshness.Live,
            ConnectorCapabilities.None,
            ConnectorCapabilities.None));

    public static ContextProviderResult MarketResult(DateTimeOffset? capturedAt = null)
    {
        var snapshot = Snapshot(capturedAt);
        return ContextProviderResult.Succeeded(
            new MarketSnapshotContextData(snapshot),
            snapshot.CapturedAt,
            1,
            "test");
    }

    public static TestContextProvider MarketProvider(
        Func<ContextProviderRequest, CancellationToken, Task<ContextProviderResult>>? handler = null,
        ContextRequirement requirement = ContextRequirement.Required,
        TimeSpan? timeout = null,
        IReadOnlyCollection<ContextProviderId>? dependencies = null,
        string id = "market") => new(
            new ContextProviderDescriptor(
                new ContextProviderId(id),
                ContextProviderCategory.MarketSnapshot,
                requirement,
                10,
                timeout ?? TimeSpan.FromSeconds(1),
                dependencies),
            handler ?? ((_, _) => Task.FromResult(MarketResult())));

    public static MarketContextBuilder Builder(
        IEnumerable<IContextProvider> providers,
        ContextEngineOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        var engineOptions = Options.Create(options ?? new ContextEngineOptions());
        return new MarketContextBuilder(
            new ContextProviderRegistry(providers),
            new ContextFreshnessPolicy(engineOptions),
            new ContextQualityPolicy(),
            new ContextSizePolicy(engineOptions),
            engineOptions,
            timeProvider ?? new FixedContextTimeProvider(Now),
            NullLogger<MarketContextBuilder>.Instance);
    }

    public static ContextSourceTrace Trace(
        string id,
        ContextProviderExecutionStatus status,
        ContextRequirement requirement,
        ContextFreshness freshness = ContextFreshness.Fresh) => new(
            new ContextProviderId(id),
            ContextProviderCategory.MarketSnapshot,
            requirement,
            status,
            Now,
            Now,
            TimeSpan.Zero,
            status == ContextProviderExecutionStatus.Succeeded ? Now : null,
            freshness,
            status == ContextProviderExecutionStatus.Succeeded ? 1 : 0,
            "test");
}

internal sealed class TestContextProvider(
    ContextProviderDescriptor descriptor,
    Func<ContextProviderRequest, CancellationToken, Task<ContextProviderResult>> handler)
    : IContextProvider
{
    public ContextProviderDescriptor Descriptor { get; } = descriptor;

    public int InvocationCount { get; private set; }

    public ContextProviderRequest? LastRequest { get; private set; }

    public async Task<ContextProviderResult> ProvideAsync(
        ContextProviderRequest request,
        CancellationToken cancellationToken)
    {
        InvocationCount++;
        LastRequest = request;
        return await handler(request, cancellationToken);
    }
}

internal sealed class FixedContextTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
