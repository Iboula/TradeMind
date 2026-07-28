namespace TradeMind.Observability.Abstractions;

public enum TelemetryOutcome
{
    Succeeded,
    Failed,
    Cancelled,
    Rejected,
    Degraded
}
