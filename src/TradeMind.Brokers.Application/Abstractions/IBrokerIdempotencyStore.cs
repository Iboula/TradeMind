using TradeMind.Brokers.Application.Execution;

namespace TradeMind.Brokers.Application.Abstractions;

public sealed record BrokerIdempotencyExecutionResult(
    BrokerExecutionResult Result,
    bool IsReplay,
    bool IsConflict,
    bool IsInProgress);

public interface IBrokerIdempotencyStore
{
    Task<BrokerIdempotencyExecutionResult> ExecuteAsync(
        string key,
        string requestHash,
        Func<CancellationToken, Task<BrokerExecutionResult>> operation,
        CancellationToken cancellationToken);

    Task MarkExecutionUnknownAsync(
        string key,
        string requestHash,
        BrokerExecutionResult result,
        CancellationToken cancellationToken);
}
