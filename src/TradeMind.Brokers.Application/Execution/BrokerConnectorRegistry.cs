using System.Collections.Concurrent;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Execution;

public sealed class BrokerConnectorRegistry : IBrokerConnectorRegistry
{
    private readonly ConcurrentDictionary<string, IBrokerConnector> _connectors = new(StringComparer.Ordinal);

    public BrokerConnectorRegistry(IEnumerable<IBrokerConnector> connectors)
    {
        ArgumentNullException.ThrowIfNull(connectors);
        foreach (var connector in connectors.OrderBy(item => item.Descriptor.ConnectorId.Value, StringComparer.Ordinal))
        {
            if (!_connectors.TryAdd(connector.Descriptor.ConnectorId.Value, connector))
            {
                throw new InvalidOperationException($"Broker connector '{connector.Descriptor.ConnectorId}' is registered more than once.");
            }
        }
    }

    public IReadOnlyList<BrokerConnectorDescriptor> Descriptors => _connectors.Values
        .Select(item => item.Descriptor)
        .OrderBy(item => item.ConnectorId.Value, StringComparer.Ordinal)
        .ToArray();

    public IBrokerConnector GetRequired(BrokerConnectorId connectorId)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        return _connectors.TryGetValue(connectorId.Value, out var connector)
            ? connector
            : throw new KeyNotFoundException($"Broker connector '{connectorId}' is not registered.");
    }

    public bool TryGet(BrokerConnectorId connectorId, out IBrokerConnector? connector)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        return _connectors.TryGetValue(connectorId.Value, out connector);
    }
}
