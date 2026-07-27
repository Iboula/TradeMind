using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Infrastructure.Authentication.ApiKeys;
using TradeMind.Identity.Infrastructure.Authentication.Jwt;
using TradeMind.Identity.Infrastructure.Audit;
using TradeMind.Identity.Infrastructure.Persistence;
using TradeMind.Identity.Infrastructure.Persistence.Repositories;
using TradeMind.Identity.Infrastructure.Security;

namespace TradeMind.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeMindIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<IJwtClaimMapper, JwtClaimMapper>();
        services.AddScoped<IApiKeyCredentialValidator, ApiKeyCredentialValidator>();
        services.AddSingleton<IApiKeySecretGenerator, ApiKeySecretGenerator>();
        services.AddSingleton<IApiKeyHasher, ApiKeyHasher>();
        var connectionString = configuration.GetConnectionString("Identity");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));
            services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
            services.AddScoped<IOrganizationRepository, OrganizationRepository>();
            services.AddScoped<IUserIdentityRepository, UserIdentityRepository>();
            services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
            services.AddScoped<IIdentityAuditWriter, IdentityAuditWriter>();
            services.AddScoped<IApiKeyUsageRecorder, ApiKeyUsageRecorder>();
        }
        else
        {
            services.AddScoped<IApiKeyRepository, NotConfiguredApiKeyRepository>();
            services.AddScoped<IOrganizationRepository, NotConfiguredOrganizationRepository>();
            services.AddScoped<IUserIdentityRepository, NotConfiguredUserIdentityRepository>();
            services.AddScoped<IIdentityUnitOfWork, NotConfiguredIdentityUnitOfWork>();
            services.AddScoped<IIdentityAuditWriter, NotConfiguredIdentityAuditWriter>();
            services.AddScoped<IApiKeyUsageRecorder, NotConfiguredApiKeyUsageRecorder>();
        }
        return services;
    }

    public static async Task ApplyIdentityMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
