namespace TradeMind.Trading.Analytics.Tests;

public sealed class NormalizationTests
{
    [Fact]
    public void Case011_SortsChronologically()
    {
        var request = TradingAnalyticsTestData.Request([
            TradingAnalyticsTestData.Trade(2), TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(1)]);
        var result = TradingAnalyticsTestData.Normalize(request);
        Assert.Equal([0, 1, 2], result.Trades.Select(trade => trade.Normalization.NormalizedRequest.OpenedAtUtc!.Value.Day - 1));
    }

    [Fact]
    public void Case012_DeduplicatesByJournalEntryId()
    {
        var first = TradingAnalyticsTestData.Trade(0, id: "same");
        var second = TradingAnalyticsTestData.Trade(0, id: "same");
        var result = TradingAnalyticsTestData.Normalize(TradingAnalyticsTestData.Request([first, second]));
        Assert.Single(result.Trades);
        Assert.Equal(1, result.DuplicateCount);
    }

    [Fact]
    public void Case013_DetectsConflictingDuplicateId()
    {
        var result = TradingAnalyticsTestData.Normalize(TradingAnalyticsTestData.Request([
            TradingAnalyticsTestData.Trade(0, resultR: 1, id: "same"),
            TradingAnalyticsTestData.Trade(1, resultR: -1, id: "same")]));
        Assert.Equal([0, 1], result.ConflictingDuplicateIndexes);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Case014_DeduplicatesByChecksumWithoutId()
    {
        var trade = TradingAnalyticsTestData.Trade(0, includeId: false);
        var result = TradingAnalyticsTestData.Normalize(TradingAnalyticsTestData.Request([trade, trade]));
        Assert.Single(result.Trades);
        Assert.StartsWith("checksum:", result.Trades[0].DeduplicationKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Case015_PreservesMostCompleteDuplicate()
    {
        var incomplete = TradingAnalyticsTestData.Trade(0, id: "same", complete: false);
        var complete = TradingAnalyticsTestData.Trade(0, id: "same", complete: true);
        var result = TradingAnalyticsTestData.Normalize(TradingAnalyticsTestData.Request([incomplete, complete]));
        Assert.Single(result.Trades);
        Assert.True(result.Trades[0].Normalization.CompletenessScore > 90);
    }

    [Fact]
    public void Case016_ProducesDuplicateCount()
    {
        var trade = TradingAnalyticsTestData.Trade(0);
        var result = TradingAnalyticsTestData.Normalize(TradingAnalyticsTestData.Request([trade, trade, trade]));
        Assert.Equal(2, result.DuplicateCount);
    }

    [Fact]
    public void Case017_OrderIsDeterministic()
    {
        var request = TradingAnalyticsTestData.Request([
            TradingAnalyticsTestData.Trade(1), TradingAnalyticsTestData.Trade(0), TradingAnalyticsTestData.Trade(2)]);
        var first = TradingAnalyticsTestData.Normalize(request).Trades.Select(trade => trade.OriginalIndex).ToArray();
        var second = TradingAnalyticsTestData.Normalize(request).Trades.Select(trade => trade.OriginalIndex).ToArray();
        Assert.Equal(first, second);
    }

    [Fact]
    public void Case018_DoesNotMutateInputs()
    {
        var original = TradingAnalyticsTestData.Trade(0, setup: "  Breakout   Review  ");
        _ = TradingAnalyticsTestData.Normalize(TradingAnalyticsTestData.Request([original]));
        Assert.Equal("  Breakout   Review  ", original.SetupName);
    }
}
