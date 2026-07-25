using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeMind.AI.PaperTrading.Domain;
using TradeMind.AI.TradingPlans.Domain;

namespace TradeMind.AI.PaperTrading.Application;

public interface IPaperTradingEngine
{
    Task<PaperTradingResult> SimulateAsync(PaperTradingRequest request, CancellationToken cancellationToken = default);
}

public interface IPaperTradingSimulator : IPaperTradingEngine
{
}

/// <summary>Deterministic paper execution engine. It has no broker or persistence boundary.</summary>
public sealed class PaperTradingSimulator(
    IPaperTradingEligibilityPolicy eligibilityPolicy,
    IOptions<PaperTradingOptions> options,
    TimeProvider timeProvider,
    ILogger<PaperTradingSimulator> logger) : IPaperTradingSimulator
{
    private readonly PaperTradingOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<PaperTradingResult> SimulateAsync(PaperTradingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var createdAtUtc = timeProvider.GetUtcNow();
        using var timeoutSource = new CancellationTokenSource(request.Timeout ?? _options.Timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        logger.LogInformation("Paper simulation started. SimulationId={SimulationId}, PlanId={PlanId}, WorkspaceId={WorkspaceId}", request.SimulationId, request.Plan.PlanId, request.Workspace.WorkspaceId);

        try
        {
            var eligibility = eligibilityPolicy.Evaluate(request, createdAtUtc);
            linkedSource.Token.ThrowIfCancellationRequested();
            if (!eligibility.CanSimulate)
            {
                logger.LogWarning("Paper simulation rejected. SimulationId={SimulationId}, ErrorCount={ErrorCount}", request.SimulationId, eligibility.Errors.Count);
                return BuildRejected(request, createdAtUtc, eligibility);
            }

            return await SimulateCoreAsync(request, createdAtUtc, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Paper simulation cancelled. SimulationId={SimulationId}", request.SimulationId);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            logger.LogWarning("Paper simulation timed out. SimulationId={SimulationId}", request.SimulationId);
            return BuildInterrupted(request, createdAtUtc, PaperTradingStatus.TimedOut, PaperTradingSimulationState.TimedOut, "PAPER_TRADING_TIMEOUT", "The simulation exceeded its configured timeout.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Paper simulation failed. SimulationId={SimulationId}", request.SimulationId);
            return BuildInterrupted(request, createdAtUtc, PaperTradingStatus.Failed, PaperTradingSimulationState.Failed, "PAPER_TRADING_FAILURE", "The simulation failed unexpectedly.");
        }
    }

    private async Task<PaperTradingResult> SimulateCoreAsync(PaperTradingRequest request, DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<PaperTradingWarning>();
        var errors = new List<PaperTradingError>();
        var blockers = new List<PaperTradingBlocker>();
        var events = new List<PaperTradingEvent>();
        var orders = new List<PaperOrder>();
        var fills = new List<PaperFill>();
        var executions = new List<PaperExecution>();
        var journal = new List<PaperTradeJournalEntry>();
        var equityCurve = new List<PaperEquityPoint>();
        var snapshots = new List<PaperPositionSnapshot>();
        var settings = SimulationSettings.Create(_options, request.Options);
        var tickLimit = Math.Min(settings.MaximumMarketTicks, _options.MaximumPricePoints);
        var ticks = request.MarketTicks.Take(tickLimit).ToArray();
        if (request.MarketTicks.Count > ticks.Length)
        {
            warnings.Add(new("MARKET_TICKS_TRUNCATED", "The market tick input exceeded the configured limit."));
            warnings.Add(new("PRICE_PATH_TRUNCATED", "The price path exceeded the configured point limit."));
        }

        var direction = request.Plan.Direction;
        var entryPrice = RoundPrice(request.Plan.Entry!.Price.Value, settings.PriceDecimals);
        var stopPrice = RoundPrice(request.Plan.Stop!.Price.Value, settings.PriceDecimals);
        var quantity = RoundQuantity(request.Plan.Quantity!.FinalQuantity, settings.QuantityDecimals);
        var targets = request.Plan.Targets.OrderBy(target => target.Ordinal).ThenBy(target => target.Price.Value).ToArray();
        var tradeId = new PaperTradeId(DeterministicGuid(request, "trade-1"));
        var positionId = new PaperTradingPositionId(DeterministicGuid(request, "position-1"));
        var entryOrderId = new PaperOrderId(DeterministicGuid(request, "order-entry"));
        var entryOrder = new PaperOrder(entryOrderId, tradeId, PaperOrderType.Entry, direction == TradingPlanDirection.Long ? PaperOrderSide.Buy : PaperOrderSide.Sell, quantity, entryPrice, createdAtUtc, PaperOrderStatus.Pending);
        orders.Add(entryOrder);
        AddEvent(events, PaperTradingEventType.SessionStarted, createdAtUtc, request.SimulationId.ToString(), "Paper simulation started.");
        AddEvent(events, PaperTradingEventType.EntryOrderCreated, createdAtUtc, entryOrderId.ToString(), "Entry order created from the trading plan.", entryOrderId, tradeId: tradeId);
        logger.LogInformation("Paper order created. SimulationId={SimulationId}, OrderId={OrderId}, OrderType={OrderType}", request.SimulationId, entryOrderId, entryOrder.Type);

        PaperFill? entryFill = null;
        PaperPositionSnapshot? openPosition = null;
        decimal realizedPnl = 0m;
        decimal grossPnl = 0m;
        decimal totalCommission = 0m;
        DateTimeOffset? entryTimestamp = null;
        decimal? filledEntryPrice = null;
        decimal entryCommission = 0m;
        var remainingQuantity = quantity;
        decimal mfe = 0m;
        decimal mae = 0m;
        var nextTargetOrdinal = 0;
        var rejectedByAmbiguousTick = false;

        foreach (var tick in ticks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            if (entryFill is null && EntryTriggered(direction, tick, entryPrice))
            {
                var fillPrice = ApplyEntrySlippage(direction, MarketEntryPrice(direction, tick, settings.SpreadHandling), settings);
                var fillId = new PaperFillId(DeterministicGuid(request, "fill-entry"));
                entryFill = new PaperFill(fillId, entryOrderId, tradeId, entryOrder.Side, quantity, fillPrice, tick.TimestampUtc);
                fills.Add(entryFill);
                entryCommission = CalculateCommission(fillPrice, quantity, settings);
                totalCommission += entryCommission;
                executions.Add(new PaperExecution(fillId.ExecutionId, entryOrderId.TradingId, positionId, entryOrder.Side, quantity, fillPrice, tick.TimestampUtc, entryCommission));
                ReplaceOrder(orders, CopyOrder(entryOrder, PaperOrderStatus.Filled, fillId));
                entryTimestamp = tick.TimestampUtc;
                filledEntryPrice = fillPrice;
                AddEvent(events, PaperTradingEventType.EntryFilled, tick.TimestampUtc, fillId.ToString(), "Entry order filled by the simulated market tick.", entryOrderId, fillId, tradeId);
                AddEvent(events, PaperTradingEventType.PositionOpened, tick.TimestampUtc, positionId.ToString(), "Paper position opened.", tradeId: tradeId);
                logger.LogInformation("Paper order filled and position opened. SimulationId={SimulationId}, OrderId={OrderId}, PositionId={PositionId}", request.SimulationId, entryOrderId, positionId);
            }

            if (entryFill is null || filledEntryPrice is null || entryTimestamp is null || openPosition?.Status == PaperPositionStatus.Closed)
            {
                equityCurve.Add(new PaperEquityPoint(tick.TimestampUtc, request.InitialBalance + realizedPnl, realizedPnl, 0m, request.InitialBalance + realizedPnl));
                continue;
            }

            var markPrice = MarketExitPrice(direction, tick, settings.SpreadHandling);
            var unrealized = CalculatePnl(direction, filledEntryPrice.Value, markPrice, quantity, settings.PointValue);
            var favorable = Math.Max(0m, unrealized);
            var adverse = Math.Max(0m, -unrealized);
            mfe = Math.Max(mfe, favorable);
            mae = Math.Max(mae, adverse);
            openPosition = new PaperPositionSnapshot(tradeId, direction, quantity, filledEntryPrice.Value, entryTimestamp.Value, markPrice, unrealized, PaperPositionStatus.Open, mfe, mae);
            snapshots.Add(openPosition);
            var exit = FindExit(direction, tick, stopPrice, targets, nextTargetOrdinal, settings.AmbiguousTriggerPolicy);
            if (exit is not null)
            {
                if (exit.Value.Rejected)
                {
                    rejectedByAmbiguousTick = true;
                    errors.Add(new("AMBIGUOUS_TICK_REJECTED", "The tick touched both a stop and a target and the configured policy rejected the ambiguity."));
                    blockers.Add(new("AMBIGUOUS_TICK", "Intratick ordering was insufficient to choose a safe exit."));
                    AddEvent(events, PaperTradingEventType.Error, tick.TimestampUtc, "AMBIGUOUS_TICK", "Ambiguous stop and target trigger rejected.", tradeId: tradeId);
                    break;
                }

                var allocation = !exit.Value.IsStop && settings.AllowPartialTargetExits
                    ? settings.TargetAllocations.FirstOrDefault(item => item.Ordinal == exit.Value.TargetOrdinal)
                    : null;
                var exitQuantity = allocation is null ? remainingQuantity : Math.Min(remainingQuantity, allocation.Quantity);
                var closesPosition = exit.Value.IsStop || remainingQuantity - exitQuantity <= 0m;
                if (!exit.Value.IsStop && allocation is not null && exitQuantity < remainingQuantity)
                {
                    nextTargetOrdinal = exit.Value.TargetOrdinal + 1;
                    warnings.Add(new("PARTIAL_TARGET_EXIT", "A configured target allocation created a partial exit."));
                }

                var exitType = exit.Value.IsStop ? PaperOrderType.StopLoss : PaperOrderType.TakeProfit;
                var exitSide = direction == TradingPlanDirection.Long ? PaperOrderSide.Sell : PaperOrderSide.Buy;
                var exitOrderId = new PaperOrderId(DeterministicGuid(request, exit.Value.IsStop ? "order-stop" : $"order-target-{exit.Value.TargetOrdinal}"));
                var exitFillId = new PaperFillId(DeterministicGuid(request, exit.Value.IsStop ? "fill-stop" : $"fill-target-{exit.Value.TargetOrdinal}"));
                var exitFillPrice = ApplyExitSlippage(direction, MarketExitPrice(direction, tick, settings.SpreadHandling), settings);
                var exitOrder = new PaperOrder(exitOrderId, tradeId, exitType, exitSide, exitQuantity, exit.Value.TriggerPrice, tick.TimestampUtc, PaperOrderStatus.Filled, exitFillId);
                var exitFill = new PaperFill(exitFillId, exitOrderId, tradeId, exitSide, exitQuantity, exitFillPrice, tick.TimestampUtc);
                orders.Add(exitOrder);
                fills.Add(exitFill);
                var exitCommission = CalculateCommission(exitFillPrice, exitQuantity, settings);
                totalCommission += exitCommission;
                executions.Add(new PaperExecution(exitFillId.ExecutionId, exitOrderId.TradingId, positionId, exitSide, exitQuantity, exitFillPrice, tick.TimestampUtc, exitCommission));
                var tradeGross = CalculatePnl(direction, filledEntryPrice.Value, exitFillPrice, exitQuantity, settings.PointValue);
                var tradePnl = tradeGross - exitCommission - (journal.Count == 0 ? entryCommission : 0m);
                grossPnl += tradeGross;
                realizedPnl += tradePnl;
                var reason = exit.Value.IsStop ? PaperExitReason.StopLoss : PaperExitReason.TakeProfit;
                journal.Add(new PaperTradeJournalEntry(tradeId, direction, exitQuantity, filledEntryPrice.Value, entryTimestamp.Value, exitFillPrice, tick.TimestampUtc, reason, tradePnl, mfe, mae, totalCommission));
                AddEvent(events, exit.Value.IsStop ? PaperTradingEventType.StopLossTriggered : !closesPosition ? PaperTradingEventType.PartialTargetHit : targets.Length == 1 ? PaperTradingEventType.TakeProfitTriggered : PaperTradingEventType.FinalTargetHit, tick.TimestampUtc, exitOrderId.ToString(), exit.Value.IsStop ? "Stop loss was triggered." : closesPosition ? "Final take profit was triggered." : "Partial take profit was triggered.", exitOrderId, exitFillId, tradeId);
                if (!exit.Value.IsStop && closesPosition && targets.Length == 1)
                {
                    AddEvent(events, PaperTradingEventType.TakeProfitTriggered, tick.TimestampUtc, exitOrderId.ToString(), "Take profit was triggered.", exitOrderId, exitFillId, tradeId);
                }
                AddEvent(events, PaperTradingEventType.ExitFilled, tick.TimestampUtc, exitFillId.ToString(), "Exit order filled by the simulated market tick.", exitOrderId, exitFillId, tradeId);
                remainingQuantity -= exitQuantity;
                var positionStatus = closesPosition ? PaperPositionStatus.Closed : PaperPositionStatus.PartiallyClosed;
                if (closesPosition)
                {
                    AddEvent(events, PaperTradingEventType.PositionClosed, tick.TimestampUtc, positionId.ToString(), "Paper position closed.", tradeId: tradeId);
                    logger.LogInformation("Paper position closed. SimulationId={SimulationId}, PositionId={PositionId}, ExitReason={ExitReason}", request.SimulationId, positionId, reason);
                }
                else
                {
                    logger.LogInformation("Paper position partially closed. SimulationId={SimulationId}, PositionId={PositionId}, RemainingQuantity={RemainingQuantity}", request.SimulationId, positionId, remainingQuantity);
                }
                openPosition = new PaperPositionSnapshot(tradeId, direction, remainingQuantity <= 0 ? quantity : remainingQuantity, filledEntryPrice.Value, entryTimestamp.Value, exitFillPrice, closesPosition ? 0m : CalculatePnl(direction, filledEntryPrice.Value, exitFillPrice, remainingQuantity, settings.PointValue), positionStatus, mfe, mae, closesPosition ? tick.TimestampUtc : null);
                snapshots.Add(openPosition);
                if (closesPosition)
                {
                    break;
                }
                continue;
            }

            AddEvent(events, PaperTradingEventType.UnrealizedPnlUpdated, tick.TimestampUtc, positionId.ToString(), "Unrealized PnL updated from the simulated market tick.", tradeId: tradeId);
            equityCurve.Add(new PaperEquityPoint(tick.TimestampUtc, request.InitialBalance + realizedPnl, request.InitialBalance + realizedPnl + unrealized, realizedPnl, unrealized));
            if (equityCurve.Count >= settings.MaximumEquityPoints)
            {
                if (ticks.Length > equityCurve.Count) warnings.Add(new("EQUITY_CURVE_TRUNCATED", "The equity curve exceeded the configured point limit."));
                break;
            }
        }

        if (entryFill is null)
        {
            ReplaceOrder(orders, CopyOrder(entryOrder, PaperOrderStatus.Expired, null));
            AddEvent(events, PaperTradingEventType.EntryExpired, createdAtUtc, entryOrderId.ToString(), "Entry was not reached during the simulated market path.", entryOrderId, tradeId: tradeId);
            warnings.Add(new("ENTRY_NOT_FILLED", "The entry level was not reached by the supplied market ticks."));
            logger.LogInformation("Paper entry expired without a fill. SimulationId={SimulationId}, OrderId={OrderId}", request.SimulationId, entryOrderId);
        }
        else if (openPosition is not null && openPosition.Status != PaperPositionStatus.Closed)
        {
            warnings.Add(new("OPEN_POSITION", "The simulation ended while the paper position was still open."));
            journal.Add(new PaperTradeJournalEntry(tradeId, direction, remainingQuantity, filledEntryPrice!.Value, entryTimestamp!.Value, null, null, null, 0m, mfe, mae, totalCommission));
        }

        var finalUnrealized = openPosition is { Status: not PaperPositionStatus.Closed } ? openPosition.UnrealizedPnl : 0m;
        var statistics = BuildStatistics(journal, request.InitialBalance, equityCurve);
        var status = rejectedByAmbiguousTick
            ? PaperTradingStatus.Rejected
            : entryFill is null
                ? PaperTradingStatus.NoFill
                : warnings.Any(warning => warning.Code is "MARKET_TICKS_TRUNCATED" or "PRICE_PATH_TRUNCATED" or "EQUITY_CURVE_TRUNCATED")
                    ? PaperTradingStatus.PartiallySucceeded
                    : PaperTradingStatus.Succeeded;
        var state = status == PaperTradingStatus.PartiallySucceeded ? PaperTradingSimulationState.PartiallyCompleted : status == PaperTradingStatus.Rejected ? PaperTradingSimulationState.Rejected : PaperTradingSimulationState.Completed;
        AddEvent(events, PaperTradingEventType.SimulationCompleted, timeProvider.GetUtcNow(), request.SimulationId.ToString(), "Paper simulation completed.");
        return CreateResult(request, status, state, orders, fills, executions, openPosition, snapshots, realizedPnl, finalUnrealized, grossPnl, totalCommission, equityCurve, journal, statistics, events, warnings, errors, blockers, createdAtUtc, timeProvider.GetUtcNow());
    }

    private PaperTradingResult BuildRejected(PaperTradingRequest request, DateTimeOffset createdAtUtc, PaperTradingEligibilityDecision eligibility)
    {
        var isBlocked = eligibility.Errors.Any(error => error.Code is "WORKSPACE_NOT_PLAN_READY" or "WORKSPACE_STATUS_INELIGIBLE" or "WORKSPACE_BLOCKED");
        var status = eligibility.IsExpired ? PaperTradingStatus.Expired : isBlocked ? PaperTradingStatus.Blocked : eligibility.Errors.Count > 0 ? PaperTradingStatus.Invalid : eligibility.IsNoTrade ? PaperTradingStatus.NoTrade : PaperTradingStatus.Invalid;
        var state = eligibility.IsExpired ? PaperTradingSimulationState.Rejected : isBlocked ? PaperTradingSimulationState.Blocked : PaperTradingSimulationState.Rejected;
        var events = new List<PaperTradingEvent>();
        AddEvent(events, PaperTradingEventType.SessionStarted, createdAtUtc, request.SimulationId.ToString(), "Paper simulation started.");
        foreach (var warning in eligibility.Warnings) AddEvent(events, PaperTradingEventType.Warning, createdAtUtc, warning.Code, warning.Message);
        foreach (var error in eligibility.Errors) AddEvent(events, isBlocked ? PaperTradingEventType.SimulationBlocked : PaperTradingEventType.SimulationRejected, createdAtUtc, error.Code, error.Message);
        AddEvent(events, PaperTradingEventType.SimulationCompleted, timeProvider.GetUtcNow(), request.SimulationId.ToString(), "Paper simulation completed without a position.");
        var blockers = eligibility.Errors.Where(error => isBlocked || error.Blocking).Select(error => new PaperTradingBlocker(error.Code, error.Message)).ToArray();
        return CreateResult(request, status, state, [], [], [], null, [], 0m, 0m, 0m, 0m, [], [], EmptyStatistics(), events, eligibility.Warnings, eligibility.Errors, blockers, createdAtUtc, timeProvider.GetUtcNow());
    }

    private PaperTradingResult BuildInterrupted(PaperTradingRequest request, DateTimeOffset createdAtUtc, PaperTradingStatus status, PaperTradingSimulationState state, string code, string message) =>
        CreateResult(request, status, state, [], [], [], null, [], 0m, 0m, 0m, 0m, [], [], EmptyStatistics(), [new PaperTradingEvent(0, status == PaperTradingStatus.TimedOut ? PaperTradingEventType.Error : PaperTradingEventType.SimulationRejected, createdAtUtc, code, message)], [], [new PaperTradingError(code, message)], [new PaperTradingBlocker(code, message)], createdAtUtc, timeProvider.GetUtcNow());

    private PaperTradingResult CreateResult(PaperTradingRequest request, PaperTradingStatus status, PaperTradingSimulationState state, IReadOnlyCollection<PaperOrder> orders, IReadOnlyCollection<PaperFill> fills, IReadOnlyCollection<PaperExecution> executions, PaperPositionSnapshot? openPosition, IReadOnlyCollection<PaperPositionSnapshot> snapshots, decimal realizedPnl, decimal unrealizedPnl, decimal grossPnl, decimal commission, IReadOnlyCollection<PaperEquityPoint> equityCurve, IReadOnlyCollection<PaperTradeJournalEntry> journal, PaperTradingStatistics statistics, IReadOnlyCollection<PaperTradingEvent> events, IReadOnlyCollection<PaperTradingWarning> warnings, IReadOnlyCollection<PaperTradingError> errors, IReadOnlyCollection<PaperTradingBlocker> blockers, DateTimeOffset createdAtUtc, DateTimeOffset completedAtUtc)
    {
        var traces = request.Plan.Traces.Select(trace => new PaperTradingTraceReference("TradingPlan", trace.Origin, "Trading plan source trace.")).Concat(request.Workspace.Traces.Select(trace => new PaperTradingTraceReference("TradingWorkspace", trace.Origin, "Trading workspace source trace."))).Distinct().ToArray();
        var finalBalance = request.InitialBalance + realizedPnl;
        var finalEquity = finalBalance + unrealizedPnl;
        return new PaperTradingResult(request.SessionId, request.Workspace, request.Plan, status, orders.Take(_options.MaximumOrders).ToArray(), fills.Take(_options.MaximumFills).ToArray(), openPosition, realizedPnl, unrealizedPnl, equityCurve.Take(_options.MaximumEquityPoints).ToArray(), journal, statistics, events.Take(_options.MaximumTimelineEvents).ToArray(), warnings.Take(_options.MaximumWarnings).ToArray(), errors.Take(_options.MaximumErrors).ToArray(), ReplayFingerprint(request), status switch { PaperTradingStatus.Succeeded => "Paper trading simulation completed.", PaperTradingStatus.NoFill => "Paper trading simulation completed without an entry fill.", PaperTradingStatus.NoTrade => "Paper trading was not started because the plan is non-directional.", PaperTradingStatus.Blocked => "Paper trading was blocked by the upstream workspace.", _ => "Paper trading simulation did not produce an executable result." }, createdAtUtc, completedAtUtc, PaperTradingResult.CurrentSchemaVersion, state, request.InitialBalance, finalBalance, finalEquity, new PaperTradingPnL(realizedPnl, unrealizedPnl, commission, grossPnl), executions.Take(_options.MaximumExecutions).ToArray(), snapshots, blockers, traces);
    }

    private static PaperTradingStatistics EmptyStatistics() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static bool EntryTriggered(TradingPlanDirection direction, PaperTradingMarketTick tick, decimal entryPrice) => direction == TradingPlanDirection.Long ? tick.Ask <= entryPrice : tick.Bid >= entryPrice;

    private static ExitTrigger? FindExit(TradingPlanDirection direction, PaperTradingMarketTick tick, decimal stopPrice, IReadOnlyCollection<TradingPlanTarget> targets, int nextTargetOrdinal, PaperAmbiguousTriggerPolicy policy)
    {
        var stopHit = direction == TradingPlanDirection.Long ? (tick.Low ?? tick.Bid) <= stopPrice : (tick.High ?? tick.Ask) >= stopPrice;
        var target = targets.FirstOrDefault(item => item.Ordinal >= nextTargetOrdinal && (direction == TradingPlanDirection.Long ? (tick.High ?? tick.Bid) >= item.Price.Value : (tick.Low ?? tick.Ask) <= item.Price.Value));
        var targetHit = target is not null;
        if (!stopHit && !targetHit) return null;
        if (stopHit && targetHit)
        {
            return policy switch
            {
                PaperAmbiguousTriggerPolicy.TargetFirst => new ExitTrigger(false, target!.Price.Value, target.Ordinal, false),
                PaperAmbiguousTriggerPolicy.RejectAmbiguousTick => new ExitTrigger(false, stopPrice, 0, true),
                _ => new ExitTrigger(true, stopPrice, 0, false)
            };
        }

        return targetHit ? new ExitTrigger(false, target!.Price.Value, target.Ordinal, false) : new ExitTrigger(true, stopPrice, 0, false);
    }

    private static decimal MarketEntryPrice(TradingPlanDirection direction, PaperTradingMarketTick tick, PaperSpreadHandling spreadHandling) => spreadHandling == PaperSpreadHandling.UseMidPrice ? tick.Mid : direction == TradingPlanDirection.Long ? tick.Ask : tick.Bid;
    private static decimal MarketExitPrice(TradingPlanDirection direction, PaperTradingMarketTick tick, PaperSpreadHandling spreadHandling) => spreadHandling == PaperSpreadHandling.UseMidPrice ? tick.Mid : direction == TradingPlanDirection.Long ? tick.Bid : tick.Ask;
    private static decimal ApplyEntrySlippage(TradingPlanDirection direction, decimal price, SimulationSettings settings) => RoundPrice(direction == TradingPlanDirection.Long ? price + settings.Slippage : price - settings.Slippage, settings.PriceDecimals);
    private static decimal ApplyExitSlippage(TradingPlanDirection direction, decimal price, SimulationSettings settings) => RoundPrice(direction == TradingPlanDirection.Long ? price - settings.Slippage : price + settings.Slippage, settings.PriceDecimals);
    private static decimal CalculatePnl(TradingPlanDirection direction, decimal entryPrice, decimal exitPrice, decimal quantity, decimal pointValue) => (direction == TradingPlanDirection.Long ? exitPrice - entryPrice : entryPrice - exitPrice) * quantity * pointValue;
    private static decimal CalculateCommission(decimal price, decimal quantity, SimulationSettings settings) => settings.CommissionPerUnit * quantity + settings.CommissionRate * Math.Abs(price * quantity);
    private static decimal RoundPrice(decimal value, int? decimals) => decimals.HasValue ? Math.Round(value, decimals.Value, MidpointRounding.ToEven) : value;
    private static decimal RoundQuantity(decimal value, int? decimals) => decimals.HasValue ? Math.Round(value, decimals.Value, MidpointRounding.ToEven) : value;

    private static PaperTradingStatistics BuildStatistics(IReadOnlyCollection<PaperTradeJournalEntry> journal, decimal initialBalance, IReadOnlyCollection<PaperEquityPoint> equityCurve)
    {
        var closed = journal.Where(entry => entry.IsClosed).ToArray();
        var grossProfit = closed.Where(entry => entry.RealizedPnl > 0).Sum(entry => entry.RealizedPnl);
        var grossLoss = closed.Where(entry => entry.RealizedPnl < 0).Sum(entry => -entry.RealizedPnl);
        var maxDrawdown = 0m;
        var peak = initialBalance;
        foreach (var point in equityCurve) { peak = Math.Max(peak, point.Equity); maxDrawdown = Math.Max(maxDrawdown, peak - point.Equity); }
        return new PaperTradingStatistics(journal.Count, closed.Length, closed.Count(entry => entry.RealizedPnl > 0), closed.Count(entry => entry.RealizedPnl < 0), closed.Count(entry => entry.RealizedPnl == 0), grossProfit, grossLoss, closed.Sum(entry => entry.RealizedPnl), maxDrawdown, closed.Length == 0 ? 0 : closed.Average(entry => entry.RealizedPnl), journal.Count == 0 ? 0 : journal.Max(entry => entry.MaximumFavorableExcursion), journal.Count == 0 ? 0 : journal.Max(entry => entry.MaximumAdverseExcursion));
    }

    private static void ReplaceOrder(List<PaperOrder> orders, PaperOrder replacement) { var index = orders.FindIndex(order => order.OrderId == replacement.OrderId); if (index >= 0) orders[index] = replacement; }
    private static PaperOrder CopyOrder(PaperOrder source, PaperOrderStatus status, PaperFillId? fillId) => new(source.OrderId, source.TradeId, source.Type, source.Side, source.Quantity, source.RequestedPrice, source.CreatedAtUtc, status, fillId);
    private static void AddEvent(List<PaperTradingEvent> events, PaperTradingEventType type, DateTimeOffset timestampUtc, string sourceId, string description, PaperOrderId? orderId = null, PaperFillId? fillId = null, PaperTradeId? tradeId = null) => events.Add(new PaperTradingEvent(events.Count, type, timestampUtc, sourceId, description, orderId, fillId, tradeId));

    private static Guid DeterministicGuid(PaperTradingRequest request, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{request.SimulationId}|{request.Plan.PlanId}|{value}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string ReplayFingerprint(PaperTradingRequest request)
    {
        var input = new StringBuilder().Append(request.SimulationId).Append('|').Append(request.Plan.PlanId).Append('|').Append(request.InitialBalance.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
        foreach (var tick in request.MarketTicks.OrderBy(item => item.TimestampUtc).ThenBy(item => item.Sequence)) input.Append('|').Append(tick.Instrument.Symbol).Append('|').Append(tick.TimestampUtc.ToString("O")).Append('|').Append(tick.Sequence).Append('|').Append(tick.Bid).Append('|').Append(tick.Ask).Append('|').Append(tick.High).Append('|').Append(tick.Low);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString()))).ToLowerInvariant();
    }

    private readonly record struct ExitTrigger(bool IsStop, decimal TriggerPrice, int TargetOrdinal, bool Rejected);

    private sealed record SimulationSettings(decimal PointValue, decimal CommissionPerUnit, decimal CommissionRate, decimal Slippage, int? PriceDecimals, int? QuantityDecimals, PaperAmbiguousTriggerPolicy AmbiguousTriggerPolicy, PaperSpreadHandling SpreadHandling, bool AllowPartialTargetExits, int MaximumMarketTicks, int MaximumEquityPoints, IReadOnlyCollection<PaperTargetAllocation> TargetAllocations)
    {
        public static SimulationSettings Create(PaperTradingOptions app, PaperTradingSimulationOptions request)
        {
            var policy = app.TriggerTieBreakPolicy switch { PaperTriggerTieBreakPolicy.TakeProfitFirst => PaperAmbiguousTriggerPolicy.TargetFirst, PaperTriggerTieBreakPolicy.RejectAmbiguousTick => PaperAmbiguousTriggerPolicy.RejectAmbiguousTick, _ => request.AmbiguousTriggerPolicy };
            return new(request.PointValue == 1m ? app.PointValue : request.PointValue, request.CommissionPerUnit == 0m ? app.CommissionPerUnit : request.CommissionPerUnit, request.CommissionRate == 0m ? app.CommissionRate : request.CommissionRate, (request.SlippagePerUnit == 0m ? app.SlippagePerUnit : request.SlippagePerUnit) + (request.SlippageTicks == 0m ? app.SlippageTicks : request.SlippageTicks) * (request.TickSize == 0m ? app.TickSize : request.TickSize), request.PriceDecimals ?? app.PriceDecimals, request.QuantityDecimals ?? app.QuantityDecimals, policy, request.SpreadHandling == PaperSpreadHandling.UseBidAsk ? app.SpreadHandling : request.SpreadHandling, request.AllowPartialTargetExits, app.MaximumMarketTicks, app.MaximumEquityPoints, request.TargetAllocations.ToArray());
        }
    }
}
