using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.Application;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public sealed class AIAgentExecutor : IAIAgentExecutor
{
    private readonly IAIAgentRegistry _registry;
    private readonly IAIAgentAuthorizer _authorizer;
    private readonly IAIAgentRequestMapper _requestMapper;
    private readonly IAIAgentResponseMapper _responseMapper;
    private readonly IAIOrchestrator _orchestrator;
    private readonly TimeProvider _timeProvider;
    private readonly AIAgentFrameworkOptions _options;
    private readonly ILogger<AIAgentExecutor> _logger;

    public AIAgentExecutor(
        IAIAgentRegistry registry,
        IAIAgentAuthorizer authorizer,
        IAIAgentRequestMapper requestMapper,
        IAIAgentResponseMapper responseMapper,
        IAIOrchestrator orchestrator,
        TimeProvider timeProvider,
        IOptions<AIAgentFrameworkOptions> options,
        ILogger<AIAgentExecutor> logger)
    {
        _registry = registry;
        _authorizer = authorizer;
        _requestMapper = requestMapper;
        _responseMapper = responseMapper;
        _orchestrator = orchestrator;
        _timeProvider = timeProvider;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<AIAgentExecutionResponse> ExecuteAsync(
        AIAgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAtUtc = _timeProvider.GetUtcNow();
        var totalTimestamp = _timeProvider.GetTimestamp();
        var resolutionDuration = TimeSpan.Zero;
        var authorizationDuration = TimeSpan.Zero;
        var mappingDuration = TimeSpan.Zero;
        var orchestrationDuration = TimeSpan.Zero;
        IAIAgent? agent = null;
        AIAgentExecutionContext? context = null;
        CancellationTokenSource? timeoutSource = null;
        CancellationTokenSource? executionSource = null;

        _logger.LogInformation(
            "AI agent resolution started for agent {AgentId}, selection {VersionSelection}, scenario {Scenario}, and correlation {CorrelationId}",
            request.AgentId.Value,
            request.VersionSelection,
            request.Scenario,
            request.CorrelationId);

        try
        {
            var timestamp = _timeProvider.GetTimestamp();
            agent = await _registry.GetAsync(
                request.AgentId,
                request.VersionSelection,
                request.ExactVersion,
                cancellationToken).ConfigureAwait(false);
            resolutionDuration = _timeProvider.GetElapsedTime(timestamp);

            context = new AIAgentExecutionContext(agent.Definition, request, startedAtUtc)
            {
                State = AIAgentExecutionState.Authorizing
            };

            _logger.LogInformation(
                "AI agent version {AgentVersion} selected for agent {AgentId} and correlation {CorrelationId}",
                agent.Definition.Version.ToString(),
                agent.Definition.Id.Value,
                request.CorrelationId);

            timestamp = _timeProvider.GetTimestamp();
            _logger.LogInformation(
                "AI agent authorization started for agent {AgentId}, version {AgentVersion}, scenario {Scenario}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.Scenario,
                request.CorrelationId);
            _authorizer.Authorize(
                agent.Definition,
                request,
                new AIAgentAuthorizationContext(
                    request.TenantId,
                    request.UserId,
                    request.Permissions,
                    request.Scenario,
                    _options.MaximumAllowedSideEffectLevel,
                    request.RequestedCapabilities,
                    _options.EnableDevelopmentAgents));
            authorizationDuration = _timeProvider.GetElapsedTime(timestamp);

            var timeout = ResolveTimeout(agent.Definition, request);
            timeoutSource = new CancellationTokenSource(timeout, _timeProvider);
            executionSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            var executionToken = executionSource.Token;

            _logger.LogInformation(
                "AI agent authorization completed for agent {AgentId}, version {AgentVersion}, tenant {TenantId}, user {UserId}, scenario {Scenario}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.TenantId,
                request.UserId,
                request.Scenario,
                request.CorrelationId);

            _logger.LogInformation(
                "AI agent starting hook started for agent {AgentId}, version {AgentVersion}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.CorrelationId);
            await agent.OnExecutionStartingAsync(context, executionToken).ConfigureAwait(false);
            _logger.LogInformation(
                "AI agent starting hook completed for agent {AgentId}, version {AgentVersion}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.CorrelationId);

            context.State = AIAgentExecutionState.Mapping;
            timestamp = _timeProvider.GetTimestamp();
            _logger.LogInformation(
                "AI agent mapping started for agent {AgentId}, version {AgentVersion}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.CorrelationId);
            var orchestrationRequest = _requestMapper.Map(agent.Definition, request);
            mappingDuration = _timeProvider.GetElapsedTime(timestamp);

            _logger.LogInformation(
                "AI agent policy application and mapping completed for agent {AgentId}, version {AgentVersion}, memory {MemoryUsed}, knowledge {KnowledgeUsed}, tool {ToolUsed}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                orchestrationRequest.Memory.Enabled,
                orchestrationRequest.Knowledge.Enabled,
                orchestrationRequest.Tool.Enabled,
                request.CorrelationId);

            context.State = AIAgentExecutionState.Executing;
            timestamp = _timeProvider.GetTimestamp();
            _logger.LogInformation(
                "AI agent orchestration started for agent {AgentId}, version {AgentVersion}, scenario {Scenario}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.Scenario,
                request.CorrelationId);
            var orchestrationResponse = await _orchestrator.ExecuteAsync(
                orchestrationRequest,
                executionToken).ConfigureAwait(false);
            orchestrationDuration = _timeProvider.GetElapsedTime(timestamp);
            _logger.LogInformation(
                "AI agent orchestration completed for agent {AgentId}, version {AgentVersion}, provider {Provider}, duration {Duration}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                orchestrationResponse.Provider,
                orchestrationDuration,
                request.CorrelationId);

            var metrics = CreateMetrics(
                resolutionDuration,
                authorizationDuration,
                mappingDuration,
                orchestrationDuration,
                _timeProvider.GetElapsedTime(totalTimestamp),
                orchestrationResponse);
            context.Metrics = metrics;
            var response = _responseMapper.Map(context, orchestrationResponse, metrics);

            _logger.LogInformation(
                "AI agent completion hook started for agent {AgentId}, version {AgentVersion}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.CorrelationId);
            await agent.OnExecutionCompletedAsync(context, response, executionToken).ConfigureAwait(false);
            _logger.LogInformation(
                "AI agent completion hook completed for agent {AgentId}, version {AgentVersion}, and correlation {CorrelationId}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                request.CorrelationId);

            var completedAtUtc = _timeProvider.GetUtcNow();
            metrics = CreateMetrics(
                resolutionDuration,
                authorizationDuration,
                mappingDuration,
                orchestrationDuration,
                _timeProvider.GetElapsedTime(totalTimestamp),
                orchestrationResponse);
            context.Metrics = metrics;
            context.State = AIAgentExecutionState.Completed;
            response = response with
            {
                CompletedAtUtc = completedAtUtc,
                State = AIAgentExecutionState.Completed,
                Metrics = metrics
            };

            _logger.LogInformation(
                "AI agent execution completed for agent {AgentId}, version {AgentVersion}, session {SessionId}, conversation {ConversationId}, correlation {CorrelationId}, state {State}, duration {Duration}, memory {MemoryUsed}, knowledge {KnowledgeUsed}, and tool {ToolUsed}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                response.SessionId,
                response.ConversationId,
                response.CorrelationId,
                response.State,
                response.Duration,
                response.MemoryUsed,
                response.KnowledgeUsed,
                response.ToolUsed);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (context is not null)
            {
                context.State = AIAgentExecutionState.Cancelled;
                context.Error = new AIAgentError("AI_AGENT_CANCELLED", "The AI agent execution was cancelled.", request.CorrelationId);
                await TryInvokeFailureHookAsync(agent, context, new OperationCanceledException(cancellationToken)).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "AI agent execution cancelled for agent {AgentId}, correlation {CorrelationId}, and state {State}",
                request.AgentId.Value,
                request.CorrelationId,
                AIAgentExecutionState.Cancelled);
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutSource?.IsCancellationRequested == true)
        {
            var timeoutException = new AIAgentTimeoutException(
                request.AgentId,
                agent?.Definition.Version ?? request.ExactVersion ?? new AIAgentVersion(0, 0, 0),
                request.CorrelationId,
                exception);
            if (context is not null)
            {
                context.State = AIAgentExecutionState.TimedOut;
                context.Error = new AIAgentError(timeoutException.ErrorCode, timeoutException.Message, request.CorrelationId);
                await TryInvokeFailureHookAsync(agent, context, timeoutException).ConfigureAwait(false);
            }

            _logger.LogWarning(
                "AI agent execution timed out for agent {AgentId}, version {AgentVersion}, correlation {CorrelationId}, and state {State}",
                request.AgentId.Value,
                agent?.Definition.Version.ToString(),
                request.CorrelationId,
                AIAgentExecutionState.TimedOut);
            throw timeoutException;
        }
        catch (AIAgentException exception)
        {
            if (context is not null)
            {
                context.State = AIAgentExecutionState.Failed;
                context.Error = new AIAgentError(exception.ErrorCode, exception.Message, exception.CorrelationId);
                await TryInvokeFailureHookAsync(agent, context, exception).ConfigureAwait(false);
            }

            LogFailure(request, agent, exception.ErrorCode);
            throw;
        }
        catch (Exception exception)
        {
            var executionException = new AIAgentExecutionException(
                request.AgentId,
                agent?.Definition.Version ?? request.ExactVersion ?? new AIAgentVersion(0, 0, 0),
                request.CorrelationId,
                exception);
            if (context is not null)
            {
                context.State = AIAgentExecutionState.Failed;
                context.Error = new AIAgentError(executionException.ErrorCode, executionException.Message, request.CorrelationId);
                await TryInvokeFailureHookAsync(agent, context, executionException).ConfigureAwait(false);
            }

            LogFailure(request, agent, executionException.ErrorCode);
            throw executionException;
        }
        finally
        {
            executionSource?.Dispose();
            timeoutSource?.Dispose();
        }
    }

    private TimeSpan ResolveTimeout(AIAgentDefinition definition, AIAgentExecutionRequest request)
    {
        var timeout = request.TimeoutOverride
            ?? definition.MaximumExecutionDuration
            ?? _options.DefaultExecutionTimeout;
        return timeout > _options.MaximumExecutionTimeout
            ? _options.MaximumExecutionTimeout
            : timeout;
    }

    private static AIAgentExecutionMetrics CreateMetrics(
        TimeSpan resolutionDuration,
        TimeSpan authorizationDuration,
        TimeSpan mappingDuration,
        TimeSpan orchestrationDuration,
        TimeSpan totalDuration,
        AIOrchestrationResponse response) =>
        new(
            resolutionDuration,
            authorizationDuration,
            mappingDuration,
            orchestrationDuration,
            totalDuration,
            promptUsed: true,
            response.MemoryUsed,
            response.KnowledgeUsed,
            response.ToolUsed,
            response.Provider,
            response.Usage?.InputTokens,
            response.Usage?.OutputTokens);

    private async ValueTask TryInvokeFailureHookAsync(
        IAIAgent? agent,
        AIAgentExecutionContext context,
        Exception exception)
    {
        if (agent is null)
        {
            return;
        }

        using var hookTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1), _timeProvider);
        try
        {
            await agent.OnExecutionFailedAsync(context, exception, hookTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception hookException)
        {
            _logger.LogWarning(
                "AI agent failure hook failed for agent {AgentId}, version {AgentVersion}, correlation {CorrelationId}, and error type {ErrorType}",
                agent.Definition.Id.Value,
                agent.Definition.Version.ToString(),
                context.CorrelationId,
                hookException.GetType().Name);
        }
    }

    private void LogFailure(
        AIAgentExecutionRequest request,
        IAIAgent? agent,
        string errorCode)
    {
        _logger.LogError(
            "AI agent execution failed for agent {AgentId}, version {AgentVersion}, correlation {CorrelationId}, state {State}, and error code {ErrorCode}",
            request.AgentId.Value,
            agent?.Definition.Version.ToString(),
            request.CorrelationId,
            AIAgentExecutionState.Failed,
            errorCode);
    }
}
