using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeMind.ExecutionSessions.Application.Abstractions;
using TradeMind.ExecutionSessions.Infrastructure.Persistence;
using TradeMind.ExecutionSessions.Infrastructure.Persistence.Entities;

namespace TradeMind.ExecutionSessions.Infrastructure.Idempotency;

public sealed class PostgreSqlIdempotencyStore(
    IDbContextFactory<ExecutionSessionsDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IOptions<ExecutionSessionsPersistenceOptions> options) : IPersistentIdempotencyStore
{
    private const string Processing = "Processing";
    private const string Completed = "Completed";

    public async Task<PersistentIdempotencyExecutionResult> ExecuteAsync(
        string key,
        string requestHash,
        Func<CancellationToken, Task<PersistentIdempotencyResponse>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentNullException.ThrowIfNull(operation);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(options.Value.IdempotencyTtlMinutes);
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO execution_idempotency_records
                (idempotency_key, request_hash, status, created_at_utc, expires_at_utc, concurrency_version)
            VALUES ({key}, {requestHash}, {Processing}, {now}, {expires}, {0L})
            ON CONFLICT (idempotency_key) DO UPDATE
                SET request_hash = EXCLUDED.request_hash,
                    status = EXCLUDED.status,
                    created_at_utc = EXCLUDED.created_at_utc,
                    expires_at_utc = EXCLUDED.expires_at_utc,
                    response_status_code = NULL,
                    response_content_type = NULL,
                    response_body = NULL,
                    concurrency_version = execution_idempotency_records.concurrency_version + 1
                WHERE execution_idempotency_records.status = {Completed}
                  AND execution_idempotency_records.expires_at_utc <= {now};
            """, cancellationToken).ConfigureAwait(false);

        if (affected == 1)
        {
            try
            {
                var response = await operation(cancellationToken).ConfigureAwait(false);
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE execution_idempotency_records
                    SET status = {Completed}, response_status_code = {response.StatusCode},
                        response_content_type = {response.ContentType}, response_body = {response.Body},
                        concurrency_version = concurrency_version + 1
                    WHERE idempotency_key = {key} AND request_hash = {requestHash} AND status = {Processing};
                    """, cancellationToken).ConfigureAwait(false);
                return new PersistentIdempotencyExecutionResult(response, false, false, false);
            }
            catch
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM execution_idempotency_records
                    WHERE idempotency_key = {key} AND request_hash = {requestHash} AND status = {Processing};
                    """, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        var existing = await dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null || existing.ExpiresAtUtc <= now)
        {
            return new PersistentIdempotencyExecutionResult(new PersistentIdempotencyResponse(409, null, []), false, true, false);
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new PersistentIdempotencyExecutionResult(new PersistentIdempotencyResponse(409, null, []), false, true, false);
        }

        if (existing.Status != Completed || existing.ResponseStatusCode is null || existing.ResponseBody is null)
        {
            return new PersistentIdempotencyExecutionResult(new PersistentIdempotencyResponse(409, null, []), false, false, true);
        }

        return new PersistentIdempotencyExecutionResult(
            new PersistentIdempotencyResponse(existing.ResponseStatusCode.Value, existing.ResponseContentType, existing.ResponseBody),
            true,
            false,
            false);
    }
}
