namespace TradeMind.ExecutionSessions.Application.Abstractions;

public sealed record PersistentIdempotencyResponse(int StatusCode, string? ContentType, byte[] Body);

public sealed record PersistentIdempotencyExecutionResult(
    PersistentIdempotencyResponse Response,
    bool IsReplay,
    bool IsConflict,
    bool IsInProgress);

public interface IPersistentIdempotencyStore
{
    Task<PersistentIdempotencyExecutionResult> ExecuteAsync(
        string key,
        string requestHash,
        Func<CancellationToken, Task<PersistentIdempotencyResponse>> operation,
        CancellationToken cancellationToken);
}
