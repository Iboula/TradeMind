using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Execution;

public sealed class NoOpBrokerReconciliationRecordWriter : IBrokerReconciliationRecordWriter
{
    public Task WriteAsync(BrokerExecutionContext context, BrokerReconciliationReport report, CancellationToken cancellationToken) => Task.CompletedTask;
}
