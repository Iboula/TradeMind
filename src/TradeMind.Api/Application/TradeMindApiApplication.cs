using System.Text.Json;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.Api.Contracts.Common;
using TradeMind.Api.Contracts.MarketContext;
using TradeMind.Api.Mapping;
using TradeMind.Market.Abstractions;

namespace TradeMind.Api.Application;

/// <summary>Application facade used by HTTP endpoints to invoke module abstractions.</summary>
public interface ITradeMindApiApplication
{
    Task<ApiOperationResult> BuildMarketContextAsync(
        BuildMarketContextApiRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ApiOperationResult> ExecuteAsync(
        ApiModule module,
        JsonElement payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed record ApiOperationResult(
    ApiModule Module,
    JsonElement Data,
    string SchemaVersion,
    IReadOnlyList<ApiWarning> Warnings,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> TraceReferences)
{
    public static ApiOperationResult Success(
        ApiModule module,
        JsonElement data,
        IReadOnlyList<ApiWarning>? warnings = null,
        IReadOnlyList<string>? blockers = null,
        IReadOnlyList<string>? traceReferences = null,
        string schemaVersion = "1.0") =>
        new(module, data, schemaVersion, warnings ?? [], blockers ?? [], traceReferences ?? []);
}

public sealed class ApiRequestValidationException(
    IReadOnlyCollection<ApiProblemError> errors) : Exception("The API request is invalid.")
{
    public IReadOnlyList<ApiProblemError> Errors { get; } = errors?.ToArray()
        ?? throw new ArgumentNullException(nameof(errors));
}

public sealed class ApiModuleNotConfiguredException(ApiModule module)
    : Exception($"The {module} application facade is not configured.")
{
    public ApiModule Module { get; } = module;
}

public sealed class TradeMindApiApplication(
    IMarketContextBuilder contextBuilder) : ITradeMindApiApplication
{
    public async Task<ApiOperationResult> BuildMarketContextAsync(
        BuildMarketContextApiRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var query = MapMarketContextRequest(request, correlationId);
        var result = await contextBuilder.BuildAsync(query, cancellationToken).ConfigureAwait(false);
        var response = ApiContractMappers.ToMarketContextResponse(result);
        var traces = result.Traces
            .SelectMany(trace => trace.References.Select(reference => $"{trace.ProviderId.Value}:{reference.Kind}:{reference.Value}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var warnings = result.Warnings
            .Select(warning => new ApiWarning(warning.Code, warning.Message))
            .ToArray();
        var blockers = result.Errors.Select(error => $"{error.Code}:{error.Message}").ToArray();
        return ApiOperationResult.Success(
            ApiModule.MarketContext,
            ApiContractMappers.ToJsonElement(response),
            warnings,
            blockers,
            traces);
    }

    public Task<ApiOperationResult> ExecuteAsync(
        ApiModule module,
        JsonElement payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<ApiOperationResult>(new ApiModuleNotConfiguredException(module));
    }

    private static BuildMarketContextQuery MapMarketContextRequest(
        BuildMarketContextApiRequest request,
        string correlationId)
    {
        if (request.SchemaVersion != 1)
        {
            throw new ApiRequestValidationException([new("UNSUPPORTED_SCHEMA_VERSION", "Schema version 1 is required.", "schemaVersion")]);
        }

        var errors = new List<ApiProblemError>();
        if (string.IsNullOrWhiteSpace(request.UserId)) errors.Add(new("REQUIRED", "UserId is required.", "userId"));
        if (string.IsNullOrWhiteSpace(request.SessionId)) errors.Add(new("REQUIRED", "SessionId is required.", "sessionId"));
        if (string.IsNullOrWhiteSpace(request.Instrument)) errors.Add(new("REQUIRED", "Instrument is required.", "instrument"));
        if (!Timeframe.TryParse(request.Timeframe, out var timeframe)) errors.Add(new("INVALID_TIMEFRAME", "Timeframe is not supported.", "timeframe"));
        if (request.MaximumMarketAgeSeconds is < 0) errors.Add(new("INVALID_VALUE", "Maximum market age cannot be negative.", "maximumMarketAgeSeconds"));
        if (errors.Count > 0) throw new ApiRequestValidationException(errors);

        Instrument instrument;
        try
        {
            instrument = new Instrument(request.Instrument);
        }
        catch (ArgumentException exception)
        {
            throw new ApiRequestValidationException([new("INVALID_INSTRUMENT", exception.Message, "instrument")]);
        }

        var categories = new List<ContextProviderCategory>();
        foreach (var category in request.RequestedCategories)
        {
            if (!Enum.TryParse<ContextProviderCategory>(category, true, out var parsed))
            {
                throw new ApiRequestValidationException([new("INVALID_CATEGORY", $"Context category '{category}' is not supported.", "requestedCategories")]);
            }

            categories.Add(parsed);
        }

        return new BuildMarketContextQuery(
            request.UserId,
            request.SessionId,
            string.IsNullOrWhiteSpace(request.ConnectorId) ? null : new ConnectorId(request.ConnectorId),
            instrument,
            timeframe!,
            request.ConversationId,
            correlationId,
            account: string.IsNullOrWhiteSpace(request.Account) ? null : new ExternalAccountReference(request.Account),
            knowledgeQuery: request.KnowledgeQuery,
            maximumMarketAge: request.MaximumMarketAgeSeconds is null ? null : TimeSpan.FromSeconds(request.MaximumMarketAgeSeconds.Value),
            requestedCategories: categories);
    }
}
