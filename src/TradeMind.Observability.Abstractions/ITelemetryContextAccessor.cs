namespace TradeMind.Observability.Abstractions;

public interface ITelemetryContextAccessor
{
    TelemetryContext Current { get; }
    IDisposable Push(TelemetryContext context);
}
