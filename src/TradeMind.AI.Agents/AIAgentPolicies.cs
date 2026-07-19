using System.Collections.ObjectModel;
using TradeMind.AI.Application;
using TradeMind.AI.Memory;
using TradeMind.AI.Tools;

namespace TradeMind.AI.Agents;

public sealed record AIAgentPromptPolicy
{
    public AIAgentPromptPolicy(
        PromptTemplateId? templateId = null,
        PromptTemplateVersion? templateVersion = null,
        string? staticSystemInstruction = null,
        IReadOnlyDictionary<string, string>? defaultVariables = null,
        IReadOnlyCollection<string>? requiredVariables = null,
        bool allowRequestTemplateOverride = false,
        bool allowRequestVariableOverride = false,
        int? maximumRenderedCharacters = null)
    {
        if (templateId is not null && !string.IsNullOrWhiteSpace(staticSystemInstruction))
        {
            throw new ArgumentException("TemplateId and StaticSystemInstruction cannot both be configured.");
        }

        if (templateVersion is not null && templateId is null)
        {
            throw new ArgumentException("TemplateVersion requires TemplateId.", nameof(templateVersion));
        }

        if (maximumRenderedCharacters is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRenderedCharacters));
        }

        TemplateId = templateId;
        TemplateVersion = templateVersion;
        StaticSystemInstruction = Normalize(staticSystemInstruction);
        DefaultVariables = AIAgentCollections.CopyDictionary(defaultVariables, StringComparer.Ordinal);
        RequiredVariables = AIAgentCollections.CopyStrings(requiredVariables, StringComparer.Ordinal);
        AllowRequestTemplateOverride = allowRequestTemplateOverride;
        AllowRequestVariableOverride = allowRequestVariableOverride;
        MaximumRenderedCharacters = maximumRenderedCharacters;
    }

    public PromptTemplateId? TemplateId { get; }

    public PromptTemplateVersion? TemplateVersion { get; }

    public string? StaticSystemInstruction { get; }

    public IReadOnlyDictionary<string, string> DefaultVariables { get; }

    public IReadOnlyCollection<string> RequiredVariables { get; }

    public bool AllowRequestTemplateOverride { get; }

    public bool AllowRequestVariableOverride { get; }

    public int? MaximumRenderedCharacters { get; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AIAgentMemoryPolicy
{
    public AIAgentMemoryPolicy(
        bool enabled = false,
        bool required = false,
        MemoryWindowOptions? defaultWindowOptions = null,
        bool allowRequestOverride = false,
        bool saveUserMessage = true,
        bool saveAssistantResponse = true,
        bool compactionEnabled = true,
        MemoryFailureMode failureMode = MemoryFailureMode.FailClosed)
    {
        if (required && !enabled)
        {
            throw new ArgumentException("Required memory must be enabled.", nameof(required));
        }

        Enabled = enabled;
        Required = required;
        DefaultWindowOptions = defaultWindowOptions ?? new MemoryWindowOptions();
        AllowRequestOverride = allowRequestOverride;
        SaveUserMessage = saveUserMessage;
        SaveAssistantResponse = saveAssistantResponse;
        CompactionEnabled = compactionEnabled;
        FailureMode = failureMode;
    }

    public bool Enabled { get; }

    public bool Required { get; }

    public MemoryWindowOptions DefaultWindowOptions { get; }

    public bool AllowRequestOverride { get; }

    public bool SaveUserMessage { get; }

    public bool SaveAssistantResponse { get; }

    public bool CompactionEnabled { get; }

    public MemoryFailureMode FailureMode { get; }
}

public sealed record AIAgentKnowledgePolicy
{
    public AIAgentKnowledgePolicy(
        bool enabled = false,
        bool required = false,
        int defaultMaxResults = 5,
        double? minimumScore = null,
        int? maxCharacters = 4000,
        int? maxEstimatedTokens = null,
        IReadOnlyCollection<string>? allowedFilters = null,
        bool allowExplicitQuery = false,
        bool useCurrentUserMessageAsQuery = true,
        bool includeCitations = true,
        KnowledgeFailureMode failureMode = KnowledgeFailureMode.FailClosed)
    {
        if (required && !enabled)
        {
            throw new ArgumentException("Required knowledge must be enabled.", nameof(required));
        }

        if (defaultMaxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultMaxResults));
        }

        if (minimumScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore));
        }

        if (maxCharacters is <= 0 || maxEstimatedTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        }

        Enabled = enabled;
        Required = required;
        DefaultMaxResults = defaultMaxResults;
        MinimumScore = minimumScore;
        MaxCharacters = maxCharacters;
        MaxEstimatedTokens = maxEstimatedTokens;
        AllowedFilters = AIAgentCollections.CopyStrings(allowedFilters, StringComparer.OrdinalIgnoreCase);
        AllowExplicitQuery = allowExplicitQuery;
        UseCurrentUserMessageAsQuery = useCurrentUserMessageAsQuery;
        IncludeCitations = includeCitations;
        FailureMode = failureMode;
    }

    public bool Enabled { get; }

    public bool Required { get; }

    public int DefaultMaxResults { get; }

    public double? MinimumScore { get; }

    public int? MaxCharacters { get; }

    public int? MaxEstimatedTokens { get; }

    public IReadOnlyCollection<string> AllowedFilters { get; }

    public bool AllowExplicitQuery { get; }

    public bool UseCurrentUserMessageAsQuery { get; }

    public bool IncludeCitations { get; }

    public KnowledgeFailureMode FailureMode { get; }
}

