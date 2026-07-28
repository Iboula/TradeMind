namespace TradeMind.Observability.Abstractions;

public interface ITradeMindTelemetry
{
    ITradeMindActivity StartActivity(
        TelemetryOperation operation,
        TelemetryContext? context = null,
        IReadOnlyCollection<TelemetryLink>? links = null);

    void EnrichCurrent(TelemetryContext context);
}
