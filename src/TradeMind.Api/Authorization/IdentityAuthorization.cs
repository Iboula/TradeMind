using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.Permissions;
using TradeMind.Observability.Abstractions;

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
    public static string ObservabilityReadDiagnostics => ForPermission("TradeMind.Observability.ReadDiagnostics");
    public static string ObservabilityReadMetrics => ForPermission("TradeMind.Observability.ReadMetrics");
    public static string ObservabilityReadTelemetry => ForPermission("TradeMind.Observability.ReadTelemetry");
    public static string BrokersRead => ForPermission("TradeMind.Brokers.Read");
    public static string BrokersReadAccounts => ForPermission("TradeMind.Brokers.ReadAccounts");
    public static string BrokersReadOrders => ForPermission("TradeMind.Brokers.ReadOrders");
    public static string BrokersReadPositions => ForPermission("TradeMind.Brokers.ReadPositions");
    public static string BrokersExecuteSimulation => ForPermission("TradeMind.Brokers.ExecuteSimulation");
    public static string BrokersExecuteDemo => ForPermission("TradeMind.Brokers.ExecuteDemo");
    public static string BrokersExecuteLive => ForPermission("TradeMind.Brokers.ExecuteLive");
    public static string BrokersModifyOrders => ForPermission("TradeMind.Brokers.ModifyOrders");
    public static string BrokersCancelOrders => ForPermission("TradeMind.Brokers.CancelOrders");
    public static string BrokersClosePositions => ForPermission("TradeMind.Brokers.ClosePositions");
    public static string BrokersReconcile => ForPermission("TradeMind.Brokers.Reconcile");
    public static string BrokersReadAudit => ForPermission("TradeMind.Brokers.ReadAudit");
    public static string BrokersManageConnectors => ForPermission("TradeMind.Brokers.ManageConnectors");

    public static string ForPermission(string permission) => $"TradeMind.Permission.{permission}";

    public static string ForPermission(Permission permission) => ForPermission(permission.Value);
}

public sealed class PermissionAuthorizationHandler(
    ICurrentActor currentActor,
    TradeMind.Identity.Application.Abstractions.IAuthorizationService identityAuthorization,
    IOptions<IdentityOptions> options,
    ITradeMindMetrics metrics) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!options.Value.Enabled || identityAuthorization.Evaluate(currentActor.Identity, requirement.Permission).IsAllowed)
            context.Succeed(requirement);
        else
            metrics.IncrementCounter(TelemetryMetricNames.AuthorizationDenials, 1, new MetricDimensions(
                Outcome: "Rejected",
                ActorType: currentActor.Identity.ActorType.ToString()));
        return Task.CompletedTask;
    }
}
