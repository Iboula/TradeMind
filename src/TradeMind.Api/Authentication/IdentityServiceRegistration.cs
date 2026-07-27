using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using TradeMind.Api.Authorization;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Identity.Infrastructure;
using TradeMind.Identity.Infrastructure.Authentication.Jwt;
using TradeMind.Api.Errors;

namespace TradeMind.Api.Authentication;

public static class IdentityServiceRegistration
{
    public static IServiceCollection AddTradeMindIdentity(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddTradeMindIdentityApplication(configuration);
        services.AddTradeMindIdentityInfrastructure(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpContextCurrentActor>();
        services.AddScoped<ICurrentTenant, HttpContextCurrentTenant>();

        var identity = configuration.GetSection(IdentityOptions.SectionName).Get<IdentityOptions>() ?? new IdentityOptions();
        if (environment.IsProduction() && (!identity.Enabled || identity.Development.EnableTestAuthentication || identity.Development.EnableSignedDeveloperTokens))
            throw new InvalidOperationException("Production requires real identity authentication and forbids development authentication.");
        if (identity.Enabled && identity.ApiKeys.Enabled && string.IsNullOrWhiteSpace(configuration.GetConnectionString("Identity")) && !environment.IsEnvironment("Test"))
            throw new InvalidOperationException("An Identity PostgreSQL connection string is required when API key authentication is enabled.");

        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityAuthenticationDefaults.PolicyScheme;
            options.DefaultChallengeScheme = IdentityAuthenticationDefaults.PolicyScheme;
        })
        .AddPolicyScheme(IdentityAuthenticationDefaults.PolicyScheme, "TradeMind identity", options =>
        {
            options.ForwardDefaultSelector = context => context.Request.Headers.ContainsKey(identity.ApiKeys.HeaderName)
                ? IdentityAuthenticationDefaults.ApiKeyScheme
                : identity.Development.EnableTestAuthentication && context.Request.Headers.ContainsKey("X-TradeMind-Test-Actor")
                    ? "TradeMind.Test"
                : IdentityAuthenticationDefaults.JwtScheme;
        })
        .AddJwtBearer(IdentityAuthenticationDefaults.JwtScheme, options => ConfigureJwt(options, identity))
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(IdentityAuthenticationDefaults.ApiKeyScheme, _ => { });
        if (environment.IsEnvironment("Test") && identity.Development.EnableTestAuthentication)
            authentication.AddScheme<TestAuthenticationOptions, TestAuthenticationHandler>("TradeMind.Test", _ => { });

        services.AddAuthorization(options =>
        {
            foreach (var permission in TradeMindPermissions.All)
            {
                options.AddPolicy(IdentityPolicies.ForPermission(permission), policy =>
                {
                    policy.AddAuthenticationSchemes(IdentityAuthenticationDefaults.PolicyScheme);
                    policy.AddRequirements(new PermissionRequirement(permission));
                });
            }
        });
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = identity.RateLimiting.WindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await ApiProblemDetails.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, "Too many requests", "The request rate limit was exceeded.").ConfigureAwait(false);
            };
            options.AddPolicy("identity", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("tm_api_key_id")?.Value
                    ?? context.User.FindFirst("sub")?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = identity.RateLimiting.PermitLimit,
                        Window = TimeSpan.FromSeconds(identity.RateLimiting.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            if (identity.RateLimiting.Enabled)
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirst("tm_api_key_id")?.Value
                            ?? context.User.FindFirst("sub")?.Value
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous", _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = identity.RateLimiting.PermitLimit,
                                Window = TimeSpan.FromSeconds(identity.RateLimiting.WindowSeconds),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));
            }
        });
        return services;
    }

    private static void ConfigureJwt(JwtBearerOptions options, IdentityOptions identity)
    {
        options.Authority = identity.Jwt.Authority;
        options.Audience = identity.Jwt.Audience;
        options.RequireHttpsMetadata = identity.Jwt.RequireHttpsMetadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = identity.Jwt.ValidateIssuer,
            ValidateAudience = identity.Jwt.ValidateAudience,
            ValidateLifetime = identity.Jwt.ValidateLifetime,
            ClockSkew = TimeSpan.FromSeconds(identity.Jwt.ClockSkewSeconds),
            NameClaimType = identity.Jwt.NameClaimType,
            RoleClaimType = identity.Jwt.RoleClaimType
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!identity.Jwt.Enabled)
                    context.NoResult();
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                try
                {
                    var mapper = context.HttpContext.RequestServices.GetRequiredService<IJwtClaimMapper>();
                    var actor = mapper.Map(context.Principal!);
                    context.HttpContext.Items[IdentityHttpContextKeys.Actor] = actor;
                    return Task.CompletedTask;
                }
                catch (IdentityAuthenticationException exception)
                {
                    context.Fail(exception.Message);
                    return Task.CompletedTask;
                }
            },
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.Items["TradeMind.Identity.AuthenticationFailed"] = true;
                return Task.CompletedTask;
            }
        };
    }
}