public sealed record AIAgentToolPolicy
{
    public AIAgentToolPolicy(
        bool enabled = false,
        IReadOnlyCollection<AIToolId>? allowedToolIds = null,
        IReadOnlyCollection<string>? requiredPermissions = null,
        AIToolSideEffectLevel maximumAllowedSideEffectLevel = AIToolSideEffectLevel.ReadOnly,
        bool allowExplicitInvocation = true,
        IReadOnlyCollection<string>? allowedScenarios = null,
        AIToolFailureMode defaultFailureMode = AIToolFailureMode.FailClosed,
        TimeSpan? maximumTimeout = null,
        bool requireIdempotencyKeyForWrites = true)
    {
        if (maximumTimeout is not null && maximumTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTimeout));
        }

        Enabled = enabled;
        AllowedToolIds = Array.AsReadOnly(
            allowedToolIds?.Distinct().OrderBy(id => id.Value, StringComparer.Ordinal).ToArray() ?? []);
        RequiredPermissions = AIAgentCollections.CopyStrings(requiredPermissions, StringComparer.OrdinalIgnoreCase);
        MaximumAllowedSideEffectLevel = maximumAllowedSideEffectLevel;
        AllowExplicitInvocation = allowExplicitInvocation;
        AllowedScenarios = AIAgentCollections.CopyStrings(allowedScenarios, StringComparer.OrdinalIgnoreCase);
        DefaultFailureMode = defaultFailureMode;
        MaximumTimeout = maximumTimeout;
        RequireIdempotencyKeyForWrites = requireIdempotencyKeyForWrites;
    }

    public bool Enabled { get; }

    public IReadOnlyCollection<AIToolId> AllowedToolIds { get; }

    public IReadOnlyCollection<string> RequiredPermissions { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; }

    public bool AllowExplicitInvocation { get; }

    public IReadOnlyCollection<string> AllowedScenarios { get; }

    public AIToolFailureMode DefaultFailureMode { get; }

    public TimeSpan? MaximumTimeout { get; }

    public bool RequireIdempotencyKeyForWrites { get; }
}

public sealed record AIAgentPolicy
{
    public AIAgentPolicy(
        AIAgentPromptPolicy? prompt = null,
        AIAgentMemoryPolicy? memory = null,
        AIAgentKnowledgePolicy? knowledge = null,
        AIAgentToolPolicy? tool = null,
        TimeSpan? maximumExecutionDuration = null,
        AIAgentFailureMode failureMode = AIAgentFailureMode.FailClosed,
        AIToolSideEffectLevel maximumAllowedSideEffectLevel = AIToolSideEffectLevel.ReadOnly,
        int? maximumContextCharacters = null)
    {
        if (maximumExecutionDuration is not null && maximumExecutionDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExecutionDuration));
        }

        if (maximumContextCharacters is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContextCharacters));
        }

        Prompt = prompt ?? new AIAgentPromptPolicy();
        Memory = memory ?? new AIAgentMemoryPolicy();
        Knowledge = knowledge ?? new AIAgentKnowledgePolicy();
        Tool = tool ?? new AIAgentToolPolicy();
        MaximumExecutionDuration = maximumExecutionDuration;
        FailureMode = failureMode;
        MaximumAllowedSideEffectLevel = maximumAllowedSideEffectLevel;
        MaximumContextCharacters = maximumContextCharacters;

        if (Tool.MaximumAllowedSideEffectLevel > MaximumAllowedSideEffectLevel)
        {
            throw new ArgumentException("Tool side effect policy cannot exceed the global agent side effect policy.", nameof(tool));
        }
    }

    public AIAgentPromptPolicy Prompt { get; }

    public AIAgentMemoryPolicy Memory { get; }

    public AIAgentKnowledgePolicy Knowledge { get; }

    public AIAgentToolPolicy Tool { get; }

    public TimeSpan? MaximumExecutionDuration { get; }

    public AIAgentFailureMode FailureMode { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; }

    public int? MaximumContextCharacters { get; }
}

internal static class AIAgentCollections
{
    public static IReadOnlyCollection<string> CopyStrings(
        IEnumerable<string>? values,
        StringComparer comparer) =>
        Array.AsReadOnly(values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(comparer)
            .OrderBy(value => value, comparer)
            .ToArray() ?? []);

    public static IReadOnlyDictionary<string, string> CopyDictionary(
        IReadOnlyDictionary<string, string>? values,
        StringComparer comparer,
        bool filterSensitiveKeys = false)
    {
        var output = new Dictionary<string, string>(comparer);
        if (values is not null)
        {
            foreach (var item in values)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Key);
                if (!filterSensitiveKeys || !IsSensitiveKey(item.Key))
                {
                    output[item.Key] = item.Value;
                }
            }
        }

        return new ReadOnlyDictionary<string, string>(output);
    }

    public static bool IsSensitiveKey(string key) =>
        key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("credential", StringComparison.OrdinalIgnoreCase)
        || key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("api-key", StringComparison.OrdinalIgnoreCase)
        || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
        || key.Contains("private-key", StringComparison.OrdinalIgnoreCase);
}
