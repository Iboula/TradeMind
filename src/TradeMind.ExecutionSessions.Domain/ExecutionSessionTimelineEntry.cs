namespace TradeMind.ExecutionSessions.Domain;

public sealed record ExecutionSessionTimelineEntry
{
    public ExecutionSessionTimelineEntry(
        Guid timelineId,
        string eventType,
        ExecutionSessionStage stage,
        DateTimeOffset occurredAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (timelineId == Guid.Empty) throw new ArgumentException("Timeline id cannot be empty.", nameof(timelineId));
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timeline timestamp must be UTC.", nameof(occurredAtUtc));
        EventType = eventType.Trim();
        if (EventType.Length > 128) throw new ArgumentException("Timeline event type is too long.", nameof(eventType));
        TimelineId = timelineId;
        Stage = stage;
        OccurredAtUtc = occurredAtUtc;
        Metadata = ExecutionSessionMetadata.Copy(metadata);
    }

    public Guid TimelineId { get; }
    public string EventType { get; }
    public ExecutionSessionStage Stage { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
