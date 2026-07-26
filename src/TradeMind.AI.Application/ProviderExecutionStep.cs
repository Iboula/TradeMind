using Microsoft.Extensions.Logging;
using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class ProviderExecutionStep : IAIOrchestrationStep
{
    private readonly IChatProvider _chatProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProviderExecutionStep> _logger;

    public ProviderExecutionStep(
        IChatProvider chatProvider,
        TimeProvider timeProvider,
        ILogger<ProviderExecutionStep> logger)
    {
        _chatProvider = chatProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public string Name => AIOrchestrationStepNames.ProviderExecution;

    public int Order => 400;

    public async Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        var chatRequest = context.ChatRequest
            ?? throw new AIOrchestrationException(
                "Chat request has not been constructed.",
                context.Session.CorrelationId,
                Name);

        var startTimestamp = _timeProvider.GetTimestamp();

        try
        {
            _logger.LogInformation(
                "AI provider call started for session {SessionId}, scenario {Scenario}, correlation id {CorrelationId}, and logical model {LogicalModel}",
                context.Session.SessionId,
                context.Session.Scenario,
                context.Session.CorrelationId,
                context.Request.Model);

            var chatResponse = await _chatProvider
                .CompleteAsync(chatRequest, cancellationToken)
                .ConfigureAwait(false);

            context.SetChatResponse(chatResponse);

            _logger.LogInformation(
                "AI provider call completed for session {SessionId}, scenario {Scenario}, provider {Provider}, model {Model}, and correlation id {CorrelationId}",
                context.Session.SessionId,
                context.Session.Scenario,
                chatResponse.ProviderName,
                chatResponse.Model,
                context.Session.CorrelationId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AIOrchestrationException(
                "AI provider execution failed.",
                context.Session.CorrelationId,
                Name,
                exception);
        }
        finally
        {
            context.Metrics.RecordProviderDuration(_timeProvider.GetElapsedTime(startTimestamp));
        }
    }
}
