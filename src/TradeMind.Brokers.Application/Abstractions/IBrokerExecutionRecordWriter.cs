using TradeMind.Brokers.Application.Execution;

namespace TradeMind.Brokers.Application.Abstractions;

/// <summary>
/// Stores the broker-neutral outcome of an execution without exposing connector-native payloads.
/// </summary>
public interface IBrokerExecutionRecordWriter
{
    Task WriteAsync(
        BrokerOrderExecutionCommand command,
        BrokerExecutionResult result,
        CancellationToken cancellationToken);
}
