using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TradeMind.Api.Health;
using TradeMind.Api.Middleware;
using TradeMind.Api.Application;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradeMind.ExecutionSessions.Infrastructure;
using TradeMind.Observability.Health;
using TradeMind.Observability.OpenTelemetry;

namespace TradeMind.Api.Composition;

public static class ApiServiceRegistration
{
    public static IServiceCollection AddTradeMindApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection("TradeMind:Api"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();
        services.AddSingleton<IIdempotencyStore>(serviceProvider =>
            serviceProvider.GetService<IPersistentIdempotencyStore>() is { } durableStore
                ? new PostgreSqlIdempotencyStoreAdapter(durableStore)
                : new InMemoryIdempotencyStore(
                    serviceProvider.GetRequiredService<IOptions<ApiOptions>>(),
                    serviceProvider.GetRequiredService<TimeProvider>()));
        services.AddScoped<ITradeMindApiApplication, TradeMindApiApplication>();
        services.AddScoped<BrokerApiApplication>();
        services.AddScoped<IExecutionSessionApiApplication>(serviceProvider =>
            serviceProvider.GetService<IExecutionSessionService>() is { } service
                ? new ExecutionSessionApiApplication(service, serviceProvider.GetRequiredService<TimeProvider>(),
                    serviceProvider.GetRequiredService<ICurrentActor>(), serviceProvider.GetRequiredService<ICurrentTenant>(),
                    serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value)
                : new ExecutionSessionNotConfiguredApiApplication());
        services.RemoveAll<IExecutionSessionAccessScope>();
        services.AddScoped<IExecutionSessionAccessScope, ApiExecutionSessionAccessScope>();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
        services.AddProblemDetails();
        services.AddHealthChecks()
            .AddCheck<ConfiguredDependencyHealthCheck>("configured-dependencies", tags: ["ready"])
            .AddCheck<ObservabilityHealthCheck>("observability", tags: ["ready"])
            .AddCheck<StartupHealthCheck>("startup", tags: ["startup"])
            .AddCheck<ReadinessHealthCheck>("readiness", tags: ["ready"]);
        services.AddSingleton<StartupHealthCheckState>();
        return services;
    }
}

internal sealed class ApiExecutionSessionAccessScope(
    TradeMind.Identity.Application.Abstractions.ICurrentTenant currentTenant,
    Microsoft.Extensions.Options.IOptions<TradeMind.Identity.Application.IdentityOptions> options) : IExecutionSessionAccessScope
{
    private readonly TradeMind.Identity.Domain.Tenancy.TenantContext? _context = currentTenant.Context;
    public string? OrganizationId => _context?.OrganizationId.Value;
    public string? TenantId => _context?.TenantId.Value;
    public bool IsRestricted => options.Value.Enabled;
    public bool IsAdministrativeOverride => _context?.IsAdministrativeOverride == true;
}

public sealed class ApiOptionsValidator : IValidateOptions<ApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.Correlation is null || options.Correlation.MaximumLength is < 8 or > 128)
            failures.Add("TradeMind:Api:Correlation:MaximumLength must be between 8 and 128.");
        if (options.Idempotency is null || options.Idempotency.TtlMinutes is < 1 or > 1440)
            failures.Add("TradeMind:Api:Idempotency:TtlMinutes must be between 1 and 1440.");
        if (options.PayloadLimits is null || options.PayloadLimits.MaximumBodyBytes is < 1024 or > 104857600)
            failures.Add("TradeMind:Api:PayloadLimits:MaximumBodyBytes must be between 1024 and 104857600.");
        if (options.Timeouts is null || options.Timeouts.RequestTimeoutSeconds is < 1 or > 300)
            failures.Add("TradeMind:Api:Timeouts:RequestTimeoutSeconds must be between 1 and 300.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
