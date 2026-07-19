using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Tests;

public sealed class AgentModelTests
{
    [Fact]
    public void AgentRunId_ShouldRejectWhitespaceAndExcessiveLength()
    {
        Assert.Throws<ArgumentException>(() => new AgentRunId("run id"));
        Assert.Throws<ArgumentException>(() => new AgentRunId(new string('a', AgentRunId.MaximumLength + 1)));
    }

    [Fact]
    public void AgentSpecialty_ShouldNormalizeAndRejectInvalidValues()
    {
        Assert.Equal("macro", new AgentSpecialty("MACRO").Value);
        Assert.Throws<ArgumentException>(() => new AgentSpecialty("market structure"));
    }

    [Fact]
    public void Request_ShouldDefensivelyCopyMetadataAndPermissions()
    {
        var context = ExpertAgentTestData.Context();
        var metadata = new Dictionary<string, string> { ["source"] = "test" };
        var permissions = new[] { "analyze" };
        var request = new AgentExecutionRequest(
            new AgentRunId("run-immutable"),
            new AgentId("test-agent"),
            context.Id,
            "user-1",
            "session-1",
            "Assess",
            metadata: metadata,
            permissions: permissions);

        metadata["changed"] = "no";
        permissions[0] = "changed";

        Assert.Single(request.Metadata);
        Assert.Equal("analyze", Assert.Single(request.Permissions));
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(request.Metadata);
    }

    [Fact]
    public void Capabilities_ShouldExposeDeterministicReadOnlyCollections()
    {
        var capabilities = new AgentCapabilities(
            supportedInstruments: [new TradeMind.Market.Abstractions.Instrument("GBPUSD"), new TradeMind.Market.Abstractions.Instrument("EURUSD")],
            supportedTimeframes: [TradeMind.Market.Abstractions.Timeframe.H4, TradeMind.Market.Abstractions.Timeframe.M5],
            requiredContextCategories: [TradeMind.AI.Context.Domain.ContextProviderCategory.Knowledge]);

        Assert.Equal(["EURUSD", "GBPUSD"], capabilities.SupportedInstruments.Select(instrument => instrument.Symbol));
        Assert.Equal(["H4", "M5"], capabilities.SupportedTimeframes.Select(timeframe => timeframe.Code));
        Assert.True(capabilities.RequiresKnowledge);
    }

    [Fact]
    public void Confidence_ShouldRejectOutOfBoundsScores()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentConfidence(101, AgentConfidenceBand.VeryHigh));
        Assert.Throws<ArgumentOutOfRangeException>(() => AgentConfidence.FromScore(double.NaN, 50, 50));
    }

    [Fact]
    public void Result_ShouldKeepCollectionsImmutable()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var request = ExpertAgentTestData.Request(context);
        var observations = new List<AgentObservation>
        {
            new("one", AgentObservationImportance.Low, "Observation")
        };
        var result = ExpertAgentTestData.Result(context, request, descriptor, observations: observations);
        observations.Add(new("two", AgentObservationImportance.Low, "Added later"));

        Assert.Single(result.Observations);
        Assert.IsAssignableFrom<IReadOnlyList<AgentObservation>>(result.Observations);
    }

    [Fact]
    public void MarketLevel_ShouldRejectInvertedBounds()
    {
        Assert.Throws<ArgumentException>(() => new AgentMarketLevel(
            "level-1",
            AgentMarketLevelType.Support,
            new TradeMind.Market.Abstractions.Price(1.1m),
            TradeMind.Market.Abstractions.Timeframe.H1,
            AgentObservationImportance.Medium,
            "Reason",
            new TradeMind.Market.Abstractions.Price(2),
            new TradeMind.Market.Abstractions.Price(1)));
    }

    [Fact]
    public void Request_ShouldRejectUnboundedQuestionAndUnsupportedExactVersionShape()
    {
        var context = ExpertAgentTestData.Context();
        Assert.Throws<ArgumentException>(() => new AgentExecutionRequest(
            new AgentRunId("run-long"),
            new AgentId("test-agent"),
            context.Id,
            "user-1",
            "session-1",
            "Assess",
            question: new string('x', AgentExecutionRequest.MaximumQuestionLength + 1)));
        Assert.Throws<ArgumentException>(() => new AgentExecutionRequest(
            new AgentRunId("run-exact"),
            new AgentId("test-agent"),
            context.Id,
            "user-1",
            "session-1",
            "Assess",
            exactVersion: AgentVersion.Parse("1.0.0")));
    }
}
