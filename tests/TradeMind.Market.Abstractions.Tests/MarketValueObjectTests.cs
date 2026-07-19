using TradeMind.Market.Abstractions;

namespace TradeMind.Market.Abstractions.Tests;

public sealed class MarketValueObjectTests
{
    [Fact]
    public void ValueObjects_AcceptValidValues()
    {
        var snapshotId = new SnapshotId(Guid.Parse("d306ca1a-cf95-43b8-ad79-daf04f65b32d"));
        var connectorId = new ConnectorId("Connector-One");
        var account = new ExternalAccountReference("Account-A/42");
        var positionId = new ExternalPositionId("Position:A-42");
        var orderId = new ExternalOrderId("Order:A-43");
        var instrument = new Instrument("eurusd.pro");
        var price = new Price(1.25m);
        var drawingId = new DrawingId("Drawing:A-1");
        var indicatorName = new IndicatorName("Relative Strength");
        var instanceId = new IndicatorInstanceId("Instance:A-1");

        Assert.Equal("d306ca1a-cf95-43b8-ad79-daf04f65b32d", snapshotId.ToString());
        Assert.Equal("connector-one", connectorId.Value);
        Assert.Equal("Account-A/42", account.Value);
        Assert.Equal("Position:A-42", positionId.Value);
        Assert.Equal("Order:A-43", orderId.Value);
        Assert.Equal("EURUSD.PRO", instrument.Symbol);
        Assert.Equal(1.25m, price.Value);
        Assert.Equal("Drawing:A-1", drawingId.Value);
        Assert.Equal("Relative Strength", indicatorName.Value);
        Assert.Equal("Instance:A-1", instanceId.Value);
    }

    [Fact]
    public void StringValueObjects_RejectEmptyValues()
    {
        Assert.Throws<ArgumentException>(() => new ConnectorId(" "));
        Assert.Throws<ArgumentException>(() => new ExternalAccountReference(" "));
        Assert.Throws<ArgumentException>(() => new ExternalPositionId(" "));
        Assert.Throws<ArgumentException>(() => new ExternalOrderId(" "));
        Assert.Throws<ArgumentException>(() => new Instrument(" "));
        Assert.Throws<ArgumentException>(() => new DrawingId(" "));
        Assert.Throws<ArgumentException>(() => new IndicatorName(" "));
        Assert.Throws<ArgumentException>(() => new IndicatorInstanceId(" "));
    }

    [Fact]
    public void SnapshotId_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => new SnapshotId(Guid.Empty));
    }

    [Fact]
    public void Price_RejectsNegativeValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Price(-0.01m));
    }

    [Fact]
    public void Price_AcceptsZero()
    {
        Assert.Equal(0m, new Price(0m).Value);
    }

    [Fact]
    public void Instrument_NormalizesCaseAndPreservesBrokerSuffix()
    {
        Assert.Equal("EURUSD.RAW-PRO", new Instrument("  eurusd.raw-pro  ").Symbol);
    }

    [Fact]
    public void Instrument_RejectsEmbeddedWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new Instrument("EUR USD"));
    }

    [Fact]
    public void ExternalIdentifiers_RemainOpaqueAndCaseSensitive()
    {
        Assert.NotEqual(new ExternalOrderId("Order-A"), new ExternalOrderId("order-a"));
        Assert.Equal("Order:A/42", new ExternalOrderId(" Order:A/42 ").Value);
    }

    [Fact]
    public void ValueObjectEquality_IsDeterministic()
    {
        Assert.Equal(new ConnectorId("SAMPLE"), new ConnectorId("sample"));
        Assert.Equal(new Instrument("eurusd.a"), new Instrument("EURUSD.A"));
        Assert.Equal(new Price(1.10m), new Price(1.100m));
    }

    [Theory]
    [InlineData("M1")]
    [InlineData("M5")]
    [InlineData("M15")]
    [InlineData("M30")]
    [InlineData("H1")]
    [InlineData("H4")]
    [InlineData("D1")]
    [InlineData("W1")]
    [InlineData("MN1")]
    public void Timeframe_ParsesSupportedValues(string value)
    {
        Assert.Equal(value, Timeframe.Parse(value.ToLowerInvariant()).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("M2")]
    [InlineData("YEAR1")]
    public void Timeframe_RejectsUnsupportedValues(string value)
    {
        Assert.False(Timeframe.TryParse(value, out var result));
        Assert.Null(result);
        Assert.Throws<ArgumentException>(() => Timeframe.Parse(value));
    }

    [Fact]
    public void Timeframe_ExposesOnlyDeterministicDurations()
    {
        Assert.Equal(TimeSpan.FromHours(4), Timeframe.H4.Duration);
        Assert.Null(Timeframe.D1.Duration);
        Assert.Null(Timeframe.W1.Duration);
        Assert.Null(Timeframe.MN1.Duration);
        Assert.True(Timeframe.D1.IsCalendarBased);
        Assert.True(Timeframe.W1.IsCalendarBased);
        Assert.True(Timeframe.MN1.IsCalendarBased);
    }

    [Fact]
    public void Timeframe_SupportedCollectionIsReadOnly()
    {
        var supported = Timeframe.GetSupported();

        Assert.Equal(9, supported.Count);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Timeframe>)supported).Add(Timeframe.M1));
    }
}
