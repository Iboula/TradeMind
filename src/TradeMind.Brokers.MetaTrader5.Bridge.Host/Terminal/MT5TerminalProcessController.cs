using System.Diagnostics;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Host.Terminal;

internal sealed class MT5TerminalProcessController : IMT5TerminalProcessController
{
    private readonly object sync = new();
    private Process? ownedProcess;

    public Task StartAsync(string terminalPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (ownedProcess is { HasExited: false }) return Task.CompletedTask;
            ownedProcess = Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetFullPath(terminalPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(terminalPath)) ?? Environment.CurrentDirectory
            }) ?? throw new InvalidOperationException("The demo terminal process could not be started.");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Process? process;
        lock (sync) process = ownedProcess;
        if (process is null) return;
        try
        {
            if (process.HasExited) return;
            process.CloseMainWindow();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            process.Dispose();
            lock (sync) ownedProcess = null;
        }
    }
}
