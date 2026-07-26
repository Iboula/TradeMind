namespace TradeMind.ExecutionSessions.Domain;

public sealed record ExecutionSessionFailure
{
    public ExecutionSessionFailure(string code, string message, string? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Message = message.Trim();
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        if (Code.Length > 128 || Message.Length > 2000 || Details?.Length > 4000)
        {
            throw new ArgumentException("Failure details exceed configured limits.");
        }
    }

    public string Code { get; }
    public string Message { get; }
    public string? Details { get; }
}
