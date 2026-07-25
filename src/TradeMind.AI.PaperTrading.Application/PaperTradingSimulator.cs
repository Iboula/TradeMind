using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.PaperTrading.Domain;
using TradeMind.AI.TradingPlans.Domain;
using TradeMind.AI.TradingWorkspace.Domain;

namespace TradeMind.AI.PaperTrading.Application;

public interface IPaperTradingSimulator
{
    Task<PaperTradingResult> SimulateAsync(PaperTradingRequest request, CancellationToken cancellationToken);
}

public sealed class PaperTradingSimulator(
    IPaperTradingEligibilityPolicy eligibilityPolicy,
    IOptions<PaperTradingOptions> options,
    TimeProvider timeProvider,
    ILogger<PaperTradingSimulator> logger) : IPaperTradingSimulator
{
    private readonly PaperTradingOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<PaperTradingResult> SimulateAsync(PaperTradingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var createdAtUtc = timeProvider.GetUtcNow();
        using var timeoutSource = new CancellationTokenSource(request.Timeout ?? _options.Timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            var eligibility = eligibilityPolicy.Evaluate(request, createdAtUtc);
            linkedSource.Token.ThrowIfCancellationRequested();
            if (!eligibility.CanSimulate)
            {
                return BuildRejected(request, createdAtUtc, eligibility);
            }

            return await SimulateCoreAsync(request, createdAtUtc, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Paper trading session {SessionId} was cancelled by the caller.", request.SessionId);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            logger.LogWarning("Paper trading session {SessionId} exceeded its timeout.", request.SessionId);
            return BuildInterrupted(request, createdAtUtc, PaperTradingStatus.TimedOut, "PAPER_TRADING_TIMEOUT", "The simulation exceeded its configured timeout.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Paper trading session {SessionId} failed unexpectedly.", request.SessionId);
            return BuildInterrupted(request, createdAtUtc, PaperTradingStatus.Failed, "PAPER_TRADING_FAILURE", "The simulation failed unexpectedly.");
        }
    }

    private async Task<PaperTradingResult> SimulateCoreAsync(
        PaperTradingRequest request,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var warnings = new List<PaperTradingWarning>();
        var errors = new List<PaperTradingError>();
        var events = new List<PaperTradingEvent>();
        var orders = new List<PaperOrder>();
        var fills = new List<PaperFill>();
        var journal = new List<PaperTradeJournalEntry>();
        var equityCurve = new List<PaperEquityPoint>();
        var sortedPoints = request.PricePath
            .OrderBy(point => point.TimestampUtc)
            .ThenBy(point => point.Bid)
            .ThenBy(point => point.Ask)
            .ToArray();
        if (sortedPoints.Length > _options.MaximumPricePoints)
        {
            sortedPoints = sortedPoints.Take(_options.MaximumPricePoints).ToArray();
            warnings.Add(new("PRICE_PATH_TRUNCATED", "The price path exceeded the configured point limit."));
        }

        var direction = request.Plan.Direction;
        var entryPrice = request.Plan.Entry!.Price.Value;
        var stopPrice = request.Plan.Stop!.Price.Value;
        var quantity = request.Plan.Quantity!.FinalQuantity;
        var targets = request.Plan.Targets
            .OrderBy(target => target.Ordinal)
            .ThenBy(target => target.Price.Value)
            .ToArray();
        var tradeId = new PaperTradeId(DeterministicGuid(request, "trade-1"));
        var entryOrderId = new PaperOrderId(DeterministicGuid(request, "order-entry"));
        var entryOrder = new PaperOrder(
            entryOrderId,
            tradeId,
            PaperOrderType.Entry,
            direction == TradingPlanDirection.Long ? PaperOrderSide.Buy : PaperOrderSide.Sell,
            quantity,
            entryPrice,
            createdAtUtc,
            PaperOrderStatus.Pending);
        orders.Add(entryOrder);
        AddEvent(events, PaperTradingEventType.SessionStarted, createdAtUtc, request.SessionId.ToString(), "Paper trading session started.");
        AddEvent(events, PaperTradingEventType.EntryOrderCreated, createdAtUtc, entryOrderId.ToString(), "Entry order created from the trading plan.", entryOrderId, tradeId: tradeId);

        PaperFill? entryFill = null;
        PaperPositionSnapshot? openPosition = null;
        var positionClosed = false;
        decimal realizedPnl = 0;
        DateTimeOffset? entryTimestamp = null;
        decimal? filledEntryPrice = null;

        foreach (var point in sortedPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            if (entryFill is null && openPosition is null && EntryTriggered(direction, point, entryPrice))
            {
                var fillPrice = direction == TradingPlanDirection.Long ? point.Ask : point.Bid;
                var fillId = new PaperFillId(DeterministicGuid(request, "fill-entry"));
                entryFill = new PaperFill(fillId, entryOrderId, tradeId, entryOrder.Side, quantity, fillPrice, point.TimestampUtc);
                fills.Add(entryFill);
                ReplaceOrder(orders, CopyOrder(entryOrder, PaperOrderStatus.Filled, fillId));
                entryTimestamp = point.TimestampUtc;
                filledEntryPrice = fillPrice;
                AddEvent(events, PaperTradingEventType.EntryFilled, point.TimestampUtc, fillId.ToString(), "Entry order filled by the simulated price path.", entryOrderId, fillId, tradeId);
                AddEvent(events, PaperTradingEventType.PositionOpened, point.TimestampUtc, tradeId.ToString(), "Paper position opened.", tradeId: tradeId);
            }

            if (entryFill is not null && !positionClosed && filledEntryPrice.HasValue && entryTimestamp.HasValue)
            {
                var mark = direction == TradingPlanDirection.Long ? point.Bid : point.Ask;
                var unrealized = CalculatePnl(direction, filledEntryPrice.Value, mark, quantity, _options.PointValue);
                openPosition = new PaperPositionSnapshot(tradeId, direction, quantity, filledEntryPrice.Value, entryTimestamp.Value, mark, unrealized);
                var exit = FindExit(direction, point, stopPrice, targets, _options.TriggerTieBreakPolicy);
                if (exit is not null)
                {
                    var exitType = exit.Value.IsStop ? PaperOrderType.StopLoss : PaperOrderType.TakeProfit;
                    var exitSide = direction == TradingPlanDirection.Long ? PaperOrderSide.Sell : PaperOrderSide.Buy;
                    var exitOrderId = new PaperOrderId(DeterministicGuid(request, exit.Value.IsStop ? "order-stop" : $"order-target-{exit.Value.TargetOrdinal}"));
                    var exitFillId = new PaperFillId(DeterministicGuid(request, exit.Value.IsStop ? "fill-stop" : $"fill-target"));
                    var exitFillPrice = direction == TradingPlanDirection.Long ? point.Bid : point.Ask;
                    var exitOrder = new PaperOrder(exitOrderId, tradeId, exitType, exitSide, quantity, exit.Value.TriggerPrice, point.TimestampUtc, PaperOrderStatus.Filled, exitFillId);
                    var exitFill = new PaperFill(exitFillId, exitOrderId, tradeId, exitSide, quantity, exitFillPrice, point.TimestampUtc);
                    orders.Add(exitOrder);
                    fills.Add(exitFill);
                    var tradePnl = CalculatePnl(direction, filledEntryPrice.Value, exitFillPrice, quantity, _options.PointValue);
                    realizedPnl += tradePnl;
                    var reason = exit.Value.IsStop ? PaperExitReason.StopLoss : PaperExitReason.TakeProfit;
                    journal.Add(new PaperTradeJournalEntry(tradeId, direction, quantity, filledEntryPrice.Value, entryTimestamp.Value, exitFillPrice, point.TimestampUtc, reason, tradePnl));
                    AddEvent(events, exit.Value.IsStop ? PaperTradingEventType.StopLossTriggered : PaperTradingEventType.TakeProfitTriggered, point.TimestampUtc, exitOrderId.ToString(), exit.Value.IsStop ? "Stop loss was triggered." : "Take profit was triggered.", exitOrderId, exitFillId, tradeId);
                    AddEvent(events, PaperTradingEventType.ExitFilled, point.TimestampUtc, exitFillId.ToString(), "Exit order filled by the simulated price path.", exitOrderId, exitFillId, tradeId);
                    AddEvent(events, PaperTradingEventType.PositionClosed, point.TimestampUtc, tradeId.ToString(), "Paper position closed.", tradeId: tradeId);
                    openPosition = null;
                    positionClosed = true;
                }
                else
                {
                    AddEvent(events, PaperTradingEventType.UnrealizedPnlUpdated, point.TimestampUtc, tradeId.ToString(), "Unrealized PnL updated from the simulated price path.", tradeId: tradeId);
                }
            }

            var currentUnrealized = openPosition?.UnrealizedPnl ?? 0m;
            equityCurve.Add(new PaperEquityPoint(point.TimestampUtc, request.InitialEquity + realizedPnl + currentUnrealized, realizedPnl, currentUnrealized));
            if (equityCurve.Count >= _options.MaximumEquityPoints)
            {
                if (sortedPoints.Length > equityCurve.Count)
                {
                    warnings.Add(new("EQUITY_CURVE_TRUNCATED", "The equity curve exceeded the configured point limit."));
                }

                break;
            }
        }

        if (entryFill is null)
        {
            ReplaceOrder(orders, CopyOrder(entryOrder, PaperOrderStatus.Expired, null));
            AddEvent(events, PaperTradingEventType.EntryExpired, createdAtUtc, entryOrderId.ToString(), "Entry was not reached during the simulated price path.", entryOrderId, tradeId: tradeId);
            warnings.Add(new("ENTRY_NOT_FILLED", "The entry level was not reached by the supplied price path."));
        }
        else if (openPosition is not null)
        {
            warnings.Add(new("OPEN_POSITION", "The simulation ended while the paper position was still open."));
            journal.Add(new PaperTradeJournalEntry(tradeId, direction, quantity, filledEntryPrice!.Value, entryTimestamp!.Value, null, null, null, 0m));
        }

        var finalUnrealized = openPosition?.UnrealizedPnl ?? 0m;
        var statistics = BuildStatistics(journal, request.InitialEquity, equityCurve);
        var status = entryFill is null
            ? PaperTradingStatus.NoFill
            : warnings.Any(warning => warning.Code is "PRICE_PATH_TRUNCATED" or "EQUITY_CURVE_TRUNCATED")
                ? PaperTradingStatus.PartiallySucceeded
                : PaperTradingStatus.Succeeded;
        AddEvent(events, PaperTradingEventType.SessionCompleted, createdAtUtc, request.SessionId.ToString(), "Paper trading session completed.");
        return CreateResult(request, status, orders, fills, openPosition, realizedPnl, finalUnrealized, equityCurve, journal, statistics, events, warnings, errors, createdAtUtc, timeProvider.GetUtcNow());
    }

    private PaperTradingResult BuildRejected(PaperTradingRequest request, DateTimeOffset createdAtUtc, PaperTradingEligibilityDecision eligibility)
    {
        var status = eligibility.IsExpired
            ? PaperTradingStatus.Expired
            : eligibility.Errors.Count > 0
                ? PaperTradingStatus.Invalid
                : eligibility.IsNoTrade ? PaperTradingStatus.NoTrade : PaperTradingStatus.Invalid;
        var events = new List<PaperTradingEvent>();
        AddEvent(events, PaperTradingEventType.SessionStarted, createdAtUtc, request.SessionId.ToString(), "Paper trading session started.");
        foreach (var warning in eligibility.Warnings)
        {
            AddEvent(events, PaperTradingEventType.Warning, createdAtUtc, warning.Code, warning.Message);
        }

        foreach (var error in eligibility.Errors)
        {
            AddEvent(events, PaperTradingEventType.Error, createdAtUtc, error.Code, error.Message);
        }

        AddEvent(events, PaperTradingEventType.SessionCompleted, createdAtUtc, request.SessionId.ToString(), "Paper trading session completed without a position.");
        return CreateResult(request, status, [], [], null, 0, 0, [], [], new PaperTradingStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), events, eligibility.Warnings, eligibility.Errors, createdAtUtc, timeProvider.GetUtcNow());
    }

    private PaperTradingResult BuildInterrupted(PaperTradingRequest request, DateTimeOffset createdAtUtc, PaperTradingStatus status, string code, string message) =>
        CreateResult(
            request,
            status,
            [],
            [],
            null,
            0,
            0,
            [],
            [],
            new PaperTradingStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            [new PaperTradingEvent(0, PaperTradingEventType.Error, createdAtUtc, code, message)],
            [],
            [new PaperTradingError(code, message)],
            createdAtUtc,
            timeProvider.GetUtcNow());

    private PaperTradingResult CreateResult(
        PaperTradingRequest request,
        PaperTradingStatus status,
        IReadOnlyCollection<PaperOrder> orders,
        IReadOnlyCollection<PaperFill> fills,
        PaperPositionSnapshot? openPosition,
        decimal realizedPnl,
        decimal unrealizedPnl,
        IReadOnlyCollection<PaperEquityPoint> equityCurve,
        IReadOnlyCollection<PaperTradeJournalEntry> journal,
        PaperTradingStatistics statistics,
        IReadOnlyCollection<PaperTradingEvent> events,
        IReadOnlyCollection<PaperTradingWarning> warnings,
        IReadOnlyCollection<PaperTradingError> errors,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc)
    {
        var limitedEvents = events.Take(_options.MaximumTimelineEvents).ToArray();
        var limitedWarnings = warnings.Take(_options.MaximumWarnings).ToArray();
        var limitedErrors = errors.Take(_options.MaximumErrors).ToArray();
        return new PaperTradingResult(
            request.SessionId,
            request.Workspace,
            request.Plan,
            status,
            orders.Take(_options.MaximumOrders).ToArray(),
            fills.Take(_options.MaximumFills).ToArray(),
            openPosition,
            realizedPnl,
            unrealizedPnl,
            equityCurve.Take(_options.MaximumEquityPoints).ToArray(),
            journal,
            statistics,
            limitedEvents,
            limitedWarnings,
            limitedErrors,
            ReplayFingerprint(request),
            status switch
            {
                PaperTradingStatus.Succeeded => "Paper trading simulation completed.",
                PaperTradingStatus.NoFill => "Paper trading simulation completed without an entry fill.",
                PaperTradingStatus.NoTrade => "Paper trading was not started because the plan is non-directional.",
                _ => "Paper trading simulation did not produce an executable result."
            },
            createdAtUtc,
            completedAtUtc);
    }

    private static bool EntryTriggered(TradingPlanDirection direction, PaperPricePoint point, decimal entryPrice) =>
        direction == TradingPlanDirection.Long ? point.Ask <= entryPrice : point.Bid >= entryPrice;

    private static ExitTrigger? FindExit(
        TradingPlanDirection direction,
        PaperPricePoint point,
        decimal stopPrice,
        IReadOnlyCollection<TradingPlanTarget> targets,
        PaperTriggerTieBreakPolicy tieBreakPolicy)
    {
        var stopHit = direction == TradingPlanDirection.Long ? point.Bid <= stopPrice : point.Ask >= stopPrice;
        var target = targets.FirstOrDefault(item => direction == TradingPlanDirection.Long ? point.Bid >= item.Price.Value : point.Ask <= item.Price.Value);
        var targetHit = target is not null;
        if (!stopHit && !targetHit)
        {
            return null;
        }

        if (stopHit && targetHit && tieBreakPolicy == PaperTriggerTieBreakPolicy.StopLossFirst)
        {
            return new ExitTrigger(true, stopPrice, 0);
        }

        if (targetHit)
        {
            return new ExitTrigger(false, target!.Price.Value, target.Ordinal);
        }

        return new ExitTrigger(true, stopPrice, 0);
    }

    private static decimal CalculatePnl(TradingPlanDirection direction, decimal entryPrice, decimal exitPrice, decimal quantity, decimal pointValue) =>
        (direction == TradingPlanDirection.Long ? exitPrice - entryPrice : entryPrice - exitPrice) * quantity * pointValue;

    private static PaperTradingStatistics BuildStatistics(
        IReadOnlyCollection<PaperTradeJournalEntry> journal,
        decimal initialEquity,
        IReadOnlyCollection<PaperEquityPoint> equityCurve)
    {
        var closed = journal.Where(entry => entry.IsClosed).ToArray();
        var grossProfit = closed.Where(entry => entry.RealizedPnl > 0).Sum(entry => entry.RealizedPnl);
        var grossLoss = closed.Where(entry => entry.RealizedPnl < 0).Sum(entry => -entry.RealizedPnl);
        var maxDrawdown = 0m;
        var peak = initialEquity;
        foreach (var point in equityCurve)
        {
            peak = Math.Max(peak, point.Equity);
            maxDrawdown = Math.Max(maxDrawdown, peak - point.Equity);
        }

        return new PaperTradingStatistics(
            journal.Count,
            closed.Length,
            closed.Count(entry => entry.RealizedPnl > 0),
            closed.Count(entry => entry.RealizedPnl < 0),
            closed.Count(entry => entry.RealizedPnl == 0),
            grossProfit,
            grossLoss,
            closed.Sum(entry => entry.RealizedPnl),
            maxDrawdown,
            closed.Length == 0 ? 0 : closed.Average(entry => entry.RealizedPnl));
    }

    private static void ReplaceOrder(List<PaperOrder> orders, PaperOrder replacement)
    {
        var index = orders.FindIndex(order => order.OrderId == replacement.OrderId);
        if (index >= 0)
        {
            orders[index] = replacement;
        }
    }

    private static PaperOrder CopyOrder(PaperOrder source, PaperOrderStatus status, PaperFillId? fillId) =>
        new(source.OrderId, source.TradeId, source.Type, source.Side, source.Quantity, source.RequestedPrice, source.CreatedAtUtc, status, fillId);

    private static void AddEvent(
        List<PaperTradingEvent> events,
        PaperTradingEventType type,
        DateTimeOffset timestampUtc,
        string sourceId,
        string description,
        PaperOrderId? orderId = null,
        PaperFillId? fillId = null,
        PaperTradeId? tradeId = null) =>
        events.Add(new PaperTradingEvent(events.Count, type, timestampUtc, sourceId, description, orderId, fillId, tradeId));

    private static Guid DeterministicGuid(PaperTradingRequest request, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{request.SessionId}|{request.Plan.PlanId}|{suffix}"));
        return new Guid(bytes[..16]);
    }

    private static string ReplayFingerprint(PaperTradingRequest request)
    {
        var payload = new StringBuilder()
            .Append(request.SessionId).Append('|')
            .Append(request.Workspace.WorkspaceId).Append('|')
            .Append(request.Plan.PlanId).Append('|')
            .Append(request.InitialEquity.ToString(CultureInfo.InvariantCulture));
        foreach (var point in request.PricePath.OrderBy(point => point.TimestampUtc).ThenBy(point => point.Bid).ThenBy(point => point.Ask))
        {
            payload.Append('|').Append(point.TimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                .Append(':').Append(point.Bid.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(point.Ask.ToString(CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString())));
    }

    private readonly record struct ExitTrigger(bool IsStop, decimal TriggerPrice, int TargetOrdinal);
}
