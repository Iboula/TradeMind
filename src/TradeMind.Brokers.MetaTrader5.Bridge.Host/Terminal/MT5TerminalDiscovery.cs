using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal sealed class MT5TerminalDiscovery : IMT5TerminalDiscovery
{
    public Task<MT5TerminalDescriptor> DiscoverAsync(string terminalPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(terminalPath)) throw new InvalidOperationException("The terminal path is not configured.");
        var fullPath = Path.GetFullPath(terminalPath);
        if (!File.Exists(fullPath)) throw new InvalidOperationException("The configured demo terminal was not found.");

        var versionInfo = FileVersionInfo.GetVersionInfo(fullPath);
        var version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "unknown";
        var build = ParseBuild(version);
        var architecture = DetectArchitecture(fullPath);
        return Task.FromResult(new MT5TerminalDescriptor(fullPath, version, build, architecture, IsRunning(fullPath)));
    }

    public bool IsRunning(string terminalPath)
    {
        var fullPath = Path.GetFullPath(terminalPath);
        var processName = Path.GetFileNameWithoutExtension(fullPath);
        if (string.IsNullOrWhiteSpace(processName)) return false;
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, fullPath, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                catch (NotSupportedException) { }
            }
        }

        return false;
    }

    private static int ParseBuild(string version)
    {
        var buildMatch = Regex.Match(version, "build\\s*(?<build>\\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (buildMatch.Success && int.TryParse(buildMatch.Groups["build"].Value, out var build)) return build;
        var values = Regex.Matches(version, "\\d+")
            .Select(match => int.TryParse(match.Value, out var value) ? value : 0)
            .Where(value => value > 0)
            .ToArray();
        return values.Length == 0 ? 0 : values.Max();
    }

    private static string DetectArchitecture(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            return reader.PEHeaders.CoffHeader.Machine switch
            {
                Machine.Amd64 => "x64",
                Machine.I386 => "x86",
                _ => "unknown"
            };
        }
        catch (IOException) { return "unknown"; }
        catch (BadImageFormatException) { return "unknown"; }
    }
}
