using Microsoft.Extensions.Options;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class ValidationAndNormalizationTests
{
    private readonly TradingJournalValidator _validator = new(Options.Create(new TradingJournalValidationOptions()));
    private readonly TradingJournalNormalizer _normalizer = new();

    [Fact]
    public void Case001_AcceptsMinimalValidRequest()
    {
        _validator.Validate(new TradingJournalAnalysisRequest(), Guid.NewGuid());
    }

    [Fact]
    public void Case002_RejectsNegativePrice()
    {
        var exception = Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(entryPrice: -1), Guid.NewGuid()));
        Assert.Contains(nameof(TradingJournalAnalysisRequest.EntryPrice), exception.InvalidFields);
    }

    [Fact]
    public void Case003_RejectsNonPositiveBalance()
    {
        Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(accountBalance: 0), Guid.NewGuid()));
    }

    [Fact]
    public void Case004_RejectsNegativeRisk()
    {
        Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(riskAmount: -1), Guid.NewGuid()));
    }

    [Fact]
    public void Case005_RejectsInconsistentPercentage()
    {
        var exception = Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(
                accountBalance: 10_000, riskAmount: 100, actualRiskPercentage: 2), Guid.NewGuid()));
        Assert.Contains(nameof(TradingJournalAnalysisRequest.ActualRiskPercentage), exception.InvalidFields);
    }

    [Fact]
    public void Case006_RejectsNonUtcDate()
    {
        Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(
                openedAtUtc: new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.FromHours(-4))), Guid.NewGuid()));
    }

    [Fact]
    public void Case007_RejectsCloseBeforeOpen()
    {
        Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(
                openedAtUtc: TradingCoachTestData.Now, closedAtUtc: TradingCoachTestData.Now.AddMinutes(-1)), Guid.NewGuid()));
    }

    [Fact]
    public void Case008_RejectsUnknownDirection()
    {
        Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(direction: "sideways"), Guid.NewGuid()));
    }

    [Fact]
    public void Case009_RejectsExcessivelyLongText()
    {
        Assert.Throws<TradingJournalValidationException>(() =>
            _validator.Validate(new TradingJournalAnalysisRequest(executionNotes: new string('x', 4_001)), Guid.NewGuid()));
    }

    [Fact]
    public void Case010_NormalizesDirection()
    {
        Assert.Equal("long", _normalizer.Normalize(new TradingJournalAnalysisRequest(direction: " ACHAT ")).NormalizedRequest.Direction);
    }

    [Fact]
    public void Case011_NormalizesTimeframe()
    {
        Assert.Equal("1H", _normalizer.Normalize(new TradingJournalAnalysisRequest(timeframe: " h1 ")).NormalizedRequest.Timeframe);
    }

    [Fact]
    public void Case012_NormalizesTags()
    {
        var result = _normalizer.Normalize(new TradingJournalAnalysisRequest(tags: [" Process ", "process", " REVIEW "]));
        Assert.Equal(["process", "review"], result.NormalizedRequest.Tags);
    }

    [Fact]
    public void Case013_PreservesExplicitValues()
    {
        var result = _normalizer.Normalize(new TradingJournalAnalysisRequest(
            accountBalance: 10_000, riskAmount: 100, actualRiskPercentage: 1.00m, resultRMultiple: 1.25m));
        Assert.Equal(1.00m, result.NormalizedRequest.ActualRiskPercentage);
        Assert.Equal(1.25m, result.NormalizedRequest.ResultRMultiple);
    }

    [Fact]
    public void Case014_ReportsDerivedFields()
    {
        var result = _normalizer.Normalize(new TradingJournalAnalysisRequest(
            accountBalance: 10_000, riskAmount: 100, resultAmount: 200));
        Assert.Contains(nameof(TradingJournalAnalysisRequest.ActualRiskPercentage), result.DerivedFields.Keys);
        Assert.Contains(nameof(TradingJournalAnalysisRequest.ResultRMultiple), result.DerivedFields.Keys);
    }

    [Fact]
    public void Case015_CalculatesCompletenessScore()
    {
        var complete = _normalizer.Normalize(TradingCoachTestData.CompleteRequest());
        var empty = _normalizer.Normalize(new TradingJournalAnalysisRequest());
        Assert.Equal(100m, complete.CompletenessScore);
        Assert.True(empty.CompletenessScore < complete.CompletenessScore);
    }

    [Fact]
    public void Case016_ExposesImmutableCollections()
    {
        var tags = new List<string> { "process" };
        var request = new TradingJournalAnalysisRequest(tags: tags);
        tags.Add("late-change");
        Assert.Single(request.Tags);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(request.Tags);
    }
}
