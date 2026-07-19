using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Tests;

public sealed class ExpertAgentCompatibilityTests
{
    [Fact]
    public void Compatibility_ShouldAcceptACompatibleContext()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();

        var result = new DefaultAgentCompatibilityPolicy(new FixedTimeProvider(ExpertAgentTestData.Now))
            .Evaluate(descriptor, context, ExpertAgentTestData.Request(context));

        Assert.True(result.IsCompatible);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void Compatibility_ShouldRejectUnsupportedInstrument()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(capabilities: new AgentCapabilities(
            supportedInstruments: [new Instrument("GBPUSD")]));

        var result = Evaluate(descriptor, context);

        Assert.Contains(result.Issues, issue => issue.Code == AgentErrorCode.UnsupportedInstrument);
        Assert.False(result.IsCompatible);
    }

    [Fact]
    public void Compatibility_ShouldRejectUnsupportedTimeframe()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(capabilities: new AgentCapabilities(
            supportedTimeframes: [Timeframe.M5]));

        var result = Evaluate(descriptor, context);

        Assert.Contains(result.Issues, issue => issue.Code == AgentErrorCode.UnsupportedTimeframe);
    }

    [Fact]
    public void Compatibility_ShouldRejectMissingRequiredContext()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(capabilities: new AgentCapabilities(
            requiredContextCategories: [ContextProviderCategory.Knowledge]));

        var result = Evaluate(descriptor, context);

        Assert.Contains(result.Issues, issue => issue.Code == AgentErrorCode.MissingRequiredContext);
        Assert.False(result.IsCompatible);
    }

    [Fact]
    public void Compatibility_ShouldRejectInsufficientQuality()
    {
        var context = ExpertAgentTestData.Context(quality: 35);
        var descriptor = ExpertAgentTestData.Descriptor(minimumQuality: 50);

        var result = Evaluate(descriptor, context);

        Assert.Contains(result.Issues, issue => issue.Code == AgentErrorCode.InsufficientContextQuality);
    }

    [Fact]
    public void Compatibility_ShouldRejectStaleRequiredContext()
    {
        var context = ExpertAgentTestData.Context(freshness: ContextFreshness.Stale);
        var descriptor = ExpertAgentTestData.Descriptor(
            capabilities: new AgentCapabilities(requiredContextCategories: [ContextProviderCategory.MarketSnapshot]));

        var result = Evaluate(descriptor, context);

        Assert.Contains(result.Issues, issue => issue.Code == AgentErrorCode.StaleContext);
    }

    [Fact]
    public void Compatibility_ShouldRejectUnsupportedContextVersion()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = new AgentDescriptor(
            new AgentId("test-agent"),
            "Test",
            AgentVersion.Parse("1.0.0"),
            AgentSpecialty.Custom,
            "Test",
            new AgentCapabilities(),
            supportedContextVersions: [2]);

        var result = Evaluate(descriptor, context);

        Assert.Contains(result.Issues, issue => issue.Code == AgentErrorCode.UnsupportedContextVersion);
    }

    [Fact]
    public void Compatibility_ShouldReturnWarningsForUsablePartialContext()
    {
        var context = ExpertAgentTestData.Context(MarketContextBuildStatus.PartiallySucceeded);

        var result = Evaluate(ExpertAgentTestData.Descriptor(), context);

        Assert.True(result.IsCompatible);
        Assert.True(result.HasWarnings);
        Assert.Contains(result.Issues, issue => issue.Severity == AgentCompatibilityIssueSeverity.Warning);
    }

    [Fact]
    public void Compatibility_ShouldHonorQuestionCapabilityAndAnalysisMode()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(capabilities: new AgentCapabilities(
            supportedAnalysisModes: [AgentAnalysisMode.Validation],
            supportsUserQuestion: false));

        var result = Evaluate(descriptor, context, ExpertAgentTestData.Request(context, question: "Why?"));

        Assert.Contains(result.Issues, issue => issue.Code == AgentErrorCode.UnsupportedAnalysisMode);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("question", StringComparison.OrdinalIgnoreCase));
    }

    private static AgentCompatibilityResult Evaluate(
        AgentDescriptor descriptor,
        MarketContext context,
        AgentExecutionRequest? request = null) =>
        new DefaultAgentCompatibilityPolicy(new FixedTimeProvider(ExpertAgentTestData.Now))
            .Evaluate(descriptor, context, request ?? ExpertAgentTestData.Request(context));
}
