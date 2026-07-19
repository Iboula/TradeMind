namespace TradeMind.AI.Agents;

public abstract class AIAgentException : Exception
{
    protected AIAgentException(
        AIAgentId agentId,
        string errorCode,
        string message,
        AIAgentVersion? version = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ErrorCode = errorCode;
        Version = version;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
    }

    public AIAgentId AgentId { get; }

    public AIAgentVersion? Version { get; }

    public string ErrorCode { get; }

    public string? CorrelationId { get; }
}

public sealed class AIAgentNotFoundException : AIAgentException
{
    public AIAgentNotFoundException(AIAgentId agentId, string? correlationId = null)
        : base(agentId, "AI_AGENT_NOT_FOUND", "The requested AI agent was not found.", correlationId: correlationId)
    {
    }
}

public sealed class AIAgentVersionNotFoundException : AIAgentException
{
    public AIAgentVersionNotFoundException(
        AIAgentId agentId,
        AIAgentVersion? version = null,
        string? correlationId = null)
        : base(agentId, "AI_AGENT_VERSION_NOT_FOUND", "The requested AI agent version was not found.", version, correlationId)
    {
    }
}

public sealed class AIAgentUnavailableException : AIAgentException
{
    public AIAgentUnavailableException(AIAgentId agentId, AIAgentVersion version, string? correlationId = null)
        : base(agentId, "AI_AGENT_UNAVAILABLE", "The requested AI agent is unavailable.", version, correlationId)
    {
    }
}

public sealed class AIAgentAuthorizationException : AIAgentException
{
    public AIAgentAuthorizationException(
        AIAgentId agentId,
        AIAgentVersion version,
        string message,
        string? correlationId = null)
        : base(agentId, "AI_AGENT_AUTHORIZATION_DENIED", message, version, correlationId)
    {
    }
}

public sealed class AIAgentValidationException : AIAgentException
{
    public AIAgentValidationException(
        AIAgentId agentId,
        string message,
        AIAgentVersion? version = null,
        string? correlationId = null,
        string errorCode = "AI_AGENT_VALIDATION_FAILED")
        : base(agentId, errorCode, message, version, correlationId)
    {
    }
}

public sealed class AIAgentPolicyViolationException : AIAgentException
{
    public AIAgentPolicyViolationException(
        AIAgentId agentId,
        AIAgentVersion version,
        string message,
        string? correlationId = null)
        : base(agentId, "AI_AGENT_POLICY_VIOLATION", message, version, correlationId)
    {
    }
}

public sealed class AIAgentExecutionException : AIAgentException
{
    public AIAgentExecutionException(
        AIAgentId agentId,
        AIAgentVersion version,
        string? correlationId,
        Exception innerException)
        : base(
            agentId,
            "AI_AGENT_EXECUTION_FAILED",
            "The AI agent execution failed.",
            version,
            correlationId,
            innerException)
    {
    }
}

public sealed class AIAgentTimeoutException : AIAgentException
{
    public AIAgentTimeoutException(
        AIAgentId agentId,
        AIAgentVersion version,
        string? correlationId,
        Exception? innerException = null)
        : base(
            agentId,
            "AI_AGENT_TIMEOUT",
            "The AI agent execution exceeded its allowed duration.",
            version,
            correlationId,
            innerException)
    {
    }
}
