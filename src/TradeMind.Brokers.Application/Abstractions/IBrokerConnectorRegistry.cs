using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Abstractions;

public interface IBrokerConnectorRegistry
{
    IReadOnlyList<BrokerConnectorDescriptor> Descriptors { get; }
    IBrokerConnector GetRequired(BrokerConnectorId connectorId);
    bool TryGet(BrokerConnectorId connectorId, out IBrokerConnector? connector);
}
