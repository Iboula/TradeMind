namespace TradeMind.Trading.Coaching;

public sealed class TradingJournalValidationException : Exception
{
    public TradingJournalValidationException(
        Guid analysisId,
        IReadOnlyCollection<string> invalidFields,
        string? correlationId = null)
        : base("The trading journal contains invalid or insufficient fields.")
    {
        AnalysisId = analysisId;
        CorrelationId = TradingCoachCollections.Normalize(correlationId);
        InvalidFields = TradingCoachCollections.CopyStrings(invalidFields);
    }

    public string ErrorCode => "TRADING_JOURNAL_VALIDATION_FAILED";
    public Guid AnalysisId { get; }
    public string? CorrelationId { get; }
    public IReadOnlyList<string> InvalidFields { get; }
}

public sealed class TradingCoachAnalysisException : Exception
{
    public TradingCoachAnalysisException(Guid analysisId, string? correlationId, Exception? innerException = null)
        : base("The trading coach analysis could not be completed.", innerException)
    {
        AnalysisId = analysisId;
        CorrelationId = TradingCoachCollections.Normalize(correlationId);
    }

    public string ErrorCode => "TRADING_COACH_ANALYSIS_FAILED";
    public Guid AnalysisId { get; }
    public string? CorrelationId { get; }
}

public sealed class TradingCoachResponseParsingException : Exception
{
    public TradingCoachResponseParsingException(Guid analysisId, string? correlationId = null, Exception? innerException = null)
        : base("The trading coach response did not match the required structured format.", innerException)
    {
        AnalysisId = analysisId;
        CorrelationId = TradingCoachCollections.Normalize(correlationId);
    }

    public string ErrorCode => "TRADING_COACH_RESPONSE_INVALID";
    public Guid AnalysisId { get; }
    public string? CorrelationId { get; }
}

public sealed class TradingCoachSafetyException : Exception
{
    public TradingCoachSafetyException(Guid analysisId, string? correlationId = null)
        : base("The trading coach response was rejected by the safety policy.")
    {
        AnalysisId = analysisId;
        CorrelationId = TradingCoachCollections.Normalize(correlationId);
    }

    public string ErrorCode => "TRADING_COACH_SAFETY_VIOLATION";
    public Guid AnalysisId { get; }
    public string? CorrelationId { get; }
}

public sealed class TradingCoachTimeoutException : Exception
{
    public TradingCoachTimeoutException(Guid analysisId, string? correlationId = null, Exception? innerException = null)
        : base("The trading coach analysis timed out.", innerException)
    {
        AnalysisId = analysisId;
        CorrelationId = TradingCoachCollections.Normalize(correlationId);
    }

    public string ErrorCode => "TRADING_COACH_TIMEOUT";
    public Guid AnalysisId { get; }
    public string? CorrelationId { get; }
}
