using TradeMind.ExecutionSessions.Application.Abstractions;

namespace TradeMind.Api.Middleware;

public sealed class PostgreSqlIdempotencyStoreAdapter(IPersistentIdempotencyStore store) : IIdempotencyStore
{
    public async Task<IdempotencyExecutionResult> ExecuteAsync(
        string key,
        string requestHash,
        Func<CancellationToken, Task<CapturedResponse>> operation,
        CancellationToken cancellationToken)
    {
        var result = await store.ExecuteAsync(key, requestHash, async token =>
        {
            var response = await operation(token).ConfigureAwait(false);
            return new PersistentIdempotencyResponse(response.StatusCode, response.ContentType, response.Body);
        }, cancellationToken).ConfigureAwait(false);
        return new IdempotencyExecutionResult(
            new CapturedResponse(result.Response.StatusCode, result.Response.ContentType, result.Response.Body),
            result.IsReplay,
            result.IsConflict,
            result.IsInProgress);
    }
}
