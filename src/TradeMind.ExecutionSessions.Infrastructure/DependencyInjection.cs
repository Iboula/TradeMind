using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TradeMind.ExecutionSessions.Application;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Infrastructure.Idempotency;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;
using TradeMind.ExecutionSessions.Infrastructure.Persistence.Repositories;
using TradeMind.ExecutionSessions.Infrastructure.Replay;

namespace TradeMind.ExecutionSessions.Infrastructure;

public static class DependencyInjection
{
    public static async Task ApplyExecutionSessionsMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExecutionSessionsDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public static IServiceCollection AddExecutionSessions(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ExecutionSessionsPersistenceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddExecutionSessionsApplication();
        var options = services.AddOptions<ExecutionSessionsPersistenceOptions>()
            .Bind(configuration.GetSection(ExecutionSessionsPersistenceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        if (configure is not null) options.Configure(configure);

        var persistence = configuration.GetSection(ExecutionSessionsPersistenceOptions.SectionName)
            .Get<ExecutionSessionsPersistenceOptions>() ?? new ExecutionSessionsPersistenceOptions();
        var connectionString = configuration.GetConnectionString(persistence.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string '{persistence.ConnectionStringName}' is required for Execution Sessions.");

        services.AddDbContext<ExecutionSessionsDbContext>(dbOptions =>
        {
            dbOptions.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ExecutionSessionsDbContext).Assembly.FullName);
                npgsql.CommandTimeout(persistence.CommandTimeoutSeconds);
            });
            dbOptions.EnableSensitiveDataLogging(persistence.EnableSensitiveDataLogging);
            dbOptions.EnableDetailedErrors(persistence.EnableDetailedErrors);
        });
        services.AddDbContextFactory<ExecutionSessionsDbContext>(dbOptions =>
        {
            dbOptions.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ExecutionSessionsDbContext).Assembly.FullName);
                npgsql.CommandTimeout(persistence.CommandTimeoutSeconds);
            });
            dbOptions.EnableSensitiveDataLogging(persistence.EnableSensitiveDataLogging);
            dbOptions.EnableDetailedErrors(persistence.EnableDetailedErrors);
        });
        services.TryAddScoped<IExecutionSessionRepository, ExecutionSessionRepository>();
        services.TryAddScoped<IExecutionSessionUnitOfWork, ExecutionSessionUnitOfWork>();
        services.TryAddScoped<IExecutionSessionReplayReader, ExecutionSessionReplayReader>();
        services.TryAddScoped<IExecutionSessionAccessScope, UnrestrictedExecutionSessionAccessScope>();
        services.TryAddSingleton<IPersistentIdempotencyStore, PostgreSqlIdempotencyStore>();
        return services;
    }
}
