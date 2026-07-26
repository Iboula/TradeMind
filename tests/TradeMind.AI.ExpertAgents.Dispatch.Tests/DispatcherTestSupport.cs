using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Dispatch.Tests;

internal static class DispatcherTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    public static MarketContext Context(
        double quality = 90,
        DateTimeOffset? builtAtUtc = null,
        ContextFreshness freshness = ContextFreshness.Fresh)
    {
        var capturedAt = builtAtUtc ?? Now;
        var snapshotId = SnapshotId.New();
        var snapshot = new MarketSnapshot(
            snapshotId,
            new ConnectorId("test-connector"),
            new ExternalAccountReference("account-1"),
            new Instrument("EURUSD"),
            Timeframe.H1,
            capturedAt,
            capturedAt,
            [],
            null,
            [],
            [],
            [],
            [],
            new SnapshotQuality(
                SnapshotFreshness.Live,
                ConnectorCapabilities.Candles,
                ConnectorCapabilities.None));
        var trace = new ContextSourceTrace(
            new ContextProviderId("market-snapshot"),
            ContextProviderCategory.MarketSnapshot,
            ContextRequirement.Required,
            ContextProviderExecutionStatus.Succeeded,
            capturedAt,
            capturedAt,
            TimeSpan.Zero,
            capturedAt,
            freshness,
            1,
            "1.0",
            [new ContextSourceReference("snapshot", snapshotId.ToString())]);
        return new MarketContext(
            MarketContextId.New(),
            MarketContext.CurrentVersion,
            "user-1",
            "session-1",
            new Instrument("EURUSD"),
            Timeframe.H1,
            capturedAt,
            MarketContextBuildStatus.Succeeded,
            snapshot,
            null,
            null,
            null,
            null,
            null,
            null,
            [trace],
            [],
            new ContextQuality(
                quality,
                quality,
                quality,
                quality,
                quality >= 85 ? ContextQualityBand.Excellent : ContextQualityBand.Limited));
    }

    public static AgentDescriptor Descriptor(
        string id = "market-agent",
        string version = "1.0.0",
        AgentSpecialty? specialty = null,
        AgentActivationStatus activationStatus = AgentActivationStatus.Enabled,
        AgentMaturity maturity = AgentMaturity.Stable,
        IReadOnlyCollection<string>? requiredPermissions = null,
        double minimumContextQuality = 0,
        bool allowStaleContext = false,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyCollection<ContextProviderCategory>? requiredContextCategories = null) =>
        new(
            new AgentId(id),
            $"{id} expert",
            AgentVersion.Parse(version),
            specialty ?? AgentSpecialty.Custom,
            "A dispatcher test agent.",
            new AgentCapabilities(requiredContextCategories: requiredContextCategories),
            activationStatus,
            maturity,
            tags: tags ?? ["test"],
            requiredPermissions: requiredPermissions,
            minimumContextQuality: minimumContextQuality,
            allowStaleContext: allowStaleContext);

    public static AgentDispatchRequest Request(
        MarketContext context,
        string objective = "Analyze current market structure.",
        IReadOnlyCollection<AgentId>? included = null,
        IReadOnlyCollection<AgentId>? excluded = null,
        IReadOnlyDictionary<AgentId, AgentDispatchRequirement>? requirements = null,
        IReadOnlyDictionary<AgentId, AgentVersion>? exactVersions = null,
        AgentVersionSelection selection = AgentVersionSelection.LatestStable,
        AgentDispatchFallbackMode fallback = AgentDispatchFallbackMode.None,
        AgentDispatchBudget? budget = null,
        int minimumAgents = 1,
        int maximumAgents = 8,
        TimeSpan? globalTimeout = null,
        TimeSpan? perAgentTimeout = null,
        bool allowExperimental = false,
        AnalysisIntent? explicitIntent = null) =>
        new(
            DispatchId.New(),
            context.Id,
            "user-1",
            "session-1",
            objective,
            explicitIntent: explicitIntent,
            includedAgentIds: included,
            excludedAgentIds: excluded,
            requirements: requirements,
            exactVersions: exactVersions,
            versionSelection: selection,
            fallbackMode: fallback,
            budget: budget,
            minimumAgents: minimumAgents,
            maximumAgents: maximumAgents,
            globalTimeout: globalTimeout,
            perAgentTimeout: perAgentTimeout,
            allowExperimentalAgents: allowExperimental);

    public static AgentAnalysisResult Result(
        MarketContext context,
        AgentExecutionRequest request,
        AgentDescriptor descriptor,
        AgentAnalysisStatus status = AgentAnalysisStatus.Succeeded,
        DateTimeOffset? startedAtUtc = null,
        DateTimeOffset? completedAtUtc = null) =>
        new(
            request.AgentRunId,
            descriptor.Id,
            descriptor.Version,
            context.Id,
            status,
            startedAtUtc ?? Now,
            completedAtUtc ?? Now.AddSeconds(1),
            AgentDirectionalBias.Neutral,
            AgentConfidence.FromScore(80, 90, 90),
            "Structured dispatcher test result",
            errors: status == AgentAnalysisStatus.Succeeded ? [] : [new AgentError(AgentErrorCode.AgentUnavailable, "Test failure")],
            schemaVersion: descriptor.ResultSchemaVersion);

    public static AgentDispatchPlanner Planner(
        IEnumerable<IExpertAgent> agents,
        AgentDispatchOptions? options = null,
        IExpertAgentAuthorizationPolicy? authorization = null,
        IAgentCompatibilityPolicy? compatibility = null,
        TimeProvider? timeProvider = null)
    {
        var dispatchOptions = options ?? new AgentDispatchOptions();
        var clock = timeProvider ?? new FixedTimeProvider(Now);
        return new AgentDispatchPlanner(
            new ImmutableExpertAgentRegistry(agents),
            authorization ?? new DefaultExpertAgentAuthorizationPolicy(Options.Create(new ExpertAgentOptions())),
            compatibility ?? new DefaultAgentCompatibilityPolicy(clock),
            new RuleBasedAnalysisIntentClassifier(),
            new DefaultAgentRelevancePolicy(),
            new DefaultAgentCostPolicy(Options.Create(dispatchOptions)),
            new DefaultAgentDispatchBudgetPolicy(),
            new DefaultAgentDispatchVersionPolicy(),
            new DefaultAgentDispatchFallbackPolicy(),
            Options.Create(dispatchOptions),
            clock,
            NullLogger<AgentDispatchPlanner>.Instance);
    }
}

internal sealed class TestExpertAgent(
    AgentDescriptor descriptor,
    Func<MarketContext, AgentExecutionRequest, CancellationToken, Task<AgentAnalysisResult>>? analyze = null) : IExpertAgent
{
    private readonly Func<MarketContext, AgentExecutionRequest, CancellationToken, Task<AgentAnalysisResult>> _analyze =
        analyze ?? ((context, request, _) => Task.FromResult(DispatcherTestData.Result(context, request, descriptor)));

    public AgentDescriptor Descriptor { get; } = descriptor;

    public Task<AgentAnalysisResult> AnalyzeAsync(
        MarketContext context,
        AgentExecutionRequest request,
        CancellationToken cancellationToken) => _analyze(context, request, cancellationToken);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class TestDispatchExecutor(
    Func<MarketContext, AgentExecutionRequest, CancellationToken, Task<AgentAnalysisResult>> execute) : IExpertAgentExecutor
{
    public Task<AgentAnalysisResult> ExecuteAsync(
        MarketContext context,
        AgentExecutionRequest request,
        CancellationToken cancellationToken) => execute(context, request, cancellationToken);
}
