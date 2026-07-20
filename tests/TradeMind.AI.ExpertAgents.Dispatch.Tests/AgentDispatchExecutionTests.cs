using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;

namespace TradeMind.AI.ExpertAgents.Dispatch.Tests;

public sealed class AgentDispatchExecutionTests
{
    [Fact]
    public async Task Dispatcher_executes_requirement_groups_in_parallel_and_returns_plan_order()
    {
        var context = DispatcherTestData.Context();
        var first = new TestExpertAgent(DispatcherTestData.Descriptor("first-agent"));
        var second = new TestExpertAgent(DispatcherTestData.Descriptor("second-agent"));
        var third = new TestExpertAgent(DispatcherTestData.Descriptor("third-agent"));
        var planner = DispatcherTestData.Planner([third, first, second]);
        var started = new ConcurrentBag<string>();
        var executor = new TestDispatchExecutor(async (marketContext, request, token) =>
        {
            started.Add(request.AgentId.Value);
            await Task.Delay(30, token);
            var descriptor = request.AgentId.Value switch
            {
                "first-agent" => first.Descriptor,
                "second-agent" => second.Descriptor,
                _ => third.Descriptor
            };
            return DispatcherTestData.Result(marketContext, request, descriptor);
        });
        var dispatcher = new ExpertDispatcher(
            planner,
            executor,
            Options.Create(new AgentDispatchOptions { MaximumParallelism = 2 }),
            new FixedTimeProvider(DispatcherTestData.Now),
            NullLogger<ExpertDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync(context, DispatcherTestData.Request(context, maximumAgents: 3), CancellationToken.None);

        Assert.Equal(DispatchExecutionStatus.Succeeded, result.Status);
        Assert.Equal(["first-agent", "second-agent", "third-agent"], result.Results.Select(item => item.AgentId.Value));
        Assert.Equal(3, started.Count);
    }

    [Fact]
    public async Task Dispatcher_distinguishes_global_timeout_and_observes_started_tasks()
    {
        var context = DispatcherTestData.Context();
        var agent = new TestExpertAgent(DispatcherTestData.Descriptor("slow-agent"));
        var planner = DispatcherTestData.Planner([agent]);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new TestDispatchExecutor(async (_, _, token) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("unreachable");
            }
            catch (OperationCanceledException)
            {
                completed.SetResult();
                throw;
            }
        });
        var dispatcher = new ExpertDispatcher(
            planner,
            executor,
            Options.Create(new AgentDispatchOptions { DefaultGlobalTimeout = TimeSpan.FromMilliseconds(30), MaximumGlobalTimeout = TimeSpan.FromSeconds(1) }),
            new FixedTimeProvider(DispatcherTestData.Now),
            NullLogger<ExpertDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync(context, DispatcherTestData.Request(context, globalTimeout: TimeSpan.FromMilliseconds(30)), CancellationToken.None);

        Assert.Equal(DispatchExecutionStatus.TimedOut, result.Status);
        Assert.Equal(AgentAnalysisStatus.TimedOut, Assert.Single(result.Results).Status);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Dispatcher_propagates_external_cancellation_and_agent_token()
    {
        var context = DispatcherTestData.Context();
        var agent = new TestExpertAgent(DispatcherTestData.Descriptor("cancelled-agent"));
        var planner = DispatcherTestData.Planner([agent]);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokenObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new TestDispatchExecutor(async (_, _, token) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("unreachable");
            }
            catch (OperationCanceledException)
            {
                tokenObserved.SetResult();
                throw;
            }
        });
        var dispatcher = new ExpertDispatcher(
            planner,
            executor,
            Options.Create(new AgentDispatchOptions()),
            new FixedTimeProvider(DispatcherTestData.Now),
            NullLogger<ExpertDispatcher>.Instance);
        using var cancellation = new CancellationTokenSource();
        var dispatchTask = dispatcher.DispatchAsync(context, DispatcherTestData.Request(context), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatchTask);
        await tokenObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
