using System.Diagnostics;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Activities;

public static class ActivityExtensions
{
    public static void Enrich(this ITradeMindActivity activity, TelemetryOperation operation, TelemetryContext context)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        activity.SetTag(TelemetryTagNames.CorrelationId, context.CorrelationId);
        activity.SetTag(TelemetryTagNames.ExecutionSessionId, context.ExecutionSessionId);
        activity.SetTag(TelemetryTagNames.OrganizationId, context.OrganizationId);
        activity.SetTag(TelemetryTagNames.TenantId, context.TenantId);
        activity.SetTag(TelemetryTagNames.ActorId, context.ActorId);
        activity.SetTag(TelemetryTagNames.ActorType, context.ActorType);
        activity.SetTag(TelemetryTagNames.ApiKeyId, context.ApiKeyId);
        activity.SetTag(TelemetryTagNames.RequestId, context.RequestId);
        activity.SetTag(TelemetryTagNames.Module, operation.Module);
        activity.SetTag(TelemetryTagNames.Operation, operation.Name);
        activity.SetTag(TelemetryTagNames.Stage, operation.Stage.ToString());
        activity.SetTag(TelemetryTagNames.CoreVersion, context.CoreVersion);
        activity.SetTag(TelemetryTagNames.ApiVersion, context.ApiVersion);
        activity.SetTag(TelemetryTagNames.Environment, context.Environment);
        activity.SetTag(TelemetryTagNames.ArtifactType, operation.ArtifactType);
        activity.SetTag(TelemetryTagNames.ArtifactCount, operation.ArtifactCount);
    }

    public static IReadOnlyList<ActivityLink> ToActivityLinks(this IReadOnlyCollection<TelemetryLink>? links)
    {
        if (links is null or { Count: 0 })
            return Array.Empty<ActivityLink>();
        var result = new List<ActivityLink>(links.Count);
        foreach (var link in links)
        {
            var spanId = link.SpanId ?? ActivitySpanId.CreateRandom().ToString();
            if (ActivityContext.TryParse($"00-{link.TraceId}-{spanId}-01", null, true, out var parent))
                result.Add(new ActivityLink(parent));
        }
        return result;
    }
}
