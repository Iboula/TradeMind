namespace TradeMind.AI.Application;

public interface IPromptRenderer
{
    Task<PromptRenderResult> RenderAsync(
        PromptRenderRequest request,
        CancellationToken cancellationToken);
}
