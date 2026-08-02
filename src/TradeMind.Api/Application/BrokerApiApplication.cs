using TradeMind.Api.Contracts.Brokers;
using TradeMind.Api.Endpoints;
using TradeMind.Api.Middleware;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;
using TradeMind.Brokers.Domain;
using TradeMind.Identity.Application.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace TradeMind.Api.Application;

public sealed class BrokerApiApplication(ICurrentActor currentActor, ICurrentTenant currentTenant)
{
    public BrokerExecutionContext Context(HttpContext httpContext)
    {
        var actor = currentActor.Identity;
        var tenant = currentTenant.Context;
        return new BrokerExecutionContext(
            actor.IsAuthenticated,
            actor.ActorId,
            actor.ActorType.ToString(),
            tenant?.TenantId.Value ?? actor.TenantId?.Value,
            tenant?.OrganizationId.Value ?? actor.OrganizationId?.Value,
            actor.Permissions.Values.Select(permission => permission.Value),
            httpContext.Request.Headers["X-Execution-Session-ID"].FirstOrDefault(),
            EndpointHelpers.CorrelationId(httpContext),
            httpContext.Request.Headers["X-TradeMind-Live-Confirmation"].FirstOrDefault());
    }

    public BrokerOrderExecutionCommand ToCommand(BrokerOrderApiRequest request, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var executionSessionId = context.Request.Headers["X-Execution-Session-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(executionSessionId)) throw new ArgumentException("X-Execution-Session-ID is required.");
        var actor = currentActor.Identity;
        var tenant = currentTenant.Context;
        var order = new BrokerOrderRequest(
            new BrokerExecutionId("exec-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', request.ConnectorId, request.AccountId, request.ClientOrderId, request.IdempotencyKey)))).ToLowerInvariant()[..24]),
            executionSessionId,
            new BrokerConnectorId(request.ConnectorId),
            new BrokerAccountId(request.AccountId),
            request.ClientOrderId,
            request.Instrument,
            Enum.Parse<BrokerOrderSide>(request.Side.ToString(), ignoreCase: false),
            Enum.Parse<BrokerOrderType>(request.OrderType.ToString(), ignoreCase: false),
            request.Quantity,
            request.RequestedPrice,
            request.StopPrice,
            request.StopLoss,
            request.TakeProfits,
            Enum.Parse<BrokerTimeInForce>(request.TimeInForce.ToString(), ignoreCase: false),
            request.ExpirationUtc,
            request.IdempotencyKey,
            EndpointHelpers.CorrelationId(context),
            tenant?.TenantId.Value ?? actor.TenantId?.Value,
            tenant?.OrganizationId.Value ?? actor.OrganizationId?.Value,
            actor.ActorId,
            request.TradingPlanId,
            request.RiskAssessmentId,
            new Dictionary<string, string>(),
            context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
        return new BrokerOrderExecutionCommand(order, null, null, Enum.Parse<BrokerExecutionMode>(request.Mode.ToString(), ignoreCase: false));
    }

    public BrokerConnectorId ConnectorId(string value) => new(value);
    public BrokerAccountId AccountId(string value) => new(value);
    public BrokerInstrumentQuery InstrumentQuery(string value) => new(value);
    public BrokerOrderQuery OrderQuery() => new();
    public BrokerPositionQuery PositionQuery() => new();
    public BrokerOrderModificationRequest OrderModification(string orderId, BrokerOrderModificationApiRequest request) => new(new BrokerOrderId(orderId), request.Quantity, request.LimitPrice, request.StopPrice, request.ExpirationUtc);
    public BrokerOrderCancellationRequest OrderCancellation(string orderId, BrokerOrderCancellationApiRequest request) => new(new BrokerOrderId(orderId), request.Reason);
    public BrokerPositionCloseRequest PositionClose(string positionId, BrokerPositionCloseApiRequest request) => new(new BrokerPositionId(positionId), request.Quantity);

    public static int StatusCode(BrokerExecutionResult result) => result.Status switch
    {
        BrokerExecutionResultStatus.Accepted => StatusCodes.Status201Created,
        BrokerExecutionResultStatus.Conflict => StatusCodes.Status409Conflict,
        BrokerExecutionResultStatus.Rejected when result.Error?.Category == BrokerErrorCategory.ConnectorUnavailable => StatusCodes.Status503ServiceUnavailable,
        BrokerExecutionResultStatus.Failed when result.Error?.Category == BrokerErrorCategory.ConnectorUnavailable => StatusCodes.Status503ServiceUnavailable,
        BrokerExecutionResultStatus.Rejected when result.Error?.Category == BrokerErrorCategory.UnsupportedCapability => StatusCodes.Status422UnprocessableEntity,
        BrokerExecutionResultStatus.Rejected when result.Error?.Category == BrokerErrorCategory.Authorization => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest
    };

    public static BrokerConnectorApiResponse ToResponse(BrokerConnectorDescriptor descriptor) => new(
        descriptor.ConnectorId.Value,
        descriptor.BrokerId.Value,
        descriptor.Name,
        descriptor.Version,
        descriptor.Environment.ToString(),
        descriptor.Mode.ToString(),
        Enum.GetValues<BrokerCapability>().Where(value => value != BrokerCapability.None && descriptor.Capabilities.HasFlag(value)).Select(value => value.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        descriptor.SupportedAssetClasses.Select(value => value.ToString()).ToArray(),
        descriptor.SupportedOrderTypes.Select(value => value.ToString()).ToArray(),
        descriptor.SupportsStreaming,
        descriptor.SupportsDemo,
        descriptor.SupportsLive,
        descriptor.SupportsReconciliation,
        descriptor.MaximumConcurrentRequests,
        descriptor.Metadata);

    public static BrokerHealthApiResponse ToResponse(BrokerHealthResult result) => new(result.Health.ConnectorId.Value, result.Health.Status.ToString(), result.Health.Message, result.Health.CheckedAtUtc, result.Health.Duration, result.Error?.Code, result.Error?.Message);
    public static BrokerAccountApiResponse ToResponse(BrokerAccount account) => new(account.AccountId.Value, account.ConnectorId.Value, account.ExternalAccountReference, account.Name, account.Currency, account.AccountType.ToString(), account.Environment.ToString(), account.Balance, account.Equity, account.Margin, account.FreeMargin, account.MarginLevel, account.Leverage, account.TradingEnabled, account.ReadOnly, account.Status.ToString(), account.UpdatedAtUtc, account.Metadata);
    public static BrokerInstrumentApiResponse ToResponse(BrokerInstrumentSpecification instrument) => new(instrument.Instrument, instrument.BrokerSymbol, instrument.AssetClass.ToString(), instrument.BaseCurrency, instrument.QuoteCurrency, instrument.PricePrecision, instrument.QuantityPrecision, instrument.TickSize, instrument.TickValue, instrument.ContractSize, instrument.MinimumQuantity, instrument.MaximumQuantity, instrument.QuantityStep, instrument.MinimumStopDistance, instrument.TradingSessions.Select(session => new BrokerTradingSessionApiResponse(session.Day.ToString(), session.OpensAtUtc.ToString("HH:mm:ss"), session.ClosesAtUtc.ToString("HH:mm:ss"))).ToArray(), instrument.MarketStatus.ToString(), instrument.SupportedOrderTypes.Select(value => value.ToString()).ToArray(), instrument.UpdatedAtUtc);
    public static BrokerOrderApiResponse ToResponse(BrokerOrder order) => new(order.OrderId.Value, order.ExecutionId.Value, order.ConnectorId.Value, order.AccountId.Value, order.ClientOrderId, order.Instrument, order.Side.ToString(), order.OrderType.ToString(), order.Quantity, order.FilledQuantity, order.RequestedPrice, order.AverageFillPrice, order.Status.ToString(), order.TimeInForce.ToString(), order.CreatedAtUtc, order.UpdatedAtUtc);
    public static BrokerPositionApiResponse ToResponse(BrokerPosition position) => new(position.PositionId.Value, position.ConnectorId.Value, position.AccountId.Value, position.Instrument, position.Side.ToString(), position.Quantity, position.AveragePrice, position.OpenedAtUtc, position.UpdatedAtUtc);
    public static BrokerExecutionApiResponse ToResponse(BrokerExecution execution) => new(execution.ExecutionId.Value, execution.Status.ToString(), "BrokerExecution", execution.OrderId.Value, execution.PositionId?.Value, execution.Error?.Code, execution.Error?.Category.ToString(), execution.Error?.Message, execution.OccurredAtUtc, false);
    public static BrokerReconciliationApiResponse ToResponse(BrokerReconciliationReport report) => new(report.ReconciliationId.Value, report.ConnectorId.Value, report.AccountId.Value, report.StartedAtUtc, report.CompletedAtUtc, report.IsConsistent, report.Mismatches.Select(item => new BrokerReconciliationMismatchApiResponse(item.Type.ToString(), item.Reference, item.Description)).ToArray(), report.Error?.Code, report.Error?.Message);

    public static BrokerExecutionApiResponse ToResponse(BrokerExecutionResult result) => new(
        result.ExecutionId.Value,
        result.Status.ToString(),
        result.Operation,
        result.Order?.OrderId.Value,
        result.Execution?.PositionId?.Value,
        result.Error?.Code,
        result.Error?.Category.ToString(),
        result.Error?.Message,
        result.CompletedAtUtc,
        result.IsLive);
}
