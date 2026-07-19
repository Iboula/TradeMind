using System.Collections.ObjectModel;
using System.Text.Json;

namespace TradeMind.AI.Tools;

public enum AIToolParameterType
{
    String,
    Integer,
    Decimal,
    Boolean,
    DateTime,
    Json
}

public enum AIToolSideEffectLevel
{
    None,
    ReadOnly,
    ReversibleWrite,
    IrreversibleWrite,
    ExternalAction
}

public enum AIToolAvailability
{
    Enabled,
    Disabled,
    DevelopmentOnly
}

public enum AIToolFailureMode
{
    FailClosed,
    ContinueWithoutTool
}

public enum AIToolOutputKind
{
    Json,
    Text
}

public sealed class AIToolParameterDefinition
{
    public AIToolParameterDefinition(
        string name,
        AIToolParameterType type,
        bool required,
        string description,
        JsonElement? defaultValue = null,
        int? maxLength = null,
        decimal? minimum = null,
        decimal? maximum = null,
        IReadOnlyList<JsonElement>? allowedValues = null,
        bool isSensitive = false,
        bool isNullable = false,
        string? jsonSchema = null)
    {
        ValidateName(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (maxLength is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength must be greater than zero.");
        }

        if (maxLength is not null && type != AIToolParameterType.String)
        {
            throw new ArgumentException("MaxLength can only be used with String parameters.", nameof(maxLength));
        }

        if ((minimum is not null || maximum is not null)
            && type is not AIToolParameterType.Integer and not AIToolParameterType.Decimal)
        {
            throw new ArgumentException("Minimum and Maximum can only be used with numeric parameters.");
        }

        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum cannot be greater than Maximum.");
        }

        ValidateJsonSchema(jsonSchema);

        Name = name;
        Type = type;
        Required = required;
        Description = description;
        DefaultValue = Clone(defaultValue);
        MaxLength = maxLength;
        Minimum = minimum;
        Maximum = maximum;
        AllowedValues = CopyJsonValues(allowedValues);
        IsSensitive = isSensitive;
        IsNullable = isNullable;
        JsonSchema = string.IsNullOrWhiteSpace(jsonSchema) ? null : jsonSchema;

        ValidateConfiguredValue(DefaultValue, nameof(defaultValue));
        foreach (var allowedValue in AllowedValues)
        {
            ValidateConfiguredValue(allowedValue, nameof(allowedValues));
        }
    }

    public string Name { get; }

    public AIToolParameterType Type { get; }

    public bool Required { get; }

    public string Description { get; }

    public JsonElement? DefaultValue { get; }

    public int? MaxLength { get; }

    public decimal? Minimum { get; }

    public decimal? Maximum { get; }

    public IReadOnlyList<JsonElement> AllowedValues { get; }

    public bool IsSensitive { get; }

    public bool IsNullable { get; }

    public string? JsonSchema { get; }

    private void ValidateConfiguredValue(JsonElement? value, string parameterName)
    {
        if (value is null)
        {
            return;
        }

        if (value.Value.ValueKind == JsonValueKind.Null)
        {
            if (!IsNullable)
            {
                throw new ArgumentException("A null configured value requires IsNullable.", parameterName);
            }

            return;
        }

        try
        {
            _ = AIToolValueConverter.Convert(this, value.Value);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Configured value is incompatible with the parameter type.", parameterName, exception);
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 64 || name[0] is < 'a' or > 'z' || name.Any(character =>
                character is not (>= 'a' and <= 'z')
                and not (>= 'A' and <= 'Z')
                and not (>= '0' and <= '9')))
        {
            throw new ArgumentException(
                "Parameter name must start with a lowercase ASCII letter and contain only ASCII letters or digits.",
                nameof(name));
        }
    }

