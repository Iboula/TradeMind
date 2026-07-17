using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradeMind.Modules.Knowledge.Application;
using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.Infrastructure;

public static class KnowledgeModule
{
    public static IServiceCollection AddKnowledgeModule(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryKnowledgeStore>();
        services.AddSingleton<IKnowledgeDocumentRepository>(sp => sp.GetRequiredService<InMemoryKnowledgeStore>());
        services.AddSingleton<IKnowledgeUnitOfWork>(sp => sp.GetRequiredService<InMemoryKnowledgeStore>());
        services.AddSingleton<IKnowledgeClock, SystemKnowledgeClock>();
        services.AddScoped<KnowledgeService>();
        return services;
    }
}

internal sealed class InMemoryKnowledgeStore : IKnowledgeDocumentRepository, IKnowledgeUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, KnowledgeDocument> _documents = new();

    public Task AddAsync(KnowledgeDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_documents.TryAdd(document.Id, document))
        {
            throw new InvalidOperationException($"Knowledge document '{document.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<KnowledgeDocument?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

internal sealed class SystemKnowledgeClock : IKnowledgeClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
