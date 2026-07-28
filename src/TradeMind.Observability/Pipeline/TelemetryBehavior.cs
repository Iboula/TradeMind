using System.Diagnostics;
using MediatR;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability;

public sealed class TelemetryBehavior<TRequest, TResponse>(
    ITradeMindTelemetry telemetry,
    ITradeMindMetrics metrics,
    ITelemetryContextAccessor contextAccessor) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var operation = TelemetryOperationClassifier.Classify(typeof(TRequest));
        using var activity = telemetry.StartActivity(operation, contextAccessor.Current);
        var started = Stopwatch.GetTimestamp();
        metrics.IncrementCounter(TelemetryMetricNames.PipelineOperations, 1, Dimensions(operation));
        try
        {
            var response = await next().ConfigureAwait(false);
            activity.SetOutcome(TelemetryOutcome.Succeeded);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetOutcome(TelemetryOutcome.Cancelled);
            throw;
        }
        catch (Exception exception)
        {
            activity.RecordException(exception);
            activity.SetOutcome(TelemetryOutcome.Failed);
            metrics.IncrementCounter(TelemetryMetricNames.PipelineFailures, 1, Dimensions(operation, TelemetryOutcome.Failed));
            throw;
        }
        finally
        {
            metrics.RecordDuration(TelemetryMetricNames.PipelineOperationDuration, Stopwatch.GetElapsedTime(started), Dimensions(operation));
        }
    }

    private static MetricDimensions Dimensions(TelemetryOperation operation, TelemetryOutcome? outcome = null) => new(
        Module: operation.Module,
        Operation: operation.Name,
        Stage: operation.Stage.ToString(),
        Outcome: outcome?.ToString());
}
