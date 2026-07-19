using System.Globalization;

namespace TradeMind.AI.Application;

public sealed record PromptTemplateVersion : IComparable<PromptTemplateVersion>
{
    public PromptTemplateVersion(int major, int minor)
    {
        if (major < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), "Major version cannot be negative.");
        }

        if (minor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minor), "Minor version cannot be negative.");
        }

        Major = major;
        Minor = minor;
    }

    public int Major { get; }

    public int Minor { get; }

    public static PromptTemplateVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new ArgumentException("Prompt template version must use major.minor format.", nameof(value));
        }

        return version;
    }

    public static bool TryParse(string? value, out PromptTemplateVersion version)
    {
        version = new PromptTemplateVersion(0, 0);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        if (major < 0 || minor < 0)
        {
            return false;
        }

        version = new PromptTemplateVersion(major, minor);
        return true;
    }

    public int CompareTo(PromptTemplateVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0
            ? majorComparison
            : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}";
}