    private static void ValidateJsonSchema(string? jsonSchema)
    {
        if (string.IsNullOrWhiteSpace(jsonSchema))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonSchema);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("JsonSchema must be a JSON object.", nameof(jsonSchema));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("JsonSchema must contain valid JSON.", nameof(jsonSchema), exception);
        }
    }

    private static JsonElement? Clone(JsonElement? value) => value?.Clone();

    private static IReadOnlyList<JsonElement> CopyJsonValues(IReadOnlyList<JsonElement>? values) =>
        Array.AsReadOnly(values?.Select(value => value.Clone()).ToArray() ?? []);
}

public sealed class AIToolDefinition
{
    public AIToolDefinition(
        AIToolId id,
        string name,
        string description,
        string version,
        IReadOnlyList<AIToolParameterDefinition>? parameters = null,
        IReadOnlyCollection<string>? requiredPermissions = null,
        AIToolSideEffectLevel sideEffectLevel = AIToolSideEffectLevel.None,
        bool isIdempotent = true,
        TimeSpan? defaultTimeout = null,
        AIToolAvailability availability = AIToolAvailability.Enabled,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyCollection<string>? allowedScenarios = null,
        IReadOnlyCollection<string>? allowedTenantIds = null,
        IReadOnlyCollection<string>? allowedUserIds = null,
        IReadOnlyCollection<string>? allowedAgentIds = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (defaultTimeout is not null && defaultTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTimeout), "Default timeout must be positive.");
        }

        if (!Enum.IsDefined(sideEffectLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(sideEffectLevel));
        }

        if (!Enum.IsDefined(availability))
        {
            throw new ArgumentOutOfRangeException(nameof(availability));
        }

        var copiedParameters = parameters?.ToArray() ?? [];
        if (copiedParameters.Any(parameter => parameter is null))
        {
            throw new ArgumentException("Tool parameters cannot contain null values.", nameof(parameters));
        }

        if (copiedParameters.GroupBy(parameter => parameter.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Tool parameter names must be unique.", nameof(parameters));
        }

        Id = id;
        Name = name;
        Description = description;
        Version = version;
        Parameters = Array.AsReadOnly(copiedParameters);
        RequiredPermissions = CopyStrings(requiredPermissions, StringComparer.OrdinalIgnoreCase);
        SideEffectLevel = sideEffectLevel;
        IsIdempotent = isIdempotent;
        DefaultTimeout = defaultTimeout;
        Availability = availability;
        Tags = CopyStrings(tags, StringComparer.OrdinalIgnoreCase);
        Metadata = CopyMetadata(metadata);
        AllowedScenarios = CopyStrings(allowedScenarios, StringComparer.OrdinalIgnoreCase);
        AllowedTenantIds = CopyStrings(allowedTenantIds, StringComparer.Ordinal);
        AllowedUserIds = CopyStrings(allowedUserIds, StringComparer.Ordinal);
        AllowedAgentIds = CopyStrings(allowedAgentIds, StringComparer.Ordinal);
    }

    public AIToolId Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string Version { get; }

    public IReadOnlyList<AIToolParameterDefinition> Parameters { get; }

    public IReadOnlyCollection<string> RequiredPermissions { get; }

    public AIToolSideEffectLevel SideEffectLevel { get; }

    public bool IsIdempotent { get; }

    public TimeSpan? DefaultTimeout { get; }

    public AIToolAvailability Availability { get; }

    public IReadOnlyCollection<string> Tags { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public IReadOnlyCollection<string> AllowedScenarios { get; }

    public IReadOnlyCollection<string> AllowedTenantIds { get; }

    public IReadOnlyCollection<string> AllowedUserIds { get; }

    public IReadOnlyCollection<string> AllowedAgentIds { get; }

    private static IReadOnlyCollection<string> CopyStrings(
        IReadOnlyCollection<string>? values,
        StringComparer comparer)
    {
        if (values is null)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        var output = values.Select(value =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return value;
        }).Distinct(comparer).OrderBy(value => value, comparer).ToArray();

        return Array.AsReadOnly(output);
    }

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (metadata is not null)
        {
            foreach (var item in metadata)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Key);
                ArgumentNullException.ThrowIfNull(item.Value);
                if (IsSensitiveMetadataKey(item.Key))
                {
                    throw new ArgumentException("Tool definition metadata cannot contain sensitive keys.", nameof(metadata));
                }

                output[item.Key] = item.Value;
            }
        }

        return new ReadOnlyDictionary<string, string>(output);
    }

    private static bool IsSensitiveMetadataKey(string key) =>
        key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("credential", StringComparison.OrdinalIgnoreCase)
        || key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("api-key", StringComparison.OrdinalIgnoreCase)
        || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
        || key.Contains("private-key", StringComparison.OrdinalIgnoreCase);
}

