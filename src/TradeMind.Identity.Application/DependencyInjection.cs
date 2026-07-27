using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Application.Authorization;

namespace TradeMind.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindIdentityApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<IdentityOptions>().Bind(configuration.GetSection(IdentityOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<IdentityOptions>, IdentityOptionsValidator>();
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value);
        services.AddScoped<ICurrentActor, EmptyCurrentActor>();
        services.AddScoped<ICurrentTenant, EmptyCurrentTenant>();
        services.AddSingleton<IAuthorizationService, PermissionEvaluator>();
        services.AddScoped<IIdentityApplicationService, IdentityApplicationService>();
        services.AddSingleton<IIdentityClock, TimeProviderIdentityClock>();
        return services;
    }

    private sealed class TimeProviderIdentityClock(TimeProvider timeProvider) : IIdentityClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }

    private sealed class EmptyCurrentActor : ICurrentActor
    {
        public TradeMind.Identity.Domain.Actors.ActorIdentity Identity => TradeMind.Identity.Domain.Actors.ActorIdentity.Anonymous();
    }

    private sealed class EmptyCurrentTenant : ICurrentTenant
    {
        public TradeMind.Identity.Domain.Tenancy.TenantContext? Context => null;
    }
}

public sealed class IdentityOptionsValidator : IValidateOptions<IdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityOptions options)
    {
        var errors = new List<string>();
        if (options.Jwt.ClockSkewSeconds is < 0 or > 300) errors.Add("TradeMind:Identity:Jwt:ClockSkewSeconds must be between 0 and 300.");
        if (options.Jwt.MaximumClaims is < 1 or > 512) errors.Add("TradeMind:Identity:Jwt:MaximumClaims must be between 1 and 512.");
        if (options.Jwt.MaximumPermissions is < 1 or > 512) errors.Add("TradeMind:Identity:Jwt:MaximumPermissions must be between 1 and 512.");
        if (options.ApiKeys.MaximumActiveKeysPerOrganization is < 1 or > 1000) errors.Add("TradeMind:Identity:ApiKeys:MaximumActiveKeysPerOrganization must be between 1 and 1000.");
        if (string.IsNullOrWhiteSpace(options.ApiKeys.HeaderName)) errors.Add("TradeMind:Identity:ApiKeys:HeaderName is required.");
        if (options.Enabled && !options.ApiKeys.Enabled && !options.Jwt.Enabled) errors.Add("At least one identity authentication mechanism must be enabled.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
