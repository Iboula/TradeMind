using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public interface IBrokerReconciliationRecordWriter
{
    Task WriteAsync(BrokerExecutionContext context, BrokerReconciliationReport report, CancellationToken cancellationToken);
}
