namespace TradeMind.Observability.Abstractions;

public sealed record TelemetryLink
{
    public TelemetryLink(string traceId, string? spanId = null, string? relationship = null)
    {
        if (string.IsNullOrWhiteSpace(traceId) || traceId.Length > 64)
            throw new ArgumentException("A trace id is required and must be at most 64 characters.", nameof(traceId));
        if (spanId is { Length: > 32 })
            throw new ArgumentException("SpanId must be at most 32 characters.", nameof(spanId));
        if (relationship is { Length: > 64 })
            throw new ArgumentException("Relationship must be at most 64 characters.", nameof(relationship));
        TraceId = traceId;
        SpanId = spanId;
        Relationship = relationship;
    }

    public string TraceId { get; }
    public string? SpanId { get; }
    public string? Relationship { get; }
}
