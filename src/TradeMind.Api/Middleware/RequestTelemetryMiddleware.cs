using System.Diagnostics;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Observability.Abstractions;
using TradeMind.Observability.Logging;
using TradeMind.Observability.OpenTelemetry;

namespace TradeMind.Api.Middleware;

public sealed class RequestTelemetryMiddleware(
    RequestDelegate next,
    ITelemetryContextAccessor contextAccessor,
    ITradeMindTelemetry telemetry,
    ITradeMindMetrics metrics,
    ICurrentActor currentActor,
    ICurrentTenant currentTenant,
    IOptions<OpenTelemetryOptions> options,
    ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var actor = currentActor.Identity;
        var tenant = currentTenant.Context;
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? context.TraceIdentifier;
        var sessionId = context.Items[ExecutionSessionHeaderMiddleware.ItemKey]?.ToString();
        var endpoint = context.GetEndpoint()?.DisplayName ?? "unknown";
        var telemetryContext = new TelemetryContext(
            correlationId,
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString(),
            sessionId,
            actor.OrganizationId?.Value,
            tenant?.TenantId.Value ?? actor.TenantId?.Value,
            actor.IsAuthenticated ? actor.ActorId : null,
            actor.IsAuthenticated ? actor.ActorType.ToString() : "Anonymous",
            actor.ApiKeyId?.Value.ToString(),
            context.TraceIdentifier,
            endpoint,
            context.Request.Method,
            "Api",
            endpoint,
            "v1.0.0-core",
            "v1",
            options.Value.Environment,
            options.Value.ServiceName,
            options.Value.ServiceVersion);

        using var contextScope = contextAccessor.Push(telemetryContext);
        telemetry.EnrichCurrent(telemetryContext);
        using var loggingScope = LoggingScopeFactory.BeginScope(logger, telemetryContext);
        var startedAt = Stopwatch.GetTimestamp();
        var dimensions = new MetricDimensions(
            Module: "Api",
            Operation: endpoint,
            Stage: TelemetryStage.Api.ToString(),
            ActorType: actor.ActorType.ToString(),
            AuthenticationMethod: actor.AuthenticationMethod,
            Environment: options.Value.Environment,
            ServiceVersion: options.Value.ServiceVersion);
        metrics.IncrementCounter(TelemetryMetricNames.ApiRequests, 1, dimensions);
        try
        {
            await next(context).ConfigureAwait(false);
            var outcome = context.Response.StatusCode >= 500
                ? TelemetryOutcome.Failed
                : context.Response.StatusCode >= 400 ? TelemetryOutcome.Rejected : TelemetryOutcome.Succeeded;
            telemetry.EnrichCurrent(telemetryContext);
            Activity.Current?.SetTag(TelemetryTagNames.Outcome, outcome.ToString());
            if (outcome == TelemetryOutcome.Failed)
                metrics.IncrementCounter(TelemetryMetricNames.ApiErrors, 1, dimensions with { Outcome = outcome.ToString() });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            Activity.Current?.SetTag(TelemetryTagNames.Outcome, TelemetryOutcome.Cancelled.ToString());
            metrics.IncrementCounter(TelemetryMetricNames.ApiErrors, 1, dimensions with { Outcome = TelemetryOutcome.Cancelled.ToString() });
            throw;
        }
        catch (Exception exception)
        {
            telemetry.EnrichCurrent(telemetryContext);
            if (Activity.Current is not null)
            {
                Activity.Current.SetTag(TelemetryTagNames.Outcome, TelemetryOutcome.Failed.ToString());
                Activity.Current.SetTag("exception.type", exception.GetType().FullName);
                Activity.Current.SetStatus(ActivityStatusCode.Error);
            }
            metrics.IncrementCounter(TelemetryMetricNames.ApiErrors, 1, dimensions with { Outcome = TelemetryOutcome.Failed.ToString() });
            throw;
        }
        finally
        {
            metrics.RecordDuration(TelemetryMetricNames.ApiRequestDuration, Stopwatch.GetElapsedTime(startedAt), dimensions);
        }
    }
}
