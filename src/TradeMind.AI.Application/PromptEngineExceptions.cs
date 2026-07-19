namespace TradeMind.AI.Application;

public class PromptEngineException : InvalidOperationException
{
    public PromptEngineException(
        PromptTemplateId templateId,
        PromptTemplateVersion? version,
        string message,
        string? correlationId = null,
        string? variableName = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        TemplateId = templateId;
        Version = version;
        CorrelationId = correlationId;
        VariableName = variableName;
    }

    public PromptTemplateId TemplateId { get; }

    public PromptTemplateVersion? Version { get; }

    public string? CorrelationId { get; }

    public string? VariableName { get; }
}

public sealed class PromptTemplateNotFoundException : PromptEngineException
{
    public PromptTemplateNotFoundException(PromptTemplateId templateId, string? correlationId = null)
        : base(templateId, null, $"Prompt template '{templateId}' was not found.", correlationId)
    {
    }
}

public sealed class PromptTemplateVersionNotFoundException : PromptEngineException
{
    public PromptTemplateVersionNotFoundException(
        PromptTemplateId templateId,
        PromptTemplateVersion version,
        string? correlationId = null)
        : base(templateId, version, $"Prompt template '{templateId}' version '{version}' was not found.", correlationId)
    {
    }
}

public sealed class PromptValidationException : PromptEngineException
{
    public PromptValidationException(
        PromptTemplateId templateId,
        PromptTemplateVersion? version,
        string message,
        string? correlationId = null,
        string? variableName = null)
        : base(templateId, version, message, correlationId, variableName)
    {
    }
}

public sealed class PromptRenderingException : PromptEngineException
{
    public PromptRenderingException(
        PromptTemplateId templateId,
        PromptTemplateVersion? version,
        string message,
        string? correlationId = null,
        string? variableName = null,
        Exception? innerException = null)
        : base(templateId, version, message, correlationId, variableName, innerException)
    {
    }
}
