using Microsoft.Extensions.Options;
using TradeMind.AI.Agents;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Tests;

public sealed class AIAgentAuthorizationTests
{
    [Fact]
    public void Authorizer_AllowsRequiredPermissions()
    {
        var definition = AIAgentTestData.Definition(permissions: ["agent.execute"]);
        Authorizer().Authorize(definition, Request(permissions: ["agent.execute"]), Context(permissions: ["agent.execute"]));
    }

    [Fact]
    public void Authorizer_RejectsMissingPermission()
    {
        var definition = AIAgentTestData.Definition(permissions: ["agent.execute"]);
        Assert.Throws<AIAgentAuthorizationException>(() => Authorizer().Authorize(definition, Request(), Context()));
    }

    [Fact]
    public void Authorizer_RejectsDisabledAgent()
    {
        var definition = AIAgentTestData.Definition(availability: AIAgentAvailability.Disabled);
        Assert.Throws<AIAgentUnavailableException>(() => Authorizer().Authorize(definition, Request(), Context()));
    }

    [Fact]
    public void Authorizer_RejectsDevelopmentAgentInProduction()
    {
        var definition = AIAgentTestData.Definition(availability: AIAgentAvailability.DevelopmentOnly);
        Assert.Throws<AIAgentUnavailableException>(() => Authorizer().Authorize(definition, Request(), Context()));
    }

    [Fact]
    public void Authorizer_RejectsUnsupportedScenario()
    {
        var definition = AIAgentTestData.Definition(scenarios: ["Supported"]);
        Assert.Throws<AIAgentAuthorizationException>(() => Authorizer().Authorize(definition, Request(), Context()));
    }

    [Fact]
    public void Authorizer_RejectsUnsupportedCapability()
    {
        var requested = new AIAgentCapabilities(supportsTools: true);
        var request = Request(requestedCapabilities: requested);
        var context = Context(requestedCapabilities: requested);
        Assert.Throws<AIAgentAuthorizationException>(() => Authorizer().Authorize(AIAgentTestData.Definition(), request, context));
    }

    [Fact]
    public void Authorizer_RejectsRestrictedTenant()
    {
        var definition = AIAgentTestData.Definition(tenants: ["allowed-tenant"]);
        Assert.Throws<AIAgentAuthorizationException>(() => Authorizer().Authorize(definition, Request(), Context()));
    }

    [Fact]
    public void Authorizer_RejectsRestrictedUser()
    {
        var definition = AIAgentTestData.Definition(users: ["allowed-user"]);
        Assert.Throws<AIAgentAuthorizationException>(() => Authorizer().Authorize(definition, Request(), Context()));
    }

    [Fact]
    public void Authorizer_RejectsExcessiveSideEffectPolicy()
    {
        var definition = AIAgentTestData.Definition();
        Assert.Throws<AIAgentAuthorizationException>(() => Authorizer().Authorize(
            definition,
            Request(),
            Context(maximumSideEffect: AIToolSideEffectLevel.None)));
    }

    [Fact]
    public void AgentFramework_DoesNotDependOnClaimsPrincipal()
    {
        var publicTypes = typeof(IAIAgentAuthorizer).Assembly.GetExportedTypes();
        Assert.DoesNotContain(publicTypes, type =>
            type.GetProperties().Any(property => property.PropertyType.FullName == "System.Security.Claims.ClaimsPrincipal")
            || type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType.FullName == "System.Security.Claims.ClaimsPrincipal"));
    }

    [Fact]
    public void AgentFramework_DoesNotReferenceHttpContext()
    {
        var references = typeof(IAIAgentAuthorizer).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true);
    }

    private static PolicyBasedAIAgentAuthorizer Authorizer() =>
        new(Options.Create(AIAgentTestData.Options()));

    private static AIAgentExecutionRequest Request(
        IReadOnlyCollection<string>? permissions = null,
        AIAgentCapabilities? requestedCapabilities = null) =>
        AIAgentTestData.Request(permissions: permissions, requestedCapabilities: requestedCapabilities);

    private static AIAgentAuthorizationContext Context(
        IReadOnlyCollection<string>? permissions = null,
        AIAgentCapabilities? requestedCapabilities = null,
        AIToolSideEffectLevel maximumSideEffect = AIToolSideEffectLevel.ReadOnly) =>
        new(
            "tenant",
            "user",
            permissions,
            "Scenario",
            maximumSideEffect,
            requestedCapabilities);
}
