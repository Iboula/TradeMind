using Microsoft.Extensions.Options;

namespace TradeMind.Observability.OpenTelemetry;

public sealed class OpenTelemetryOptionsValidator : IValidateOptions<OpenTelemetryOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ServiceName) || options.ServiceName.Length > 128)
            failures.Add("TradeMind:Observability:ServiceName must be between 1 and 128 characters.");
        if (string.IsNullOrWhiteSpace(options.ServiceNamespace) || options.ServiceNamespace.Length > 128)
            failures.Add("TradeMind:Observability:ServiceNamespace must be between 1 and 128 characters.");
        if (string.IsNullOrWhiteSpace(options.Environment) || options.Environment.Length > 64)
            failures.Add("TradeMind:Observability:Environment must be between 1 and 64 characters.");
        if (options.Tracing is null || options.Metrics is null || options.Logging is null || options.Health is null)
            failures.Add("Tracing, Metrics, Logging and Health options are required.");
        else
        {
            if (options.Tracing.Sampling is null || options.Tracing.Sampling.Ratio is < 0 or > 1)
                failures.Add("Sampling ratio must be between 0 and 1.");
            if (options.Tracing.Sampling is not null && !string.Equals(options.Tracing.Sampling.Strategy, "ParentBased", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(options.Tracing.Sampling.Strategy, "TraceIdRatio", StringComparison.OrdinalIgnoreCase))
                failures.Add("Sampling strategy must be ParentBased or TraceIdRatio.");
            ValidateOtlp(options.Tracing.Otlp, "Tracing", failures);
            ValidateOtlp(options.Metrics.Otlp, "Metrics", failures);
            if (options.Metrics.Prometheus is null || !IsEndpoint(options.Metrics.Prometheus.Endpoint))
                failures.Add("Metrics Prometheus endpoint must be an absolute path.");
        }
        if (options.Tracing?.Otlp is { Enabled: true } tracing && options.Metrics?.Otlp is { Enabled: true } metrics
            && !Uri.TryCreate(tracing.Endpoint, UriKind.Absolute, out _)
            && !Uri.TryCreate(metrics.Endpoint, UriKind.Absolute, out _))
            failures.Add("At least one enabled OTLP endpoint must be a valid absolute URI.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateOtlp(OtlpOptions? options, string section, ICollection<string> failures)
    {
        if (options is null)
        {
            failures.Add($"{section} OTLP options are required.");
            return;
        }
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https"))
            failures.Add($"{section} OTLP endpoint must be an absolute HTTP or HTTPS URI.");
        if (options.Protocol is not ("Grpc" or "HttpProtobuf"))
            failures.Add($"{section} OTLP protocol must be Grpc or HttpProtobuf.");
        if (options.Headers.Keys.Any(key => string.IsNullOrWhiteSpace(key) || key.Length > 64)
            || options.Headers.Values.Any(value => value.Length > 256))
            failures.Add($"{section} OTLP headers exceed safe limits.");
    }

    private static bool IsEndpoint(string? endpoint) =>
        !string.IsNullOrWhiteSpace(endpoint)
        && endpoint.StartsWith("/", StringComparison.Ordinal)
        && endpoint.Length <= 64
        && !endpoint.Contains("..", StringComparison.Ordinal);
}
