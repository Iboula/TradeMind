namespace TradeMind.AI.Tools;

public sealed class AIToolNotFoundException : KeyNotFoundException
{
    public AIToolNotFoundException(AIToolId toolId, string? correlationId = null)
        : base($"Tool '{toolId}' was not found.")
    {
        ToolId = toolId;
        CorrelationId = correlationId;
    }

    public AIToolId ToolId { get; }

    public string ErrorCode => "AI_TOOL_NOT_FOUND";

    public string? CorrelationId { get; }
}

public sealed class AIToolUnavailableException : InvalidOperationException
{
    public AIToolUnavailableException(AIToolId toolId, string message, string? correlationId = null)
        : base(message)
    {
        ToolId = toolId;
        CorrelationId = correlationId;
    }

    public AIToolId ToolId { get; }

    public string ErrorCode => "AI_TOOL_UNAVAILABLE";

    public string? CorrelationId { get; }
}

public sealed class AIToolAuthorizationException : UnauthorizedAccessException
{
    public AIToolAuthorizationException(AIToolId toolId, string message, string? correlationId = null)
        : base(message)
    {
        ToolId = toolId;
        CorrelationId = correlationId;
    }

    public AIToolId ToolId { get; }

    public string ErrorCode => "AI_TOOL_AUTHORIZATION_DENIED";

    public string? CorrelationId { get; }
}

public sealed class AIToolValidationException : ArgumentException
{
    public AIToolValidationException(
        AIToolId toolId,
        string message,
        string? correlationId = null,
        string? parameterName = null,
        Exception? innerException = null)
        : base(message, parameterName, innerException)
    {
        ToolId = toolId;
        CorrelationId = correlationId;
        SafeParameterName = string.IsNullOrWhiteSpace(parameterName) ? null : parameterName;
    }

    public AIToolId ToolId { get; }

    public string ErrorCode => "AI_TOOL_VALIDATION_FAILED";

    public string? CorrelationId { get; }

    public string? SafeParameterName { get; }
}

public class AIToolExecutionException : Exception
{
    public AIToolExecutionException(
        AIToolId toolId,
        string message,
        string? correlationId = null,
        Exception? innerException = null,
        AIToolExecutionMetrics? metrics = null)
        : this(toolId, "AI_TOOL_EXECUTION_FAILED", message, correlationId, innerException, metrics)
    {
    }

    protected AIToolExecutionException(
        AIToolId toolId,
        string errorCode,
        string message,
        string? correlationId,
        Exception? innerException,
        AIToolExecutionMetrics? metrics)
        : base(message, innerException)
    {
        ToolId = toolId;
        ErrorCode = errorCode;
        CorrelationId = correlationId;
        Metrics = metrics;
    }

    public AIToolId ToolId { get; }

    public string ErrorCode { get; }

    public string? CorrelationId { get; }

    public AIToolExecutionMetrics? Metrics { get; }
}

public sealed class AIToolTimeoutException : AIToolExecutionException
{
    public AIToolTimeoutException(
        AIToolId toolId,
        TimeSpan timeout,
        string? correlationId,
        Exception? innerException,
        AIToolExecutionMetrics metrics)
        : base(
            toolId,
            "AI_TOOL_TIMEOUT",
            "Tool execution exceeded its allowed timeout.",
            correlationId,
            innerException,
            metrics)
    {
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}
