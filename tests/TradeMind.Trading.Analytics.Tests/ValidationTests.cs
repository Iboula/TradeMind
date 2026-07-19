using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Analytics.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void Case001_RejectsEmptyCollection()
    {
        Assert.Throws<ArgumentException>(() => new TradingJournalAnalyticsRequest([], TradingAnalyticsTestData.Profile()));
    }

    [Fact]
    public void Case002_RejectsTooManyTrades()
    {
        var request = TradingAnalyticsTestData.Request([TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1)]);
        var options = TradingAnalyticsTestData.Options();
        options.MaximumTradesPerAnalysis = 1;
        Assert.Throws<TradingJournalAnalyticsValidationException>(() => TradingAnalyticsTestData.Validate(request, options));
    }

    [Fact]
    public void Case003_RejectsDateToBeforeDateFrom()
    {
        Assert.Throws<ArgumentException>(() => new TradingJournalAnalyticsRequest(
            [TradingAnalyticsTestData.Trade()], TradingAnalyticsTestData.Profile(),
            TradingAnalyticsTestData.Now, TradingAnalyticsTestData.Now.AddDays(-1)));
    }

    [Fact]
    public void Case004_RejectsLocalDateOffset()
    {
        var local = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(-4));
        Assert.Throws<ArgumentException>(() => new TradingJournalAnalyticsRequest(
            [TradingAnalyticsTestData.Trade()], TradingAnalyticsTestData.Profile(), local));
    }

    [Fact]
    public void Case005_ValidatesEveryTrade()
    {
        var counter = new CountingTradeValidator();
        var validator = new TradingJournalAnalyticsValidator(counter, new TradingJournalNormalizer());
        var request = TradingAnalyticsTestData.Request();
        validator.Validate(request, TradingAnalyticsTestData.Options(), Guid.NewGuid());
        Assert.Equal(request.Trades.Count, counter.Calls);
    }

    [Fact]
    public void Case006_FailOnInvalidTradeThrows()
    {
        var request = TradingAnalyticsTestData.Request([TradingAnalyticsTestData.Trade(invalid: true)]);
        var exception = Assert.Throws<TradingJournalAnalyticsValidationException>(() =>
            TradingAnalyticsTestData.Validate(request, TradingAnalyticsTestData.Options(failOnInvalid: true)));
        Assert.Contains("Trades[0].EntryPrice", exception.Fields);
    }

    [Fact]
    public void Case007_ExcludeInvalidTradesWorks()
    {
        var request = TradingAnalyticsTestData.Request([
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1, invalid: true)]);
        var result = TradingAnalyticsTestData.Validate(request, TradingAnalyticsTestData.Options(failOnInvalid: false, excludeInvalid: true));
        Assert.Single(result.ValidTrades);
        Assert.Equal([1], result.InvalidTradeIndexes);
    }

    [Fact]
    public void Case008_IdentifiesIncompleteTrades()
    {
        var request = TradingAnalyticsTestData.Request([
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1, complete: false)]);
        var result = TradingAnalyticsTestData.Validate(request);
        Assert.Equal([1], result.IncompleteTradeIndexes);
    }

    [Fact]
    public void Case009_CalculatesGlobalCompleteness()
    {
        var request = TradingAnalyticsTestData.Request([
            TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1, complete: false)]);
        var result = TradingAnalyticsTestData.Validate(request);
        Assert.InRange(result.DataCompleteness, 50m, 80m);
    }

    [Fact]
    public void Case010_CollectionsAreImmutable()
    {
        var source = new List<TradingJournalAnalysisRequest> { TradingAnalyticsTestData.Trade(0) };
        var request = new TradingJournalAnalyticsRequest(source, TradingAnalyticsTestData.Profile());
        source.Add(TradingAnalyticsTestData.Trade(1));
        Assert.Single(request.Trades);
        Assert.Throws<NotSupportedException>(() => ((IList<TradingJournalAnalysisRequest>)request.Trades).Add(TradingAnalyticsTestData.Trade(2)));
    }

    private sealed class CountingTradeValidator : ITradingJournalValidator
    {
        public int Calls { get; private set; }

        public void Validate(TradingJournalAnalysisRequest request, Guid analysisId, string? correlationId = null) => Calls++;
    }
}
