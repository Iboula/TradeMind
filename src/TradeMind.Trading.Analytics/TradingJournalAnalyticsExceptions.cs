namespace TradeMind.Trading.Analytics;

public abstract class TradingJournalAnalyticsException : Exception
{
    protected TradingJournalAnalyticsException(
        string errorCode,
        string message,
        Guid analysisId,
        string? correlationId,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        AnalysisId = analysisId;
        CorrelationId = correlationId;
    }

    public string ErrorCode { get; }
    public Guid AnalysisId { get; }
    public string? CorrelationId { get; }
}

public sealed class TradingJournalAnalyticsValidationException : TradingJournalAnalyticsException
{
    public TradingJournalAnalyticsValidationException(
        Guid analysisId,
        IReadOnlyCollection<string> fields,
        string? correlationId = null,
        Exception? innerException = null)
        : base("JOURNAL_ANALYTICS_VALIDATION_FAILED", "The journal analytics request is invalid.", analysisId, correlationId, innerException)
    {
        Fields = Array.AsReadOnly(fields.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<string> Fields { get; }
}

public sealed class TradingJournalAnalyticsParsingException : TradingJournalAnalyticsException
{
    public TradingJournalAnalyticsParsingException(Guid analysisId, string? correlationId = null, Exception? innerException = null)
        : base("JOURNAL_ANALYTICS_RESPONSE_INVALID", "The journal analytics response is invalid.", analysisId, correlationId, innerException)
    {
    }
}

public sealed class TradingJournalAnalyticsSafetyException : TradingJournalAnalyticsException
{
    public TradingJournalAnalyticsSafetyException(Guid analysisId, string? correlationId = null)
        : base("JOURNAL_ANALYTICS_SAFETY_VIOLATION", "The journal analytics report violates the educational safety policy.", analysisId, correlationId)
    {
    }
}

public sealed class TradingJournalAnalyticsTimeoutException : TradingJournalAnalyticsException
{
    public TradingJournalAnalyticsTimeoutException(Guid analysisId, string? correlationId, Exception innerException)
        : base("JOURNAL_ANALYTICS_TIMEOUT", "The journal analytics interpretation timed out.", analysisId, correlationId, innerException)
    {
    }
}

public sealed class TradingJournalAnalyticsAnalysisException : TradingJournalAnalyticsException
{
    public TradingJournalAnalyticsAnalysisException(Guid analysisId, string? correlationId, Exception innerException)
        : base("JOURNAL_ANALYTICS_FAILED", "The journal analytics operation failed.", analysisId, correlationId, innerException)
    {
    }
}
