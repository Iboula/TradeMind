using TradeMind.Observability.Abstractions;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Telemetry;

internal sealed class NullBridgeMetrics : ITradeMindMetrics
{
    public static NullBridgeMetrics Instance { get; } = new();

    private NullBridgeMetrics() { }

    public void IncrementCounter(string name, long value, MetricDimensions dimensions) { }
    public void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions) { }
    public void SetActiveExecutionSessions(long value) { }
}
