using TradeMind.ExecutionSessions.Domain;

namespace TradeMind.ExecutionSessions.Application.DTOs;

public sealed record ExecutionSessionOutboxMessage(
    Guid OutboxMessageId,
    ExecutionSessionId SessionId,
    string EventType,
    string Payload,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset? ProcessedAtUtc = null,
    int AttemptCount = 0,
    string? LastError = null,
    long ConcurrencyVersion = 0);
