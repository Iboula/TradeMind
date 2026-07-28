namespace TradeMind.Observability.Abstractions;

public interface ITradeMindMetrics
{
    void IncrementCounter(string name, long value, MetricDimensions dimensions);
    void RecordDuration(string name, TimeSpan duration, MetricDimensions dimensions);
    void SetActiveExecutionSessions(long value);
}
