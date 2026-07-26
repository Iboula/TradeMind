using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TradeMind.AI.Tools;

public sealed class AIToolExecutor : IAIToolExecutor
{
    private readonly IAIToolRegistry _registry;
    private readonly IAIToolAuthorizer _authorizer;
    private readonly IAIToolArgumentValidator _argumentValidator;
    private readonly AIToolEngineOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AIToolExecutor> _logger;

    public AIToolExecutor(
        IAIToolRegistry registry,
        IAIToolAuthorizer authorizer,
        IAIToolArgumentValidator argumentValidator,
        IOptions<AIToolEngineOptions> options,
        TimeProvider timeProvider,
        ILogger<AIToolExecutor> logger)
    {
        _registry = registry;
        _authorizer = authorizer;
        _argumentValidator = argumentValidator;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AIToolExecutionResult> ExecuteAsync(
        AIToolExecutionRequest request,
        AIToolAuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authorizationContext);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = _timeProvider.GetUtcNow();
        var startTimestamp = _timeProvider.GetTimestamp();

        _logger.LogInformation(
            "AI tool execution requested for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, conversation {ConversationId}, tenant {TenantId}, user {UserId}, agent {AgentId}, and scenario {Scenario}",
            request.ToolId.Value,
            request.SessionId,
            request.CorrelationId,
            request.ConversationId,
            request.TenantId,
            request.UserId,
            request.AgentId,
            request.Scenario);

        IAITool tool;
        try
        {
            tool = await _registry.GetAsync(request.ToolId, cancellationToken).ConfigureAwait(false);
        }
        catch (AIToolNotFoundException exception)
        {
            _logger.LogWarning(
                "AI tool resolution failed for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, and error code {ErrorCode}",
                request.ToolId.Value,
                request.SessionId,
                request.CorrelationId,
                exception.ErrorCode);
            throw;
        }

        _logger.LogInformation(
            "AI tool resolved for tool {ToolId}, version {Version}, session {SessionId}, and correlation {CorrelationId}",
            tool.Definition.Id.Value,
            tool.Definition.Version,
            request.SessionId,
            request.CorrelationId);

        try
        {
            _authorizer.Authorize(tool.Definition, authorizationContext);
            _logger.LogInformation(
                "AI tool authorization granted for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, and side effect {SideEffectLevel}",
                tool.Definition.Id.Value,
                request.SessionId,
                request.CorrelationId,
                tool.Definition.SideEffectLevel);
        }
        catch (Exception exception) when (exception is AIToolAuthorizationException or AIToolUnavailableException)
        {
            var errorCode = exception is AIToolAuthorizationException authorizationException
                ? authorizationException.ErrorCode
                : ((AIToolUnavailableException)exception).ErrorCode;
            _logger.LogWarning(
                "AI tool authorization refused for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, and error code {ErrorCode}",
                tool.Definition.Id.Value,
                request.SessionId,
                request.CorrelationId,
                errorCode);
            throw;
        }

        AIToolArguments arguments;
        try
        {
            arguments = _argumentValidator.Validate(tool.Definition, request.Arguments, request.CorrelationId);
            _logger.LogInformation(
                "AI tool argument validation succeeded for tool {ToolId}, session {SessionId}, and correlation {CorrelationId}",
                tool.Definition.Id.Value,
                request.SessionId,
                request.CorrelationId);
        }
        catch (AIToolValidationException exception)
        {
            _logger.LogWarning(
                "AI tool argument validation failed for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, and error code {ErrorCode}",
                tool.Definition.Id.Value,
                request.SessionId,
                request.CorrelationId,
                exception.ErrorCode);
            throw;
        }

        var timeout = ResolveTimeout(request, tool.Definition);
        var context = new AIToolExecutionContext(request, arguments, authorizationContext, _timeProvider);

        using var timeoutSource = new CancellationTokenSource(timeout, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            _logger.LogInformation(
                "AI tool execution started for tool {ToolId}, version {Version}, side effect {SideEffectLevel}, session {SessionId}, and correlation {CorrelationId}",
                tool.Definition.Id.Value,
                tool.Definition.Version,
                tool.Definition.SideEffectLevel,
                request.SessionId,
                request.CorrelationId);

            var result = await tool.ExecuteAsync(context, linkedSource.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Tool returned no execution result.");
            var completedAtUtc = _timeProvider.GetUtcNow();
            var duration = _timeProvider.GetElapsedTime(startTimestamp);
            var normalized = result.Normalize(
                tool.Definition.Id,
                startedAtUtc,
                completedAtUtc,
                duration,
                request.IdempotencyKey);

            _logger.LogInformation(
                "AI tool execution completed for tool {ToolId}, version {Version}, session {SessionId}, correlation {CorrelationId}, success {Success}, error code {ErrorCode}, and duration {DurationMs} ms",
                tool.Definition.Id.Value,
                tool.Definition.Version,
                request.SessionId,
                request.CorrelationId,
                normalized.Success,
                normalized.Error?.Code,
                normalized.Duration.TotalMilliseconds);

            return normalized;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "AI tool execution cancelled by caller for tool {ToolId}, session {SessionId}, and correlation {CorrelationId}",
                request.ToolId.Value,
                request.SessionId,
                request.CorrelationId);
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutSource.IsCancellationRequested)
        {
            var metrics = new AIToolExecutionMetrics(
                startedAtUtc,
                _timeProvider.GetUtcNow(),
                _timeProvider.GetElapsedTime(startTimestamp),
                timedOut: true);

            _logger.LogWarning(
                "AI tool execution timed out for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, and duration {DurationMs} ms",
                request.ToolId.Value,
                request.SessionId,
                request.CorrelationId,
                metrics.Duration.TotalMilliseconds);

            throw new AIToolTimeoutException(
                request.ToolId,
                timeout,
                request.CorrelationId,
                exception,
                metrics);
        }
        catch (Exception exception) when (exception is not AIToolExecutionException)
        {
            var metrics = new AIToolExecutionMetrics(
                startedAtUtc,
                _timeProvider.GetUtcNow(),
                _timeProvider.GetElapsedTime(startTimestamp));

            _logger.LogError(
                "AI tool execution failed for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, error code {ErrorCode}, and duration {DurationMs} ms",
                request.ToolId.Value,
                request.SessionId,
                request.CorrelationId,
                "AI_TOOL_EXECUTION_FAILED",
                metrics.Duration.TotalMilliseconds);

            throw new AIToolExecutionException(
                request.ToolId,
                _options.IncludeDetailedInternalErrors
                    ? $"Tool execution failed with internal exception type {exception.GetType().Name}."
                    : "Tool execution failed.",
                request.CorrelationId,
                exception,
                metrics);
        }
    }

    private TimeSpan ResolveTimeout(AIToolExecutionRequest request, AIToolDefinition definition)
    {
        var requestedTimeout = request.TimeoutOverride
            ?? definition.DefaultTimeout
            ?? _options.DefaultTimeout;

        return requestedTimeout > _options.MaximumTimeout
            ? _options.MaximumTimeout
            : requestedTimeout;
    }
}
