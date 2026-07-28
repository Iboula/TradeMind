using Microsoft.Extensions.Logging;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Logging;

public static class LoggingScopeFactory
{
    public static IDisposable BeginScope(ILogger logger, TelemetryContext context)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);
        return logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [TelemetryTagNames.CorrelationId] = context.CorrelationId,
            [TelemetryTagNames.TraceId] = context.TraceId,
            [TelemetryTagNames.SpanId] = context.SpanId,
            [TelemetryTagNames.ExecutionSessionId] = context.ExecutionSessionId,
            [TelemetryTagNames.OrganizationId] = context.OrganizationId,
            [TelemetryTagNames.TenantId] = context.TenantId,
            [TelemetryTagNames.ActorId] = context.ActorId,
            [TelemetryTagNames.ActorType] = context.ActorType,
            [TelemetryTagNames.RequestId] = context.RequestId,
            [TelemetryTagNames.Module] = context.Module,
            [TelemetryTagNames.Operation] = context.Operation,
            [TelemetryTagNames.ServiceName] = context.ServiceName,
            [TelemetryTagNames.Environment] = context.Environment,
            ["trademind.service.version"] = context.ServiceVersion
        }) ?? NullScope.Instance;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
