using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TradeMind.Modules.Knowledge.Application;

namespace TradeMind.Modules.Knowledge.Infrastructure;

public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/knowledge").WithTags("Knowledge");

        group.MapPost("/documents", async (
            CreateKnowledgeDocumentRequest request,
            KnowledgeService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.CreateAsync(
                new CreateKnowledgeDocumentCommand(request.Title, request.Source),
                cancellationToken);

            return Results.Created($"/api/knowledge/documents/{response.Id}", response);
        });

        group.MapGet("/documents/{id:guid}", async (
            Guid id,
            KnowledgeService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.GetAsync(id, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        return endpoints;
    }
}

public sealed record CreateKnowledgeDocumentRequest(string Title, string Source);
