using TradeMind.Brokers.Application.Abstractions;

namespace TradeMind.Brokers.Application.Execution;

public sealed class NoOpBrokerAuditWriter : IBrokerAuditWriter
{
    public Task WriteAsync(BrokerAuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
}
