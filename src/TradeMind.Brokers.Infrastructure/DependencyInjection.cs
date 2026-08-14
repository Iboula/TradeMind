using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.LiveSafety;
using TradeMind.Brokers.Infrastructure.Audit;
using TradeMind.Brokers.Infrastructure.Health;
using TradeMind.Brokers.Infrastructure.Idempotency;
using TradeMind.Brokers.Infrastructure.InMemory;
using TradeMind.Brokers.Infrastructure.LiveSafety;
using TradeMind.Brokers.Infrastructure.Locking;
using TradeMind.Brokers.Infrastructure.Persistence;
using TradeMind.Brokers.Infrastructure.Reconciliation;

namespace TradeMind.Brokers.Infrastructure;

public static class BrokersInfrastructureDependencyInjection
{
    public static IServiceCollection AddTradeMindBrokersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<BrokerPersistenceOptions>().Bind(configuration.GetSection("TradeMind:Brokers:Persistence"));
        services.AddSingleton<InMemoryBrokerState>();
        services.AddSingleton<InMemoryBrokerConnector>();
        services.AddSingleton<IBrokerConnector>(provider => provider.GetRequiredService<InMemoryBrokerConnector>());
        services.RemoveAll<IBrokerReconciliationService>();
        services.AddSingleton<IBrokerReconciliationService, BrokerReconciliationService>();
        services.AddSingleton<IBrokerOrphanPositionDetector, BrokerOrphanPositionDetector>();
        services.AddHealthChecks().AddCheck<BrokerConnectorHealthCheck>("broker-connectors", tags: ["ready"]);
        services.AddHealthChecks().AddCheck<LiveSafetyHealthCheck>("live-safety", tags: ["ready"]);

        var persistence = configuration.GetSection("TradeMind:Brokers:Persistence").Get<BrokerPersistenceOptions>();
        if (persistence?.Enabled == true && !string.IsNullOrWhiteSpace(persistence.ConnectionString))
        {
            services.AddPooledDbContextFactory<BrokerDbContext>(options => options.UseNpgsql(persistence.ConnectionString, npgsql => npgsql.MigrationsAssembly(typeof(BrokerDbContext).Assembly.FullName)));
            services.RemoveAll<IBrokerIdempotencyStore>();
            services.RemoveAll<IBrokerExecutionRecordWriter>();
            services.RemoveAll<IBrokerReconciliationRecordWriter>();
            services.RemoveAll<IBrokerAuditWriter>();
            services.RemoveAll<ICriticalAuditWriter>();
            services.RemoveAll<IKillSwitchStore>();
            services.RemoveAll<IExecutionQuarantineStore>();
            services.RemoveAll<IBrokerPositionOwnershipStore>();
            services.RemoveAll<IBrokerExecutionLock>();
            services.RemoveAll<IBrokerExecutionRecoveryReader>();
            services.AddSingleton<IBrokerIdempotencyStore, PostgreSqlBrokerIdempotencyStore>();
            services.AddSingleton<IBrokerExecutionRecordWriter, BrokerExecutionRecordWriter>();
            services.AddSingleton<IBrokerReconciliationRecordWriter, BrokerReconciliationRecordWriter>();
            services.AddSingleton<IBrokerAuditWriter, PostgreSqlBrokerAuditWriter>();
            services.AddSingleton<ICriticalAuditWriter, PostgreSqlCriticalAuditWriter>();
            services.AddSingleton<IKillSwitchStore, PostgreSqlKillSwitchStore>();
            services.AddSingleton<IExecutionQuarantineStore, PostgreSqlExecutionQuarantineStore>();
            services.AddSingleton<IBrokerPositionOwnershipStore, PostgreSqlBrokerPositionOwnershipStore>();
            services.AddSingleton<IBrokerExecutionLock>(provider => new PostgreSqlBrokerExecutionLock(persistence.ConnectionString!, provider.GetRequiredService<TimeProvider>()));
            services.AddSingleton<IBrokerExecutionRecoveryReader, PostgreSqlBrokerExecutionRecoveryReader>();
        }

        return services;
    }
}
