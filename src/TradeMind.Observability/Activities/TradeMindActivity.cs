using System.Diagnostics;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Activities;

internal sealed class TradeMindActivity(Activity? activity) : ITradeMindActivity
{
    public bool IsRecording => activity?.IsAllDataRequested == true;
    public string? TraceId => activity?.TraceId.ToString();
    public string? SpanId => activity?.SpanId.ToString();

    public void SetTag(string name, string? value)
    {
        if (activity is not null && ActivityTagSanitizer.IsAllowed(name))
            activity.SetTag(name, ActivityTagSanitizer.Sanitize(value));
    }

    public void SetTag(string name, long? value)
    {
        if (activity is not null && value.HasValue && ActivityTagSanitizer.IsAllowed(name))
            activity.SetTag(name, value.Value);
    }

    public void SetOutcome(TelemetryOutcome outcome)
    {
        if (activity is null)
            return;
        activity.SetTag(TelemetryTagNames.Outcome, outcome.ToString());
        if (outcome == TelemetryOutcome.Failed)
            activity.SetStatus(ActivityStatusCode.Error);
        else if (outcome is TelemetryOutcome.Succeeded or TelemetryOutcome.Rejected or TelemetryOutcome.Degraded)
            activity.SetStatus(ActivityStatusCode.Ok);
    }

    public void RecordException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (activity is null)
            return;
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", "An operation failed.");
        activity.SetStatus(ActivityStatusCode.Error);
    }

    public void Dispose() => activity?.Stop();
}

internal sealed class NoopTradeMindActivity : ITradeMindActivity
{
    public bool IsRecording => false;
    public string? TraceId => null;
    public string? SpanId => null;
    public void SetTag(string name, string? value) { }
    public void SetTag(string name, long? value) { }
    public void SetOutcome(TelemetryOutcome outcome) { }
    public void RecordException(Exception exception) { }
    public void Dispose() { }
}
