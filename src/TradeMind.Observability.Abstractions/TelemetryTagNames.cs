namespace TradeMind.Observability.Abstractions;

public static class TelemetryTagNames
{
    public const string CorrelationId = "trademind.correlation.id";
    public const string TraceId = "trace_id";
    public const string SpanId = "span_id";
    public const string ExecutionSessionId = "trademind.execution_session.id";
    public const string OrganizationId = "trademind.organization.id";
    public const string TenantId = "trademind.tenant.id";
    public const string ActorId = "trademind.actor.id";
    public const string ActorType = "trademind.actor.type";
    public const string ApiKeyId = "trademind.api_key.id";
    public const string RequestId = "trademind.request.id";
    public const string Module = "trademind.module";
    public const string Operation = "trademind.operation";
    public const string Stage = "trademind.stage";
    public const string Outcome = "trademind.outcome";
    public const string CoreVersion = "trademind.core.version";
    public const string ApiVersion = "trademind.api.version";
    public const string Environment = "deployment.environment.name";
    public const string ArtifactType = "trademind.artifact.type";
    public const string ArtifactCount = "trademind.artifact.count";
    public const string ReplayEnabled = "trademind.replay.enabled";
    public const string IdempotencyReplay = "trademind.idempotency.replay";
    public const string Permission = "trademind.permission";
    public const string AuthenticationMethod = "trademind.auth.method";
    public const string ServiceName = "service.name";

    public static IReadOnlySet<string> Allowed { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CorrelationId, ExecutionSessionId, OrganizationId, TenantId, ActorId, ActorType, ApiKeyId,
        RequestId, Module, Operation, Stage, Outcome, CoreVersion, ApiVersion, Environment,
        ArtifactType, ArtifactCount, ReplayEnabled, IdempotencyReplay, Permission, AuthenticationMethod
    };
}
