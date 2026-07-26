using System.ComponentModel.DataAnnotations;

namespace TradeMind.ExecutionSessions.Infrastructure.Persistence;

public sealed class ExecutionSessionsPersistenceOptions
{
    public const string SectionName = "TradeMind:Persistence";

    [Required]
    public string Provider { get; set; } = "PostgreSql";

    [Required]
    public string ConnectionStringName { get; set; } = "TradeMind";

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    [Range(1, 1440)]
    public int IdempotencyTtlMinutes { get; set; } = 15;

    public bool EnableSensitiveDataLogging { get; set; }
    public bool EnableDetailedErrors { get; set; }
    public bool ApplyMigrationsOnStartup { get; set; }
}
