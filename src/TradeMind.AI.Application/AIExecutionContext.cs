using TradeMind.AI.Abstractions;

namespace TradeMind.AI.Application;

public sealed class AIExecutionContext
{
    private readonly Dictionary<AIExecutionContextItemKey, object> _items = [];
    private readonly List<string> _executedSteps = [];

    public AIExecutionContext(
        AISession session,
        AIOrchestrationRequest request,
        DateTimeOffset startedAtUtc)
    {
        Session = session;
        Request = request;
        Metrics = new AIExecutionMetrics(startedAtUtc);
    }

    public AISession Session { get; }

    public AIOrchestrationRequest Request { get; }

    public ChatRequest? ChatRequest { get; private set; }

    public PromptRenderResult? PromptRenderResult { get; private set; }

    public ChatResponse? ChatResponse { get; private set; }

    public AIOrchestrationResponse? FinalResponse { get; private set; }

    public AIExecutionMetrics Metrics { get; }

    public IReadOnlyList<string> ExecutedSteps => _executedSteps;

    public AIExecutionState State { get; private set; } = AIExecutionState.Created;

    public AIExecutionError? Error { get; private set; }

    public IReadOnlyDictionary<AIExecutionContextItemKey, object> Items => _items;

    public void Start()
    {
        EnsureState(AIExecutionState.Created, "Only a created execution can start.");
        State = AIExecutionState.Running;
    }

    public void MarkStepStarted(string stepName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        EnsureState(AIExecutionState.Running, "Steps can only run while execution is running.");

        if (_executedSteps.Contains(stepName, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Step '{stepName}' has already been recorded.");
        }

        _executedSteps.Add(stepName);
    }

    public void MarkStepCompleted(string stepName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        EnsureState(AIExecutionState.Running, "Steps can only complete while execution is running.");

        if (!_executedSteps.Contains(stepName, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Step '{stepName}' was not started.");
        }
    }

    public void SetChatRequest(ChatRequest chatRequest)
    {
        ArgumentNullException.ThrowIfNull(chatRequest);
        EnsureState(AIExecutionState.Running, "Chat request can only be set while execution is running.");
        ChatRequest = chatRequest;
    }

    public void SetPromptRenderResult(PromptRenderResult promptRenderResult)
    {
        ArgumentNullException.ThrowIfNull(promptRenderResult);
        EnsureState(AIExecutionState.Running, "Prompt render result can only be set while execution is running.");
        PromptRenderResult = promptRenderResult;
    }

    public void SetChatResponse(ChatResponse chatResponse)
    {
        ArgumentNullException.ThrowIfNull(chatResponse);
        EnsureState(AIExecutionState.Running, "Chat response can only be set while execution is running.");

        ChatResponse = chatResponse;
        Metrics.RecordProvider(chatResponse.ProviderName, chatResponse.Model);
        Metrics.RecordTokens(
            chatResponse.Usage?.InputTokens,
            chatResponse.Usage?.OutputTokens,
            chatResponse.Usage?.TotalTokens);
    }

    public void SetFinalResponse(AIOrchestrationResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        EnsureState(AIExecutionState.Running, "Final response can only be set while execution is running.");

        if (FinalResponse is not null)
        {
            throw new InvalidOperationException("Final response has already been set.");
        }

        FinalResponse = response;
    }

    public void SetItem(AIExecutionContextItemKey key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (value is IServiceProvider)
        {
            throw new ArgumentException("Services cannot be stored in the execution context.", nameof(value));
        }

        _items[key] = value;
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        EnsureState(AIExecutionState.Running, "Only a running execution can complete.");
        Metrics.Complete(completedAtUtc);
        State = AIExecutionState.Completed;

        if (FinalResponse is not null)
        {
            FinalResponse = FinalResponse with
            {
                TotalDuration = Metrics.TotalDuration ?? TimeSpan.Zero,
                ProviderDuration = Metrics.ProviderDuration,
                CompletedAtUtc = completedAtUtc,
                State = State
            };
        }
    }

    public void Fail(AIExecutionError error, DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (State is AIExecutionState.Completed or AIExecutionState.Cancelled)
        {
            throw new InvalidOperationException($"Cannot fail an execution in state {State}.");
        }

        Error ??= error;
        Metrics.Complete(completedAtUtc);
        State = AIExecutionState.Failed;
    }

    public void Cancel(DateTimeOffset completedAtUtc)
    {
        if (State is AIExecutionState.Completed or AIExecutionState.Failed)
        {
            throw new InvalidOperationException($"Cannot cancel an execution in state {State}.");
        }

        Metrics.Complete(completedAtUtc);
        State = AIExecutionState.Cancelled;
    }

    private void EnsureState(AIExecutionState expectedState, string message)
    {
        if (State != expectedState)
        {
            throw new InvalidOperationException(message);
        }
    }
}
