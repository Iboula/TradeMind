using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Application.Options;

namespace TradeMind.Brokers.Application;

public static class BrokersApplicationDependencyInjection
{
    public static IServiceCollection AddTradeMindBrokersApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<BrokerOptions>().Bind(configuration.GetSection("TradeMind:Brokers")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<BrokerOptions>, BrokerOptionsValidator>();
        services.TryAddSingleton<IBrokerClock>(provider => new TimeProviderBrokerClock(provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IBrokerConnectorRegistry, BrokerConnectorRegistry>();
        services.TryAddSingleton<IBrokerAuthorizationPolicy, BrokerAuthorizationPolicy>();
        services.TryAddSingleton<IBrokerIdempotencyStore, InMemoryBrokerIdempotencyStore>();
        services.TryAddSingleton<IBrokerExecutionRecordWriter, NoOpBrokerExecutionRecordWriter>();
        services.TryAddSingleton<IBrokerReconciliationRecordWriter, NoOpBrokerReconciliationRecordWriter>();
        services.TryAddSingleton<IBrokerAuditWriter, NoOpBrokerAuditWriter>();
        services.AddSingleton<BrokerExecutionValidator>();
        services.AddSingleton<IBrokerExecutionService, BrokerExecutionService>();
        return services;
    }
}
