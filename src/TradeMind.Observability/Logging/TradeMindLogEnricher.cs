using Microsoft.Extensions.Logging;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Logging;

public sealed class TradeMindLogEnricher(ITelemetryContextAccessor contextAccessor)
{
    public IDisposable BeginScope(ILogger logger) => LoggingScopeFactory.BeginScope(logger, contextAccessor.Current);
}
