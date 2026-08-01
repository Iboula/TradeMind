using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public interface IBrokerConnector
{
    BrokerConnectorDescriptor Descriptor { get; }

    Task<BrokerHealthResult> GetHealthAsync(BrokerExecutionContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<BrokerAccount>> GetAccountsAsync(BrokerExecutionContext context, CancellationToken cancellationToken);
    Task<BrokerInstrumentSpecification?> GetInstrumentAsync(BrokerExecutionContext context, BrokerInstrumentQuery query, CancellationToken cancellationToken);
    Task<BrokerOrderSubmissionResult> SubmitOrderAsync(BrokerExecutionContext context, BrokerOrderRequest request, CancellationToken cancellationToken);
    Task<BrokerOrderModificationResult> ModifyOrderAsync(BrokerExecutionContext context, BrokerOrderModificationRequest request, CancellationToken cancellationToken);
    Task<BrokerOrderCancellationResult> CancelOrderAsync(BrokerExecutionContext context, BrokerOrderCancellationRequest request, CancellationToken cancellationToken);
    Task<BrokerPositionCloseResult> ClosePositionAsync(BrokerExecutionContext context, BrokerPositionCloseRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(BrokerExecutionContext context, BrokerOrderQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(BrokerExecutionContext context, BrokerPositionQuery query, CancellationToken cancellationToken);
}
