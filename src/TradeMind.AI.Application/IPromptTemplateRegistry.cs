namespace TradeMind.AI.Application;

public interface IPromptTemplateRegistry
{
    Task<PromptTemplate> GetAsync(
        PromptTemplateId templateId,
        PromptTemplateVersion? version,
        CancellationToken cancellationToken);

    Task<PromptTemplate> GetLatestAsync(
        PromptTemplateId templateId,
        CancellationToken cancellationToken);
}
