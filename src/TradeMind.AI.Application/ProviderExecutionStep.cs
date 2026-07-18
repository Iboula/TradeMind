using System.Diagnostics;
using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class ProviderExecutionStep : IAIOrchestrationStep
{
    private readonly IChatProvider _chatProvider;

    public ProviderExecutionStep(IChatProvider chatProvider)
    {
        _chatProvider = chatProvider;
    }

    public int Order => 400;

    public async Task ExecuteAsync(
        AIOrchestrationContext context,
        CancellationToken cancellationToken)
    {
        var chatRequest = context.ChatRequest
            ?? throw new AIOrchestrationException(
                "Chat request has not been constructed.",
                context.CorrelationId,
                nameof(ProviderExecutionStep));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            context.ChatResponse = await _chatProvider
                .CompleteAsync(chatRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AIOrchestrationException(
                "AI provider execution failed.",
                context.CorrelationId,
                nameof(ProviderExecutionStep),
                exception);
        }
        finally
        {
            stopwatch.Stop();
            context.ProviderDuration = stopwatch.Elapsed;
        }
    }
}
