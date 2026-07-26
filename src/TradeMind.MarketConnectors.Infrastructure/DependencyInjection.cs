using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;

namespace TradeMind.MarketConnectors.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketConnectorCore(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MarketConnectorCoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("MarketConnectors")
            ?? throw new InvalidOperationException("Connection string 'MarketConnectors' is required.");

        services.AddDbContext<MarketConnectorsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(MarketConnectorsDbContext).Assembly.FullName)));

        var optionsBuilder = services.AddOptions<MarketConnectorCoreOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<MarketConnectorCoreOptions>,
            MarketConnectorCoreOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMarketSnapshotSerializer, CanonicalMarketSnapshotSerializer>();
        services.TryAddSingleton<IMarketSnapshotHasher, Sha256MarketSnapshotHasher>();
        services.TryAddSingleton<IMarketConnectorRegistry, MarketConnectorRegistry>();
        services.AddScoped<IMarketSnapshotRepository, PostgreSqlMarketSnapshotRepository>();
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<ApplicationAssemblyMarker>());

        return services;
    }
}
