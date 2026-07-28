using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TradeMind.Observability.Abstractions;
using TradeMind.Observability.Activities;
using TradeMind.Observability.Context;
using TradeMind.Observability.Health;
using TradeMind.Observability.Metrics;

namespace TradeMind.Observability.OpenTelemetry;

public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddTradeMindObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName))
            .ValidateOnStart();
        services.PostConfigure<OpenTelemetryOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.Environment) || string.Equals(options.Environment, "Unknown", StringComparison.OrdinalIgnoreCase))
                options.Environment = environmentName;
        });
        services.AddSingleton<IValidateOptions<OpenTelemetryOptions>, OpenTelemetryOptionsValidator>();
        services.AddSingleton<ITelemetryContextAccessor, TelemetryContextAccessor>();
        services.AddSingleton<ITradeMindTelemetry, TradeMindTelemetry>();
        services.AddSingleton<ITradeMindMetrics, TradeMindMetrics>();
        services.AddSingleton<ObservabilityHealthCheck>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TelemetryBehavior<,>));

        var options = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();
        options.Environment = string.IsNullOrWhiteSpace(options.Environment) ? environmentName : options.Environment;
        if (!options.Enabled)
            return services;

        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName, options.ServiceNamespace, options.ServiceVersion)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", options.Environment)]));

        if (options.Tracing.Enabled)
        {
            telemetry.WithTracing(tracing =>
            {
                tracing.AddSource(TelemetryActivityNames.All.ToArray());
                tracing.AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = true;
                    instrumentation.Filter = context => !context.Request.Path.StartsWithSegments(options.Metrics.Prometheus.Endpoint);
                });
                tracing.AddHttpClientInstrumentation();
                tracing.AddEntityFrameworkCoreInstrumentation();
                tracing.SetSampler(options.Tracing.Sampling.Strategy.Equals("TraceIdRatio", StringComparison.OrdinalIgnoreCase)
                    ? new TraceIdRatioBasedSampler(options.Tracing.Sampling.Ratio)
                    : new ParentBasedSampler(new TraceIdRatioBasedSampler(options.Tracing.Sampling.Ratio)));
                if (options.Tracing.ConsoleExporter.Enabled)
                    tracing.AddConsoleExporter();
                if (options.Tracing.Otlp.Enabled)
                    tracing.AddOtlpExporter(exporter => ExporterRegistration.Configure(exporter, options.Tracing.Otlp));
            });
        }

        if (options.Metrics.Enabled)
        {
            telemetry.WithMetrics(metrics =>
            {
                metrics.AddMeter(TradeMindMeter.Name);
                metrics.AddAspNetCoreInstrumentation();
                if (options.Metrics.Prometheus.Enabled)
                    metrics.AddPrometheusExporter();
                if (options.Metrics.Otlp.Enabled)
                    metrics.AddOtlpExporter(exporter => ExporterRegistration.Configure(exporter, options.Metrics.Otlp));
            });
        }
        return services;
    }
}
