using TradeMind.Api.Application;
using TradeMind.Api.Authorization;
using TradeMind.Api.Contracts.Brokers;
using TradeMind.Api.Middleware;
using TradeMind.Brokers.Application.Abstractions;
using TradeMind.Brokers.Application.Execution;

namespace TradeMind.Api.Endpoints;

public static class BrokerEndpoints
{
    public static void MapBrokerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Brokers");
        group.MapGet("/brokers", (BrokerApiApplication api, IBrokerConnectorRegistry registry) => Results.Ok(new { items = registry.Descriptors.Select(BrokerApiApplication.ToResponse) })).RequireAuthorization(IdentityPolicies.BrokersRead);
        group.MapGet("/brokers/{connectorId}", (string connectorId, BrokerApiApplication api, IBrokerConnectorRegistry registry) =>
        {
            return registry.TryGet(api.ConnectorId(connectorId), out var connector) && connector is not null
                ? Results.Ok(BrokerApiApplication.ToResponse(connector.Descriptor))
                : EndpointHelpers.Problem(StatusCodes.Status404NotFound, "Broker connector not found", "The broker connector was not found.");
        }).RequireAuthorization(IdentityPolicies.BrokersRead);
        group.MapGet("/brokers/{connectorId}/health", async (string connectorId, HttpContext httpContext, BrokerApiApplication api, IBrokerConnectorRegistry registry, CancellationToken token) =>
        {
            var connector = registry.GetRequired(api.ConnectorId(connectorId));
            return Results.Ok(BrokerApiApplication.ToResponse(await connector.GetHealthAsync(api.Context(httpContext), token).ConfigureAwait(false)));
        }).RequireAuthorization(IdentityPolicies.BrokersRead);
        group.MapGet("/brokers/{connectorId}/accounts", async (string connectorId, HttpContext httpContext, BrokerApiApplication api, IBrokerConnectorRegistry registry, CancellationToken token) =>
        {
            var connector = registry.GetRequired(api.ConnectorId(connectorId));
            return Results.Ok(new { items = (await connector.GetAccountsAsync(api.Context(httpContext), token).ConfigureAwait(false)).Select(BrokerApiApplication.ToResponse) });
        }).RequireAuthorization(IdentityPolicies.BrokersReadAccounts);
        group.MapGet("/brokers/{connectorId}/accounts/{accountId}", async (string connectorId, string accountId, HttpContext httpContext, BrokerApiApplication api, IBrokerConnectorRegistry registry, CancellationToken token) =>
        {
            var connector = registry.GetRequired(api.ConnectorId(connectorId));
            var account = (await connector.GetAccountsAsync(api.Context(httpContext), token).ConfigureAwait(false)).SingleOrDefault(item => item.AccountId.Value == accountId);
            return account is null ? EndpointHelpers.Problem(StatusCodes.Status404NotFound, "Account not found", "The broker account was not found.") : Results.Ok(BrokerApiApplication.ToResponse(account));
        }).RequireAuthorization(IdentityPolicies.BrokersReadAccounts);
        group.MapGet("/brokers/{connectorId}/instruments/{instrument}", async (string connectorId, string instrument, HttpContext httpContext, BrokerApiApplication api, IBrokerConnectorRegistry registry, CancellationToken token) =>
        {
            var specification = await registry.GetRequired(api.ConnectorId(connectorId)).GetInstrumentAsync(api.Context(httpContext), api.InstrumentQuery(instrument), token).ConfigureAwait(false);
            return specification is null ? EndpointHelpers.Problem(StatusCodes.Status404NotFound, "Instrument not found", "The broker instrument was not found.") : Results.Ok(BrokerApiApplication.ToResponse(specification));
        }).RequireAuthorization(IdentityPolicies.BrokersRead);
        group.MapGet("/brokers/{connectorId}/orders", async (string connectorId, HttpContext httpContext, BrokerApiApplication api, IBrokerConnectorRegistry registry, CancellationToken token) => Results.Ok(new { items = (await registry.GetRequired(api.ConnectorId(connectorId)).GetOrdersAsync(api.Context(httpContext), api.OrderQuery(), token).ConfigureAwait(false)).Select(BrokerApiApplication.ToResponse) })).RequireAuthorization(IdentityPolicies.BrokersReadOrders);
        group.MapGet("/brokers/{connectorId}/positions", async (string connectorId, HttpContext httpContext, BrokerApiApplication api, IBrokerConnectorRegistry registry, CancellationToken token) => Results.Ok(new { items = (await registry.GetRequired(api.ConnectorId(connectorId)).GetPositionsAsync(api.Context(httpContext), api.PositionQuery(), token).ConfigureAwait(false)).Select(BrokerApiApplication.ToResponse) })).RequireAuthorization(IdentityPolicies.BrokersReadPositions);
        group.MapPost("/broker-orders", async (BrokerOrderApiRequest? request, HttpContext httpContext, BrokerApiApplication api, IBrokerExecutionService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            try
            {
                var result = await service.SubmitOrderAsync(api.Context(httpContext), api.ToCommand(request, httpContext), token).ConfigureAwait(false);
                var response = BrokerApiApplication.ToResponse(result);
                return Results.Json(response, statusCode: BrokerApiApplication.StatusCode(result));
            }
            catch (ArgumentException exception) { return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", exception.Message); }
        }).RequireAuthorization(IdentityPolicies.BrokersExecuteSimulation).WithMetadata(new IdempotencyMetadata.Required());
        group.MapPost("/broker-orders/{orderId}/modify", async (string orderId, BrokerOrderModificationApiRequest? request, HttpContext httpContext, BrokerApiApplication api, IBrokerExecutionService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await service.ModifyOrderAsync(api.Context(httpContext), api.ConnectorId(request.ConnectorId), api.OrderModification(orderId, request), token).ConfigureAwait(false);
            return result.Error is null && result.Order is not null ? Results.Ok(BrokerApiApplication.ToResponse(result.Order)) : EndpointHelpers.Problem(StatusCodes.Status422UnprocessableEntity, "Broker operation rejected", result.Error?.Message ?? "The broker did not return an order.");
        }).RequireAuthorization(IdentityPolicies.BrokersModifyOrders);
        group.MapPost("/broker-orders/{orderId}/cancel", async (string orderId, BrokerOrderCancellationApiRequest? request, HttpContext httpContext, BrokerApiApplication api, IBrokerExecutionService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await service.CancelOrderAsync(api.Context(httpContext), api.ConnectorId(request.ConnectorId), api.OrderCancellation(orderId, request), token).ConfigureAwait(false);
            return result.Error is null && result.Order is not null ? Results.Ok(BrokerApiApplication.ToResponse(result.Order)) : EndpointHelpers.Problem(StatusCodes.Status422UnprocessableEntity, "Broker operation rejected", result.Error?.Message ?? "The broker did not return an order.");
        }).RequireAuthorization(IdentityPolicies.BrokersCancelOrders);
        group.MapPost("/broker-positions/{positionId}/close", async (string positionId, BrokerPositionCloseApiRequest? request, HttpContext httpContext, BrokerApiApplication api, IBrokerExecutionService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var result = await service.ClosePositionAsync(api.Context(httpContext), api.ConnectorId(request.ConnectorId), api.PositionClose(positionId, request), token).ConfigureAwait(false);
            return result.Error is null && result.Execution is not null ? Results.Ok(BrokerApiApplication.ToResponse(result.Execution)) : EndpointHelpers.Problem(StatusCodes.Status422UnprocessableEntity, "Broker operation rejected", result.Error?.Message ?? "The broker did not return an execution.");
        }).RequireAuthorization(IdentityPolicies.BrokersClosePositions);
        group.MapPost("/brokers/{connectorId}/reconcile", async (string connectorId, BrokerReconciliationApiRequest? request, HttpContext httpContext, BrokerApiApplication api, IBrokerReconciliationService service, CancellationToken token) =>
        {
            if (request is null) return EndpointHelpers.Problem(StatusCodes.Status400BadRequest, "Invalid request", "A JSON request is required.");
            var report = await service.ReconcileAsync(api.Context(httpContext), api.ConnectorId(connectorId), api.AccountId(request.AccountId), token).ConfigureAwait(false);
            return report.Error is null ? Results.Ok(BrokerApiApplication.ToResponse(report)) : EndpointHelpers.Problem(StatusCodes.Status422UnprocessableEntity, "Broker reconciliation rejected", report.Error.Message);
        }).RequireAuthorization(IdentityPolicies.BrokersReconcile);
        group.MapGet("/broker-executions/{executionId}", () => EndpointHelpers.Problem(StatusCodes.Status501NotImplemented, "Broker execution lookup unavailable", "Execution record querying requires the configured broker persistence reader.")).RequireAuthorization(IdentityPolicies.BrokersRead);
    }
}
