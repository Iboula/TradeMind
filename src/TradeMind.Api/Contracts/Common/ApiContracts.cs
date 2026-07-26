using System.Text.Json;

namespace TradeMind.Api.Contracts.Common;

/// <summary>Identifies an application module exposed through the API composition layer.</summary>
public enum ApiModule
{
    MarketContext,
    ExpertDispatch,
    ExpertAnalysis,
    Consensus,
    TradingDecision,
    Risk,
    TradingPlan,
    TradingWorkspace,
    TradingAssistant,
    PaperTrading
}

/// <summary>A non-blocking warning returned with an API operation.</summary>
public sealed record ApiWarning(string Code, string Message);

/// <summary>Common trace and schema metadata for an API response.</summary>
public sealed record ApiResponseMetadata(
    string RequestId,
    string CorrelationId,
    DateTimeOffset GeneratedAtUtc,
    string SchemaVersion,
    IReadOnlyList<ApiWarning> Warnings,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> TraceReferences);

/// <summary>Transport envelope used by module operation responses.</summary>
public sealed record ApiResponseEnvelope(
    string Module,
    JsonElement Data,
    ApiResponseMetadata Metadata);

/// <summary>Non-sensitive host and release metadata.</summary>
public sealed record VersionResponse(
    string ApiVersion,
    string ApplicationName,
    string CoreReleaseVersion,
    string Environment,
    string BuildVersion,
    DateTimeOffset GeneratedAtUtc);

/// <summary>Machine-readable process or dependency health status.</summary>
public sealed record HealthResponse(
    string Status,
    string Check,
    DateTimeOffset CheckedAtUtc);

/// <summary>A structured validation error associated with an optional request field.</summary>
public sealed record ApiProblemError(
    string Code,
    string Message,
    string? Field = null);
