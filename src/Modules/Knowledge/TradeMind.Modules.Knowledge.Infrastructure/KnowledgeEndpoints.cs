using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TradeMind.Modules.Knowledge.Application;

namespace TradeMind.Modules.Knowledge.Infrastructure;

public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/knowledge").WithTags("KnowledgeHub");

        group.MapPost("/sources", async (
            IFormFile file,
            KnowledgeHubService service,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(new { error = "The uploaded file is empty." });
            }

            if (!string.Equals(file.ContentType, "text/plain", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Only text/plain files are supported in the MVP." });
            }

            await using var stream = file.OpenReadStream();
            var response = await service.ImportAsync(
                new ImportKnowledgeSourceCommand(file.FileName, file.ContentType, stream),
                cancellationToken);

            return Results.Created($"/knowledge/sources/{response.Id}", response);
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<KnowledgeSourceResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/sources/{id:guid}", async (
            Guid id,
            KnowledgeHubService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.GetAsync(id, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        group.MapGet("/search", async (
            string query,
            int? limit,
            KnowledgeHubService service,
            CancellationToken cancellationToken) =>
        {
            var results = await service.SearchAsync(query, limit ?? 5, cancellationToken);
            return Results.Ok(results);
        });

        return endpoints;
    }
}
