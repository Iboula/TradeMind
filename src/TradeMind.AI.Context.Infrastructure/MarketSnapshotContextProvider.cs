using MediatR;
using TradeMind.AI.Context.Application;
using TradeMind.AI.Context.Domain;
using TradeMind.MarketConnectors.Application;

namespace TradeMind.AI.Context.Infrastructure;

public sealed class MarketSnapshotContextProvider(ISender sender) : IContextProvider
{
    public static readonly ContextProviderId ProviderId = new("market-snapshot");

    public ContextProviderDescriptor Descriptor { get; } = new(
        ProviderId,
        ContextProviderCategory.MarketSnapshot,
        ContextRequirement.Required,
        10,
        TimeSpan.FromSeconds(3));

    public async Task<ContextProviderResult> ProvideAsync(
        ContextProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Query.ConnectorId is null)
        {
            return ContextProviderResult.NotConfigured(
                ProviderId,
                "A connector is required to retrieve the market snapshot.");
        }

        var snapshot = await sender.Send(
            new GetLatestMarketSnapshotQuery(
                request.Query.ConnectorId,
                request.Query.Account,
                request.Query.Instrument,
                request.Query.Timeframe,
                request.Query.MaximumMarketAge),
            cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return ContextProviderResult.Unavailable(
                ProviderId,
                "No market snapshot matched the request.");
        }

        return ContextProviderResult.Succeeded(
            new MarketSnapshotContextData(snapshot),
            snapshot.CapturedAt,
            1,
            "1.0",
            [
                new ContextSourceReference("snapshot", snapshot.Id.ToString()),
                new ContextSourceReference("connector", snapshot.ConnectorId.Value)
            ]);
    }
}
