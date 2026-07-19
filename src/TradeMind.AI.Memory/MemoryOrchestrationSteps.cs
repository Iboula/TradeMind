using Microsoft.Extensions.Logging;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Application;

namespace TradeMind.AI.Memory;

public sealed class MemoryReadStep : IAIOrchestrationStep
{
    private readonly IMemoryReader _memoryReader;
    private readonly MemoryOrchestrationOptions _options;
    private readonly ILogger<MemoryReadStep> _logger;

    public MemoryReadStep(
        IMemoryReader memoryReader,
        MemoryOrchestrationOptions options,
        ILogger<MemoryReadStep> logger)
    {
        _memoryReader = memoryReader;
        _options = options;
        _logger = logger;
    }

    public string Name => AIOrchestrationStepNames.MemoryRead;

    public int Order => 150;

    public async Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!IsMemoryEnabled(context.Request))
        {
            context.SetItem(AIExecutionContextItemKey.MemoryUsed, false);
            return;
        }

        try
        {
            var key = CreateKey(context);

            _logger.LogInformation(
                "Memory read started for conversation {ConversationId}, tenant {TenantId}, user {UserId}, session {SessionId}, and correlation {CorrelationId}",
                key.ConversationId,
                key.TenantId,
                key.UserId,
                context.Session.SessionId,
                context.Session.CorrelationId);

            var result = await _memoryReader.ReadAsync(
                new MemoryReadRequest(key, GetWindow(context.Request)),
                cancellationToken).ConfigureAwait(false);

            context.SetItem(AIExecutionContextItemKey.MemoryChatMessages, ToChatMessages(result));
            context.SetItem(AIExecutionContextItemKey.MemoryUsed, true);
            context.SetItem(AIExecutionContextItemKey.MemorySelectedEntryCount, result.SelectedEntryCount);

            _logger.LogInformation(
                "Memory read completed for conversation {ConversationId}, tenant {TenantId}, user {UserId}, selected {SelectedEntryCount} entries, truncated {Truncated}, oldest sequence {OldestSequence}, and newest sequence {NewestSequence}",
                key.ConversationId,
                key.TenantId,
                key.UserId,
                result.SelectedEntryCount,
                result.Truncated,
                result.OldestIncludedSequence,
                result.NewestIncludedSequence);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (GetFailureMode(context.Request) == MemoryFailureMode.ContinueWithoutMemory)
            {
                context.SetItem(AIExecutionContextItemKey.MemoryUsed, false);
                context.SetItem(AIExecutionContextItemKey.MemoryChatMessages, Array.Empty<ChatMessage>());
                _logger.LogWarning(
                    exception,
                    "Memory read failed and orchestration continues without memory for session {SessionId}, correlation {CorrelationId}, and conversation {ConversationId}",
                    context.Session.SessionId,
                    context.Session.CorrelationId,
                    context.Session.ConversationId);
                return;
            }

            throw new MemoryOperationException("Memory read failed.", null, exception);
        }
    }

    private static bool IsMemoryEnabled(AIOrchestrationRequest request) =>
        request.UseMemory || request.Memory.Enabled;

    private MemoryWindowOptions GetWindow(AIOrchestrationRequest request)
    {
        if (!request.Memory.Enabled)
        {
            return _options.Window;
        }

        var window = request.Memory.Window;
        return new MemoryWindowOptions(
            window.MaxEntries,
            window.MaxCharacters,
            window.MaxEstimatedTokens,
            window.IncludeSystemMessages,
            window.IncludeSummary,
            window.RecentUserMessagesMinimum,
            window.RecentAssistantMessagesMinimum);
    }

    private MemoryFailureMode GetFailureMode(AIOrchestrationRequest request) =>
        request.Memory.Enabled
            ? request.Memory.FailureMode == AIMemoryFailureMode.ContinueWithoutMemory
                ? MemoryFailureMode.ContinueWithoutMemory
                : MemoryFailureMode.FailClosed
            : _options.FailureMode;

    private static ConversationMemoryKey CreateKey(AIExecutionContext context)
    {
        return new ConversationMemoryKey(
            context.Session.ConversationId
                ?? throw new MemoryValidationException("ConversationId is required when memory is enabled."),
            context.Session.TenantId,
            context.Session.UserId);
    }

    private static IReadOnlyList<ChatMessage> ToChatMessages(MemoryReadResult result)
    {
        var messages = new List<ChatMessage>();
        if (result.Summary is not null)
        {
            messages.Add(new ChatMessage(
                ChatRole.System,
                $"Conversation summary through sequence {result.Summary.SummarizedThroughSequence}:{Environment.NewLine}{result.Summary.Content}"));
        }

        messages.AddRange(result.SelectedEntries.Select(entry => new ChatMessage(ToChatRole(entry.Role), entry.Content)));
        return messages.ToArray();
    }

    private static ChatRole ToChatRole(ConversationMemoryRole role)
    {
        return role switch
        {
            ConversationMemoryRole.System => ChatRole.System,
            ConversationMemoryRole.User => ChatRole.User,
            ConversationMemoryRole.Assistant => ChatRole.Assistant,
            ConversationMemoryRole.Tool => ChatRole.Tool,
            _ => throw new ArgumentOutOfRangeException(nameof(role), "Unsupported memory role.")
        };
    }
}

