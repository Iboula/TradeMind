using Microsoft.Extensions.Logging;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Application;

public sealed class ToolExecutionStep : IAIOrchestrationStep
{
    private readonly IAIToolExecutor _executor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolExecutionStep> _logger;

    public ToolExecutionStep(
        IAIToolExecutor executor,
        TimeProvider timeProvider,
        ILogger<ToolExecutionStep> logger)
    {
        _executor = executor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public string Name => AIOrchestrationStepNames.ToolExecution;

    public int Order => 250;

    public async Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        var options = context.Request.Tool;
        if (!options.Enabled)
        {
            return;
        }

        var toolId = options.ToolId
            ?? throw new AIOrchestrationValidationException(
                ["ToolId is required when tool invocation is enabled."],
                context.Session.CorrelationId,
                Name);

        try
        {
            var request = new AIToolExecutionRequest(
                toolId,
                options.Arguments,
                context.Session.SessionId,
                context.Session.CorrelationId,
                context.Session.ConversationId,
                context.Session.TenantId,
                context.Session.UserId,
                context.Session.AgentId,
                context.Session.Scenario,
                _timeProvider.GetUtcNow(),
                options.TimeoutOverride,
                options.IdempotencyKey,
                context.Request.Metadata);
            var authorization = new AIToolAuthorizationContext(
                context.Session.TenantId,
                context.Session.UserId,
                context.Session.AgentId,
                options.Permissions,
                context.Session.Scenario,
                options.MaximumAllowedSideEffectLevel,
                context.Session.CorrelationId);

            var result = await _executor.ExecuteAsync(request, authorization, cancellationToken).ConfigureAwait(false);
            context.SetToolExecutionResult(result);

            if (!result.Success)
            {
                if (options.FailureMode == AIToolFailureMode.ContinueWithoutTool)
                {
                    _logger.LogWarning(
                        "AI orchestration continues without tool result for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, and error code {ErrorCode}",
                        toolId.Value,
                        context.Session.SessionId,
                        context.Session.CorrelationId,
                        result.Error?.Code);
                    return;
                }

                throw CreateOrchestrationException(
                    context,
                    result.Error?.Code ?? "AI_TOOL_EXECUTION_FAILED");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AIToolNotFoundException exception)
        {
            context.RecordToolFailure(toolId, false, exception.ErrorCode);
            throw CreateOrchestrationException(context, exception.ErrorCode);
        }
        catch (AIToolUnavailableException exception)
        {
            context.RecordToolFailure(toolId, false, exception.ErrorCode);
            throw CreateOrchestrationException(context, exception.ErrorCode);
        }
        catch (AIToolAuthorizationException exception)
        {
            context.RecordToolFailure(toolId, false, exception.ErrorCode);
            throw CreateOrchestrationException(context, exception.ErrorCode);
        }
        catch (AIToolValidationException exception)
        {
            context.RecordToolFailure(toolId, false, exception.ErrorCode);
            if (options.FailureMode == AIToolFailureMode.ContinueWithoutTool)
            {
                LogContinuation(context, toolId, exception.ErrorCode);
                return;
            }

            throw CreateOrchestrationException(context, exception.ErrorCode);
        }
        catch (AIToolTimeoutException exception)
        {
            context.RecordToolFailure(toolId, true, exception.ErrorCode, exception.Metrics?.Duration);
            if (options.FailureMode == AIToolFailureMode.ContinueWithoutTool)
            {
                LogContinuation(context, toolId, exception.ErrorCode);
                return;
            }

            throw CreateOrchestrationException(context, exception.ErrorCode);
        }
        catch (AIToolExecutionException exception)
        {
            context.RecordToolFailure(toolId, true, exception.ErrorCode, exception.Metrics?.Duration);
            if (options.FailureMode == AIToolFailureMode.ContinueWithoutTool)
            {
                LogContinuation(context, toolId, exception.ErrorCode);
                return;
            }

            throw CreateOrchestrationException(context, exception.ErrorCode);
        }
    }

    private void LogContinuation(AIExecutionContext context, AIToolId toolId, string errorCode)
    {
        _logger.LogWarning(
            "AI orchestration continues without tool result for tool {ToolId}, session {SessionId}, correlation {CorrelationId}, and error code {ErrorCode}",
            toolId.Value,
            context.Session.SessionId,
            context.Session.CorrelationId,
            errorCode);
    }

    private static AIOrchestrationException CreateOrchestrationException(
        AIExecutionContext context,
        string errorCode) =>
        new(
            $"AI tool invocation failed with error code {errorCode}.",
            context.Session.CorrelationId,
            AIOrchestrationStepNames.ToolExecution);
}

public sealed class ToolResultCompositionStep : IAIOrchestrationStep
{
    private readonly IAIToolResultComposer _composer;

    public ToolResultCompositionStep(IAIToolResultComposer composer)
    {
        _composer = composer;
    }

    public string Name => AIOrchestrationStepNames.ToolResultComposition;

    public int Order => 275;

    public Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!context.Request.Tool.Enabled
            || context.ToolExecutionResult is not { Success: true } result)
        {
            return Task.CompletedTask;
        }

        var chatRequest = context.ChatRequest
            ?? throw new AIOrchestrationException(
                "Chat request has not been constructed before tool result composition.",
                context.Session.CorrelationId,
                Name);
        var output = chatRequest.Messages.ToList();
        var insertIndex = output.FindLastIndex(message => message.Role == ChatRole.User);
        if (insertIndex < 0)
        {
            insertIndex = output.Count;
        }

        output.Insert(insertIndex, _composer.Compose(result));
        context.SetChatRequest(chatRequest with { Messages = output.ToArray() });
        return Task.CompletedTask;
    }
}
