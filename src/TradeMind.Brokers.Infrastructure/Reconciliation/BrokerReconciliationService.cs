using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Infrastructure.Reconciliation;

public sealed class BrokerReconciliationService(IBrokerConnectorRegistry registry, IBrokerClock clock, IBrokerReconciliationRecordWriter recordWriter) : IBrokerReconciliationService
{
    public async Task<BrokerReconciliationReport> ReconcileAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerAccountId accountId, CancellationToken cancellationToken)
    {
        var started = clock.UtcNow;
        var connector = registry.GetRequired(connectorId);
        if (!connector.Descriptor.SupportsReconciliation)
        {
            var unsupported = Unsupported(connectorId, accountId, started);
            await recordWriter.WriteAsync(context, unsupported, cancellationToken).ConfigureAwait(false);
            return unsupported;
        }
        var orders = await connector.GetOrdersAsync(context, new BrokerOrderQuery(), cancellationToken).ConfigureAwait(false);
        var positions = await connector.GetPositionsAsync(context, new BrokerPositionQuery(), cancellationToken).ConfigureAwait(false);
        var mismatches = new List<BrokerReconciliationMismatch>();
        mismatches.AddRange(orders.GroupBy(order => order.ClientOrderId, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => new BrokerReconciliationMismatch(BrokerReconciliationMismatchType.DuplicateExecution, group.Key, "More than one order has the same client order id.")));
        mismatches.AddRange(positions.GroupBy(position => position.Instrument, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => new BrokerReconciliationMismatch(BrokerReconciliationMismatchType.PositionMismatch, group.Key, "More than one position exists for the same instrument in the reference connector.")));
        var report = new BrokerReconciliationReport(new BrokerReconciliationId("rec-" + started.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + connectorId.Value), connectorId, accountId, started, clock.UtcNow, mismatches.OrderBy(item => item.Type).ThenBy(item => item.Reference, StringComparer.Ordinal).ToArray(), null);
        await recordWriter.WriteAsync(context, report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private BrokerReconciliationReport Unsupported(BrokerConnectorId connectorId, BrokerAccountId accountId, DateTimeOffset started) => new(new BrokerReconciliationId("rec-" + started.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture)), connectorId, accountId, started, clock.UtcNow, [], new BrokerError("RECONCILIATION_UNSUPPORTED", BrokerErrorCategory.UnsupportedCapability, "The connector does not support reconciliation.", false, false, null, new BrokerTraceReference(null, null, null), clock.UtcNow));
}
