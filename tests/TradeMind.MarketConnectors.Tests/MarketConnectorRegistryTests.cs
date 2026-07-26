using TradeMind.Market.Abstractions;
using TradeMind.MarketConnectors.Application;

namespace TradeMind.MarketConnectors.Tests;

public sealed class MarketConnectorRegistryTests
{
    [Fact]
    public void EmptyRegistry_HasNoConnectors()
    {
        var registry = new MarketConnectorRegistry([]);

        Assert.Empty(registry.GetAvailableConnectors());
        Assert.False(registry.TryGet(new ConnectorId("missing"), out _));
    }

    [Fact]
    public void Registry_ResolvesConnectorById()
    {
        IMarketConnector connector = new FakeConnector("sample");
        var registry = new MarketConnectorRegistry([connector]);

        Assert.True(registry.TryGet(new ConnectorId("SAMPLE"), out var resolved));
        Assert.Same(connector, resolved);
    }

    [Fact]
    public void Registry_RejectsDuplicateConnectorId()
    {
        var exception = Assert.Throws<DuplicateMarketConnectorRegistrationException>(() =>
            new MarketConnectorRegistry([new FakeConnector("duplicate"), new FakeConnector("DUPLICATE")]));

        Assert.Equal("duplicate", exception.ConnectorId.Value);
    }

    [Fact]
    public void Registry_ReturnsDescriptorsInDeterministicOrder()
    {
        var registry = new MarketConnectorRegistry([
            new FakeConnector("zeta"),
            new FakeConnector("alpha"),
            new FakeConnector("middle")]);

        Assert.Equal(
            ["alpha", "middle", "zeta"],
            registry.GetAvailableConnectors().Select(descriptor => descriptor.Id.Value));
    }

    [Fact]
    public void Registry_DescriptorCollectionIsReadOnly()
    {
        var registry = new MarketConnectorRegistry([new FakeConnector("sample")]);
        var descriptors = registry.GetAvailableConnectors();

        Assert.Throws<NotSupportedException>(() => ((ICollection<ConnectorDescriptor>)descriptors).Clear());
    }
}
