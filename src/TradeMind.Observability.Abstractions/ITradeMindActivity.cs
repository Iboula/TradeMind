namespace TradeMind.Observability.Abstractions;

public interface ITradeMindActivity : IDisposable
{
    bool IsRecording { get; }
    string? TraceId { get; }
    string? SpanId { get; }
    void SetTag(string name, string? value);
    void SetTag(string name, long? value);
    void SetOutcome(TelemetryOutcome outcome);
    void RecordException(Exception exception);
}
