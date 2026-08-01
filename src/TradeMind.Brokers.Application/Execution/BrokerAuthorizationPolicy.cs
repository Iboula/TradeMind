using Microsoft.Extensions.Options;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Execution;

public static class BrokerPermissionNames
{
    public const string Read = "TradeMind.Brokers.Read";
    public const string ReadAccounts = "TradeMind.Brokers.ReadAccounts";
    public const string ReadOrders = "TradeMind.Brokers.ReadOrders";
    public const string ReadPositions = "TradeMind.Brokers.ReadPositions";
    public const string ExecuteSimulation = "TradeMind.Brokers.ExecuteSimulation";
    public const string ExecuteDemo = "TradeMind.Brokers.ExecuteDemo";
    public const string ExecuteLive = "TradeMind.Brokers.ExecuteLive";
    public const string ModifyOrders = "TradeMind.Brokers.ModifyOrders";
    public const string CancelOrders = "TradeMind.Brokers.CancelOrders";
    public const string ClosePositions = "TradeMind.Brokers.ClosePositions";
    public const string Reconcile = "TradeMind.Brokers.Reconcile";
    public const string ReadAudit = "TradeMind.Brokers.ReadAudit";
    public const string ManageConnectors = "TradeMind.Brokers.ManageConnectors";
}

public sealed class BrokerAuthorizationPolicy(IOptions<BrokerOptions> options) : IBrokerAuthorizationPolicy
{
    public BrokerAuthorizationDecision Evaluate(BrokerExecutionContext context, BrokerExecutionMode mode, string operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!context.IsAuthenticated) return BrokerAuthorizationDecision.Deny("AUTHENTICATION_REQUIRED", "An authenticated actor is required.");
        if (string.IsNullOrWhiteSpace(context.TenantId)) return BrokerAuthorizationDecision.Deny("TENANT_REQUIRED", "A tenant scope is required.");

        var permission = mode switch
        {
            BrokerExecutionMode.Simulation => BrokerPermissionNames.ExecuteSimulation,
            BrokerExecutionMode.Demo => BrokerPermissionNames.ExecuteDemo,
            BrokerExecutionMode.Live => BrokerPermissionNames.ExecuteLive,
            _ => BrokerPermissionNames.Read
        };
        if (!context.HasPermission(permission)) return BrokerAuthorizationDecision.Deny("FORBIDDEN", $"Permission '{permission}' is required.");
        if (mode == BrokerExecutionMode.Demo && !options.Value.AllowDemoExecution) return BrokerAuthorizationDecision.Deny("DEMO_DISABLED", "Demo execution is disabled by configuration.");
        if (mode == BrokerExecutionMode.Live)
        {
            if (!options.Value.AllowLiveExecution) return BrokerAuthorizationDecision.Deny("LIVE_DISABLED", "Live execution is disabled by configuration.");
            if (options.Value.RequireExplicitLiveConfirmation && string.IsNullOrWhiteSpace(context.ConfirmationToken)) return BrokerAuthorizationDecision.Deny("LIVE_CONFIRMATION_REQUIRED", "Explicit live execution confirmation is required.");
        }

        return BrokerAuthorizationDecision.Allow();
    }
}
