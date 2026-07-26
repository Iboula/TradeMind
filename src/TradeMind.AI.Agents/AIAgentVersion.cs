using System.Globalization;

namespace TradeMind.AI.Agents;

public sealed record AIAgentVersion : IComparable<AIAgentVersion>
{
    public AIAgentVersion(int major, int minor, int patch, string? preRelease = null)
    {
        if (major < 0 || minor < 0 || patch < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), "Semantic version components cannot be negative.");
        }

        if (preRelease is not null && !IsValidPreRelease(preRelease))
        {
            throw new ArgumentException("Pre-release must contain dot-separated ASCII alphanumeric or hyphen identifiers.", nameof(preRelease));
        }

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public string? PreRelease { get; }

    public bool IsStable => PreRelease is null;

    public static AIAgentVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new ArgumentException("Agent version must use semantic major.minor.patch format.", nameof(value));
        }

        return version;
    }

    public static bool TryParse(string? value, out AIAgentVersion version)
    {
        version = new AIAgentVersion(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        var versionAndPreRelease = value.Split('-', 2);
        var parts = versionAndPreRelease[0].Split('.');
        if (parts.Length != 3
            || !TryParseComponent(parts[0], out var major)
            || !TryParseComponent(parts[1], out var minor)
            || !TryParseComponent(parts[2], out var patch))
        {
            return false;
        }

        var preRelease = versionAndPreRelease.Length == 2 ? versionAndPreRelease[1] : null;
        if (preRelease is not null && !IsValidPreRelease(preRelease))
        {
            return false;
        }

        version = new AIAgentVersion(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(AIAgentVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var comparison = Major.CompareTo(other.Major);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0)
        {
            return comparison;
        }

        if (PreRelease is null)
        {
            return other.PreRelease is null ? 0 : 1;
        }

        if (other.PreRelease is null)
        {
            return -1;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public override string ToString()
    {
        var core = string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");
        return PreRelease is null ? core : $"{core}-{PreRelease}";
    }

    private static bool TryParseComponent(string value, out int component)
    {
        component = 0;
        return value.Length > 0
            && (value.Length == 1 || value[0] != '0')
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out component);
    }

    private static bool IsValidPreRelease(string value)
    {
        var identifiers = value.Split('.');
        return identifiers.All(identifier =>
            identifier.Length > 0
            && identifier.All(character =>
                character is >= 'a' and <= 'z'
                    or >= 'A' and <= 'Z'
                    or >= '0' and <= '9'
                    or '-'))
            && identifiers.All(identifier =>
                identifier.Length == 1
                || identifier[0] != '0'
                || identifier.Any(character => character is not (>= '0' and <= '9')));
    }

    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        for (var index = 0; index < Math.Min(leftParts.Length, rightParts.Length); index++)
        {
            var leftNumeric = int.TryParse(leftParts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightNumeric = int.TryParse(rightParts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            int comparison;
            if (leftNumeric && rightNumeric)
            {
                comparison = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric)
            {
                comparison = -1;
            }
            else if (rightNumeric)
            {
                comparison = 1;
            }
            else
            {
                comparison = string.Compare(leftParts[index], rightParts[index], StringComparison.Ordinal);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }
}
