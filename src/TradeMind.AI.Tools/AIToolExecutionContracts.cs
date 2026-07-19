using System.Collections.ObjectModel;
using System.Text.Json;

namespace TradeMind.AI.Tools;

public sealed record AIToolExecutionRequest
{
    public AIToolExecutionRequest(
        AIToolId toolId,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        string sessionId,
        string correlationId,
        string? conversationId,
        string? tenantId,
        string? userId,
        string? agentId,
        string scenario,
        DateTimeOffset requestedAtUtc,
        TimeSpan? timeoutOverride = null,
        string? idempotencyKey = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(toolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        if (requestedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Tool request date must be UTC.", nameof(requestedAtUtc));
        }

        if (timeoutOverride is not null && timeoutOverride <= TimeSpan.Zero)
        {
            throw new AIToolValidationException(
                toolId,
                "TimeoutOverride must be positive when provided.",
                correlationId,
                nameof(timeoutOverride));
        }

        ValidateIdempotencyKey(toolId, idempotencyKey, correlationId);

        ToolId = toolId;
        Arguments = CopyJsonDictionary(arguments);
        SessionId = sessionId;
        CorrelationId = correlationId;
        ConversationId = Normalize(conversationId);
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        AgentId = Normalize(agentId);
        Scenario = scenario;
        RequestedAtUtc = requestedAtUtc;
        TimeoutOverride = timeoutOverride;
        IdempotencyKey = Normalize(idempotencyKey);
        Metadata = CopyMetadata(metadata);
    }

    public AIToolId ToolId { get; }

    public IReadOnlyDictionary<string, JsonElement> Arguments { get; }

    public string SessionId { get; }

    public string CorrelationId { get; }

    public string? ConversationId { get; }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string? AgentId { get; }

    public string Scenario { get; }

    public DateTimeOffset RequestedAtUtc { get; }

    public TimeSpan? TimeoutOverride { get; }

    public string? IdempotencyKey { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static void ValidateIdempotencyKey(AIToolId toolId, string? value, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Length > 128 || value.Any(character =>
                character is not (>= 'a' and <= 'z')
                and not (>= 'A' and <= 'Z')
                and not (>= '0' and <= '9')
                and not '-' and not '_' and not '.' and not ':'))
        {
            throw new AIToolValidationException(
                toolId,
                "IdempotencyKey has an invalid format.",
                correlationId,
                nameof(IdempotencyKey));
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> CopyJsonDictionary(
        IReadOnlyDictionary<string, JsonElement>? values)
    {
        var output = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (values is not null)
        {
            foreach (var item in values)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Key);
                output[item.Key] = item.Value.Clone();
            }
        }

        return new ReadOnlyDictionary<string, JsonElement>(output);
    }

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        IReadOnlyDictionary<string, string>? values) =>
        new ReadOnlyDictionary<string, string>(values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class AIToolArguments
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    internal AIToolArguments(IReadOnlyDictionary<string, object?> values)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in values)
        {
            output[item.Key] = item.Value is JsonElement json ? json.Clone() : item.Value;
        }

        _values = new ReadOnlyDictionary<string, object?>(output);
    }

    public IReadOnlyDictionary<string, object?> Values => _values;

    public string GetRequiredString(string name) => GetRequired<string>(name);

    public string? GetOptionalString(string name) => GetOptional<string>(name);

    public long GetRequiredInteger(string name) => GetRequired<long>(name);

    public decimal GetRequiredDecimal(string name) => GetRequired<decimal>(name);

    public bool GetRequiredBoolean(string name) => GetRequired<bool>(name);

    public DateTimeOffset GetRequiredDateTime(string name) => GetRequired<DateTimeOffset>(name);

    public JsonElement GetRequiredJson(string name) => GetRequired<JsonElement>(name).Clone();

    private T GetRequired<T>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_values.TryGetValue(name, out var value) || value is null)
        {
            throw new KeyNotFoundException($"Required tool argument '{name}' is unavailable.");
        }

        return value is T typed
            ? typed
            : throw new InvalidOperationException($"Tool argument '{name}' is not a {typeof(T).Name}.");
    }

    private T? GetOptional<T>(string name)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_values.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value is T typed
            ? typed
            : throw new InvalidOperationException($"Tool argument '{name}' is not a {typeof(T).Name}.");
    }
}

public sealed record AIToolExecutionContext
{
    public AIToolExecutionContext(
        AIToolExecutionRequest request,
        AIToolArguments arguments,
        AIToolAuthorizationContext authorization,
        TimeProvider timeProvider,
        IReadOnlyDictionary<string, string>? internalMetadata = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        TimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        InternalMetadata = new ReadOnlyDictionary<string, string>(internalMetadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(internalMetadata, StringComparer.OrdinalIgnoreCase));
    }

    public AIToolExecutionRequest Request { get; }

    public AIToolArguments Arguments { get; }

    public AIToolAuthorizationContext Authorization { get; }

    public TimeProvider TimeProvider { get; }

    public IReadOnlyDictionary<string, string> InternalMetadata { get; }

