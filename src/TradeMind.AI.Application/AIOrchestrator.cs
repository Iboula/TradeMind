using Microsoft.Extensions.Logging;

namespace TradeMind.AI.Application;

public sealed class AIOrchestrator : IAIOrchestrator
{
    private readonly IReadOnlyList<IAIOrchestrationStep> _steps;
    private readonly IAISessionFactory _sessionFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AIOrchestrator> _logger;

    public AIOrchestrator(
        IEnumerable<IAIOrchestrationStep> steps,
        IAISessionFactory sessionFactory,
        TimeProvider timeProvider,
        ILogger<AIOrchestrator> logger)
    {
        _steps = steps.OrderBy(step => step.Order).ToArray();
        _sessionFactory = sessionFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AIOrchestrationResponse> ExecuteAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Tool.Enabled
            && (!_steps.Any(step => step.Name == AIOrchestrationStepNames.ToolExecution)
                || !_steps.Any(step => step.Name == AIOrchestrationStepNames.ToolResultComposition)))
        {
            throw new AIOrchestrationValidationException(
                ["Tool Engine orchestration must be registered when tool invocation is enabled."],
                request.CorrelationId,
                AIOrchestrationStepNames.RequestValidation);
        }

        var session = _sessionFactory.Create(request);
        var context = new AIExecutionContext(session, request, _timeProvider.GetUtcNow());
        var currentStep = string.Empty;

        _logger.LogInformation(
            "AI session created with session id {SessionId}, correlation id {CorrelationId}, conversation id {ConversationId}, tenant id {TenantId}, user id {UserId}, agent id {AgentId}, and scenario {Scenario}",
            session.SessionId,
            session.CorrelationId,
            session.ConversationId,
            session.TenantId,
            session.UserId,
            session.AgentId,
            session.Scenario);

        context.Start();

        _logger.LogInformation(
            "AI orchestration started for session {SessionId}, scenario {Scenario}, correlation id {CorrelationId}, logical model {LogicalModel}, and {StepCount} steps",
            session.SessionId,
            session.Scenario,
            session.CorrelationId,
            request.Model,
            _steps.Count);

        try
        {
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentStep = step.Name;
                context.MarkStepStarted(currentStep);

                _logger.LogInformation(
                    "AI orchestration step {StepName} started for session {SessionId} with correlation id {CorrelationId}",
                    currentStep,
                    session.SessionId,
                    session.CorrelationId);

                await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

                context.MarkStepCompleted(currentStep);

                _logger.LogInformation(
                    "AI orchestration step {StepName} completed for session {SessionId} with correlation id {CorrelationId}",
                    currentStep,
                    session.SessionId,
                    session.CorrelationId);
            }

            var response = context.FinalResponse
                ?? throw new AIOrchestrationException(
                    "The AI orchestration pipeline completed without producing a response.",
                    session.CorrelationId,
                    currentStep);

            context.Complete(_timeProvider.GetUtcNow());

            _logger.LogInformation(
                "AI orchestration completed for session {SessionId}, scenario {Scenario}, provider {Provider}, model {Model}, state {State}, and duration {ElapsedMilliseconds} ms",
                session.SessionId,
                session.Scenario,
                context.FinalResponse?.Provider,
                context.FinalResponse?.Model,
                context.State,
                context.Metrics.TotalDuration?.TotalMilliseconds);

            return context.FinalResponse
                ?? response;
        }
        catch (OperationCanceledException)
        {
            context.Cancel(_timeProvider.GetUtcNow());

            _logger.LogInformation(
                "AI orchestration cancelled for session {SessionId}, scenario {Scenario}, at step {StepName}, state {State}, and correlation id {CorrelationId}",
                session.SessionId,
                session.Scenario,
                currentStep,
                context.State,
                session.CorrelationId);
            throw;
        }
        catch (AIOrchestrationException exception)
        {
            context.Fail(
                AIExecutionError.FromException(
                    exception,
                    _timeProvider.GetUtcNow(),
                    string.IsNullOrWhiteSpace(exception.StepName) ? currentStep : exception.StepName,
                    context.Metrics.ProviderName),
                _timeProvider.GetUtcNow());

            _logger.LogError(
                exception,
                "AI orchestration failed for session {SessionId}, scenario {Scenario}, at step {StepName}, state {State}, and correlation id {CorrelationId}",
                session.SessionId,
                session.Scenario,
                string.IsNullOrWhiteSpace(exception.StepName) ? currentStep : exception.StepName,
                context.State,
                session.CorrelationId);
            throw;
        }
        catch (PromptEngineException exception)
        {
            context.Fail(
                new AIExecutionError(
                    "AI_PROMPT_RENDERING_FAILED",
                    exception.Message,
                    _timeProvider.GetUtcNow(),
                    currentStep,
                    context.Metrics.ProviderName,
                    exceptionType: exception.GetType().Name),
                _timeProvider.GetUtcNow());

            _logger.LogError(
                exception,
                "AI orchestration prompt rendering failed for session {SessionId}, scenario {Scenario}, at step {StepName}, state {State}, template {TemplateId}, version {Version}, and correlation id {CorrelationId}",
                session.SessionId,
                session.Scenario,
                currentStep,
                context.State,
                exception.TemplateId.Value,
                exception.Version?.ToString(),
                session.CorrelationId);
            throw;
        }
        catch (Exception exception)
        {
            var orchestrationException = new AIOrchestrationException(
                "The AI orchestration pipeline failed.",
                session.CorrelationId,
                currentStep,
                exception);

            context.Fail(
                AIExecutionError.FromException(
                    orchestrationException,
                    _timeProvider.GetUtcNow(),
                    currentStep,
                    context.Metrics.ProviderName),
                _timeProvider.GetUtcNow());

            _logger.LogError(
                exception,
                "AI orchestration failed for session {SessionId}, scenario {Scenario}, at step {StepName}, state {State}, and correlation id {CorrelationId}",
                session.SessionId,
                session.Scenario,
                currentStep,
                context.State,
                session.CorrelationId);

            throw orchestrationException;
        }
    }
}
