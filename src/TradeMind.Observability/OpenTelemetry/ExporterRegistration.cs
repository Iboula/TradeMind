using OpenTelemetry.Exporter;

namespace TradeMind.Observability.OpenTelemetry;

public static class ExporterRegistration
{
    public static void Configure(OtlpExporterOptions exporter, OtlpOptions options)
    {
        exporter.Endpoint = new Uri(options.Endpoint, UriKind.Absolute);
        exporter.Protocol = string.Equals(options.Protocol, "HttpProtobuf", StringComparison.Ordinal)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;
        foreach (var header in options.Headers)
            exporter.Headers = string.IsNullOrWhiteSpace(exporter.Headers)
                ? $"{header.Key}={header.Value}"
                : $"{exporter.Headers},{header.Key}={header.Value}";
    }
}
