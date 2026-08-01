using System.Collections.Concurrent;
using TradeMind.Brokers.Domain;

namespace TradeMind.Brokers.Infrastructure.InMemory;

public sealed class InMemoryBrokerState
{
    internal ConcurrentDictionary<string, BrokerAccount> Accounts { get; } = new(StringComparer.Ordinal);
    internal ConcurrentDictionary<string, BrokerInstrumentSpecification> Instruments { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal ConcurrentDictionary<string, BrokerOrder> Orders { get; } = new(StringComparer.Ordinal);
    internal ConcurrentDictionary<string, BrokerPosition> Positions { get; } = new(StringComparer.Ordinal);
    internal ConcurrentDictionary<string, BrokerError> Rejections { get; } = new(StringComparer.Ordinal);

    public void ConfigureAccount(BrokerAccount account) => Accounts[account.AccountId.Value] = account;
    public void ConfigureInstrument(BrokerInstrumentSpecification specification) => Instruments[specification.Instrument] = specification;
    public void RejectOperation(string operation, BrokerError error) => Rejections[operation] = error;
    public void ClearRejections() => Rejections.Clear();
}
