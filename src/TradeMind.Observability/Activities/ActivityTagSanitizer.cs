using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Activities;

public static class ActivityTagSanitizer
{
    private static readonly string[] ForbiddenFragments =
    ["authorization", "token", "secret", "password", "cookie", "body", "prompt", "sql", "connection"];

    public static bool IsAllowed(string name) =>
        TelemetryTagNames.Allowed.Contains(name)
        && !ForbiddenFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }
}