public sealed class MemoryWriteStep : IAIOrchestrationStep
{
    private readonly IMemoryWriter _memoryWriter;
    private readonly MemoryOrchestrationOptions _options;
    private readonly ILogger<MemoryWriteStep> _logger;

    public MemoryWriteStep(
        IMemoryWriter memoryWriter,
        MemoryOrchestrationOptions options,
        ILogger<MemoryWriteStep> logger)
    {
        _memoryWriter = memoryWriter;
        _options = options;
        _logger = logger;
    }

    public string Name => AIOrchestrationStepNames.MemoryWrite;

    public int Order => 450;

    public async Task ExecuteAsync(
        AIExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!IsMemoryEnabled(context.Request))
        {
            return;
        }

        try
        {
            var key = new ConversationMemoryKey(
                context.Session.ConversationId
                    ?? throw new MemoryValidationException("ConversationId is required when memory is enabled."),
                context.Session.TenantId,
                context.Session.UserId);

            var response = context.ChatResponse
                ?? throw new MemoryOperationException("Cannot write assistant memory before provider response.", key);

            _logger.LogInformation(
                "Memory write started after provider success for conversation {ConversationId}, tenant {TenantId}, user {UserId}, session {SessionId}, and correlation {CorrelationId}",
                key.ConversationId,
                key.TenantId,
                key.UserId,
                context.Session.SessionId,
                context.Session.CorrelationId);

            if (!context.Request.Memory.Enabled || context.Request.Memory.SaveUserMessage)
            {
                await _memoryWriter.WriteUserMessageAsync(
                    new MemoryWriteRequest(
                        key,
                        ConversationMemoryRole.User,
                        context.Request.UserMessage,
                        context.Session.CorrelationId,
                        context.Session.SessionId,
                        allowCompaction: !context.Request.Memory.Enabled || context.Request.Memory.CompactionEnabled),
                    cancellationToken).ConfigureAwait(false);
            }

            if (!context.Request.Memory.Enabled || context.Request.Memory.SaveAssistantResponse)
            {
                await _memoryWriter.WriteAssistantMessageAsync(
                    new MemoryWriteRequest(
                        key,
                        ConversationMemoryRole.Assistant,
                        response.Content,
                        context.Session.CorrelationId,
                        context.Session.SessionId,
                        response.Usage?.OutputTokens,
                        allowCompaction: !context.Request.Memory.Enabled || context.Request.Memory.CompactionEnabled),
                    cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Memory write completed after provider success for conversation {ConversationId}, tenant {TenantId}, user {UserId}, session {SessionId}, and correlation {CorrelationId}",
                key.ConversationId,
                key.TenantId,
                key.UserId,
                context.Session.SessionId,
                context.Session.CorrelationId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (GetFailureMode(context.Request) == MemoryFailureMode.ContinueWithoutMemory)
            {
                _logger.LogWarning(
                    exception,
                    "Memory write failed after provider response for session {SessionId}, correlation {CorrelationId}, and conversation {ConversationId}",
                    context.Session.SessionId,
                    context.Session.CorrelationId,
                    context.Session.ConversationId);
                return;
            }

            throw new MemoryOperationException("Memory write failed after provider response.", null, exception);
        }
    }

    private static bool IsMemoryEnabled(AIOrchestrationRequest request) =>
        request.UseMemory || request.Memory.Enabled;

    private MemoryFailureMode GetFailureMode(AIOrchestrationRequest request) =>
        request.Memory.Enabled
            ? request.Memory.FailureMode == AIMemoryFailureMode.ContinueWithoutMemory
                ? MemoryFailureMode.ContinueWithoutMemory
                : MemoryFailureMode.FailClosed
            : _options.FailureMode;
}
