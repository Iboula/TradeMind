using TradeMind.Modules.Knowledge.Domain;

namespace TradeMind.Modules.Knowledge.Application;

public sealed class KnowledgeService(
    IKnowledgeDocumentRepository repository,
    IKnowledgeUnitOfWork unitOfWork,
    IKnowledgeClock clock)
{
    public async Task<KnowledgeDocumentResponse> CreateAsync(
        CreateKnowledgeDocumentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = KnowledgeDocument.Create(command.Title, command.Source, clock.UtcNow);
        await repository.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(document);
    }

    public async Task<KnowledgeDocumentResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await repository.GetAsync(id, cancellationToken);
        return document is null ? null : Map(document);
    }

    private static KnowledgeDocumentResponse Map(KnowledgeDocument document) =>
        new(document.Id, document.Title, document.Source, document.Status.ToString(), document.Chunks.Count);
}
