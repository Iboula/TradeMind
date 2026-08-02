using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public interface IBrokerExecutionService
{
    Task<BrokerExecutionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderExecutionCommand command, CancellationToken cancellationToken);
    Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerOrderModificationRequest request, CancellationToken cancellationToken);
    Task<BrokerOrderCancellationResult> CancelOrderAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerOrderCancellationRequest request, CancellationToken cancellationToken);
    Task<BrokerPositionCloseResult> ClosePositionAsync(BrokerExecutionContext context, BrokerConnectorId connectorId, BrokerPositionCloseRequest request, CancellationToken cancellationToken);
}
