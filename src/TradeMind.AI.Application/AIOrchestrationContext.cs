using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class AIOrchestrationContext
{
    private readonly List<string> _executedSteps = [];

    public AIOrchestrationContext(AIOrchestrationRequest request, DateTimeOffset startedAtUtc)
    {
        Request = request;
        CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;
        StartedAtUtc = startedAtUtc;
    }

    public AIOrchestrationRequest Request { get; }

    public string CorrelationId { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public ChatRequest? ChatRequest { get; set; }

    public ChatResponse? ChatResponse { get; set; }

    public AIOrchestrationResponse? FinalResponse { get; set; }

    public TimeSpan? ProviderDuration { get; set; }

    public IReadOnlyList<string> ExecutedSteps => _executedSteps;

    public IDictionary<AIOrchestrationContextItemKey, object> Items { get; } =
        new Dictionary<AIOrchestrationContextItemKey, object>();

    public void MarkStepExecuted(string stepName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        _executedSteps.Add(stepName);
    }
}
