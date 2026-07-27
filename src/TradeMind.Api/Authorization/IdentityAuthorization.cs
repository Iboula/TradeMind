using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Permissions;

namespace TradeMind.Api.Authorization;

public sealed class PermissionRequirement(Permission permission) : IAuthorizationRequirement
{
    public Permission Permission { get; } = permission;
}

public static class IdentityPolicies
{
    public static string MarketContextBuild => ForPermission("TradeMind.MarketContext.Build");
    public static string ExpertsDispatch => ForPermission("TradeMind.Experts.Dispatch");
    public static string ExpertsAnalyze => ForPermission("TradeMind.Experts.Analyze");
    public static string ConsensusBuild => ForPermission("TradeMind.Consensus.Build");
    public static string TradingDecisionsEvaluate => ForPermission("TradeMind.TradingDecisions.Evaluate");
    public static string RiskEvaluate => ForPermission("TradeMind.Risk.Evaluate");
    public static string TradingPlansGenerate => ForPermission("TradeMind.TradingPlans.Generate");
    public static string TradingWorkspaceBuild => ForPermission("TradeMind.TradingWorkspace.Build");
    public static string TradingAssistantAsk => ForPermission("TradeMind.TradingAssistant.Ask");
    public static string PaperTradingSimulate => ForPermission("TradeMind.PaperTrading.Simulate");
    public static string ExecutionSessionsCreate => ForPermission("TradeMind.ExecutionSessions.Create");
    public static string ExecutionSessionsRead => ForPermission("TradeMind.ExecutionSessions.Read");
    public static string ExecutionSessionsSearch => ForPermission("TradeMind.ExecutionSessions.Search");
    public static string ExecutionSessionsAdvance => ForPermission("TradeMind.ExecutionSessions.Advance");
    public static string ExecutionSessionsCancel => ForPermission("TradeMind.ExecutionSessions.Cancel");
    public static string ExecutionSessionsFail => ForPermission("TradeMind.ExecutionSessions.Fail");
    public static string ExecutionSessionsReplay => ForPermission("TradeMind.ExecutionSessions.Replay");
    public static string ExecutionSessionsReadAudit => ForPermission("TradeMind.ExecutionSessions.ReadAudit");
    public static string ApiKeysCreate => ForPermission("TradeMind.ApiKeys.Create");
    public static string ApiKeysRead => ForPermission("TradeMind.ApiKeys.Read");
    public static string ApiKeysRevoke => ForPermission("TradeMind.ApiKeys.Revoke");
    public static string ApiKeysRotate => ForPermission("TradeMind.ApiKeys.Rotate");

    public static string ForPermission(string permission) => $"TradeMind.Permission.{permission}";

    public static string ForPermission(Permission permission) => ForPermission(permission.Value);
}

public sealed class PermissionAuthorizationHandler(
    ICurrentActor currentActor,
    TradeMind.Identity.Application.Abstractions.IAuthorizationService identityAuthorization,
    IOptions<IdentityOptions> options) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!options.Value.Enabled || identityAuthorization.Evaluate(currentActor.Identity, requirement.Permission).IsAllowed)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
