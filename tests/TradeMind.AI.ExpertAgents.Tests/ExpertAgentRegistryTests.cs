using System.Collections.Concurrent;
using TradeMind.AI.Context.Domain;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Domain;
using TradeMind.Market.Abstractions;

namespace TradeMind.AI.ExpertAgents.Tests;

public sealed class ExpertAgentRegistryTests
{
    [Fact]
    public void Registry_ShouldRejectDuplicateAgentIdAndVersion()
    {
        var descriptor = ExpertAgentTestData.Descriptor();
        IExpertAgent[] agents = [new TestExpertAgent(descriptor), new TestExpertAgent(descriptor)];

        Assert.Throws<ExpertAgentRegistryException>(() =>
        {
            _ = new ImmutableExpertAgentRegistry(agents);
        });
    }

    [Fact]
    public void Registry_ShouldResolveExactVersion()
    {
        var v1 = new TestExpertAgent(ExpertAgentTestData.Descriptor(version: "1.0.0"));
        var v2 = new TestExpertAgent(ExpertAgentTestData.Descriptor(version: "2.0.0"));
        var registry = new ImmutableExpertAgentRegistry([v2, v1]);

        var found = registry.TryResolve(
            new AgentId("test-agent"),
            AgentVersionSelection.Exact,
            AgentVersion.Parse("1.0.0"),
            out var resolved);

        Assert.True(found);
        Assert.Same(v1, resolved);
    }

    [Fact]
    public void Registry_ShouldSelectLatestStableOverPrerelease()
    {
        var beta = new TestExpertAgent(ExpertAgentTestData.Descriptor(version: "3.0.0-beta.1"));
        var stable = new TestExpertAgent(ExpertAgentTestData.Descriptor(version: "2.0.0"));
        var registry = new ImmutableExpertAgentRegistry([beta, stable]);

        registry.TryResolve(new AgentId("test-agent"), AgentVersionSelection.LatestStable, null, out var resolved);

        Assert.Same(stable, resolved);
    }

    [Fact]
    public void Registry_ShouldOrderDiscoveryByIdThenDescendingVersion()
    {
        var agents = new IExpertAgent[]
        {
            new TestExpertAgent(ExpertAgentTestData.Descriptor("zeta-agent", "1.0.0")),
            new TestExpertAgent(ExpertAgentTestData.Descriptor("alpha-agent", "1.0.0")),
            new TestExpertAgent(ExpertAgentTestData.Descriptor("alpha-agent", "2.0.0"))
        };
        var registry = new ImmutableExpertAgentRegistry(agents);

        var ids = registry.GetAvailable().Select(descriptor => $"{descriptor.Id.Value}:{descriptor.Version}").ToArray();

        Assert.Equal(["alpha-agent:2.0.0", "alpha-agent:1.0.0", "zeta-agent:1.0.0"], ids);
    }

    [Fact]
    public void Registry_ShouldResolveBySpecialtyAndCapabilities()
    {
        var macro = new TestExpertAgent(ExpertAgentTestData.Descriptor(
            "macro-agent",
            specialty: AgentSpecialty.Macro,
            capabilities: new AgentCapabilities(
                supportedInstruments: [new Instrument("EURUSD")],
                supportedTimeframes: [Timeframe.H1],
                requiredContextCategories: [ContextProviderCategory.Knowledge])));
        var risk = new TestExpertAgent(ExpertAgentTestData.Descriptor("risk-agent", specialty: AgentSpecialty.Risk));
        var registry = new ImmutableExpertAgentRegistry([risk, macro]);

        var found = registry.Find(new ExpertAgentDiscoveryQuery
        {
            Specialty = AgentSpecialty.Macro,
            Instrument = new Instrument("EURUSD"),
            Timeframe = Timeframe.H1,
            RequiredContextCategories = [ContextProviderCategory.Knowledge]
        });

        Assert.Single(found);
        Assert.Equal("macro-agent", found[0].Id.Value);
    }

    [Fact]
    public void Registry_ShouldReturnEmptyForUnknownAgent()
    {
        var registry = new ImmutableExpertAgentRegistry([]);

        Assert.False(registry.TryResolve(new AgentId("unknown-agent"), AgentVersionSelection.LatestStable, null, out _));
        Assert.Empty(registry.GetVersions(new AgentId("unknown-agent")));
    }

    [Fact]
    public void Registry_ShouldBeSafeForConcurrentReads()
    {
        var registry = new ImmutableExpertAgentRegistry(
            Enumerable.Range(1, 5)
                .Select(index => (IExpertAgent)new TestExpertAgent(
                    ExpertAgentTestData.Descriptor($"agent-{index}")))
                .ToArray());
        var errors = new ConcurrentBag<Exception>();

        Parallel.For(0, 500, iteration =>
        {
            try
            {
                var available = registry.GetAvailable();
                var found = registry.Find(new ExpertAgentDiscoveryQuery());
                Assert.NotNull(available);
                Assert.NotNull(found);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        });

        Assert.Empty(errors);
    }
}
