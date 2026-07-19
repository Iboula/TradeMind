using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Tests;

internal static class ExpertAgentTestData
{
    public static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    public static MarketContext Context(
        MarketContextBuildStatus status = MarketContextBuildStatus.Succeeded,
        DateTimeOffset? builtAtUtc = null,
        double quality = 90,
        bool includeKnowledge = false,
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
        var snapshotReference = new ContextSourceReference("snapshot", snapshotId.ToString());
        var traces = new List<ContextSourceTrace>
        {
            new(
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
                [snapshotReference])
        };
        KnowledgeContext? knowledge = null;
        if (includeKnowledge)
        {
            var knowledgeReference = new ContextSourceReference("knowledge-source", "knowledge-1");
            knowledge = new KnowledgeContext(
                "EURUSD H1",
                [new KnowledgeChunk("fragment-1", "knowledge-1", "Market structure context.", 0.95, 0)],
                capturedAt,
                false,
                1);
            traces.Add(new ContextSourceTrace(
                new ContextProviderId("knowledge"),
                ContextProviderCategory.Knowledge,
                ContextRequirement.Preferred,
                ContextProviderExecutionStatus.Succeeded,
                capturedAt,
                capturedAt,
                TimeSpan.Zero,
                capturedAt,
                freshness,
                1,
                "1.0",
                [knowledgeReference]));
        }

        return new MarketContext(
            MarketContextId.New(),
            MarketContext.CurrentVersion,
            "user-1",
            "session-1",
            new Instrument("EURUSD"),
            Timeframe.H1,
            capturedAt,
            status,
            snapshot,
            null,
            null,
            null,
            knowledge,
            null,
            null,
            traces,
            [],
            new ContextQuality(quality, quality, quality, quality, quality >= 80 ? ContextQualityBand.Excellent : ContextQualityBand.Limited));
    }

    public static AgentDescriptor Descriptor(
        string id = "test-agent",
        string version = "1.0.0",
        AgentSpecialty? specialty = null,
        AgentCapabilities? capabilities = null,
        AgentActivationStatus activationStatus = AgentActivationStatus.Enabled,
        AgentMaturity maturity = AgentMaturity.Stable,
        IReadOnlyCollection<string>? requiredPermissions = null,
        double minimumQuality = 0,
        bool allowStale = false) =>
        new(
            new AgentId(id),
            "Test expert agent",
            AgentVersion.Parse(version),
            specialty ?? AgentSpecialty.Custom,
            "A test-only expert agent.",
            capabilities ?? new AgentCapabilities(),
            activationStatus,
            maturity,
            tags: ["test"],
            requiredPermissions: requiredPermissions,
            minimumContextQuality: minimumQuality,
            allowStaleContext: allowStale);

    public static AgentExecutionRequest Request(
        MarketContext context,
        AgentId? agentId = null,
        TimeSpan? timeout = null,
        string? question = null,
        AgentVersionSelection selection = AgentVersionSelection.LatestStable,
        AgentVersion? exactVersion = null,
        IReadOnlyCollection<string>? permissions = null) =>
        new(
            new AgentRunId("run-1"),
            agentId ?? new AgentId("test-agent"),
            context.Id,
            "user-1",
            "session-1",
            "Assess the current market context.",
            question: question,
            requestedTimeout: timeout,
            permissions: permissions,
            versionSelection: selection,
            exactVersion: exactVersion,
            correlationId: "correlation-1");

    public static AgentAnalysisResult Result(
        MarketContext context,
        AgentExecutionRequest request,
        AgentDescriptor descriptor,
        AgentAnalysisStatus status = AgentAnalysisStatus.Succeeded,
        DateTimeOffset? startedAtUtc = null,
        DateTimeOffset? completedAtUtc = null,
        IReadOnlyCollection<AgentObservation>? observations = null,
        IReadOnlyCollection<AgentEvidence>? evidence = null,
        IReadOnlyCollection<AgentWarning>? warnings = null,
        IReadOnlyCollection<AgentError>? errors = null,
        string? summary = "Structured analysis")
    {
        var started = startedAtUtc ?? Now;
        var completed = completedAtUtc ?? started.AddSeconds(1);
        var source = new ContextSourceReference("snapshot", context.MarketSnapshot.Id.ToString());
        return new AgentAnalysisResult(
            request.AgentRunId,
            descriptor.Id,
            descriptor.Version,
            context.Id,
            status,
            started,
            completed,
            AgentDirectionalBias.Neutral,
            AgentConfidence.FromScore(72, 90, 90, positiveFactors: ["Market snapshot available"]),
            summary,
            observations ?? [new AgentObservation("market-state", AgentObservationImportance.Medium, "The market context is available.", references: [source])],
            evidence ?? [new AgentEvidence(ContextProviderCategory.MarketSnapshot, source, "The persisted market snapshot is the primary evidence.")],
            marketLevels: [],
            scenarios: [],
            invalidations: [],
            risks: [],
            warnings: warnings ?? [],
            limitations: [],
            errors: errors ?? [],
            schemaVersion: descriptor.ResultSchemaVersion);
    }

    public static ExpertAgentExecutor Executor(
        IEnumerable<IExpertAgent> agents,
        ExpertAgentOptions? options = null,
        IExpertAgentAuthorizationPolicy? authorization = null,
        IAgentCompatibilityPolicy? compatibility = null,
        TimeProvider? timeProvider = null) =>
        new(
            new ImmutableExpertAgentRegistry(agents),
            authorization ?? new DefaultExpertAgentAuthorizationPolicy(Options.Create(options ?? new ExpertAgentOptions())),
            compatibility ?? new DefaultAgentCompatibilityPolicy(timeProvider ?? TimeProvider.System),
            new AgentAnalysisResultValidator(Options.Create(options ?? new ExpertAgentOptions())),
            Options.Create(options ?? new ExpertAgentOptions()),
            timeProvider ?? TimeProvider.System,
            NullLogger<ExpertAgentExecutor>.Instance);
}

internal sealed class TestExpertAgent(
    AgentDescriptor descriptor,
    Func<MarketContext, AgentExecutionRequest, CancellationToken, Task<AgentAnalysisResult>>? analyze = null) : IExpertAgent
{
    private readonly Func<MarketContext, AgentExecutionRequest, CancellationToken, Task<AgentAnalysisResult>> _analyze =
        analyze ?? ((context, request, _) => Task.FromResult(ExpertAgentTestData.Result(context, request, descriptor)));

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

internal sealed class DenyAllAuthorizationPolicy : IExpertAgentAuthorizationPolicy
{
    public AgentAuthorizationResult Evaluate(AgentDescriptor descriptor, AgentExecutionRequest request) =>
        AgentAuthorizationResult.Deny(AgentErrorCode.UnauthorizedAgent, "Denied by test policy.");
}
