using Microsoft.Extensions.Options;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;

namespace TradeMind.AI.Context.Tests;

public sealed class ContextPolicyTests
{
    [Fact]
    public void Freshness_ShouldClassifyFreshData()
    {
        Assert.Equal(
            ContextFreshness.Fresh,
            Policy().Classify(
                ContextProviderCategory.MarketSnapshot,
                ContextTestData.Now.AddMinutes(-1),
                ContextTestData.Now));
    }

    [Fact]
    public void Freshness_ShouldClassifyAgingData()
    {
        Assert.Equal(
            ContextFreshness.Aging,
            Policy().Classify(
                ContextProviderCategory.MarketSnapshot,
                ContextTestData.Now.AddMinutes(-2),
                ContextTestData.Now));
    }

    [Fact]
    public void Freshness_ShouldClassifyStaleData()
    {
        Assert.Equal(
            ContextFreshness.Stale,
            Policy().Classify(
                ContextProviderCategory.MarketSnapshot,
                ContextTestData.Now.AddMinutes(-16),
                ContextTestData.Now));
    }

    [Fact]
    public void Freshness_ShouldTreatFreshBoundaryAsFresh()
    {
        var options = Options.Create(OptionsWithThresholds(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)));

        var result = new ContextFreshnessPolicy(options).Classify(
            ContextProviderCategory.MarketSnapshot,
            ContextTestData.Now.AddMinutes(-2),
            ContextTestData.Now);

        Assert.Equal(ContextFreshness.Fresh, result);
    }

    [Fact]
    public void Freshness_ShouldTreatAgingBoundaryAsAging()
    {
        var options = Options.Create(OptionsWithThresholds(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)));

        var result = new ContextFreshnessPolicy(options).Classify(
            ContextProviderCategory.MarketSnapshot,
            ContextTestData.Now.AddMinutes(-5),
            ContextTestData.Now);

        Assert.Equal(ContextFreshness.Aging, result);
    }

    [Fact]
    public void Freshness_ShouldRejectInvalidThresholds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ContextFreshnessThreshold(TimeSpan.FromSeconds(-1), TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentException>(() =>
            new ContextFreshnessThreshold(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Freshness_ShouldReturnUnknownWithoutTimestamp()
    {
        Assert.Equal(
            ContextFreshness.Unknown,
            Policy().Classify(
                ContextProviderCategory.Knowledge,
                null,
                ContextTestData.Now));
    }

    [Fact]
    public void Freshness_ShouldReturnUnknownForFutureTimestamp()
    {
        Assert.Equal(
            ContextFreshness.Unknown,
            Policy().Classify(
                ContextProviderCategory.Memory,
                ContextTestData.Now.AddSeconds(1),
                ContextTestData.Now));
    }

    [Fact]
    public void Freshness_ShouldReturnNotApplicableForNonTemporalCategory()
    {
        Assert.Equal(
            ContextFreshness.NotApplicable,
            Policy().Classify(
                ContextProviderCategory.Workspace,
                null,
                ContextTestData.Now));
    }

    [Fact]
    public void Quality_ShouldBeFullForFreshSuccessfulSources()
    {
        var descriptors = new[]
        {
            Descriptor("market", ContextRequirement.Required),
            Descriptor("knowledge", ContextRequirement.Preferred)
        };
        var traces = new[]
        {
            ContextTestData.Trace("market", ContextProviderExecutionStatus.Succeeded, ContextRequirement.Required),
            ContextTestData.Trace("knowledge", ContextProviderExecutionStatus.Succeeded, ContextRequirement.Preferred)
        };

        var quality = new ContextQualityPolicy().Evaluate(descriptors, traces);

        Assert.Equal(100, quality.Score);
        Assert.Equal(ContextQualityBand.Excellent, quality.Band);
    }

    [Fact]
    public void Quality_ShouldDecreaseForPartialSources()
    {
        var descriptors = new[]
        {
            Descriptor("market", ContextRequirement.Required),
            Descriptor("knowledge", ContextRequirement.Preferred)
        };
        var traces = new[]
        {
            ContextTestData.Trace("market", ContextProviderExecutionStatus.Succeeded, ContextRequirement.Required),
            ContextTestData.Trace("knowledge", ContextProviderExecutionStatus.Unavailable, ContextRequirement.Preferred)
        };

        var quality = new ContextQualityPolicy().Evaluate(descriptors, traces);

        Assert.True(quality.Score < 100);
        Assert.True(quality.Completeness < 100);
    }

    [Fact]
    public void Quality_ShouldBeDeterministicRegardlessOfInputOrder()
    {
        var descriptors = new[]
        {
            Descriptor("market", ContextRequirement.Required),
            Descriptor("knowledge", ContextRequirement.Preferred),
            Descriptor("workspace", ContextRequirement.Optional)
        };
        var traces = new[]
        {
            ContextTestData.Trace("market", ContextProviderExecutionStatus.Succeeded, ContextRequirement.Required),
            ContextTestData.Trace("knowledge", ContextProviderExecutionStatus.Succeeded, ContextRequirement.Preferred, ContextFreshness.Aging),
            ContextTestData.Trace("workspace", ContextProviderExecutionStatus.NotConfigured, ContextRequirement.Optional, ContextFreshness.NotApplicable)
        };
        var policy = new ContextQualityPolicy();

        var first = policy.Evaluate(descriptors, traces);
        var second = policy.Evaluate(descriptors.Reverse().ToArray(), traces.Reverse().ToArray());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Quality_ShouldWeightRequiredSourcesMoreThanOptionalSources()
    {
        var policy = new ContextQualityPolicy();
        var requiredMissing = policy.Evaluate(
            [Descriptor("required", ContextRequirement.Required), Descriptor("optional", ContextRequirement.Optional)],
            [
                ContextTestData.Trace("required", ContextProviderExecutionStatus.Unavailable, ContextRequirement.Required),
                ContextTestData.Trace("optional", ContextProviderExecutionStatus.Succeeded, ContextRequirement.Optional)
            ]);
        var optionalMissing = policy.Evaluate(
            [Descriptor("required", ContextRequirement.Required), Descriptor("optional", ContextRequirement.Optional)],
            [
                ContextTestData.Trace("required", ContextProviderExecutionStatus.Succeeded, ContextRequirement.Required),
                ContextTestData.Trace("optional", ContextProviderExecutionStatus.Unavailable, ContextRequirement.Optional)
            ]);

        Assert.True(requiredMissing.Score < optionalMissing.Score);
    }

    [Fact]
    public void Quality_ShouldReturnInsufficientForEmptyRegistry()
    {
        var quality = new ContextQualityPolicy().Evaluate([], []);

        Assert.Equal(0, quality.Score);
        Assert.Equal(ContextQualityBand.Insufficient, quality.Band);
    }

    private static ContextFreshnessPolicy Policy() =>
        new(Options.Create(new ContextEngineOptions()));

    private static ContextEngineOptions OptionsWithThresholds(TimeSpan fresh, TimeSpan aging) => new()
    {
        MarketFreshness = new ContextFreshnessThreshold(fresh, aging)
    };

    private static ContextProviderDescriptor Descriptor(string id, ContextRequirement requirement) => new(
        new ContextProviderId(id),
        ContextProviderCategory.MarketSnapshot,
        requirement,
        10,
        TimeSpan.FromSeconds(1));
}
