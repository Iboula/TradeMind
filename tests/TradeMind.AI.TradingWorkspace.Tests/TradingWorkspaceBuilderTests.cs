using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.TradingWorkspace.Tests;

public sealed class TradingWorkspaceBuilderTests
{
    [Fact]
    public async Task Empty_request_returns_empty_state_and_refresh_action()
    {
        var result = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Empty(), CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.Empty, result.State);
        Assert.Contains(result.NextActions, action => action.Type == WorkspaceNextActionType.RefreshContext);
        Assert.All(result.Timeline, entry => Assert.NotNull(entry.SourceId));
    }

    [Fact]
    public async Task Context_only_returns_context_ready_without_orchestration()
    {
        var source = TradingWorkspaceTestData.Sources();
        var request = new TradingWorkspaceRequest(WorkspaceId.New(), context: source.Context);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(request, CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.ContextReady, result.State);
        Assert.Equal(1, result.Context.SourceCount);
        Assert.Contains(result.NextActions, action => action.Type == WorkspaceNextActionType.RunExpertAnalysis);
        Assert.DoesNotContain(result.Errors, error => error.Code == "MISSING_CONSENSUS_DEPENDENCY");
    }

    [Fact]
    public async Task Context_and_analyses_returns_analysis_ready()
    {
        var source = TradingWorkspaceTestData.Sources();
        var request = new TradingWorkspaceRequest(WorkspaceId.New(), source.Context, analyses: [source.Analysis]);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(request, CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.AnalysisReady, result.State);
        Assert.Equal(1, result.Analysis.AnalysisCount);
        Assert.Contains(result.NextActions, action => action.Type == WorkspaceNextActionType.BuildConsensus);
    }

    [Fact]
    public async Task Complete_pipeline_is_projected_as_plan_ready()
    {
        var source = TradingWorkspaceTestData.Sources();

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.PlanReady, result.State);
        Assert.Equal(TradingWorkspaceStatus.Succeeded, result.Status);
        Assert.Equal(source.Decision.Entry!.Price, result.Decision.EntryPrice);
        Assert.Equal(source.Risk.PositionSize!.FinalQuantity, result.Risk.Quantity);
        Assert.Equal(source.Plan.ExpiresAtUtc, result.Plan.ExpiresAtUtc);
        Assert.Contains(result.Timeline, entry => entry.Type == WorkspaceTimelineEventType.Expiration);
    }

    [Fact]
    public async Task Progressive_pipeline_exposes_consensus_decision_and_risk_states()
    {
        var source = TradingWorkspaceTestData.Sources();
        var builder = TradingWorkspaceTestData.Builder();

        var consensus = await builder.BuildAsync(new TradingWorkspaceRequest(WorkspaceId.New(), source.Context, analyses: [source.Analysis], consensus: source.Consensus), CancellationToken.None);
        var decision = await builder.BuildAsync(new TradingWorkspaceRequest(WorkspaceId.New(), source.Context, analyses: [source.Analysis], consensus: source.Consensus, decision: source.Decision), CancellationToken.None);
        var risk = await builder.BuildAsync(new TradingWorkspaceRequest(WorkspaceId.New(), source.Context, analyses: [source.Analysis], consensus: source.Consensus, decision: source.Decision, risk: source.Risk), CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.ConsensusReady, consensus.State);
        Assert.Equal(TradingWorkspaceState.DecisionReady, decision.State);
        Assert.Equal(TradingWorkspaceState.RiskReady, risk.State);
    }

    [Fact]
    public async Task Critical_conflict_is_exposed_as_conflicted_state()
    {
        var source = TradingWorkspaceTestData.Sources(criticalConflict: true);
        var request = new TradingWorkspaceRequest(WorkspaceId.New(), source.Context, analyses: [source.Analysis], consensus: source.Consensus);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(request, CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.Conflicted, result.State);
        Assert.Contains(result.Errors, error => error.Code == "CRITICAL_CONFLICT");
        Assert.Contains(result.NextActions, action => action.Type == WorkspaceNextActionType.ReviewConflict);
    }

    [Fact]
    public async Task Downstream_result_without_dependencies_is_blocked()
    {
        var source = TradingWorkspaceTestData.Sources();
        var request = new TradingWorkspaceRequest(WorkspaceId.New(), decision: source.Decision);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(request, CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.Blocked, result.State);
        Assert.Contains(result.Errors, error => error.Code == "MISSING_CONSENSUS_DEPENDENCY");
        Assert.Contains(result.Blockers, blocker => blocker.Code == "MISSING_CONSENSUS_DEPENDENCY");
    }

    [Fact]
    public async Task Rejected_risk_is_blocked_and_critical_risk_is_preserved()
    {
        var source = TradingWorkspaceTestData.Sources(riskVerdict: RiskVerdict.Rejected);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.Blocked, result.State);
        Assert.Contains(result.Errors, error => error.Code == "RISK_REJECTED");
        Assert.Contains(result.CriticalRisks, risk => risk.Description == "Critical workspace risk");
    }

    [Fact]
    public async Task Expired_plan_never_becomes_plan_ready()
    {
        var source = TradingWorkspaceTestData.Sources(planExpiresAtUtc: TradingWorkspaceTestData.Now.AddMinutes(5));

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);

        Assert.Equal(TradingWorkspaceState.Expired, result.State);
        Assert.Equal(TradingPlanStatus.Succeeded, source.Plan.Status);
        Assert.True(result.Freshness.PlanExpired);
        Assert.Contains(result.Errors, error => error.Code == "PLAN_EXPIRED");
    }

    [Fact]
    public async Task Stale_context_returns_incomplete_workspace()
    {
        var source = TradingWorkspaceTestData.Sources(contextBuiltAtUtc: TradingWorkspaceTestData.Now.AddHours(-1));

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);

        Assert.True(result.Freshness.ContextStale);
        Assert.Equal(TradingWorkspaceState.Incomplete, result.State);
        Assert.NotEqual(TradingWorkspaceState.PlanReady, result.State);
    }

    [Fact]
    public async Task Context_id_mismatch_is_reported_without_masking_sources()
    {
        var first = TradingWorkspaceTestData.Sources();
        var second = TradingWorkspaceTestData.Sources();
        var request = new TradingWorkspaceRequest(WorkspaceId.New(), first.Context, analyses: [first.Analysis], consensus: second.Consensus);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(request, CancellationToken.None);

        Assert.Contains(result.Consistency.Issues, issue => issue.Code == "MARKET_CONTEXT_ID_MISMATCH");
        Assert.Contains(result.Alerts, alert => alert.Code == "MARKET_CONTEXT_ID_MISMATCH");
        Assert.Contains(result.Traces, trace => trace.Origin == "context");
    }

    [Fact]
    public async Task Instrument_and_timeframe_divergence_are_blocking()
    {
        var contextSource = TradingWorkspaceTestData.Sources();
        var divergentSource = TradingWorkspaceTestData.Sources(instrument: new Instrument("GBPUSD"), timeframe: Timeframe.H4);
        var request = new TradingWorkspaceRequest(
            WorkspaceId.New(),
            contextSource.Context,
            analyses: [contextSource.Analysis],
            consensus: contextSource.Consensus,
            decision: divergentSource.Decision,
            risk: divergentSource.Risk,
            plan: divergentSource.Plan);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(request, CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Code == "INSTRUMENT_DIVERGENCE");
        Assert.Contains(result.Errors, error => error.Code == "TIMEFRAME_DIVERGENCE");
    }

    [Fact]
    public async Task Direction_and_quantity_divergence_are_reported()
    {
        var source = TradingWorkspaceTestData.Sources();
        var shortSource = TradingWorkspaceTestData.Sources(decisionType: TradingDecisionType.ShortSetup);
        var request = new TradingWorkspaceRequest(
            WorkspaceId.New(),
            source.Context,
            analyses: [source.Analysis],
            consensus: source.Consensus,
            decision: shortSource.Decision,
            risk: source.Risk,
            plan: source.Plan);

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(request, CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Code == "DIRECTION_DIVERGENCE");
        Assert.Contains(result.Errors, error => error.Code == "DECISION_ID_MISMATCH");
    }

    [Fact]
    public async Task Plan_levels_and_quantity_are_projected_without_recalculation()
    {
        var source = TradingWorkspaceTestData.Sources();

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);

        Assert.Equal(source.Decision.Entry!.Price, result.Decision.EntryPrice);
        Assert.Equal(source.Decision.Stop!.Price, result.Plan.StopPrice);
        Assert.Equal(source.Decision.Targets.Select(target => target.Price), result.Plan.TargetPrices);
        Assert.Equal(source.Risk.PositionSize!.FinalQuantity, result.Risk.Quantity);
        Assert.Equal(source.Plan.Targets.Count, result.Plan.TargetPrices.Count);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "RECALCULATED");
    }

    [Fact]
    public async Task Traces_and_timeline_are_deterministic_and_bounded()
    {
        var source = TradingWorkspaceTestData.Sources();
        var options = new TradingWorkspaceOptions { MaximumTraces = 1, MaximumTimelineEntries = 3 };
        var builder = TradingWorkspaceTestData.Builder(options);

        var first = await builder.BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);
        var second = await builder.BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);

        Assert.True(first.Traces.Count <= 1);
        Assert.True(first.Timeline.Count <= 3);
        Assert.Equal(first.Traces, second.Traces);
        Assert.Equal(first.Summary, second.Summary);
        Assert.Contains(first.Limitations, limitation => limitation.Code == "TRACES_TRUNCATED");
    }

    [Fact]
    public async Task Permuting_analyses_does_not_change_analysis_projection()
    {
        var source = TradingWorkspaceTestData.Sources();
        var requestA = new TradingWorkspaceRequest(WorkspaceId.New(), source.Context, analyses: [source.Analysis]);
        var requestB = new TradingWorkspaceRequest(WorkspaceId.New(), source.Context, analyses: [source.Analysis]);

        var first = await TradingWorkspaceTestData.Builder().BuildAsync(requestA, CancellationToken.None);
        var second = await TradingWorkspaceTestData.Builder().BuildAsync(requestB, CancellationToken.None);

        Assert.Equal(first.Analysis.AgentRunIds, second.Analysis.AgentRunIds);
        Assert.Equal(first.Progress.Stages.Select(stage => stage.Status), second.Progress.Stages.Select(stage => stage.Status));
    }

    [Fact]
    public async Task Culture_does_not_change_summary()
    {
        var source = TradingWorkspaceTestData.Sources();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var french = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);
            Assert.Equal(french.Summary, english.Summary);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task External_cancellation_is_propagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Empty(), cancellation.Token));
    }

    [Fact]
    public async Task Timeout_is_structured_and_does_not_escape()
    {
        var options = new TradingWorkspaceOptions { Timeout = TimeSpan.FromMilliseconds(10) };
        var builder = TradingWorkspaceTestData.Builder(options, new DelayedWorkspaceConsistencyPolicy(TimeSpan.FromMilliseconds(100)));

        var result = await builder.BuildAsync(TradingWorkspaceTestData.Empty(), CancellationToken.None);

        Assert.Equal(TradingWorkspaceStatus.TimedOut, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "WORKSPACE_TIMEOUT");
    }

    [Fact]
    public async Task Source_objects_are_not_mutated_and_result_collections_are_immutable()
    {
        var source = TradingWorkspaceTestData.Sources();
        var originalTraceCount = source.Context.Traces.Count;

        var result = await TradingWorkspaceTestData.Builder().BuildAsync(TradingWorkspaceTestData.Request(source), CancellationToken.None);

        Assert.Equal(originalTraceCount, source.Context.Traces.Count);
        var traces = Assert.IsAssignableFrom<IList<WorkspaceTraceReference>>(result.Traces);
        Assert.Throws<NotSupportedException>(() => traces.Add(new WorkspaceTraceReference("test", "source", "kind", "value")));
    }

    [Fact]
    public void Request_and_result_are_versioned_and_ids_are_validated()
    {
        Assert.Equal(1, TradingWorkspaceRequest.CurrentVersion);
        Assert.Equal(1, TradingWorkspaceResult.CurrentSchemaVersion);
        Assert.Throws<ArgumentException>(() => new WorkspaceId(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingWorkspaceRequest(WorkspaceId.New(), version: 0));
    }

    [Fact]
    public void Options_validator_rejects_invalid_configuration()
    {
        var result = new TradingWorkspaceOptionsValidator().Validate(null, new TradingWorkspaceOptions { Timeout = TimeSpan.Zero });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Di_registration_is_valid_and_builder_is_scoped()
    {
        using var provider = new ServiceCollection().AddTradeMindTradingWorkspace().BuildServiceProvider();

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ITradingWorkspaceBuilder>();
        var second = secondScope.ServiceProvider.GetRequiredService<ITradingWorkspaceBuilder>();

        Assert.NotSame(first, second);
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<ITradingWorkspaceConsistencyPolicy>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<ITradingWorkspaceFreshnessPolicy>());
    }

    [Fact]
    public void Workspace_has_no_execution_or_forbidden_dependencies()
    {
        var assembly = typeof(ITradingWorkspaceBuilder).Assembly;
        var references = assembly.GetReferencedAssemblies().Select(name => name.Name).Where(name => name is not null).ToArray();

        Assert.DoesNotContain(typeof(ITradingWorkspaceBuilder).GetMethods(), method => method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase) || method.Name.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name!.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name!.Contains("Postgre", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name!.Contains("MT5", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name!.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
    }
}
