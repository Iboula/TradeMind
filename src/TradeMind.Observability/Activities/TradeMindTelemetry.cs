using System.Diagnostics;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Activities;

public sealed class TradeMindTelemetry : ITradeMindTelemetry
{
    public void EnrichCurrent(TelemetryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Activity.Current is null)
            return;
        var operation = new TelemetryOperation("TradeMind.Api.Request", "Api", TelemetryStage.Api);
        var wrapper = new TradeMindActivity(Activity.Current);
        wrapper.Enrich(operation, context);
    }

    public ITradeMindActivity StartActivity(
        TelemetryOperation operation,
        TelemetryContext? context = null,
        IReadOnlyCollection<TelemetryLink>? links = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var source = TradeMindActivitySource.Get(SourceFor(operation.Stage));
        var activity = source.StartActivity(
            operation.Name,
            ActivityKind.Internal,
            default(ActivityContext),
            tags: null,
            links: links.ToActivityLinks());
        ITradeMindActivity wrapper = activity is null ? new NoopTradeMindActivity() : new TradeMindActivity(activity);
        wrapper.Enrich(operation, context ?? TelemetryContext.System());
        return wrapper;
    }

    private static string SourceFor(TelemetryStage stage) => stage switch
    {
        TelemetryStage.Api => TelemetryActivityNames.Api,
        TelemetryStage.Authentication or TelemetryStage.Authorization => TelemetryActivityNames.Identity,
        TelemetryStage.ExecutionSession => TelemetryActivityNames.ExecutionSessions,
        TelemetryStage.Persistence or TelemetryStage.Database => TelemetryActivityNames.Persistence,
        TelemetryStage.MarketContext => TelemetryActivityNames.MarketContext,
        TelemetryStage.ExpertDispatch or TelemetryStage.ExpertAnalysis => TelemetryActivityNames.Experts,
        TelemetryStage.Consensus => TelemetryActivityNames.Consensus,
        TelemetryStage.TradingDecision => TelemetryActivityNames.TradingDecisions,
        TelemetryStage.Risk => TelemetryActivityNames.Risk,
        TelemetryStage.TradingPlan => TelemetryActivityNames.TradingPlans,
        TelemetryStage.TradingWorkspace => TelemetryActivityNames.TradingWorkspace,
        TelemetryStage.TradingAssistant => TelemetryActivityNames.TradingAssistant,
        TelemetryStage.PaperTrading => TelemetryActivityNames.PaperTrading,
        TelemetryStage.Outbox => TelemetryActivityNames.Outbox,
        TelemetryStage.Replay => TelemetryActivityNames.Replay,
        _ => TelemetryActivityNames.Source
    };
}
