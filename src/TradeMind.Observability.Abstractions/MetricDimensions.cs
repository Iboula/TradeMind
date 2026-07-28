namespace TradeMind.Observability.Abstractions;

public sealed record MetricDimensions(
    string? Module = null,
    string? Operation = null,
    string? Stage = null,
    string? Outcome = null,
    string? StatusClass = null,
    string? AuthenticationMethod = null,
    string? ActorType = null,
    string? Environment = null,
    string? ServiceVersion = null);
