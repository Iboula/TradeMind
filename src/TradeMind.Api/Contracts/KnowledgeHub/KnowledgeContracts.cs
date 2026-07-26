namespace TradeMind.Api.Contracts.KnowledgeHub;

/// <summary>Transport representation of a persisted KnowledgeHub source.</summary>
public sealed record KnowledgeSourceResponse(
    Guid Id,
    string Title,
    string Type,
    string Status,
    DateTimeOffset ImportedAtUtc,
    string? FailureReason,
    IReadOnlyList<KnowledgeFragmentResponse> Fragments);

/// <summary>Transport representation of a source fragment.</summary>
public sealed record KnowledgeFragmentResponse(
    Guid Id,
    int Sequence,
    string Content,
    int TokenCount);

/// <summary>Transport representation of a semantic search hit.</summary>
public sealed record KnowledgeSearchResponse(
    Guid SourceId,
    string SourceTitle,
    Guid FragmentId,
    int Sequence,
    string Content,
    double Score);
