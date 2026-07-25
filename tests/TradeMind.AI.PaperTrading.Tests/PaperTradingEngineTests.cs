using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.PaperTrading.Application;
using TradeMind.AI.PaperTrading.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingWorkspace.Domain;
using TradeMind.Market.Abstractions;
using Xunit;

namespace TradeMind.AI.PaperTrading.Tests;

public sealed class PaperTradingEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Public_identifiers_reject_empty_values_and_ticks_validate_prices()
    {
        Assert.Throws<ArgumentException>(() => new PaperTradingSimulationId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new PaperTradingPositionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new PaperTradingExecutionId(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaperTradingMarketTick(new Instrument("EURUSD"), Now, 0m, 1m));
        Assert.Throws<ArgumentException>(() => new PaperTradingMarketTick(new Instrument("EURUSD"), Now.ToOffset(TimeSpan.FromHours(1)), 1m, 1.1m));
    }

    [Fact]
    public void Request_rejects_duplicate_sequences_and_ticks_outside_interval()
    {
        var source = PaperTradingTestData.Create();
        var duplicate = new[]
        {
            new PaperTradingMarketTick(source.Plan.Instrument, Now, 100m, 100.1m, 7),
            new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(1), 100m, 100.1m, 7)
        };
        Assert.Throws<ArgumentException>(() => new PaperTradingRequest(new PaperTradingSimulationId(Guid.NewGuid()), source.Workspace, source.Plan, duplicate, 1_000m));
        Assert.Throws<ArgumentException>(() => new PaperTradingRequest(new PaperTradingSimulationId(Guid.NewGuid()), source.Workspace, source.Plan, [new PaperTradingMarketTick(source.Plan.Instrument, Now, 100m, 100.1m)], 1_000m, startAtUtc: Now.AddMinutes(1)));
    }

    [Fact]
    public void Ticks_are_sorted_by_utc_timestamp_then_stable_sequence()
    {
        var source = PaperTradingTestData.Create();
        var request = new PaperTradingRequest(
            new PaperTradingSimulationId(Guid.Parse("88888888-8888-8888-8888-888888888888")),
            source.Workspace,
            source.Plan,
            [
                new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(2), 102m, 102.1m, 2),
                new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(1), 100m, 100.1m, 1),
                new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(1), 100.2m, 100.3m, 0)
            ],
            1_000m);

        Assert.Equal([0L, 1L, 2L], request.MarketTicks.Select(tick => tick.Sequence));
        Assert.Equal(Now.AddMinutes(1), request.MarketTicks[0].TimestampUtc);
    }

    [Fact]
    public async Task New_engine_contract_exposes_source_ids_position_execution_and_account_values()
    {
        var source = PaperTradingTestData.Create();
        var request = NewRequest(source, [
            new(source.Plan.Instrument, Now.AddMinutes(1), 99.9m, 100m, 1),
            new(source.Plan.Instrument, Now.AddMinutes(2), 102m, 102.1m, 2)
        ]);
        var result = await PaperTradingTestData.Simulator().SimulateAsync(request);

        Assert.Equal(request.SimulationId, result.SimulationId);
        Assert.Equal(source.Workspace.WorkspaceId, result.SourceWorkspaceId);
        Assert.Equal(source.Plan.PlanId, result.SourcePlanId);
        Assert.Equal(2, result.Executions.Count);
        Assert.Equal(1_002m, result.FinalBalance);
        Assert.Equal(result.FinalBalance, result.FinalEquity);
        Assert.NotEmpty(result.Traces);
        Assert.Equal(PaperTradingSimulationState.Completed, result.SimulationState);
    }

    [Fact]
    public async Task Explicit_commission_and_slippage_are_applied_without_hidden_costs()
    {
        var source = PaperTradingTestData.Create();
        var request = NewRequest(source, [
            new(source.Plan.Instrument, Now.AddMinutes(1), 99.9m, 100m, 1),
            new(source.Plan.Instrument, Now.AddMinutes(2), 102m, 102.1m, 2)
        ], new PaperTradingSimulationOptions { SlippagePerUnit = 0.1m, CommissionPerUnit = 1m });
        var result = await PaperTradingTestData.Simulator().SimulateAsync(request);

        Assert.Equal(100.1m, result.Fills[0].Price);
        Assert.Equal(101.9m, result.Fills[1].Price);
        Assert.Equal(-0.2m, result.RealizedPnl);
        Assert.Equal(2m, result.Pnl.Commission);
    }

    [Fact]
    public async Task Ambiguous_tick_defaults_to_conservative_stop_first()
    {
        var source = PaperTradingTestData.Create();
        var request = NewRequest(source, [
            new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(1), 99.9m, 100m, 1),
            new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(2), 100m, 100.1m, 2, high: 103m, low: 89m)
        ]);
        var result = await PaperTradingTestData.Simulator().SimulateAsync(request);

        Assert.Equal(PaperExitReason.StopLoss, result.Journal.Single().ExitReason);
        Assert.Contains(result.Timeline, item => item.Type == PaperTradingEventType.StopLossTriggered);
    }

    [Fact]
    public async Task Ambiguous_tick_can_be_rejected_explicitly()
    {
        var source = PaperTradingTestData.Create();
        var request = NewRequest(source, [
            new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(1), 99.9m, 100m, 1),
            new PaperTradingMarketTick(source.Plan.Instrument, Now.AddMinutes(2), 100m, 100.1m, 2, high: 103m, low: 89m)
        ], new PaperTradingSimulationOptions { AmbiguousTriggerPolicy = PaperAmbiguousTriggerPolicy.RejectAmbiguousTick });
        var result = await PaperTradingTestData.Simulator().SimulateAsync(request);

        Assert.Equal(PaperTradingStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "AMBIGUOUS_TICK_REJECTED");
        Assert.NotEmpty(result.Blockers);
    }

    [Fact]
    public async Task Configured_target_allocations_produce_partial_then_final_exit()
    {
        var source = PaperTradingTestData.Create(targetCount: 2);
        var request = NewRequest(source, [
            new(source.Plan.Instrument, Now.AddMinutes(1), 99.9m, 100m, 1),
            new(source.Plan.Instrument, Now.AddMinutes(2), 102m, 102.1m, 2),
            new(source.Plan.Instrument, Now.AddMinutes(3), 104m, 104.1m, 3)
        ], new PaperTradingSimulationOptions { TargetAllocations = [new PaperTargetAllocation(1, 0.4m), new PaperTargetAllocation(2, 0.6m)] });
        var result = await PaperTradingTestData.Simulator().SimulateAsync(request);

        Assert.Equal(PaperTradingStatus.Succeeded, result.Status);
        Assert.Contains(result.Timeline, item => item.Type == PaperTradingEventType.PartialTargetHit);
        Assert.Contains(result.Timeline, item => item.Type == PaperTradingEventType.FinalTargetHit);
        Assert.Equal(3, result.Fills.Count);
        Assert.Equal(0.4m, result.Fills[1].Quantity);
        Assert.Equal(0.6m, result.Fills[2].Quantity);
    }

    [Fact]
    public async Task Expired_plan_and_blocked_workspace_are_distinct_non_execution_results()
    {
        var expired = PaperTradingTestData.Create(expiresAtUtc: Now.AddMinutes(1));
        var expiredResult = await PaperTradingTestData.Simulator().SimulateAsync(NewRequest(expired, []));
        var blocked = PaperTradingTestData.Create(workspaceState: TradingWorkspaceState.Blocked);
        var blockedResult = await PaperTradingTestData.Simulator().SimulateAsync(NewRequest(blocked, []));

        Assert.Equal(PaperTradingStatus.Expired, expiredResult.Status);
        Assert.Equal(PaperTradingStatus.Blocked, blockedResult.Status);
        Assert.NotEmpty(blockedResult.Blockers);
        Assert.Empty(blockedResult.Orders);
    }

    [Fact]
    public async Task Workspace_and_plan_identifiers_must_match()
    {
        var source = PaperTradingTestData.Create();
        var other = PaperTradingTestData.Create(planGuid: Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var result = await PaperTradingTestData.Simulator().SimulateAsync(NewRequest((source.Workspace, other.Plan), []));

        Assert.Equal(PaperTradingStatus.Invalid, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "PLAN_ID_MISMATCH");
    }

    [Fact]
    public async Task Timeout_returns_a_structured_result_and_external_cancellation_is_not_swallowed()
    {
        var source = PaperTradingTestData.Create();
        var ticks = Enumerable.Range(0, 10_000).Select(index => new PaperTradingMarketTick(source.Plan.Instrument, Now.AddTicks(index + 1), 99.9m, 100m, index)).ToArray();
        var timeoutResult = await PaperTradingTestData.Simulator().SimulateAsync(NewRequest(source, ticks, timeout: TimeSpan.FromMilliseconds(1)));
        Assert.Equal(PaperTradingStatus.TimedOut, timeoutResult.Status);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => PaperTradingTestData.Simulator().SimulateAsync(NewRequest(source, []), cancellation.Token));
    }

    [Fact]
    public async Task Result_collections_are_defensively_copied_and_replay_is_stable()
    {
        var source = PaperTradingTestData.Create();
        var request = NewRequest(source, [new(source.Plan.Instrument, Now.AddMinutes(1), 99.9m, 100m, 1)]);
        var first = await PaperTradingTestData.Simulator().SimulateAsync(request);
        var second = await PaperTradingTestData.Simulator().SimulateAsync(request);
        var list = (IList<PaperTradingEvent>)first.Timeline;

        Assert.Equal(first.ReplayFingerprint, second.ReplayFingerprint);
        Assert.Throws<NotSupportedException>(() => list.Add(first.Timeline[0]));
        Assert.Equal(first.SourcePlanId, second.SourcePlanId);
    }

    [Fact]
    public void Dependency_injection_exposes_engine_without_infrastructure_dependencies()
    {
        using var provider = new ServiceCollection().AddTradeMindPaperTrading().BuildServiceProvider();
        Assert.IsType<PaperTradingEngine>(provider.GetRequiredService<IPaperTradingEngine>());
        var forbidden = typeof(PaperTradingEngine).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).Where(name => name is not null).ToArray();
        Assert.DoesNotContain(forbidden, name => name!.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(forbidden, name => name!.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(forbidden, name => name!.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(forbidden, name => name!.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
    }

    private static PaperTradingRequest NewRequest((TradingWorkspaceResult Workspace, TradingPlanResult Plan) source, IReadOnlyCollection<PaperTradingMarketTick> ticks, PaperTradingSimulationOptions? options = null, TimeSpan? timeout = null) =>
        new(new PaperTradingSimulationId(Guid.Parse("77777777-7777-7777-7777-777777777777")), source.Workspace, source.Plan, ticks, 1_000m, timeout, options);
}
