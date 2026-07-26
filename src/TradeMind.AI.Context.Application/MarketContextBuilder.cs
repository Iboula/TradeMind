using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Application;

public sealed class MarketContextBuilder : IMarketContextBuilder
{
    private readonly IContextProviderRegistry _registry;
    private readonly IContextFreshnessPolicy _freshnessPolicy;
    private readonly IContextQualityPolicy _qualityPolicy;
    private readonly IContextSizePolicy _sizePolicy;
    private readonly ContextEngineOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MarketContextBuilder> _logger;

    public MarketContextBuilder(
        IContextProviderRegistry registry,
        IContextFreshnessPolicy freshnessPolicy,
        IContextQualityPolicy qualityPolicy,
        IContextSizePolicy sizePolicy,
        IOptions<ContextEngineOptions> options,
        TimeProvider timeProvider,
        ILogger<MarketContextBuilder> logger)
    {
        _registry = registry;
        _freshnessPolicy = freshnessPolicy;
        _qualityPolicy = qualityPolicy;
        _sizePolicy = sizePolicy;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<MarketContextBuildResult> BuildAsync(
        BuildMarketContextQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var buildStartedTimestamp = _timeProvider.GetTimestamp();

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return Failure(request, buildStartedTimestamp, [], [], [validationError], []);
        }

        if (request.ContextVersion != MarketContext.CurrentVersion)
        {
            return Failure(
                request,
                buildStartedTimestamp,
                [],
                [],
                [new ContextBuildError(
                    ContextBuildErrorCode.UnsupportedContextVersion,
                    $"Context version {request.ContextVersion} is not supported.")],
                []);
        }

        var plan = _registry.CreateExecutionPlan(request.RequestedCategories);
        if (plan.Count == 0)
        {
            return Failure(
                request,
                buildStartedTimestamp,
                [],
                [],
                [new ContextBuildError(
                    ContextBuildErrorCode.MissingRequiredSource,
                    "No context providers are registered for this request.")],
                []);
        }

        using var globalTimeout = new CancellationTokenSource(_options.GlobalTimeout, _timeProvider);
        using var buildCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            globalTimeout.Token);
        var executions = new Dictionary<ContextProviderId, ProviderExecution>();

