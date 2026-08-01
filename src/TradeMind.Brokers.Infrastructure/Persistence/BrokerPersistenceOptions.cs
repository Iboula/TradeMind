namespace TradeMind.Brokers.Infrastructure.Persistence;

public sealed class BrokerPersistenceOptions
{
    public bool Enabled { get; set; }
    public bool ApplyMigrationsOnStartup { get; set; }
    public string? ConnectionString { get; set; }
}