    public string GetRequiredString(string name) => Arguments.GetRequiredString(name);

    public string? GetOptionalString(string name) => Arguments.GetOptionalString(name);

    public long GetRequiredInteger(string name) => Arguments.GetRequiredInteger(name);

    public decimal GetRequiredDecimal(string name) => Arguments.GetRequiredDecimal(name);

    public bool GetRequiredBoolean(string name) => Arguments.GetRequiredBoolean(name);

    public DateTimeOffset GetRequiredDateTime(string name) => Arguments.GetRequiredDateTime(name);

    public JsonElement GetRequiredJson(string name) => Arguments.GetRequiredJson(name);
}

public sealed record AIToolExecutionMetrics
{
    public AIToolExecutionMetrics(
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        bool timedOut = false)
    {
        if (startedAtUtc.Offset != TimeSpan.Zero || completedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Tool metric dates must be UTC.");
        }

        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Tool completion date cannot precede its start date.");
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Duration = duration;
        TimedOut = timedOut;
    }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public TimeSpan Duration { get; }

    public bool TimedOut { get; }
}

public sealed record AIToolError
{
    public AIToolError(
        string code,
        string message,
        string? parameterName = null,
        string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        ParameterName = string.IsNullOrWhiteSpace(parameterName) ? null : parameterName;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
    }

    public string Code { get; }

    public string Message { get; }

    public string? ParameterName { get; }

    public string? CorrelationId { get; }
}

public sealed record AIToolExecutionResult
{
    private AIToolExecutionResult(
        AIToolId toolId,
        bool success,
        JsonElement? output,
        AIToolOutputKind outputKind,
        string contentType,
        AIToolExecutionMetrics metrics,
        AIToolError? error,
        IReadOnlyDictionary<string, string>? metadata,
        bool? isRetryable,
        string? idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(toolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(metrics);

        if (success && error is not null || !success && error is null)
        {
            throw new ArgumentException("Tool result success and error values are inconsistent.", nameof(error));
        }

        ToolId = toolId;
        Success = success;
        Output = output?.Clone();
        OutputKind = outputKind;
        ContentType = contentType;
        Metrics = metrics;
        Error = error;
        Metadata = CopyPublicMetadata(metadata);
        IsRetryable = isRetryable;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey;
    }

    public AIToolId ToolId { get; }

    public bool Success { get; }

    public JsonElement? Output { get; }

    public AIToolOutputKind OutputKind { get; }

    public string ContentType { get; }

    public DateTimeOffset StartedAtUtc => Metrics.StartedAtUtc;

    public DateTimeOffset CompletedAtUtc => Metrics.CompletedAtUtc;

    public TimeSpan Duration => Metrics.Duration;

    public AIToolExecutionMetrics Metrics { get; }

    public AIToolError? Error { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public bool? IsRetryable { get; }

    public string? IdempotencyKey { get; }

    public static AIToolExecutionResult Succeeded(
        AIToolId toolId,
        JsonElement output,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        AIToolOutputKind outputKind = AIToolOutputKind.Json,
        string contentType = "application/json",
        IReadOnlyDictionary<string, string>? metadata = null,
        string? idempotencyKey = null,
        TimeSpan? duration = null) =>
        new(
            toolId,
            true,
            output,
            outputKind,
            contentType,
            new AIToolExecutionMetrics(startedAtUtc, completedAtUtc, duration ?? completedAtUtc - startedAtUtc),
            null,
            metadata,
            null,
            idempotencyKey);

    public static AIToolExecutionResult Failed(
        AIToolId toolId,
        AIToolError error,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool? isRetryable = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? idempotencyKey = null,
        TimeSpan? duration = null) =>
        new(
            toolId,
            false,
            null,
            AIToolOutputKind.Json,
            "application/json",
            new AIToolExecutionMetrics(startedAtUtc, completedAtUtc, duration ?? completedAtUtc - startedAtUtc),
            error ?? throw new ArgumentNullException(nameof(error)),
            metadata,
            isRetryable,
            idempotencyKey);

    internal AIToolExecutionResult Normalize(
        AIToolId toolId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        string? idempotencyKey) =>
        new(
            toolId,
            Success,
            Output,
            OutputKind,
            ContentType,
            new AIToolExecutionMetrics(startedAtUtc, completedAtUtc, duration),
            Error,
            Metadata,
            IsRetryable,
            idempotencyKey);

    private static IReadOnlyDictionary<string, string> CopyPublicMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (metadata is not null)
        {
            foreach (var item in metadata.Where(item => !IsSensitiveKey(item.Key)))
            {
                output[item.Key] = item.Value;
            }
        }

        return new ReadOnlyDictionary<string, string>(output);
    }

    private static bool IsSensitiveKey(string key) =>
        key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("credential", StringComparison.OrdinalIgnoreCase)
        || key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("api-key", StringComparison.OrdinalIgnoreCase)
        || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
        || key.Contains("private-key", StringComparison.OrdinalIgnoreCase);
}
