namespace TradeMind.Observability.Abstractions;

public sealed record TelemetryContext
{
    public TelemetryContext(
        string correlationId,
        string? traceId,
        string? spanId,
        string? executionSessionId,
        string? organizationId,
        string? tenantId,
        string? actorId,
        string? actorType,
        string? apiKeyId,
        string? requestId,
        string? endpoint,
        string? httpMethod,
        string? module,
        string? operation,
        string coreVersion,
        string apiVersion,
        string environment,
        string serviceName,
        string serviceVersion)
    {
        ValidateRequired(correlationId, nameof(correlationId), 128);
        ValidateRequired(coreVersion, nameof(coreVersion), 64);
        ValidateRequired(apiVersion, nameof(apiVersion), 32);
        ValidateRequired(environment, nameof(environment), 64);
        ValidateRequired(serviceName, nameof(serviceName), 128);
        ValidateRequired(serviceVersion, nameof(serviceVersion), 64);
        ValidateOptional(traceId, nameof(traceId));
        ValidateOptional(spanId, nameof(spanId));
        ValidateOptional(executionSessionId, nameof(executionSessionId));
        ValidateOptional(organizationId, nameof(organizationId));
        ValidateOptional(tenantId, nameof(tenantId));
        ValidateOptional(actorId, nameof(actorId));
        ValidateOptional(actorType, nameof(actorType));
        ValidateOptional(apiKeyId, nameof(apiKeyId));
        ValidateOptional(requestId, nameof(requestId));
        ValidateOptional(endpoint, nameof(endpoint));
        ValidateOptional(httpMethod, nameof(httpMethod));
        ValidateOptional(module, nameof(module));
        ValidateOptional(operation, nameof(operation));
        CorrelationId = correlationId;
        TraceId = traceId;
        SpanId = spanId;
        ExecutionSessionId = executionSessionId;
        OrganizationId = organizationId;
        TenantId = tenantId;
        ActorId = actorId;
        ActorType = actorType;
        ApiKeyId = apiKeyId;
        RequestId = requestId;
        Endpoint = endpoint;
        HttpMethod = httpMethod;
        Module = module;
        Operation = operation;
        CoreVersion = coreVersion;
        ApiVersion = apiVersion;
        Environment = environment;
        ServiceName = serviceName;
        ServiceVersion = serviceVersion;
    }

    public string CorrelationId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ExecutionSessionId { get; init; }
    public string? OrganizationId { get; init; }
    public string? TenantId { get; init; }
    public string? ActorId { get; init; }
    public string? ActorType { get; init; }
    public string? ApiKeyId { get; init; }
    public string? RequestId { get; init; }
    public string? Endpoint { get; init; }
    public string? HttpMethod { get; init; }
    public string? Module { get; init; }
    public string? Operation { get; init; }
    public string CoreVersion { get; init; }
    public string ApiVersion { get; init; }
    public string Environment { get; init; }
    public string ServiceName { get; init; }
    public string ServiceVersion { get; init; }

    public static TelemetryContext System(string environment = "Unknown", string serviceName = "TradeMind", string serviceVersion = "Unknown") => new(
        "system", null, null, null, null, null, null, "System", null, null, null, null, null, null,
        "v1.0.0-core", "v1", environment, serviceName, serviceVersion);

    private static void ValidateRequired(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw new ArgumentException($"{name} is required and must be at most {maximumLength} characters.", name);
    }

    private static void ValidateOptional(string? value, string name)
    {
        if (value is not null && value.Length > 256)
            throw new ArgumentException($"{name} must be at most 256 characters.", name);
    }
}
