using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TradeMind.ExecutionSessions.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddExecutionSessionsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<ExecutionSessionService>());
        services.TryAddScoped<IExecutionSessionService, ExecutionSessionService>();
        services.TryAddSingleton<Abstractions.IExecutionSessionIdFactory, DefaultExecutionSessionIdFactory>();
        services.TryAddSingleton<Abstractions.IExecutionSessionClock>(provider =>
            new Abstractions.TimeProviderExecutionSessionClock(provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
