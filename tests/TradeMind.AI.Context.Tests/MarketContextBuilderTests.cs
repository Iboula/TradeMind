using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.Context.Tests;

public sealed class MarketContextBuilderTests
{
    [Fact]
    public async Task Builder_ShouldSucceedWithMinimalMarketSnapshot()
    {
        var result = await ContextTestData.Builder([ContextTestData.MarketProvider()])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.IsType<SucceededMarketContextBuildResult>(result);
        Assert.NotNull(result.Context?.MarketSnapshot);
        Assert.Equal(MarketContext.CurrentVersion, result.Context.Version);
    }

    [Fact]
    public async Task Builder_ShouldExposeBuildMetadataAndFreshnessDetails()
    {
        var result = await ContextTestData.Builder([ContextTestData.MarketProvider()])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal("correlation-1", result.CorrelationId);
        Assert.True(result.Duration >= TimeSpan.Zero);
        Assert.Equal([new ContextProviderId("market")], result.ExecutedProviders);
        Assert.Empty(result.SkippedProviders);

        var trace = Assert.Single(result.Traces);
        Assert.Equal(TimeSpan.FromSeconds(10), trace.SourceAge);
        Assert.Equal(TimeSpan.FromMinutes(1), trace.FreshnessThresholds?.FreshMaximumAge);
        Assert.Equal(TimeSpan.FromMinutes(15), trace.FreshnessThresholds?.AgingMaximumAge);
    }

