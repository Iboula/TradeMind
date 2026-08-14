using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using TradeMind.Brokers.Application.LiveSafety;

namespace TradeMind.Brokers.Infrastructure.Locking;

public sealed class PostgreSqlBrokerExecutionLock(string connectionString, TimeProvider timeProvider) : IBrokerExecutionLock
{
    public async Task<BrokerExecutionLockResult> AcquireAsync(BrokerExecutionLockScope scope, string ownerId, BrokerExecutionLockOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(connectionString)) return BrokerExecutionLockResult.Unavailable("A PostgreSQL connection string is required.");

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var key = HashScope(scope);
            var deadline = timeProvider.GetUtcNow().Add(options.AcquisitionTimeout);
            while (true)
            {
                await using var command = new NpgsqlCommand("select pg_try_advisory_lock(@key)", connection);
                command.Parameters.Add("key", NpgsqlDbType.Bigint).Value = key;
                if (Equals(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), true))
                    return BrokerExecutionLockResult.AcquiredBy(new Lease(connection, scope, ownerId.Trim(), key));
                if (timeProvider.GetUtcNow() >= deadline)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                    return BrokerExecutionLockResult.Unavailable("The PostgreSQL advisory lock acquisition timed out.");
                }
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static long HashScope(BrokerExecutionLockScope scope)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", scope.TenantId, scope.BrokerId, scope.AccountId, scope.Instrument)));
        return BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(0, sizeof(long)));
    }

    private sealed class Lease(NpgsqlConnection connection, BrokerExecutionLockScope scope, string ownerId, long key) : IBrokerExecutionLockLease
    {
        private int released;

        public string OwnerId => ownerId;
        public BrokerExecutionLockScope Scope => scope;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) != 0) return;
            try
            {
                await using var command = new NpgsqlCommand("select pg_advisory_unlock(@key)", connection);
                command.Parameters.Add("key", NpgsqlDbType.Bigint).Value = key;
                await command.ExecuteScalarAsync().ConfigureAwait(false);
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
