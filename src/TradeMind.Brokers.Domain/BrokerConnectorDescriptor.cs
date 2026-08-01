namespace TradeMind.Brokers.Domain;

public sealed record BrokerConnectorDescriptor
{
    public BrokerConnectorDescriptor(
        BrokerConnectorId connectorId,
        BrokerId brokerId,
        string name,
        string version,
        BrokerEnvironment environment,
        BrokerExecutionMode mode,
        BrokerCapability capabilities,
        IEnumerable<BrokerAssetClass>? supportedAssetClasses = null,
        IEnumerable<BrokerOrderType>? supportedOrderTypes = null,
        bool supportsStreaming = false,
        bool supportsDemo = false,
        bool supportsLive = false,
        bool supportsReconciliation = false,
        int maximumConcurrentRequests = 1,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ConnectorId = connectorId ?? throw new ArgumentNullException(nameof(connectorId));
        BrokerId = brokerId ?? throw new ArgumentNullException(nameof(brokerId));
        Name = BrokerValidation.Required(name, nameof(name), 128);
        Version = BrokerValidation.Required(version, nameof(version), 64);
        if (maximumConcurrentRequests is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrentRequests));
        }

        if (mode == BrokerExecutionMode.Live && (!supportsLive || !capabilities.HasFlag(BrokerCapability.LiveTrading)))
        {
            throw new ArgumentException("Live mode requires an explicitly live-capable connector.", nameof(mode));
        }

        Capabilities = new BrokerCapabilities(capabilities);
        Environment = environment;
        Mode = mode;
        SupportedAssetClasses = Array.AsReadOnly((supportedAssetClasses ?? []).Distinct().OrderBy(item => item).ToArray());
        SupportedOrderTypes = Array.AsReadOnly((supportedOrderTypes ?? []).Distinct().OrderBy(item => item).ToArray());
        SupportsStreaming = supportsStreaming;
        SupportsDemo = supportsDemo;
        SupportsLive = supportsLive;
        SupportsReconciliation = supportsReconciliation;
        MaximumConcurrentRequests = maximumConcurrentRequests;
        Metadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            .ToDictionary(pair => BrokerValidation.Required(pair.Key, nameof(metadata), 64), pair => BrokerValidation.Required(pair.Value, nameof(metadata), 256), StringComparer.Ordinal)
            .AsReadOnly();
    }

    public BrokerConnectorId ConnectorId { get; }
    public BrokerId BrokerId { get; }
    public string Name { get; }
    public string Version { get; }
    public BrokerEnvironment Environment { get; }
    public BrokerExecutionMode Mode { get; }
    public BrokerCapabilities Capabilities { get; }
    public IReadOnlyList<BrokerAssetClass> SupportedAssetClasses { get; }
    public IReadOnlyList<BrokerOrderType> SupportedOrderTypes { get; }
    public bool SupportsStreaming { get; }
    public bool SupportsDemo { get; }
    public bool SupportsLive { get; }
    public bool SupportsReconciliation { get; }
    public int MaximumConcurrentRequests { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
