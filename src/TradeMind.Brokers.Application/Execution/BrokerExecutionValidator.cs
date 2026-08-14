using Microsoft.Extensions.Options;
using TradeMind.AI.RiskEngine.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Options;
using TradeMind.Brokers.Application.LiveSafety;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Application.Execution;

public sealed class BrokerExecutionValidator(
    IBrokerAuthorizationPolicy authorizationPolicy,
    IBrokerClock clock,
    IOptions<BrokerOptions> options,
    ILiveTradingSafetyGate? liveSafetyGate = null)
{
    public async Task<BrokerEligibilityResult> ValidateAsync(
        BrokerExecutionContext context,
        BrokerOrderExecutionCommand command,
        IBrokerConnector connector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(connector);
        var request = command.Order;
        var authorization = authorizationPolicy.Evaluate(context, command.RequestedMode, "SubmitOrder");
        if (!authorization.IsAllowed) return Reject(authorization.Code, BrokerErrorCategory.Authorization, authorization.SafeReason!, context, request);
        if (!options.Value.Enabled) return Reject("BROKERS_DISABLED", BrokerErrorCategory.ConnectorUnavailable, "Broker execution is disabled.", context, request);
        if (!string.Equals(context.TenantId, request.TenantId, StringComparison.Ordinal)) return Reject("TENANT_MISMATCH", BrokerErrorCategory.TenantMismatch, "The request is outside the current tenant.", context, request);
        if (!string.Equals(context.ExecutionSessionId, request.ExecutionSessionId, StringComparison.Ordinal)) return Reject("EXECUTION_SESSION_MISMATCH", BrokerErrorCategory.Validation, "The request is outside the current execution session.", context, request);
        if (request.ActorId is not null && !string.Equals(context.ActorId, request.ActorId, StringComparison.Ordinal)) return Reject("ACTOR_MISMATCH", BrokerErrorCategory.Authorization, "The request actor does not match the authenticated actor.", context, request);
        if (options.Value.AllowedConnectors.Length > 0 && !options.Value.AllowedConnectors.Contains(request.ConnectorId.Value, StringComparer.Ordinal)) return Reject("CONNECTOR_NOT_ALLOWED", BrokerErrorCategory.Authorization, "The connector is not enabled for this deployment.", context, request);
        if (request.ConnectorId != connector.Descriptor.ConnectorId) return Reject("CONNECTOR_MISMATCH", BrokerErrorCategory.Validation, "The connector does not match the request.", context, request);
        if (connector.Descriptor.Mode != command.RequestedMode) return Reject("MODE_MISMATCH", BrokerErrorCategory.Validation, "The requested execution mode is not supported by this connector.", context, request);
        if (request.Quantity > options.Value.MaximumOrderQuantity) return Reject("MAXIMUM_QUANTITY", BrokerErrorCategory.InvalidQuantity, "The requested quantity exceeds the configured limit.", context, request);
        if (command.RequestedMode == BrokerExecutionMode.Live && !connector.Descriptor.SupportsLive) return Reject("LIVE_UNSUPPORTED", BrokerErrorCategory.UnsupportedCapability, "The connector does not support live trading.", context, request);
        if (command.RequestedMode == BrokerExecutionMode.Demo && !connector.Descriptor.SupportsDemo) return Reject("DEMO_UNSUPPORTED", BrokerErrorCategory.UnsupportedCapability, "The connector does not support demo trading.", context, request);

        if (options.Value.RequireTradingPlanAndRiskApproval)
        {
            if (command.TradingPlan is null || command.RiskAssessment is null) return Reject("PLAN_AND_RISK_REQUIRED", BrokerErrorCategory.Validation, "An approved trading plan and risk assessment are required.", context, request);
            if (command.TradingPlan.Status is not (TradingPlanStatus.Succeeded or TradingPlanStatus.PartiallySucceeded) || command.TradingPlan.Type != TradingPlanType.ExecutableCandidate)
                return Reject("PLAN_NOT_ELIGIBLE", BrokerErrorCategory.Validation, "The trading plan is not eligible for broker execution.", context, request);
            if (command.TradingPlan.ExpiresAtUtc <= clock.UtcNow) return Reject("PLAN_EXPIRED", BrokerErrorCategory.Validation, "The trading plan has expired.", context, request);
            if (command.RiskAssessment.Status is not (RiskAssessmentStatus.Succeeded or RiskAssessmentStatus.PartiallySucceeded) || command.RiskAssessment.Verdict is not (RiskVerdict.Approved or RiskVerdict.Reduced))
                return Reject("RISK_NOT_APPROVED", BrokerErrorCategory.Authorization, "The risk assessment is not approved for execution.", context, request);
            if (!string.Equals(command.TradingPlan.Instrument.Symbol, request.Instrument, StringComparison.OrdinalIgnoreCase)) return Reject("INSTRUMENT_MISMATCH", BrokerErrorCategory.Validation, "The request instrument does not match the trading plan.", context, request);
        }

        var accounts = await connector.GetAccountsAsync(context, cancellationToken).ConfigureAwait(false);
        var account = accounts.SingleOrDefault(item => item.AccountId == request.AccountId);
        if (account is null) return Reject("ACCOUNT_UNAVAILABLE", BrokerErrorCategory.AccountUnavailable, "The broker account is not available.", context, request);
        if (!account.TradingEnabled || account.ReadOnly || account.Status != BrokerAccountStatus.Active) return Reject("ACCOUNT_DISABLED", BrokerErrorCategory.Authorization, "The broker account is not enabled for trading.", context, request);
        if (account.Metadata.TryGetValue("tenant_id", out var accountTenant) && !string.Equals(accountTenant, context.TenantId, StringComparison.Ordinal)) return Reject("TENANT_MISMATCH", BrokerErrorCategory.TenantMismatch, "The broker account is outside the current tenant.", context, request);

        var instrument = await connector.GetInstrumentAsync(context, new BrokerInstrumentQuery(request.Instrument), cancellationToken).ConfigureAwait(false);
        if (instrument is null) return Reject("INSTRUMENT_UNAVAILABLE", BrokerErrorCategory.InstrumentUnavailable, "The broker instrument is not available.", context, request);
        if (instrument.MarketStatus != BrokerMarketStatus.Open) return Reject("MARKET_CLOSED", BrokerErrorCategory.MarketClosed, "The market is not open.", context, request);
        if (!instrument.SupportedOrderTypes.Contains(request.OrderType)) return Reject("ORDER_TYPE_UNSUPPORTED", BrokerErrorCategory.UnsupportedCapability, "The order type is not supported by the instrument.", context, request);
        if (!instrument.IsQuantityValid(request.Quantity)) return Reject("INVALID_QUANTITY", BrokerErrorCategory.InvalidQuantity, "The quantity does not satisfy the instrument rules.", context, request);
        var capability = request.OrderType switch
        {
            BrokerOrderType.Market => BrokerCapability.SubmitMarketOrders,
            BrokerOrderType.Limit => BrokerCapability.SubmitLimitOrders,
            BrokerOrderType.Stop or BrokerOrderType.StopLimit => BrokerCapability.SubmitStopOrders,
            _ => BrokerCapability.None
        };
        if (!connector.Descriptor.Capabilities.HasFlag(capability)) return Reject("UNSUPPORTED_CAPABILITY", BrokerErrorCategory.UnsupportedCapability, "The connector does not support this order operation.", context, request);
        if (request.StopLoss is not null && !connector.Descriptor.Capabilities.HasFlag(BrokerCapability.StopLoss)) return Reject("STOP_LOSS_UNSUPPORTED", BrokerErrorCategory.UnsupportedCapability, "Stop loss is not supported.", context, request);
        if (request.TakeProfits.Count > 0 && !connector.Descriptor.Capabilities.HasFlag(BrokerCapability.TakeProfit)) return Reject("TAKE_PROFIT_UNSUPPORTED", BrokerErrorCategory.UnsupportedCapability, "Take profit is not supported.", context, request);

        if (command.RequestedMode == BrokerExecutionMode.Live && liveSafetyGate is not null)
        {
            var liveDecision = await liveSafetyGate.EvaluateAsync(new LiveTradingSafetyRequest(
                context.TenantId!,
                connector.Descriptor.ConnectorId.Value,
                account.AccountId.Value,
                request.Instrument,
                request.ExecutionSessionId,
                allowLive: true,
                liveSafetyEnabled: true,
                account.Environment,
                connector.Descriptor.SupportsLive,
                account.Environment == BrokerEnvironment.Production,
                tenantAuthorized: true,
                actorAuthorized: context.HasPermission(BrokerPermissionNames.ExecuteLive),
                command.RiskAssessment?.Status is RiskAssessmentStatus.Succeeded or RiskAssessmentStatus.PartiallySucceeded && command.RiskAssessment.Verdict is RiskVerdict.Approved or RiskVerdict.Reduced,
                command.TradingPlan?.Status is TradingPlanStatus.Succeeded or TradingPlanStatus.PartiallySucceeded && command.TradingPlan.Type == TradingPlanType.ExecutableCandidate,
                workspaceClear: false,
                reconciliationClean: false,
                hasOrphanPositions: true,
                heartbeatFresh: false,
                brokerHealthy: false,
                idempotencyAvailable: true,
                distributedLockAvailable: false,
                operatorConfirmed: !string.IsNullOrWhiteSpace(context.ConfirmationToken),
                activationWindowValid: false,
                killSwitchDisabled: true,
                dualConfirmationComplete: false,
                drawdownWithinLimit: false,
                exposureWithinLimit: false,
                configurationEnvironment: "Production"), cancellationToken).ConfigureAwait(false);
            if (!liveDecision.IsAllowed)
            {
                var cause = liveDecision.Causes.FirstOrDefault();
                return Reject(cause?.Code ?? "LIVE_SAFETY_BLOCKED", BrokerErrorCategory.Authorization, cause?.Message ?? "Live execution is blocked by the independent safety gate.", context, request);
            }
        }

        return BrokerEligibilityResult.Success();
    }

    private BrokerEligibilityResult Reject(string code, BrokerErrorCategory category, string message, BrokerExecutionContext context, BrokerOrderRequest request) =>
        BrokerEligibilityResult.Reject(new BrokerError(code, category, message, false, false, null, new BrokerTraceReference(context.CorrelationId, null, request.ExecutionSessionId), clock.UtcNow));
}
