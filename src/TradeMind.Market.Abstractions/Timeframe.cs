namespace TradeMind.Market.Abstractions;

public sealed record Timeframe
{
    public static readonly Timeframe M1 = new("M1", TimeSpan.FromMinutes(1), false);
    public static readonly Timeframe M5 = new("M5", TimeSpan.FromMinutes(5), false);
    public static readonly Timeframe M15 = new("M15", TimeSpan.FromMinutes(15), false);
    public static readonly Timeframe M30 = new("M30", TimeSpan.FromMinutes(30), false);
    public static readonly Timeframe H1 = new("H1", TimeSpan.FromHours(1), false);
    public static readonly Timeframe H4 = new("H4", TimeSpan.FromHours(4), false);
    public static readonly Timeframe D1 = new("D1", null, true);
    public static readonly Timeframe W1 = new("W1", null, true);
    public static readonly Timeframe MN1 = new("MN1", null, true);

    private static readonly IReadOnlyDictionary<string, Timeframe> Supported =
        new Dictionary<string, Timeframe>(StringComparer.Ordinal)
        {
            [M1.Code] = M1,
            [M5.Code] = M5,
            [M15.Code] = M15,
            [M30.Code] = M30,
            [H1.Code] = H1,
            [H4.Code] = H4,
            [D1.Code] = D1,
            [W1.Code] = W1,
            [MN1.Code] = MN1
        };

    private Timeframe(string code, TimeSpan? duration, bool isCalendarBased)
    {
        Code = code;
        Duration = duration;
        IsCalendarBased = isCalendarBased;
    }

    public string Code { get; }

    public TimeSpan? Duration { get; }

    public bool IsCalendarBased { get; }

    public static IReadOnlyCollection<Timeframe> GetSupported() =>
        Array.AsReadOnly(Supported.Values.ToArray());

    public static Timeframe Parse(string value)
    {
        if (!TryParse(value, out var timeframe))
        {
            throw new ArgumentException("Timeframe is not supported.", nameof(value));
        }

        return timeframe!;
    }

    public static bool TryParse(string? value, out Timeframe? timeframe)
    {
        timeframe = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Supported.TryGetValue(value.Trim().ToUpperInvariant(), out timeframe);
    }

    public override string ToString() => Code;
}
