using Microsoft.Extensions.Options;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIToolAuthorizationTests
{
    [Fact]
    public void Authorizer_ShouldAllowRequiredPermission()
    {
        Authorizer().Authorize(
            AIToolTestData.Definition(permissions: ["tool.use"]),
            AIToolTestData.Authorization(permissions: ["tool.use"]));
    }

    [Fact]
    public void Authorizer_ShouldRejectMissingPermission()
    {
        Assert.Throws<AIToolAuthorizationException>(() => Authorizer().Authorize(
            AIToolTestData.Definition(permissions: ["tool.use"]),
            AIToolTestData.Authorization(permissions: ["other"])));
    }

    [Fact]
    public void Authorizer_ShouldRejectDisabledTool()
    {
        Assert.Throws<AIToolUnavailableException>(() => Authorizer().Authorize(
            AIToolTestData.Definition(availability: AIToolAvailability.Disabled),
            AIToolTestData.Authorization()));
    }

    [Fact]
    public void Authorizer_ShouldRejectDevelopmentToolInProduction()
    {
        Assert.Throws<AIToolUnavailableException>(() => Authorizer(enableDevelopmentTools: false).Authorize(
            AIToolTestData.Definition(availability: AIToolAvailability.DevelopmentOnly),
            AIToolTestData.Authorization()));
    }

    [Fact]
    public void Authorizer_ShouldRejectSideEffectAboveMaximum()
    {
        Assert.Throws<AIToolAuthorizationException>(() => Authorizer().Authorize(
            AIToolTestData.Definition(sideEffect: AIToolSideEffectLevel.ReversibleWrite),
            AIToolTestData.Authorization(maximumSideEffect: AIToolSideEffectLevel.ReadOnly)));
    }

    [Fact]
    public void Authorizer_ShouldAllowNoSideEffect()
    {
        Authorizer().Authorize(
            AIToolTestData.Definition(sideEffect: AIToolSideEffectLevel.None),
            AIToolTestData.Authorization(maximumSideEffect: AIToolSideEffectLevel.None));
    }

    [Fact]
    public void Authorizer_ShouldAllowReadOnlyPolicy()
    {
        Authorizer().Authorize(
            AIToolTestData.Definition(sideEffect: AIToolSideEffectLevel.ReadOnly),
            AIToolTestData.Authorization(maximumSideEffect: AIToolSideEffectLevel.ReadOnly));
    }

    [Fact]
    public void ToolEngine_ShouldNotReferenceClaimsPrincipal()
    {
        Assert.DoesNotContain(
            typeof(PermissionBasedAIToolAuthorizer).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "System.Security.Claims");
    }

    [Fact]
    public void ToolEngine_ShouldNotReferenceHttpContext()
    {
        Assert.DoesNotContain(
            typeof(PermissionBasedAIToolAuthorizer).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Authorizer_ShouldApplyIdentityAndScenarioRestrictions()
    {
        var definition = AIToolTestData.Definition(
            allowedScenarios: ["Coach"],
            allowedTenants: ["tenant-a"],
            allowedUsers: ["user-a"],
            allowedAgents: ["agent-a"]);

        Authorizer().Authorize(
            definition,
            AIToolTestData.Authorization(
                tenantId: "tenant-a",
                userId: "user-a",
                agentId: "agent-a",
                scenario: "Coach"));
        Assert.Throws<AIToolAuthorizationException>(() => Authorizer().Authorize(
            definition,
            AIToolTestData.Authorization(tenantId: "tenant-b", scenario: "Coach")));
    }

    private static PermissionBasedAIToolAuthorizer Authorizer(bool enableDevelopmentTools = false) =>
        new(Options.Create(new AIToolEngineOptions { EnableDevelopmentTools = enableDevelopmentTools }));
}
