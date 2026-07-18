using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeMind.Modules.Knowledge.Application;

namespace TradeMind.Modules.Knowledge.Infrastructure;

public static class KnowledgeModule
{
    public static IServiceCollection AddKnowledgeModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KnowledgeDatabase")
            ?? throw new InvalidOperationException("Connection string 'KnowledgeDatabase' is required.");

        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        services.AddScoped<IKnowledgeSourceRepository, KnowledgeSourceRepository>();
        services.AddScoped<IKnowledgeUnitOfWork>(provider => provider.GetRequiredService<KnowledgeDbContext>());
        services.AddScoped<IKnowledgeImporter, TxtKnowledgeImporter>();
        services.AddScoped<ITextExtractor, PlainTextExtractor>();
        services.AddSingleton<IFragmenter, SlidingWindowFragmenter>();
        services.AddSingleton<IEmbeddingGenerator, FakeEmbeddingGenerator>();
        services.AddScoped<IKnowledgeIndexer, DatabaseKnowledgeIndexer>();
        services.AddScoped<IKnowledgeSearcher, PgvectorKnowledgeSearcher>();
        services.AddSingleton<IKnowledgeClock, SystemKnowledgeClock>();
        services.AddScoped<KnowledgeHubService>();
        return services;
    }

    public static async Task ApplyKnowledgeMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
