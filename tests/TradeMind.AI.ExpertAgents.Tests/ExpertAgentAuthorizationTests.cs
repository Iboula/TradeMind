using Microsoft.Extensions.Options;
using TradeMind.AI.ExpertAgents.Application;
using TradeMind.AI.ExpertAgents.Domain;

namespace TradeMind.AI.ExpertAgents.Tests;

public sealed class ExpertAgentAuthorizationTests
{
    [Fact]
    public void Authorization_ShouldAllowStableAgentWithRequiredPermission()
    {
        var policy = new DefaultExpertAgentAuthorizationPolicy(Options.Create(new ExpertAgentOptions()));
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(requiredPermissions: ["market:read"]);

        var result = policy.Evaluate(descriptor, ExpertAgentTestData.Request(context, permissions: ["market:read"]));

        Assert.True(result.Allowed);
    }

    [Fact]
    public void Authorization_ShouldDenyMissingPermissionWithSafeReason()
    {
        var policy = new DefaultExpertAgentAuthorizationPolicy(Options.Create(new ExpertAgentOptions()));
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(requiredPermissions: ["market:read"]);

        var result = policy.Evaluate(descriptor, ExpertAgentTestData.Request(context));

        Assert.False(result.Allowed);
        Assert.Equal(AgentErrorCode.UnauthorizedAgent, result.ReasonCode);
        Assert.DoesNotContain("secret", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authorization_ShouldDenyDisabledAgent()
    {
        var policy = new DefaultExpertAgentAuthorizationPolicy(Options.Create(new ExpertAgentOptions()));
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(activationStatus: AgentActivationStatus.Disabled);

        var result = policy.Evaluate(descriptor, ExpertAgentTestData.Request(context));

        Assert.False(result.Allowed);
        Assert.Equal(AgentErrorCode.AgentDisabled, result.ReasonCode);
    }

    [Fact]
    public void Authorization_ShouldDenyExperimentalAgentByDefault()
    {
        var policy = new DefaultExpertAgentAuthorizationPolicy(Options.Create(new ExpertAgentOptions()));
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor(maturity: AgentMaturity.Experimental);

        var result = policy.Evaluate(descriptor, ExpertAgentTestData.Request(context));

        Assert.False(result.Allowed);
        Assert.Equal(AgentErrorCode.UnauthorizedAgent, result.ReasonCode);
    }

    [Fact]
    public void Authorization_ShouldBeReplaceable()
    {
        var context = ExpertAgentTestData.Context();
        var descriptor = ExpertAgentTestData.Descriptor();
        var result = new DenyAllAuthorizationPolicy().Evaluate(descriptor, ExpertAgentTestData.Request(context));

        Assert.False(result.Allowed);
        Assert.Equal(AgentErrorCode.UnauthorizedAgent, result.ReasonCode);
    }
}
