using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Dispatch.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Dispatch.Application;

public interface IExpertDispatcher
{
    Task<AgentDispatchPlan> PlanAsync(
        MarketContext context,
        AgentDispatchRequest request,
        CancellationToken cancellationToken);

    Task<AgentDispatchExecutionResult> DispatchAsync(
        MarketContext context,
        AgentDispatchRequest request,
        CancellationToken cancellationToken);
}

public sealed class ExpertDispatcher(
    IAgentDispatchPlanner planner,
    IExpertAgentExecutor agentExecutor,
    IOptions<AgentDispatchOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpertDispatcher> logger) : IExpertDispatcher
{
    private readonly AgentDispatchOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public Task<AgentDispatchPlan> PlanAsync(
        MarketContext context,
        AgentDispatchRequest request,
        CancellationToken cancellationToken) =>
        planner.PlanAsync(context, request, cancellationToken);

    public async Task<AgentDispatchExecutionResult> DispatchAsync(
        MarketContext context,
        AgentDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = await planner.PlanAsync(context, request, cancellationToken).ConfigureAwait(false);
        if (!plan.IsExecutable)
        {
            return new AgentDispatchExecutionResult(
                plan,
                DispatchExecutionStatus.Rejected,
                [],
                timeProvider.GetUtcNow());
        }

        var timeout = request.GlobalTimeout ?? _options.DefaultGlobalTimeout;
        if (timeout > _options.MaximumGlobalTimeout)
        {
            timeout = _options.MaximumGlobalTimeout;
        }

        using var globalTimeout = new CancellationTokenSource(timeout, timeProvider);
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, globalTimeout.Token);
        var results = new ConcurrentDictionary<int, AgentAnalysisResult>();
        var globalTimedOut = false;

        try
        {
            for (var groupIndex = 0; groupIndex < plan.ExecutionGroups.Count; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (globalTimeout.IsCancellationRequested)
                {
                    globalTimedOut = true;
                    break;
                }

                var group = plan.ExecutionGroups[groupIndex];
                var indexedCandidates = group.Candidates
                    .Select(candidate => (Candidate: candidate, Index: FindCandidateIndex(plan, candidate)))
                    .ToArray();
                try
                {
                    await Parallel.ForEachAsync(
                        indexedCandidates,
                        new ParallelOptions
                        {
                            CancellationToken = executionCancellation.Token,
                            MaxDegreeOfParallelism = _options.MaximumParallelism
                        },
                        async (item, token) =>
                        {
                            var result = await agentExecutor.ExecuteAsync(context, item.Candidate.ExecutionRequest, token).ConfigureAwait(false);
                            results[item.Index] = result;
                        }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && globalTimeout.IsCancellationRequested)
                {
                    globalTimedOut = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Agent dispatch cancelled externally. DispatchId={DispatchId}", request.DispatchId);
            throw;
        }

        if (globalTimedOut || globalTimeout.IsCancellationRequested)
        {
            globalTimedOut = true;
            AddMissingResults(plan, results, AgentAnalysisStatus.TimedOut, AgentErrorCode.AgentTimeout, "The dispatch global timeout was exceeded.");
        }

        var orderedResults = plan.Candidates
            .Select((candidate, index) => results.TryGetValue(index, out var result)
                ? result
                : AgentAnalysisResult.Failure(
                    candidate.ExecutionRequest,
                    candidate.Descriptor.Version,
                    AgentAnalysisStatus.Failed,
                    new AgentError(AgentErrorCode.AgentUnavailable, "The dispatch did not produce a result for the selected agent."),
                    timeProvider.GetUtcNow(),
                    timeProvider.GetUtcNow()))
            .ToArray();
        var status = globalTimedOut
            ? DispatchExecutionStatus.TimedOut
            : orderedResults.All(result => result.Status == AgentAnalysisStatus.Succeeded)
                ? DispatchExecutionStatus.Succeeded
                : orderedResults.Any(result => result.Status == AgentAnalysisStatus.Succeeded)
                    ? DispatchExecutionStatus.PartiallySucceeded
                    : DispatchExecutionStatus.Failed;
        logger.LogInformation(
            "Agent dispatch completed. DispatchId={DispatchId}, Status={Status}, ResultCount={ResultCount}, GlobalTimeout={GlobalTimeout}",
            request.DispatchId,
            status,
            orderedResults.Length,
            globalTimedOut);
        return new AgentDispatchExecutionResult(plan, status, orderedResults, timeProvider.GetUtcNow());
    }

    private void AddMissingResults(
        AgentDispatchPlan plan,
        ConcurrentDictionary<int, AgentAnalysisResult> results,
        AgentAnalysisStatus status,
        AgentErrorCode errorCode,
        string message)
    {
        foreach (var (candidate, index) in plan.Candidates.Select((candidate, index) => (candidate, index)))
        {
            results.TryAdd(index, AgentAnalysisResult.Failure(
                candidate.ExecutionRequest,
                candidate.Descriptor.Version,
                status,
                new AgentError(errorCode, message),
                timeProvider.GetUtcNow(),
                timeProvider.GetUtcNow()));
        }
    }

    private static int FindCandidateIndex(
        AgentDispatchPlan plan,
        AgentDispatchCandidate candidate) =>
        plan.Candidates
            .Select((value, index) => (value, index))
            .Single(item => ReferenceEquals(item.value, candidate))
            .index;
}
