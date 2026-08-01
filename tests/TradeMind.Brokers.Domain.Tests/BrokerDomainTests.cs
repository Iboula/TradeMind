using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Domain.Tests;

public sealed class BrokerDomainTests
{
    [Fact]
    public void Identifiers_reject_empty_values()
    {
        Assert.Throws<ArgumentException>(() => new BrokerId(" "));
        Assert.Throws<ArgumentException>(() => new BrokerConnectorId(""));
        Assert.Throws<ArgumentException>(() => new BrokerAccountId(""));
    }

    [Fact]
    public void Descriptor_is_immutable_and_live_requires_explicit_capability()
    {
        var descriptor = new BrokerConnectorDescriptor(new("simulation"), new("trademind"), "Simulation", "1", BrokerEnvironment.Test, BrokerExecutionMode.Simulation, BrokerCapability.ReadAccounts);
        Assert.Equal(BrokerExecutionMode.Simulation, descriptor.Mode);
        Assert.Throws<ArgumentException>(() => new BrokerConnectorDescriptor(new("live"), new("trademind"), "Live", "1", BrokerEnvironment.Production, BrokerExecutionMode.Live, BrokerCapability.None));
    }

    [Fact]
    public void Instrument_validates_tick_and_quantity_rules()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewInstrument(tickSize: 0));
        var instrument = NewInstrument();
        Assert.True(instrument.IsQuantityValid(0.10m));
        Assert.False(instrument.IsQuantityValid(0.105m));
        Assert.False(instrument.IsQuantityValid(11m));
    }

    [Fact]
    public void Market_order_cannot_have_requested_price()
    {
        Assert.Throws<ArgumentException>(() => new BrokerOrderRequest(new("exec"), "session", new("in-memory"), new("account"), "client", "EURUSD", BrokerOrderSide.Buy, BrokerOrderType.Market, 1, 1, null, null, [], BrokerTimeInForce.Day, null, "key", "corr", "tenant", "org", "actor", null, null, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Order_request_copies_collections()
    {
        var targets = new List<decimal> { 1.2m };
        var request = new BrokerOrderRequest(new("exec"), "session", new("in-memory"), new("account"), "client", "EURUSD", BrokerOrderSide.Buy, BrokerOrderType.Limit, 1, 1, null, null, targets, BrokerTimeInForce.Day, null, "key", "corr", "tenant", "org", "actor", null, null, null, DateTimeOffset.UtcNow);
        targets.Add(2.4m);
        Assert.Single(request.TakeProfits);
    }

    [Fact]
    public void Capabilities_and_metadata_are_defensively_exposed()
    {
        var metadata = new Dictionary<string, string> { ["environment"] = "test" };
        var descriptor = new BrokerConnectorDescriptor(new("simulation"), new("trademind"), "Simulation", "1", BrokerEnvironment.Test, BrokerExecutionMode.Simulation, BrokerCapability.ReadAccounts, metadata: metadata);
        metadata["environment"] = "changed";
        Assert.True(descriptor.Capabilities.Supports(BrokerCapability.ReadAccounts));
        Assert.Equal("test", descriptor.Metadata["environment"]);
    }

    private static BrokerInstrumentSpecification NewInstrument(decimal tickSize = 0.01m) => new("EURUSD", "EURUSD", BrokerAssetClass.Forex, "EUR", "USD", 5, 2, tickSize, 1, 100000, 0.01m, 10m, 0.01m, 0.01m, [], BrokerMarketStatus.Open, [BrokerOrderType.Market, BrokerOrderType.Limit], DateTimeOffset.UtcNow);
}
