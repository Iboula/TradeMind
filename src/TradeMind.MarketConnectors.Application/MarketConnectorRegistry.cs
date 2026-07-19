using TradeMind.Market.Abstractions;

namespace TradeMind.MarketConnectors.Application;

public sealed class DuplicateMarketConnectorRegistrationException(ConnectorId connectorId)
    : InvalidOperationException($"A market connector with id '{connectorId}' is already registered.")
{
    public ConnectorId ConnectorId { get; } = connectorId;
}

public sealed class MarketConnectorRegistry : IMarketConnectorRegistry
{
    private readonly IReadOnlyDictionary<ConnectorId, IMarketConnector> _connectors;
    private readonly IReadOnlyCollection<ConnectorDescriptor> _descriptors;

    public MarketConnectorRegistry(IEnumerable<IMarketConnector> connectors)
    {
        ArgumentNullException.ThrowIfNull(connectors);

        var materialized = connectors.ToArray();
        var duplicate = materialized
            .GroupBy(connector => connector.Descriptor.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new DuplicateMarketConnectorRegistrationException(duplicate.Key);
        }

        _connectors = materialized.ToDictionary(connector => connector.Descriptor.Id);
        _descriptors = Array.AsReadOnly(materialized
            .Select(connector => connector.Descriptor)
            .OrderBy(descriptor => descriptor.Id.Value, StringComparer.Ordinal)
            .ToArray());
    }

    public bool TryGet(ConnectorId connectorId, out IMarketConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connectorId);
        return _connectors.TryGetValue(connectorId, out connector!);
    }

    public IReadOnlyCollection<ConnectorDescriptor> GetAvailableConnectors() => _descriptors;
}
