using TradeMind.Api.Contracts.KnowledgeHub;
using TradeMind.KnowledgeHub.Application;

namespace TradeMind.Api.Endpoints;

public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapGroup(endpoints, "/api/v1/knowledge");
        MapGroup(endpoints, "/knowledge");
        return endpoints;
    }

    private static void MapGroup(IEndpointRouteBuilder endpoints, string prefix)
    {
        var group = endpoints.MapGroup(prefix).WithTags("KnowledgeHub");
        group.MapPost("/sources", async (
            IFormFile file,
            string? title,
            KnowledgeHubService service,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(file)] = ["The uploaded file is empty."]
                });
            }

            await using var stream = file.OpenReadStream();
            var id = await service.IngestAsync(
                new IngestKnowledgeSourceRequest(title ?? file.FileName, file.FileName, stream),
                cancellationToken).ConfigureAwait(false);
            return Results.Created($"/api/v1/knowledge/sources/{id}", new { id });
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/sources/{id:guid}", async (
            Guid id,
            KnowledgeHubService service,
            CancellationToken cancellationToken) =>
        {
            var source = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (source is null) return Results.NotFound();
            var response = new KnowledgeSourceResponse(
                source.Id,
                source.Title,
                source.Type.ToString(),
                source.Status.ToString(),
                source.ImportedAtUtc,
                source.FailureReason,
                source.Fragments.Select(fragment => new KnowledgeFragmentResponse(
                    fragment.Id,
                    fragment.Sequence,
                    fragment.Content,
                    fragment.TokenCount)).ToArray());
            return Results.Ok(response);
        })
        .Produces<KnowledgeSourceResponse>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/search", async (
            string q,
            int? limit,
            KnowledgeHubService service,
            CancellationToken cancellationToken) =>
        {
            var results = await service.SearchAsync(q, limit ?? 5, cancellationToken).ConfigureAwait(false);
            return Results.Ok(results.Select(result => new KnowledgeSearchResponse(
                result.SourceId,
                result.SourceTitle,
                result.FragmentId,
                result.Sequence,
                result.Content,
                result.Score)).ToArray());
        })
        .Produces<IReadOnlyList<KnowledgeSearchResponse>>();
    }
}