public sealed record AIToolDiscoveryContext
{
    public AIToolDiscoveryContext(
        IReadOnlyCollection<string>? permissions = null,
        string? tenantId = null,
        string? userId = null,
        string? agentId = null,
        string? scenario = null,
        AIToolSideEffectLevel maximumAllowedSideEffectLevel = AIToolSideEffectLevel.ReadOnly,
        IReadOnlyCollection<string>? tags = null)
    {
        Permissions = CopyStrings(permissions, StringComparer.OrdinalIgnoreCase);
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        AgentId = Normalize(agentId);
        Scenario = Normalize(scenario);
        MaximumAllowedSideEffectLevel = maximumAllowedSideEffectLevel;
        Tags = CopyStrings(tags, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Permissions { get; }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string? AgentId { get; }

    public string? Scenario { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; }

    public IReadOnlyCollection<string> Tags { get; }

    private static IReadOnlyCollection<string> CopyStrings(
        IReadOnlyCollection<string>? values,
        StringComparer comparer) =>
        Array.AsReadOnly(values?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(comparer).ToArray() ?? []);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record AIToolAuthorizationContext
{
    public AIToolAuthorizationContext(
        string? tenantId,
        string? userId,
        string? agentId,
        IReadOnlyCollection<string>? permissions,
        string scenario,
        AIToolSideEffectLevel maximumAllowedSideEffectLevel = AIToolSideEffectLevel.ReadOnly,
        string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        TenantId = Normalize(tenantId);
        UserId = Normalize(userId);
        AgentId = Normalize(agentId);
        Permissions = Array.AsReadOnly(
            permissions?.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? []);
        Scenario = scenario;
        MaximumAllowedSideEffectLevel = maximumAllowedSideEffectLevel;
        CorrelationId = Normalize(correlationId);
    }

    public string? TenantId { get; }

    public string? UserId { get; }

    public string? AgentId { get; }

    public IReadOnlyCollection<string> Permissions { get; }

    public string Scenario { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; }

    public string? CorrelationId { get; }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record AIToolInvocationOptions
{
    public static AIToolInvocationOptions Disabled { get; } = new();

    public AIToolInvocationOptions(
        bool enabled = false,
        AIToolId? toolId = null,
        IReadOnlyDictionary<string, JsonElement>? arguments = null,
        IReadOnlyCollection<string>? permissions = null,
        AIToolSideEffectLevel maximumAllowedSideEffectLevel = AIToolSideEffectLevel.ReadOnly,
        TimeSpan? timeoutOverride = null,
        string? idempotencyKey = null,
        AIToolFailureMode failureMode = AIToolFailureMode.FailClosed)
    {
        Enabled = enabled;
        ToolId = toolId;
        Arguments = CopyJsonDictionary(arguments);
        Permissions = Array.AsReadOnly(
            permissions?.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? []);
        MaximumAllowedSideEffectLevel = maximumAllowedSideEffectLevel;
        TimeoutOverride = timeoutOverride;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey;
        FailureMode = failureMode;
    }

    public bool Enabled { get; }

    public AIToolId? ToolId { get; }

    public IReadOnlyDictionary<string, JsonElement> Arguments { get; }

    public IReadOnlyCollection<string> Permissions { get; }

    public AIToolSideEffectLevel MaximumAllowedSideEffectLevel { get; }

    public TimeSpan? TimeoutOverride { get; }

    public string? IdempotencyKey { get; }

    public AIToolFailureMode FailureMode { get; }

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
}
