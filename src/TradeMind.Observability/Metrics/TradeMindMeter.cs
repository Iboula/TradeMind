using System.Diagnostics.Metrics;

namespace TradeMind.Observability.Metrics;

public static class TradeMindMeter
{
    public const string Name = "TradeMind.Telemetry";
    public const string Version = "1.0.0";
    public static Meter Meter { get; } = new(Name, Version);
}
