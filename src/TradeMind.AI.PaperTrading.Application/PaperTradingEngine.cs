using TradeMind.AI.PaperTrading.Domain;

namespace TradeMind.AI.PaperTrading.Application;

/// <summary>Public engine facade for composition roots that do not need simulator implementation details.</summary>
public sealed class PaperTradingEngine(IPaperTradingSimulator simulator) : IPaperTradingEngine
{
    public Task<PaperTradingResult> SimulateAsync(PaperTradingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return simulator.SimulateAsync(request, cancellationToken);
    }
}
