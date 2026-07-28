namespace TradeMind.Observability.OpenTelemetry;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "TradeMind:Observability";
    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; } = "TradeMind.Api";
    public string ServiceNamespace { get; set; } = "TradeMind";
    public string ServiceVersion { get; set; } = "v1.0.0-core";
    public string Environment { get; set; } = "Unknown";
    public TracingOptions Tracing { get; set; } = new();
    public MetricsOptions Metrics { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();
    public HealthOptions Health { get; set; } = new();
}

public sealed class TracingOptions
{
    public bool Enabled { get; set; } = true;
    public SamplingOptions Sampling { get; set; } = new();
    public ExporterOptions ConsoleExporter { get; set; } = new();
    public OtlpOptions Otlp { get; set; } = new();
}

public sealed class MetricsOptions
{
    public bool Enabled { get; set; } = true;
    public PrometheusOptions Prometheus { get; set; } = new();
    public OtlpOptions Otlp { get; set; } = new();
}

public sealed class SamplingOptions
{
    public string Strategy { get; set; } = "ParentBased";
    public double Ratio { get; set; } = 1.0;
}

public sealed class ExporterOptions
{
    public bool Enabled { get; set; }
}

public sealed class PrometheusOptions
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "/metrics";
}

public sealed class OtlpOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:4317";
    public string Protocol { get; set; } = "Grpc";
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.Ordinal);
}

public sealed class LoggingOptions
{
    public bool IncludeTraceIdentifiers { get; set; } = true;
}

public sealed class HealthOptions
{
    public bool IncludeDetailedData { get; set; }
}
