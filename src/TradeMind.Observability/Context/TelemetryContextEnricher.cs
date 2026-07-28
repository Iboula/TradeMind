using System.Diagnostics;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Context;

public static class TelemetryContextEnricher
{
    public static TelemetryContext WithActivity(TelemetryContext context, Activity? activity) =>
        context with
        {
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString()
        };
}