    [Fact]
    public async Task Builder_ShouldFailWhenRequiredMarketSnapshotIsMissing()
    {
        var provider = ContextTestData.MarketProvider((_, _) => Task.FromResult(
            ContextProviderResult.Unavailable(new ContextProviderId("market"), "missing")));

        var result = await ContextTestData.Builder([provider])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.IsType<FailedMarketContextBuildResult>(result);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.MissingRequiredSource);
    }

    [Fact]
    public async Task Builder_ShouldPartiallySucceedWhenPreferredKnowledgeIsUnavailable()
    {
        var knowledge = Provider(
            "knowledge",
            ContextProviderCategory.Knowledge,
            ContextRequirement.Preferred,
            (_, _) => Task.FromResult(ContextProviderResult.Unavailable(
                new ContextProviderId("knowledge"),
                "unavailable")));

        var result = await ContextTestData.Builder([ContextTestData.MarketProvider(), knowledge])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.IsType<PartiallySucceededMarketContextBuildResult>(result);
        Assert.NotNull(result.Context);
        Assert.Null(result.Context.Knowledge);
    }

    [Fact]
    public async Task Builder_ShouldSucceedWithMarketKnowledgeAndMemory()
    {
        var knowledge = KnowledgeProvider();
        var memory = MemoryProvider();

        var result = await ContextTestData.Builder([
            ContextTestData.MarketProvider(),
            knowledge,
            memory
        ]).BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.IsType<SucceededMarketContextBuildResult>(result);
        Assert.NotNull(result.Context?.Knowledge);
        Assert.NotNull(result.Context?.Memory);
        Assert.Equal(3, result.Traces.Count);
    }

    [Fact]
    public async Task Builder_ShouldExecuteIndependentProvidersInParallel()
    {
        var arrivals = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Arrive(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                release.SetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
        }

        var market = ContextTestData.MarketProvider(async (_, cancellationToken) =>
        {
            await Arrive(cancellationToken);
            return ContextTestData.MarketResult();
        });
        var knowledge = Provider(
            "knowledge",
            ContextProviderCategory.Knowledge,
            ContextRequirement.Preferred,
            async (_, cancellationToken) =>
            {
                await Arrive(cancellationToken);
                return KnowledgeResult();
            });

        var result = await ContextTestData.Builder([market, knowledge])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Succeeded, result.Status);
        Assert.Equal(2, arrivals);
    }

    [Fact]
    public async Task Builder_ShouldPassCompletedDependenciesToDependentProvider()
    {
        var market = ContextTestData.MarketProvider();
        var knowledge = Provider(
            "knowledge",
            ContextProviderCategory.Knowledge,
            ContextRequirement.Preferred,
            (request, _) =>
            {
                Assert.Equal(
                    ContextProviderExecutionStatus.Succeeded,
                    request.Dependencies[market.Descriptor.Id].Status);
                return Task.FromResult(KnowledgeResult());
            },
            dependencies: [market.Descriptor.Id]);

        var result = await ContextTestData.Builder([knowledge, market])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Succeeded, result.Status);
        Assert.Equal(1, knowledge.InvocationCount);
    }

    [Fact]
    public async Task Builder_ShouldSkipProviderWhenDependencyFails()
    {
        var market = ContextTestData.MarketProvider((_, _) => Task.FromResult(
            ContextProviderResult.Unavailable(new ContextProviderId("market"), "missing")));
        var knowledge = KnowledgeProvider([market.Descriptor.Id]);

        var result = await ContextTestData.Builder([market, knowledge])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(0, knowledge.InvocationCount);
        Assert.Contains(result.Traces, trace =>
            trace.ProviderId == knowledge.Descriptor.Id
            && trace.Status == ContextProviderExecutionStatus.Skipped);
    }

    [Fact]
    public async Task Builder_ShouldEnforceProviderTimeout()
    {
        var provider = ContextTestData.MarketProvider(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ContextTestData.MarketResult();
            },
            timeout: TimeSpan.FromMilliseconds(30));

        var result = await ContextTestData.Builder([provider])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.ProviderTimeout);
    }

    [Fact]
    public async Task Builder_ShouldRejectLateResultFromProviderIgnoringCancellation()
    {
        var completed = false;
        var provider = ContextTestData.MarketProvider(
            async (_, _) =>
            {
                await Task.Delay(50);
                completed = true;
                return ContextTestData.MarketResult();
            },
            timeout: TimeSpan.FromMilliseconds(10));

        var result = await ContextTestData.Builder([provider])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.True(completed);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.ProviderTimeout);
    }

    [Fact]
    public async Task Builder_ShouldEnforceGlobalTimeout()
    {
        var provider = ContextTestData.MarketProvider(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ContextTestData.MarketResult();
            },
            timeout: TimeSpan.FromSeconds(2));
        var options = new ContextEngineOptions { GlobalTimeout = TimeSpan.FromMilliseconds(30) };

        var result = await ContextTestData.Builder([provider], options)
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.GlobalTimeout);
    }

    [Fact]
    public async Task Builder_ShouldPropagateCallerCancellation()
    {
        var provider = ContextTestData.MarketProvider(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ContextTestData.MarketResult();
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ContextTestData.Builder([provider]).BuildAsync(ContextTestData.Query(), cancellation.Token));
    }

    [Fact]
    public async Task Builder_ShouldObserveProviderCompletionBeforeReturningAfterTimeout()
    {
        var completed = false;
        var provider = ContextTestData.MarketProvider(
            async (_, cancellationToken) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return ContextTestData.MarketResult();
                }
                finally
                {
                    completed = true;
                }
            },
            timeout: TimeSpan.FromMilliseconds(30));

        _ = await ContextTestData.Builder([provider])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.True(completed);
    }

    [Fact]
    public async Task Builder_ShouldRejectUnsupportedContextVersion()
    {
        var result = await ContextTestData.Builder([ContextTestData.MarketProvider()])
            .BuildAsync(ContextTestData.Query(version: 99), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.UnsupportedContextVersion);
    }

    [Fact]
    public async Task Builder_ShouldReturnStructuredInvalidRequest()
    {
        var result = await ContextTestData.Builder([ContextTestData.MarketProvider()])
            .BuildAsync(ContextTestData.Query(userId: " "), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.InvalidBuildRequest);
    }

    [Fact]
    public async Task Builder_ShouldRejectProviderDataFromWrongCategory()
    {
        var provider = ContextTestData.MarketProvider((_, _) => Task.FromResult(KnowledgeResult()));

        var result = await ContextTestData.Builder([provider])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.InvalidProviderResult);
    }

    [Fact]
    public async Task Builder_ShouldNotMaskUnexpectedTechnicalException()
    {
        var provider = ContextTestData.MarketProvider((_, _) =>
            throw new InvalidOperationException("technical failure"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ContextTestData.Builder([provider]).BuildAsync(ContextTestData.Query(), CancellationToken.None));

        Assert.Equal("technical failure", exception.Message);
    }

    [Fact]
    public async Task Builder_ShouldAllowNormalOptionalAbsence()
    {
        var optional = Provider(
            "workspace",
            ContextProviderCategory.Workspace,
            ContextRequirement.Optional,
            (_, _) => Task.FromResult(ContextProviderResult.NotConfigured(
                new ContextProviderId("workspace"),
                "not configured")));

        var result = await ContextTestData.Builder([ContextTestData.MarketProvider(), optional])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.IsType<SucceededMarketContextBuildResult>(result);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, warning => warning.Code == "optional-source-unavailable");
    }

    [Fact]
    public async Task Builder_ShouldUseInjectedTimeProviderForBuiltAt()
    {
        var timeProvider = new FixedContextTimeProvider(ContextTestData.Now.AddHours(3));

        var result = await ContextTestData.Builder(
            [ContextTestData.MarketProvider()],
            timeProvider: timeProvider).BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(timeProvider.UtcNow, result.Context?.BuiltAtUtc);
    }

    [Fact]
    public async Task Builder_ShouldCreateSuccessfulSourceTrace()
    {
        var result = await ContextTestData.Builder([ContextTestData.MarketProvider()])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        var trace = Assert.Single(result.Traces);
        Assert.Equal(ContextProviderExecutionStatus.Succeeded, trace.Status);
        Assert.Equal(ContextFreshness.Fresh, trace.Freshness);
        Assert.Equal(1, trace.ItemCount);
    }

    [Fact]
    public async Task Builder_ShouldCreateFailedSourceTrace()
    {
        var provider = ContextTestData.MarketProvider((_, _) => Task.FromResult(
            ContextProviderResult.Unavailable(new ContextProviderId("market"), "missing")));

        var result = await ContextTestData.Builder([provider])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        var trace = Assert.Single(result.Traces);
        Assert.Equal(ContextProviderExecutionStatus.Unavailable, trace.Status);
        Assert.NotNull(trace.Error);
    }

    [Fact]
    public async Task Builder_ShouldExposeSkippedProvidersInBuildMetadata()
    {
        var market = ContextTestData.MarketProvider((_, _) => Task.FromResult(
            ContextProviderResult.Unavailable(new ContextProviderId("market"), "missing")));
        var dependent = new TestContextProvider(
            new ContextProviderDescriptor(
                new ContextProviderId("knowledge"),
                ContextProviderCategory.Knowledge,
                ContextRequirement.Optional,
                20,
                TimeSpan.FromSeconds(1),
                [market.Descriptor.Id]),
            (_, _) => Task.FromResult(ContextProviderResult.NotConfigured(
                new ContextProviderId("knowledge"),
                "not configured")));

        var result = await ContextTestData.Builder([market, dependent])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Contains(market.Descriptor.Id, result.ExecutedProviders);
        Assert.Contains(dependent.Descriptor.Id, result.SkippedProviders);
        Assert.Contains(result.Traces, trace =>
            trace.ProviderId == dependent.Descriptor.Id
            && trace.Status == ContextProviderExecutionStatus.Skipped);
    }

    [Fact]
    public async Task Builder_ShouldReturnStructuredFailureForNullProviderResult()
    {
        var provider = ContextTestData.MarketProvider((_, _) =>
            Task.FromResult<ContextProviderResult>(null!));

        var result = await ContextTestData.Builder([provider])
            .BuildAsync(ContextTestData.Query(), CancellationToken.None);

        Assert.Equal(MarketContextBuildStatus.Failed, result.Status);
        Assert.Contains(result.Errors, error => error.Code == ContextBuildErrorCode.InvalidProviderResult);
        Assert.Contains(result.Traces, trace =>
            trace.Status == ContextProviderExecutionStatus.Failed
            && trace.Error?.Code == ContextBuildErrorCode.InvalidProviderResult);
    }

    [Fact]
    public async Task Builder_ShouldPropagateCallerCancellationToEveryActiveProvider()
    {
        var arrived = 0;
        var allCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<ContextProviderResult> WaitForCancellation(
            ContextProviderRequest _,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref arrived);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Decrement(ref arrived) == 0)
                {
                    allCanceled.SetResult();
                }

                throw;
            }

            return ContextTestData.MarketResult();
        }

        var first = ContextTestData.MarketProvider(WaitForCancellation);
        var second = ContextTestData.MarketProvider(WaitForCancellation, id: "market-2");
        using var cancellation = new CancellationTokenSource();
        var build = ContextTestData.Builder([first, second])
            .BuildAsync(ContextTestData.Query(), cancellation.Token);

        while (Volatile.Read(ref arrived) != 2)
        {
            await Task.Yield();
        }

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => build);
        await allCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void BuildQuery_ShouldKeepConnectorAndWorkspaceOptional()
    {
        var query = new BuildMarketContextQuery(
            "user-1",
            "session-1",
            null,
            new Instrument("EURUSD"),
            Timeframe.M15,
            workspaceId: "workspace-1");

        Assert.Null(query.ConnectorId);
        Assert.Equal("workspace-1", query.WorkspaceId);
    }

    private static TestContextProvider KnowledgeProvider(
        IReadOnlyCollection<ContextProviderId>? dependencies = null) => Provider(
            "knowledge",
            ContextProviderCategory.Knowledge,
            ContextRequirement.Preferred,
            (_, _) => Task.FromResult(KnowledgeResult()),
            dependencies: dependencies);

    private static TestContextProvider MemoryProvider() => Provider(
        "memory",
        ContextProviderCategory.Memory,
        ContextRequirement.Preferred,
        (_, _) => Task.FromResult(ContextProviderResult.Succeeded(
            new MemoryContextData(new MemoryContext(
                "conversation-1",
                null,
                [new MemoryItem("1", "User", "remember risk", ContextTestData.Now, 1)],
                false,
                1)),
            ContextTestData.Now,
            1,
            "test")));

    private static ContextProviderResult KnowledgeResult() => ContextProviderResult.Succeeded(
        new KnowledgeContextData(new KnowledgeContext(
            "EURUSD",
            [new KnowledgeChunk("fragment", "source", "liquidity", 0.9, 0)],
            ContextTestData.Now,
            false,
            1)),
        ContextTestData.Now,
        1,
        "test");

    private static TestContextProvider Provider(
        string id,
        ContextProviderCategory category,
        ContextRequirement requirement,
        Func<ContextProviderRequest, CancellationToken, Task<ContextProviderResult>> handler,
        TimeSpan? timeout = null,
        IReadOnlyCollection<ContextProviderId>? dependencies = null) => new(
            new ContextProviderDescriptor(
                new ContextProviderId(id),
                category,
                requirement,
                20,
                timeout ?? TimeSpan.FromSeconds(1),
                dependencies),
            handler);
}
