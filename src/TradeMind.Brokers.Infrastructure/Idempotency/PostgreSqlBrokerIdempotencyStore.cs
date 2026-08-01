using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Infrastructure.Persistence;
using TradeMind.Brokers.Infrastructure.Persistence.Entities;

namespace TradeMind.Brokers.Infrastructure.Idempotency;

public sealed class PostgreSqlBrokerIdempotencyStore(
    IDbContextFactory<BrokerDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IOptions<TradeMind.Brokers.Application.Options.BrokerOptions> brokerOptions) : IBrokerIdempotencyStore
{
    private const string Processing = "Processing";
    private const string Completed = "Completed";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BrokerIdempotencyExecutionResult> ExecuteAsync(string key, string requestHash, Func<CancellationToken, Task<BrokerExecutionResult>> operation, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var entity = new BrokerIdempotencyEntity { KeyHash = key, RequestHash = requestHash, Status = Processing, CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(brokerOptions.Value.IdempotencyTtlMinutes), ConcurrencyVersion = 0 };
        try
        {
            context.Idempotency.Add(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            var existing = await context.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.KeyHash == key, cancellationToken).ConfigureAwait(false);
            if (existing is null || existing.ExpiresAtUtc <= now) return new BrokerIdempotencyExecutionResult(Conflict(), false, true, false);
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)) return new BrokerIdempotencyExecutionResult(Conflict(), false, true, false);
            if (existing.Status != Completed || existing.ResultJson is null) return new BrokerIdempotencyExecutionResult(Conflict(), false, false, true);
            var replay = JsonSerializer.Deserialize<BrokerExecutionResult>(existing.ResultJson, JsonOptions) ?? Conflict();
            return new BrokerIdempotencyExecutionResult(replay, true, false, false);
        }

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            entity.Status = Completed;
            entity.ResultJson = JsonSerializer.Serialize(result, JsonOptions);
            entity.ConcurrencyVersion++;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new BrokerIdempotencyExecutionResult(result, false, false, false);
        }
        catch
        {
            context.Idempotency.Remove(entity);
            await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static BrokerExecutionResult Conflict() => new(new TradeMind.Brokers.Domain.BrokerExecutionId("idempotency-conflict"), BrokerExecutionResultStatus.Conflict, null, null, new TradeMind.Brokers.Domain.BrokerError("IDEMPOTENCY_CONFLICT", TradeMind.Brokers.Domain.BrokerErrorCategory.Conflict, "The idempotency key is associated with a different request.", false, false, null, new TradeMind.Brokers.Domain.BrokerTraceReference(null, null, null), DateTimeOffset.UnixEpoch), "SubmitOrder", DateTimeOffset.UnixEpoch);
}
