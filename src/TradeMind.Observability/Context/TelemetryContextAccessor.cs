using System.Threading;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Observability.Context;

public sealed class TelemetryContextAccessor : ITelemetryContextAccessor
{
    private readonly AsyncLocal<TelemetryContext?> _current = new();

    public TelemetryContext Current => _current.Value ?? TelemetryContext.System();

    public IDisposable Push(TelemetryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = _current.Value;
        _current.Value = context;
        return new Scope(() => _current.Value = previous);
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        private Action? _restore = restore;
        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}
