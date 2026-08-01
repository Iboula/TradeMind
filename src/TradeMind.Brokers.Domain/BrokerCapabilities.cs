namespace TradeMind.Brokers.Domain;

public sealed record BrokerCapabilities
{
    public BrokerCapabilities(BrokerCapability values)
    {
        Values = values;
    }

    public BrokerCapability Values { get; }
    public bool Supports(BrokerCapability capability) => (Values & capability) == capability;
    public bool HasFlag(BrokerCapability capability) => Supports(capability);
}