        foreach (var level in plan)
        {
            var tasks = level.Select(provider => ExecuteOrSkipAsync(
                provider,
                request,
                executions,
                cancellationToken,
                globalTimeout.Token,
                buildCancellation.Token));
            var levelExecutions = await Task.WhenAll(tasks).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var execution in levelExecutions)
            {
                executions.Add(execution.Provider.Descriptor.Id, execution);
            }
        }

        var builtAtUtc = _timeProvider.GetUtcNow();
        var orderedExecutions = plan
            .SelectMany(level => level)
            .Select(provider => executions[provider.Descriptor.Id])
            .ToArray();
        var traces = orderedExecutions
            .Select(execution => CreateTrace(execution, builtAtUtc))
            .ToArray();
        var buildDuration = _timeProvider.GetElapsedTime(buildStartedTimestamp);
        var descriptors = orderedExecutions
            .Select(execution => execution.Provider.Descriptor)
            .ToArray();
        var quality = _qualityPolicy.Evaluate(descriptors, traces);
        var successfulData = orderedExecutions
            .Where(execution => execution.Result.Status == ContextProviderExecutionStatus.Succeeded)
            .Select(execution => execution.Result.Data!)
            .ToArray();
        var normalized = _sizePolicy.Normalize(successfulData);
        var warnings = new List<ContextBuildWarning>(normalized.Warnings);
        var errors = new List<ContextBuildError>();
        var failed = false;
        var partial = false;

        foreach (var execution in orderedExecutions)
        {
            var descriptor = execution.Provider.Descriptor;
            if (execution.Result.Status == ContextProviderExecutionStatus.Succeeded)
            {
                continue;
            }

            var normalOptionalAbsence = descriptor.Requirement == ContextRequirement.Optional
                && execution.Result.Status is ContextProviderExecutionStatus.Unavailable
                    or ContextProviderExecutionStatus.NotConfigured;
            if (normalOptionalAbsence)
            {
                warnings.Add(new ContextBuildWarning(
                    "optional-source-unavailable",
                    execution.Result.Error?.Message ?? "An optional context source is unavailable.",
                    descriptor.Id));
                continue;
            }

            errors.Add(execution.Result.Error ?? new ContextBuildError(
                ContextBuildErrorCode.InvalidProviderResult,
                "The provider returned an invalid result.",
                descriptor.Id));

            if (descriptor.Requirement == ContextRequirement.Required)
            {
                failed = true;
                errors.Add(new ContextBuildError(
                    ContextBuildErrorCode.MissingRequiredSource,
                    $"Required context source '{descriptor.Id}' did not succeed.",
                    descriptor.Id));
            }
            else
            {
                partial = true;
            }

            if (execution.Result.Error?.Code == ContextBuildErrorCode.GlobalTimeout)
            {
                failed = true;
            }
        }

        if (normalized.Market is null)
        {
            failed = true;
            errors.Add(new ContextBuildError(
                ContextBuildErrorCode.MissingRequiredSource,
                "A market snapshot is required to materialize MarketContext."));
        }

        warnings = warnings.Take(_options.MaximumWarnings).ToList();
        if (failed)
        {
            return new FailedMarketContextBuildResult(
                traces,
                warnings,
                errors,
                quality,
                request.CorrelationId,
                buildDuration);
        }

        var status = partial
            ? MarketContextBuildStatus.PartiallySucceeded
            : MarketContextBuildStatus.Succeeded;
        var context = new MarketContext(
            MarketContextId.New(),
            request.ContextVersion,
            request.UserId,
            request.SessionId,
            request.Instrument,
            request.Timeframe,
            builtAtUtc,
            status,
            normalized.Market!.Snapshot,
            normalized.TraderProfile?.Context,
            normalized.Workspace?.Context,
            normalized.Memory?.Context,
            normalized.Knowledge?.Context,
            normalized.EconomicCalendar?.Context,
            normalized.News?.Context,
            traces,
            warnings,
            quality);

        _logger.LogInformation(
            "Market context {ContextId} version {Version} built for session {SessionId}, correlation {CorrelationId}, instrument {Instrument}, timeframe {Timeframe}, status {Status}, quality {QualityScore}, duration {Duration}, executed providers {ExecutedProviderCount}, skipped providers {SkippedProviderCount}, warnings {WarningCount}, and errors {ErrorCount}",
            context.Id,
            context.Version,
            context.SessionId,
            request.CorrelationId,
            context.Instrument.Symbol,
            context.Timeframe.Code,
            context.Status,
            context.Quality.Score,
            buildDuration,
            traces.Count(trace => trace.Status != ContextProviderExecutionStatus.Skipped),
            traces.Count(trace => trace.Status == ContextProviderExecutionStatus.Skipped),
            warnings.Count,
            errors.Count);

        return partial
            ? new PartiallySucceededMarketContextBuildResult(
                context,
                errors,
                request.CorrelationId,
                buildDuration)
            : new SucceededMarketContextBuildResult(
                context,
                request.CorrelationId,
                buildDuration);
    }

    private async Task<ProviderExecution> ExecuteOrSkipAsync(
        IContextProvider provider,
        BuildMarketContextQuery query,
        IReadOnlyDictionary<ContextProviderId, ProviderExecution> completed,
        CancellationToken callerCancellation,
        CancellationToken globalTimeout,
        CancellationToken buildCancellation)
    {
        var dependencyResults = provider.Descriptor.Dependencies
            .ToDictionary(id => id, id => completed[id].Result);
        var failedDependency = dependencyResults
            .FirstOrDefault(pair => pair.Value.Status != ContextProviderExecutionStatus.Succeeded);
        if (!failedDependency.Equals(default(KeyValuePair<ContextProviderId, ContextProviderResult>)))
        {
            var now = _timeProvider.GetUtcNow();
            return new ProviderExecution(
                provider,
                ContextProviderResult.Skipped(
                    provider.Descriptor.Id,
                    $"Dependency '{failedDependency.Key}' did not succeed."),
                now,
                now,
                TimeSpan.Zero);
        }

        if (globalTimeout.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow();
            return new ProviderExecution(
                provider,
                ContextProviderResult.TimedOut(provider.Descriptor.Id, true),
                now,
                now,
                TimeSpan.Zero);
        }

        var requestedAtUtc = _timeProvider.GetUtcNow();
        var startedTimestamp = _timeProvider.GetTimestamp();
        using var providerTimeout = new CancellationTokenSource(
            provider.Descriptor.Timeout,
            _timeProvider);
        using var providerCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            buildCancellation,
            providerTimeout.Token);

        ContextProviderResult result;
        try
        {
            result = await provider.ProvideAsync(
                new ContextProviderRequest(query, dependencyResults),
                providerCancellation.Token).ConfigureAwait(false);
            result = globalTimeout.IsCancellationRequested
                ? ContextProviderResult.TimedOut(provider.Descriptor.Id, true)
                : providerTimeout.IsCancellationRequested
                    ? ContextProviderResult.TimedOut(provider.Descriptor.Id, false)
                    : ValidateResult(provider, result);
        }
        catch (OperationCanceledException) when (callerCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (globalTimeout.IsCancellationRequested)
        {
            result = ContextProviderResult.TimedOut(provider.Descriptor.Id, true);
        }
        catch (OperationCanceledException) when (providerTimeout.IsCancellationRequested)
        {
            result = ContextProviderResult.TimedOut(provider.Descriptor.Id, false);
        }

        var completedAtUtc = _timeProvider.GetUtcNow();
        return new ProviderExecution(
            provider,
            result,
            requestedAtUtc,
            completedAtUtc,
            _timeProvider.GetElapsedTime(startedTimestamp));
    }

    private static ContextProviderResult ValidateResult(
        IContextProvider provider,
        ContextProviderResult? result)
    {
        if (result is null
            || (result.Status == ContextProviderExecutionStatus.Succeeded && result.Data is null)
            || (result.Status != ContextProviderExecutionStatus.Succeeded && result.Data is not null)
            || (result.Data is not null && result.Data.Category != provider.Descriptor.Category))
        {
            return ContextProviderResult.Failed(
                provider.Descriptor.Id,
                ContextBuildErrorCode.InvalidProviderResult,
                "The provider result does not match its descriptor.");
        }

        return result;
    }

    private ContextSourceTrace CreateTrace(ProviderExecution execution, DateTimeOffset builtAtUtc)
    {
        var freshness = _freshnessPolicy.Evaluate(
            execution.Provider.Descriptor.Category,
            execution.Result.SourceTimestampUtc,
            builtAtUtc);
        return new ContextSourceTrace(
            execution.Provider.Descriptor.Id,
            execution.Provider.Descriptor.Category,
            execution.Provider.Descriptor.Requirement,
            execution.Result.Status,
            execution.RequestedAtUtc,
            execution.CompletedAtUtc,
            execution.Duration,
            execution.Result.SourceTimestampUtc,
            freshness.Classification,
            execution.Result.ItemCount,
            execution.Result.ProviderVersion,
            execution.Result.References,
            execution.Result.Error,
            freshness.Age,
            freshness.Thresholds);
    }

    private FailedMarketContextBuildResult Failure(
        BuildMarketContextQuery request,
        long buildStartedTimestamp,
        IReadOnlyCollection<ContextSourceTrace> traces,
        IReadOnlyCollection<ContextBuildWarning> warnings,
        IReadOnlyCollection<ContextBuildError> errors,
        IReadOnlyCollection<ContextProviderDescriptor> descriptors)
    {
        return new FailedMarketContextBuildResult(
            traces,
            warnings,
            errors,
            _qualityPolicy.Evaluate(descriptors, traces),
            request.CorrelationId,
            _timeProvider.GetElapsedTime(buildStartedTimestamp));
    }

    private static ContextBuildError? ValidateRequest(BuildMarketContextQuery request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return new ContextBuildError(ContextBuildErrorCode.InvalidBuildRequest, "UserId is required.");
        }

        return string.IsNullOrWhiteSpace(request.SessionId)
            ? new ContextBuildError(ContextBuildErrorCode.InvalidBuildRequest, "SessionId is required.")
            : null;
    }

    private sealed record ProviderExecution(
        IContextProvider Provider,
        ContextProviderResult Result,
        DateTimeOffset RequestedAtUtc,
        DateTimeOffset CompletedAtUtc,
        TimeSpan Duration);
}
