using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Application;

public interface IExpertAgentExecutor
{
    Task<AgentAnalysisResult> ExecuteAsync(
        MarketContext context,
        AgentExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class ExpertAgentExecutor(
    IExpertAgentRegistry registry,
    IExpertAgentAuthorizationPolicy authorizationPolicy,
    IAgentCompatibilityPolicy compatibilityPolicy,
    IAgentAnalysisResultValidator resultValidator,
    IOptions<ExpertAgentOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpertAgentExecutor> logger) : IExpertAgentExecutor
{
    private static readonly AgentVersion UnknownVersion = new(0, 0, 0);
    private readonly ExpertAgentOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<AgentAnalysisResult> ExecuteAsync(
        MarketContext context,
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = timeProvider.GetUtcNow();
        if (request.Version != AgentExecutionRequest.CurrentVersion)
        {
            return Failure(request, AgentAnalysisStatus.Failed, new(
                AgentErrorCode.InvalidRequest,
                "The agent execution request version is not supported."),
                startedAtUtc);
        }

        if (context.Id != request.MarketContextId)
        {
            return Failure(request, AgentAnalysisStatus.Incompatible, new(
                AgentErrorCode.IncompatibleContext,
                "The request does not target the supplied market context."),
                startedAtUtc);
        }

        if (!registry.TryResolve(
                request.AgentId,
                request.VersionSelection,
                request.ExactVersion,
                out var agent)
            || agent is null)
        {
            var errorCode = request.VersionSelection == AgentVersionSelection.Exact
                ? AgentErrorCode.AgentVersionNotFound
                : AgentErrorCode.AgentNotFound;
            return Failure(request, AgentAnalysisStatus.Unavailable, new(errorCode, "The requested expert agent could not be resolved."), startedAtUtc);
        }

        var authorization = authorizationPolicy.Evaluate(agent.Descriptor, request);
        if (!authorization.Allowed)
        {
            return Failure(
                request,
                authorization.ReasonCode == AgentErrorCode.AgentDisabled
                    ? AgentAnalysisStatus.Unavailable
                    : AgentAnalysisStatus.Unauthorized,
                new(authorization.ReasonCode ?? AgentErrorCode.UnauthorizedAgent, authorization.Message),
                startedAtUtc,
                agent.Descriptor.Version);
        }

        var compatibility = compatibilityPolicy.Evaluate(agent.Descriptor, context, request);
        if (!compatibility.IsCompatible)
        {
            return FailureMany(
                request,
                AgentAnalysisStatus.Incompatible,
                compatibility.Issues
                    .Where(issue => issue.Severity == AgentCompatibilityIssueSeverity.Blocking)
                    .Select(issue => new AgentError(issue.Code, issue.Message))
                    .ToArray(),
                startedAtUtc,
                agent.Descriptor.Version);
        }

        var timeout = ResolveTimeout(agent.Descriptor, request);
        logger.LogInformation(
            "Expert agent execution started. AgentRunId={AgentRunId}, AgentId={AgentId}, AgentVersion={AgentVersion}, MarketContextId={MarketContextId}, UserId={UserId}, SessionId={SessionId}, Instrument={Instrument}, Timeframe={Timeframe}, Mode={Mode}",
            request.AgentRunId.Value,
            agent.Descriptor.Id.Value,
            agent.Descriptor.Version.ToString(),
            context.Id.ToString(),
            request.UserId,
            request.SessionId,
            context.Instrument.Symbol,
            context.Timeframe.Code,
            request.AnalysisMode.ToString());

        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var waitCancellation = new CancellationTokenSource();
        using var externalRegistration = cancellationToken.Register(
            static state => ((CancellationTokenSource)state!).Cancel(),
            waitCancellation);

        Task<AgentAnalysisResult> executionTask;
        try
        {
            executionTask = agent.AnalyzeAsync(context, request, executionCancellation.Token);
        }
        catch (Exception exception)
        {
            return UnexpectedFailure(request, agent.Descriptor.Version, startedAtUtc, exception);
        }

        var timeoutTask = Task.Delay(timeout, timeProvider, waitCancellation.Token);
        var externalCancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, waitCancellation.Token);
        var completedTask = await Task.WhenAny(executionTask, timeoutTask, externalCancellationTask).ConfigureAwait(false);
        waitCancellation.Cancel();

        if (cancellationToken.IsCancellationRequested)
        {
            executionCancellation.Cancel();
            await ObserveCompletionAsync(executionTask).ConfigureAwait(false);
            logger.LogInformation(
                "Expert agent execution cancelled externally. AgentRunId={AgentRunId}, AgentId={AgentId}",
                request.AgentRunId.Value,
                agent.Descriptor.Id.Value);
            throw new OperationCanceledException(cancellationToken);
        }

        if (completedTask == timeoutTask)
        {
            executionCancellation.Cancel();
            await ObserveCompletionAsync(executionTask).ConfigureAwait(false);
            var timeoutResult = Failure(
                request,
                AgentAnalysisStatus.TimedOut,
                new(AgentErrorCode.AgentTimeout, "The expert agent execution exceeded its bounded timeout."),
                startedAtUtc,
                agent.Descriptor.Version);
            logger.LogInformation(
                "Expert agent execution timed out. AgentRunId={AgentRunId}, AgentId={AgentId}, Duration={Duration}",
                request.AgentRunId.Value,
                agent.Descriptor.Id.Value,
                timeoutResult.Duration);
            return timeoutResult;
        }

        AgentAnalysisResult? rawResult;
        try
        {
            rawResult = await executionTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(
                request,
                AgentAnalysisStatus.Cancelled,
                new(AgentErrorCode.AgentCancelled, "The expert agent cancelled its execution."),
                startedAtUtc,
                agent.Descriptor.Version);
        }
        catch (Exception exception)
        {
            return UnexpectedFailure(request, agent.Descriptor.Version, startedAtUtc, exception);
        }

        if (rawResult is null)
        {
            return Failure(
                request,
                AgentAnalysisStatus.Failed,
                new(AgentErrorCode.InvalidAgentResult, "The expert agent returned no result."),
                startedAtUtc,
                agent.Descriptor.Version);
        }

        var validation = resultValidator.ValidateAndNormalize(agent.Descriptor, context, request, rawResult);
        if (!validation.IsValid || validation.Result is null)
        {
            return FailureMany(
                request,
                AgentAnalysisStatus.Failed,
                validation.Errors.Count == 0
                    ? [new AgentError(AgentErrorCode.InvalidAgentResult, "The expert agent result failed validation.")]
                    : validation.Errors,
                startedAtUtc,
                agent.Descriptor.Version);
        }

        var result = AddCompatibilityWarnings(validation.Result, compatibility.Issues);
        logger.LogInformation(
            "Expert agent execution completed. AgentRunId={AgentRunId}, AgentId={AgentId}, AgentVersion={AgentVersion}, MarketContextId={MarketContextId}, Status={Status}, Duration={Duration}, WarningCount={WarningCount}, ErrorCount={ErrorCount}, Confidence={Confidence}",
            result.AgentRunId.Value,
            result.AgentId.Value,
            result.AgentVersion.ToString(),
            result.MarketContextId.ToString(),
            result.Status.ToString(),
            result.Duration,
            result.Warnings.Count,
            result.Errors.Count,
            result.Confidence.Score);
        return result;
    }

    private TimeSpan ResolveTimeout(AgentDescriptor descriptor, AgentExecutionRequest request)
    {
        var requested = request.RequestedTimeout ?? _options.DefaultExecutionTimeout;
        var bounded = requested > _options.MaximumExecutionTimeout
            ? _options.MaximumExecutionTimeout
            : requested;
        if (descriptor.Capabilities.RecommendedTimeout is { } recommended)
        {
            bounded = bounded < recommended ? bounded : recommended;
        }

        return bounded;
    }

    private AgentAnalysisResult UnexpectedFailure(
        AgentExecutionRequest request,
        AgentVersion version,
        DateTimeOffset startedAtUtc,
        Exception exception)
    {
        logger.LogError(
            exception,
            "Expert agent execution failed unexpectedly. AgentRunId={AgentRunId}, AgentId={AgentId}, AgentVersion={AgentVersion}",
            request.AgentRunId.Value,
            request.AgentId.Value,
            version.ToString());
        return Failure(
            request,
            AgentAnalysisStatus.Failed,
            new(AgentErrorCode.UnexpectedAgentFailure, "The expert agent failed unexpectedly."),
            startedAtUtc,
            version);
    }

    private AgentAnalysisResult Failure(
        AgentExecutionRequest request,
        AgentAnalysisStatus status,
        AgentError error,
        DateTimeOffset startedAtUtc,
        AgentVersion? version = null) =>
        FailureMany(request, status, [error], startedAtUtc, version);

    private AgentAnalysisResult FailureMany(
        AgentExecutionRequest request,
        AgentAnalysisStatus status,
        IReadOnlyCollection<AgentError> errors,
        DateTimeOffset startedAtUtc,
        AgentVersion? version = null)
    {
        var completedAtUtc = timeProvider.GetUtcNow();
        return new AgentAnalysisResult(
            request.AgentRunId,
            request.AgentId,
            version ?? UnknownVersion,
            request.MarketContextId,
            status,
            startedAtUtc,
            completedAtUtc,
            AgentDirectionalBias.InsufficientData,
            AgentConfidence.Unavailable,
            errors: errors);
    }

    private static async Task ObserveCompletionAsync(Task<AgentAnalysisResult> task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
    }

    private static AgentAnalysisResult AddCompatibilityWarnings(
        AgentAnalysisResult result,
        IReadOnlyCollection<AgentCompatibilityIssue> issues)
    {
        var warnings = issues
            .Where(issue => issue.Severity == AgentCompatibilityIssueSeverity.Warning)
            .Select(issue => new AgentWarning(issue.Code.ToString(), issue.Message))
            .ToArray();
        if (warnings.Length == 0)
        {
            return result;
        }

        return new AgentAnalysisResult(
            result.AgentRunId,
            result.AgentId,
            result.AgentVersion,
            result.MarketContextId,
            result.Status,
            result.StartedAtUtc,
            result.CompletedAtUtc,
            result.DirectionalBias,
            result.Confidence,
            result.Summary,
            result.Observations,
            result.Evidence,
            result.MarketLevels,
            result.Scenarios,
            result.Invalidations,
            result.Risks,
            result.Warnings.Concat(warnings).ToArray(),
            result.Limitations,
            result.Errors,
            result.SchemaVersion,
            result.Metadata);
    }
}
