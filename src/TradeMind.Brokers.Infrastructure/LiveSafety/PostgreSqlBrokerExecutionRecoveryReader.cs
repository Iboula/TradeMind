using Microsoft.EntityFrameworkCore;
using TradeMind.Brokers.Application.LiveSafety;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Domain;
using TradeMind.Brokers.Infrastructure.Persistence;

namespace TradeMind.Brokers.Infrastructure.LiveSafety;

public sealed class PostgreSqlBrokerExecutionRecoveryReader(IDbContextFactory<BrokerDbContext> dbContextFactory) : IBrokerExecutionRecoveryReader
{
    private static readonly HashSet<string> TerminalStatuses = [
        BrokerExecutionResultStatus.Accepted.ToString(),
        BrokerExecutionResultStatus.Replayed.ToString(),
        BrokerExecutionResultStatus.Rejected.ToString(),
        BrokerExecutionResultStatus.Failed.ToString(),
        BrokerExecutionResultStatus.Conflict.ToString()];

    public async Task<BrokerRecoverySnapshot> ReadAsync(string tenantId, string brokerId, string accountId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var executions = await context.Executions.AsNoTracking().Where(item => item.TenantId == tenantId && item.AccountId == accountId && item.ConnectorId == brokerId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var orders = await context.Orders.AsNoTracking().Where(item => item.TenantId == tenantId && item.AccountId == accountId && item.ConnectorId == brokerId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var positions = await context.Positions.AsNoTracking().Where(item => item.TenantId == tenantId && item.AccountId == accountId && item.ConnectorId == brokerId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ownership = await context.PositionOwnership.AsNoTracking().Where(item => item.TenantId == tenantId && item.BrokerAccountId == accountId).ToDictionaryAsync(item => item.PositionId, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        var reconciliation = await context.Reconciliations.AsNoTracking().Where(item => item.TenantId == tenantId && item.AccountId == accountId && item.ConnectorId == brokerId).OrderByDescending(item => item.CompletedAtUtc).Select(item => (DateTimeOffset?)item.CompletedAtUtc).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        var nonTerminal = executions.Where(item => !TerminalStatuses.Contains(item.Status)).Select(item => new BrokerRecoveryExecution(item.Id, tenantId, brokerId, accountId, orders.FirstOrDefault(order => order.ExecutionId == item.Id)?.Instrument ?? "unknown", item.Status, item.CreatedAtUtc)).ToArray();
        var normalizedOrders = orders.Select(item => new BrokerOrder(new BrokerOrderId(item.Id), new BrokerExecutionId(item.ExecutionId), new BrokerConnectorId(item.ConnectorId), new BrokerAccountId(item.AccountId), item.ClientOrderId, item.Instrument, BrokerOrderSide.Buy, BrokerOrderType.Market, item.Quantity, item.FilledQuantity, null, null, Enum.TryParse<BrokerOrderStatus>(item.Status, true, out var status) ? status : BrokerOrderStatus.Unknown, BrokerTimeInForce.Day, item.CreatedAtUtc, item.UpdatedAtUtc)).ToArray();
        var normalizedPositions = positions.Select(item => new BrokerRecoveryPosition(item.Id, tenantId, brokerId, accountId, item.Instrument, ownership.TryGetValue(item.Id, out var reference) ? reference.BrokerExecutionId : null, ownership.TryGetValue(item.Id, out reference) ? reference.TradingPlanId : null, ownership.TryGetValue(item.Id, out reference) ? reference.RiskAssessmentId : null, ownership.ContainsKey(item.Id) ? ownership[item.Id].ReconciliationState : "UnknownExternal")).ToArray();
        return new BrokerRecoverySnapshot(nonTerminal, normalizedOrders, normalizedPositions, reconciliation);
    }
}
