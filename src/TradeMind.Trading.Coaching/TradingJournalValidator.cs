using Microsoft.Extensions.Options;

namespace TradeMind.Trading.Coaching;

public sealed class TradingJournalValidator : ITradingJournalValidator
{
    private static readonly HashSet<string> SupportedDirections = new(
        ["long", "short", "buy", "sell", "achat", "vente"],
        StringComparer.OrdinalIgnoreCase);

    private readonly TradingJournalValidationOptions _options;

    public TradingJournalValidator(IOptions<TradingJournalValidationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public void Validate(TradingJournalAnalysisRequest request, Guid analysisId, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalidFields = new HashSet<string>(StringComparer.Ordinal);

        RequirePositive(request.EntryPrice, nameof(request.EntryPrice), invalidFields);
        RequirePositive(request.ExitPrice, nameof(request.ExitPrice), invalidFields);
        RequirePositive(request.StopLoss, nameof(request.StopLoss), invalidFields);
        RequirePositive(request.TakeProfit, nameof(request.TakeProfit), invalidFields);
        RequireNonNegative(request.PositionSize, nameof(request.PositionSize), invalidFields);
        RequirePositive(request.AccountBalance, nameof(request.AccountBalance), invalidFields);
        RequireNonNegative(request.RiskAmount, nameof(request.RiskAmount), invalidFields);
        RequirePercentage(request.PlannedRiskPercentage, nameof(request.PlannedRiskPercentage), invalidFields);
        RequirePercentage(request.ActualRiskPercentage, nameof(request.ActualRiskPercentage), invalidFields);

        if (request.ResultRMultiple is { } rMultiple
            && Math.Abs(rMultiple) > _options.MaximumAbsoluteRMultiple)
        {
            invalidFields.Add(nameof(request.ResultRMultiple));
        }

        if (request.Direction is { } direction && !SupportedDirections.Contains(direction.Trim()))
        {
            invalidFields.Add(nameof(request.Direction));
        }

        ValidateUtc(request.OpenedAtUtc, nameof(request.OpenedAtUtc), invalidFields);
        ValidateUtc(request.ClosedAtUtc, nameof(request.ClosedAtUtc), invalidFields);
        if (request.OpenedAtUtc is { } opened && request.ClosedAtUtc is { } closed && closed < opened)
        {
            invalidFields.Add(nameof(request.ClosedAtUtc));
        }

        ValidateTextFields(request, invalidFields);
        ValidateRiskConsistency(request, invalidFields);

        if (invalidFields.Count > 0)
        {
            throw new TradingJournalValidationException(
                analysisId,
                invalidFields.OrderBy(field => field, StringComparer.Ordinal).ToArray(),
                correlationId);
        }
    }

    private static void RequirePositive(decimal? value, string field, ISet<string> invalidFields)
    {
        if (value is <= 0)
        {
            invalidFields.Add(field);
        }
    }

    private static void RequireNonNegative(decimal? value, string field, ISet<string> invalidFields)
    {
        if (value is < 0)
        {
            invalidFields.Add(field);
        }
    }

    private static void RequirePercentage(decimal? value, string field, ISet<string> invalidFields)
    {
        if (value is < 0 or > 100)
        {
            invalidFields.Add(field);
        }
    }

    private static void ValidateUtc(DateTimeOffset? value, string field, ISet<string> invalidFields)
    {
        if (value is not null && value.Value.Offset != TimeSpan.Zero)
        {
            invalidFields.Add(field);
        }
    }

    private void ValidateRiskConsistency(TradingJournalAnalysisRequest request, ISet<string> invalidFields)
    {
        if (request.RiskAmount is not { } risk
            || request.AccountBalance is not { } balance
            || request.ActualRiskPercentage is not { } explicitPercentage
            || balance <= 0)
        {
            return;
        }

        var calculated = risk / balance * 100;
        if (Math.Abs(calculated - explicitPercentage) > _options.PercentageConsistencyTolerance)
        {
            invalidFields.Add(nameof(request.ActualRiskPercentage));
            invalidFields.Add(nameof(request.RiskAmount));
        }
    }

    private void ValidateTextFields(TradingJournalAnalysisRequest request, ISet<string> invalidFields)
    {
        ValidateText(request.JournalEntryId, nameof(request.JournalEntryId), invalidFields);
        ValidateText(request.Instrument, nameof(request.Instrument), invalidFields);
        ValidateText(request.Market, nameof(request.Market), invalidFields);
        ValidateText(request.Direction, nameof(request.Direction), invalidFields);
        ValidateText(request.SetupName, nameof(request.SetupName), invalidFields);
        ValidateText(request.Timeframe, nameof(request.Timeframe), invalidFields);
        ValidateText(request.EntryReason, nameof(request.EntryReason), invalidFields);
        ValidateText(request.ExitReason, nameof(request.ExitReason), invalidFields);
        ValidateText(request.PlanBeforeTrade, nameof(request.PlanBeforeTrade), invalidFields);
        ValidateText(request.ExecutionNotes, nameof(request.ExecutionNotes), invalidFields);
        ValidateText(request.EmotionsBefore, nameof(request.EmotionsBefore), invalidFields);
        ValidateText(request.EmotionsDuring, nameof(request.EmotionsDuring), invalidFields);
        ValidateText(request.EmotionsAfter, nameof(request.EmotionsAfter), invalidFields);
        ValidateCollection(request.RulesRespected, nameof(request.RulesRespected), invalidFields);
        ValidateCollection(request.Mistakes, nameof(request.Mistakes), invalidFields);
        ValidateCollection(request.Lessons, nameof(request.Lessons), invalidFields);
        ValidateCollection(request.Tags, nameof(request.Tags), invalidFields);
    }

    private void ValidateText(string? value, string field, ISet<string> invalidFields)
    {
        if (value?.Length > _options.MaximumTextLength)
        {
            invalidFields.Add(field);
        }
    }

    private void ValidateCollection(IEnumerable<string> values, string field, ISet<string> invalidFields)
    {
        if (values.Any(value => value.Length > _options.MaximumTextLength))
        {
            invalidFields.Add(field);
        }
    }
}
