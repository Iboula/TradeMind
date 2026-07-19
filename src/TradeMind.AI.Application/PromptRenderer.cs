using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TradeMind.AI.Application;

public sealed partial class PromptRenderer : IPromptRenderer
{
    private readonly IPromptTemplateRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PromptRenderer> _logger;

    public PromptRenderer(
        IPromptTemplateRegistry registry,
        TimeProvider timeProvider,
        ILogger<PromptRenderer> logger)
    {
        _registry = registry;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PromptRenderResult> RenderAsync(
        PromptRenderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startTimestamp = _timeProvider.GetTimestamp();

        _logger.LogInformation(
            "Prompt rendering started for template {TemplateId}, requested version {Version}, scenario {Scenario}, correlation id {CorrelationId}, and {VariableCount} variables",
            request.TemplateId.Value,
            request.Version?.ToString(),
            request.Scenario,
            request.CorrelationId,
            request.Variables.Count);

        var template = await _registry
            .GetAsync(request.TemplateId, request.Version, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Prompt template {TemplateId} version {Version} selected for scenario {Scenario} and correlation id {CorrelationId}",
            template.Id.Value,
            template.Version.ToString(),
            template.Scenario,
            request.CorrelationId);

        try
        {
            var culture = string.IsNullOrWhiteSpace(request.Culture)
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(request.Culture);

            var values = ValidateAndFormatVariables(template, request, culture);
            var renderedMessages = RenderMessages(template, values, request.CorrelationId);
            var sensitiveNames = template.Variables
                .Where(variable => variable.IsSensitive)
                .Select(variable => variable.Name)
                .Where(values.ContainsKey)
                .ToArray();
            var safeVariables = values
                .Where(item => !sensitiveNames.Contains(item.Key, StringComparer.Ordinal))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

            var result = new PromptRenderResult(
                template.Id,
                template.Version,
                template.Scenario,
                renderedMessages,
                safeVariables,
                sensitiveNames,
                _timeProvider.GetUtcNow(),
                request.CorrelationId);

            _logger.LogInformation(
                "Prompt rendering completed for template {TemplateId}, version {Version}, scenario {Scenario}, correlation id {CorrelationId}, {MessageCount} messages, {VariableCount} variables, and duration {DurationMilliseconds} ms",
                template.Id.Value,
                template.Version.ToString(),
                template.Scenario,
                request.CorrelationId,
                result.Messages.Count,
                result.VariablesUsed.Count,
                _timeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds);

            return result;
        }
        catch (PromptEngineException exception)
        {
            _logger.LogWarning(
                exception,
                "Prompt rendering validation failed for template {TemplateId}, version {Version}, scenario {Scenario}, correlation id {CorrelationId}, and variable {VariableName}",
                request.TemplateId.Value,
                request.Version?.ToString(),
                request.Scenario,
                request.CorrelationId,
                exception.VariableName);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new PromptRenderingException(
                request.TemplateId,
                request.Version,
                "Prompt rendering failed.",
                request.CorrelationId,
                innerException: exception);
        }
    }

    private static IReadOnlyDictionary<string, string> ValidateAndFormatVariables(
        PromptTemplate template,
        PromptRenderRequest request,
        CultureInfo culture)
    {
        var definitions = template.Variables.ToDictionary(variable => variable.Name, StringComparer.Ordinal);
        foreach (var variableName in request.Variables.Keys)
        {
            if (!definitions.ContainsKey(variableName))
            {
                throw new PromptValidationException(
                    template.Id,
                    template.Version,
                    $"Prompt variable '{variableName}' is not declared.",
                    request.CorrelationId,
                    variableName);
            }
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in template.Variables)
        {
            var hasProvidedValue = request.Variables.TryGetValue(definition.Name, out var providedValue);
            var rawValue = hasProvidedValue ? providedValue : definition.DefaultValue;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (definition.Required)
                {
                    throw new PromptValidationException(
                        template.Id,
                        template.Version,
                        $"Prompt variable '{definition.Name}' is required.",
                        request.CorrelationId,
                        definition.IsSensitive ? null : definition.Name);
                }

                continue;
            }

            if (definition.MaxLength is not null && rawValue.Length > definition.MaxLength.Value)
            {
                throw new PromptValidationException(
                    template.Id,
                    template.Version,
                    $"Prompt variable '{definition.Name}' exceeds the configured maximum length.",
                    request.CorrelationId,
                    definition.IsSensitive ? null : definition.Name);
            }

            values[definition.Name] = FormatValue(template, request, definition, rawValue, culture);
        }

        return values;
    }

    private static string FormatValue(
        PromptTemplate template,
        PromptRenderRequest request,
        PromptVariableDefinition definition,
        string rawValue,
        CultureInfo culture)
    {
        try
        {
            return definition.Type switch
            {
                PromptVariableType.String => rawValue,
                PromptVariableType.Integer => int.Parse(rawValue, NumberStyles.Integer, culture).ToString(CultureInfo.InvariantCulture),
                PromptVariableType.Decimal => decimal.Parse(rawValue, NumberStyles.Number, culture).ToString(CultureInfo.InvariantCulture),
                PromptVariableType.Boolean => bool.Parse(rawValue).ToString(CultureInfo.InvariantCulture),
                PromptVariableType.DateTime => DateTimeOffset.Parse(rawValue, culture).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                PromptVariableType.Json => ValidateJson(rawValue),
                _ => throw new ArgumentOutOfRangeException(nameof(definition), "Unsupported prompt variable type.")
            };
        }
        catch (Exception exception) when (exception is not PromptEngineException)
        {
            throw new PromptValidationException(
                template.Id,
                template.Version,
                $"Prompt variable '{definition.Name}' is not compatible with type {definition.Type}.",
                request.CorrelationId,
                definition.IsSensitive ? null : definition.Name);
        }
    }

    private static string ValidateJson(string rawValue)
    {
        using var _ = JsonDocument.Parse(rawValue);
        return rawValue;
    }

    private static IReadOnlyList<PromptRenderedMessage> RenderMessages(
        PromptTemplate template,
        IReadOnlyDictionary<string, string> values,
        string? correlationId)
    {
        var messages = new List<PromptRenderedMessage>();

        foreach (var message in template.Messages.OrderBy(message => message.Order))
        {
            var content = VariableRegex().Replace(message.ContentTemplate, match =>
            {
                var variableName = match.Groups["name"].Value;
                if (!values.TryGetValue(variableName, out var value))
                {
                    throw new PromptValidationException(
                        template.Id,
                        template.Version,
                        $"Prompt variable '{variableName}' was not resolved.",
                        correlationId,
                        variableName);
                }

                return value;
            });

            if (VariableRegex().IsMatch(content))
            {
                throw new PromptRenderingException(
                    template.Id,
                    template.Version,
                    "Rendered prompt contains unresolved placeholders.",
                    correlationId);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                messages.Add(new PromptRenderedMessage(message.Role, content));
            }
        }

        if (messages.All(message => message.Role != PromptMessageRole.User))
        {
            throw new PromptRenderingException(
                template.Id,
                template.Version,
                "Rendered prompt must contain at least one user message.",
                correlationId);
        }

        return messages;
    }

    [GeneratedRegex("\\{\\{(?<name>[A-Za-z][A-Za-z0-9_]*)\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariableRegex();
}
