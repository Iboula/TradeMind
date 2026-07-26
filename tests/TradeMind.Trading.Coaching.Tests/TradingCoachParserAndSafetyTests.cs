using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TradeMind.Trading.Coaching;

namespace TradeMind.Trading.Coaching.Tests;

public sealed class TradingCoachParserAndSafetyTests
{
    private readonly TradingCoachResponseParser _parser = new();
    private readonly TradingCoachSafetyFilter _safety = new(Options.Create(new TradingCoachSafetyOptions()));

    [Fact]
    public void Case056_ParsesValidJsonResponse()
    {
        var analysis = Parse(TradingCoachTestData.ValidResponseJson());
        Assert.Equal(80, analysis.Scores.PlanAdherence);
        Assert.False(analysis.Scores.Explanations[0].IsRuleBased);
    }

    [Fact]
    public void Case057_RejectsInvalidJson()
    {
        Assert.Throws<TradingCoachResponseParsingException>(() => Parse("not-json"));
    }

    [Fact]
    public void Case058_RejectsMissingCriticalField()
    {
        var node = JsonNode.Parse(TradingCoachTestData.ValidResponseJson())!.AsObject();
        node.Remove("summary");
        Assert.Throws<TradingCoachResponseParsingException>(() => Parse(node.ToJsonString()));
    }

    [Fact]
    public void Case059_RejectsOutOfBoundsScore()
    {
        var node = JsonNode.Parse(TradingCoachTestData.ValidResponseJson())!.AsObject();
        node["scores"]!["planAdherence"] = 101;
        Assert.Throws<TradingCoachResponseParsingException>(() => Parse(node.ToJsonString()));
    }

    [Fact]
    public void Case060_ParsedCollectionsAreImmutable()
    {
        var analysis = Parse(TradingCoachTestData.ValidResponseJson());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)analysis.Strengths).Add("changed"));
    }

    [Fact]
    public void Case061_DoesNotRevealRawResponse()
    {
        const string raw = "private-journal-value";
        var exception = Assert.Throws<TradingCoachResponseParsingException>(() => Parse(raw));
        Assert.DoesNotContain(raw, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Case062_DetectsBuyNow()
    {
        AssertUnsafe("Buy now before the move.");
    }

    [Fact]
    public void Case063_DetectsSellNow()
    {
        AssertUnsafe("Sell now without delay.");
    }

    [Fact]
    public void Case064_DetectsEnterLong()
    {
        AssertUnsafe("Enter long immediately.");
    }

    [Fact]
    public void Case065_DetectsEnterShort()
    {
        AssertUnsafe("Enter short immediately.");
    }

    [Fact]
    public void Case066_DetectsGuaranteedProfit()
    {
        AssertUnsafe("This provides guaranteed profit.");
    }

    [Fact]
    public void Case067_DetectsExcessiveLeverage()
    {
        AssertUnsafe("Use maximum leverage for this setup.");
    }

    [Fact]
    public void Case068_DetectsExactPrediction()
    {
        AssertUnsafe("The price will reach 1234 tomorrow.");
    }

    [Fact]
    public void Case069_SupportsFrenchVariants()
    {
        AssertUnsafe("Achetez maintenant avant la hausse.");
    }

    [Fact]
    public void Case070_AllowsSafeEducationalCoaching()
    {
        var analysis = TradingCoachTestData.Analysis();
        Assert.Same(analysis, _safety.Validate(analysis));
    }

    [Fact]
    public void Case071_DoesNotExposeProhibitedContentInFailure()
    {
        const string prohibited = "Buy now secret payload";
        var exception = Assert.Throws<TradingCoachSafetyException>(() =>
            _safety.Validate(TradingCoachTestData.Analysis(summary: prohibited)));
        Assert.DoesNotContain(prohibited, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(TradingCoachSafetyFilter).GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType.Name.StartsWith("ILogger", StringComparison.Ordinal));
    }

    private TradingCoachAnalysis Parse(string json) => _parser.Parse(
        json,
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        TradingCoachTestData.Now,
        TradingCoachConstants.AgentVersion);

    private void AssertUnsafe(string content)
    {
        Assert.Throws<TradingCoachSafetyException>(() =>
            _safety.Validate(TradingCoachTestData.Analysis(summary: content)));
    }
}
