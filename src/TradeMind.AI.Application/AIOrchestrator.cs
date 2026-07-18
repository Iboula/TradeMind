using Microsoft.Extensions.Logging;

namespace TradeMind.AI.Application;

public sealed class AIOrchestrator : IAIOrchestrator
{
    private readonly IReadOnlyList<IAIOrchestrationStep> _steps;
    private readonly ILogger<AIOrchestrator> _logger;

    public AIOrchestrator(
        IEnumerable<IAIOrchestrationStep> steps,
        ILogger<AIOrchestrator> logger)
    {
        _steps = steps.OrderBy(step => step.Order).ToArray();
        _logger = logger;
    }

    public async Task<AIOrchestrationResponse> ExecuteAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new AIOrchestrationContext(request, DateTimeOffset.UtcNow);
        var currentStep = string.Empty;

        _logger.LogInformation(
            "AI orchestration started for scenario {Scenario} with correlation id {CorrelationId}, logical model {LogicalModel}, and {StepCount} steps",
            request.Scenario,
            context.CorrelationId,
            request.Model,
            _steps.Count);

        try
        {
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentStep = step.GetType().Name;
                context.MarkStepExecuted(currentStep);

                await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }

            var response = context.FinalResponse
                ?? throw new AIOrchestrationException(
                    "The AI orchestration pipeline completed without producing a response.",
                    context.CorrelationId,
                    currentStep);

            _logger.LogInformation(
                "AI orchestration completed for scenario {Scenario} using provider {Provider} in {ElapsedMilliseconds} ms",
                request.Scenario,
                response.ProviderName,
                response.TotalDuration.TotalMilliseconds);

            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "AI orchestration cancelled for scenario {Scenario} at step {StepName} with correlation id {CorrelationId}",
                request.Scenario,
                currentStep,
                context.CorrelationId);
            throw;
        }
        catch (AIOrchestrationException exception)
        {
            _logger.LogError(
                exception,
                "AI orchestration failed for scenario {Scenario} at step {StepName} with correlation id {CorrelationId}",
                request.Scenario,
                string.IsNullOrWhiteSpace(exception.StepName) ? currentStep : exception.StepName,
                context.CorrelationId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "AI orchestration failed for scenario {Scenario} at step {StepName} with correlation id {CorrelationId}",
                request.Scenario,
                currentStep,
                context.CorrelationId);

            throw new AIOrchestrationException(
                "The AI orchestration pipeline failed.",
                context.CorrelationId,
                currentStep,
                exception);
        }
    }
}
