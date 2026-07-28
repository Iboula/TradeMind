using OpenTelemetry.Resources;

namespace TradeMind.Observability.OpenTelemetry;

public static class ResourceBuilderFactory
{
    public static ResourceBuilder Create(OpenTelemetryOptions options) =>
        ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName, options.ServiceNamespace, options.ServiceVersion)
            .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", options.Environment)]);
}
