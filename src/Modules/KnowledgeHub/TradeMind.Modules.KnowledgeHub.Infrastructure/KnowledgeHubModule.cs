using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using TradeMind.Modules.KnowledgeHub.Application;

namespace TradeMind.Modules.KnowledgeHub.Infrastructure;

public static class KnowledgeHubModule
{
    public static IServiceCollection AddKnowledgeHub(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KnowledgeHub")
            ?? throw new InvalidOperationException("Connection string 'KnowledgeHub' is required.");

        services.AddDbContext<KnowledgeHubDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        services.AddSingleton(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector();
            return builder.Build();
        });

        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ImportKnowledgeSourceCommand).Assembly));
        services.AddScoped<IKnowledgeSourceRepository, KnowledgeSourceRepository>();
        services.AddScoped<IKnowledgeHubUnitOfWork>(sp => sp.GetRequiredService<KnowledgeHubDbContext>());
        services.AddScoped<IKnowledgeSearcher, KnowledgeSearcher>();
        services.AddSingleton<IKnowledgeImporter, TxtKnowledgeImporter>();
        services.AddSingleton<ITextExtractor, Utf8TextExtractor>();
        services.AddSingleton<IFragmenter, SlidingWindowFragmenter>();
        services.AddSingleton<IEmbeddingGenerator, FakeEmbeddingGenerator>();
        services.AddSingleton<IKnowledgeIndexer, KnowledgeIndexer>();
        return services;
    }

    public static async Task ApplyKnowledgeHubMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        await db.Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapKnowledgeHubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/knowledge").WithTags("KnowledgeHub");

        group.MapPost("/sources", async (IFormFile file, ISender sender, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new ImportKnowledgeSourceCommand(stream, file.FileName, file.ContentType), ct);
            return Results.Created($"/knowledge/sources/{result.Id}", result);
        }).DisableAntiforgery().Accepts<IFormFile>("multipart/form-data").Produces<KnowledgeSourceDto>(StatusCodes.Status201Created);

        group.MapGet("/sources/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetKnowledgeSourceQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/search", async (string query, ISender sender, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(query)) return Results.BadRequest(new { error = "Query is required." });
            return Results.Ok(await sender.Send(new SearchKnowledgeQuery(query, 5), ct));
        });

        return endpoints;
    }
}
