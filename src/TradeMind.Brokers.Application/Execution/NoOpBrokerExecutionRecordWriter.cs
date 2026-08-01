using TradeMind.Brokers.Application.Abstractions;

namespace TradeMind.Brokers.Application.Execution;

public sealed class NoOpBrokerExecutionRecordWriter : IBrokerExecutionRecordWriter
{
    public Task WriteAsync(BrokerOrderExecutionCommand command, BrokerExecutionResult result, CancellationToken cancellationToken) => Task.CompletedTask;
}
