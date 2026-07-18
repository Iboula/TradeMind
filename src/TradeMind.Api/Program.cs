using TradeMind.KnowledgeHub.Application;
using TradeMind.KnowledgeHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddKnowledgeHub();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var knowledge = app.MapGroup("/knowledge");

knowledge.MapPost("/sources", async (
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
        cancellationToken);

    return Results.Created($"/knowledge/sources/{id}", new { id });
})
.DisableAntiforgery()
.Accepts<IFormFile>("multipart/form-data")
.Produces(StatusCodes.Status201Created)
.ProducesValidationProblem();

knowledge.MapGet("/sources/{id:guid}", async (
    Guid id,
    KnowledgeHubService service,
    CancellationToken cancellationToken) =>
{
    var source = await service.GetAsync(id, cancellationToken);
    return source is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            source.Id,
            source.Title,
            source.Type,
            source.Status,
            source.ImportedAtUtc,
            source.FailureReason,
            fragments = source.Fragments.Select(fragment => new
            {
                fragment.Id,
                fragment.Sequence,
                fragment.Content,
                fragment.TokenCount
            })
        });
});

knowledge.MapGet("/search", async (
    string q,
    int? limit,
    KnowledgeHubService service,
    CancellationToken cancellationToken) =>
{
    var results = await service.SearchAsync(q, limit ?? 5, cancellationToken);
    return Results.Ok(results);
});

app.Run();

public partial class Program;
