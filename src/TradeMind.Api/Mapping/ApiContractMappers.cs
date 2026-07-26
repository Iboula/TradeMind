using System.Text.Json;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.Api.Contracts.MarketContext;

namespace TradeMind.Api.Mapping;

public static class ApiContractMappers
{
    public static JsonElement ToJsonElement<T>(T value) =>
        JsonSerializer.SerializeToElement(value, ApiJson.Options);

    public static MarketContextApiResponse ToMarketContextResponse(
        MarketContextBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new MarketContextApiResponse(
            result.Status.ToString(),
            result.Context?.Id.Value.ToString("D"),
            result.Context?.Version,
            result.Context?.Instrument.Symbol,
            result.Context?.Timeframe.Code,
            result.Context?.BuiltAtUtc,
            result.Quality.Band.ToString(),
            result.Quality.Score,
            result.Traces
                .Select(trace => new ApiContextTrace(
                    trace.ProviderId.Value,
                    trace.Status.ToString(),
                    trace.References.Select(reference => $"{reference.Kind}:{reference.Value}").ToArray(),
                    trace.SourceTimestampUtc))
                .ToArray(),
            result.Warnings
                .Select(warning => new ApiContextWarning(warning.Code, warning.Message))
                .ToArray(),
            result.Errors
                .Select(error => new ApiContextError(error.Code.ToString(), error.Message))
                .ToArray());
    }
}
