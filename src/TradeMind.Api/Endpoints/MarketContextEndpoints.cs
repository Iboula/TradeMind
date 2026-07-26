using TradeMind.AI.Context.Application;
using TradeMind.Api.Application;
using TradeMind.Api.Contracts.MarketContext;

namespace TradeMind.Api.Endpoints;

public static class MarketContextEndpoints
{
    public static IEndpointRouteBuilder MapMarketContextEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/market-context/build", async (
            BuildMarketContextApiRequest request,
            HttpContext context,
            ITradeMindApiApplication application,
            CancellationToken cancellationToken) =>
        {
            var validation = EndpointHelpers.ValidateMarketContextRequest(request);
            if (validation is not null) return validation;

            var result = await application.BuildMarketContextAsync(
                request,
                EndpointHelpers.CorrelationId(context),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(EndpointHelpers.ToEnvelope(
                context,
                result,
                context.RequestServices.GetRequiredService<TimeProvider>()));
        })
        .WithName("BuildMarketContext")
        .WithTags("Market Context")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
        return endpoints;
    }
}
