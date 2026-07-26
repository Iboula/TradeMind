using System.ComponentModel.DataAnnotations;

namespace TradeMind.Api.Composition;

public sealed class ApiOptions
{
    public CorrelationOptions Correlation { get; set; } = new();
    public IdempotencyOptions Idempotency { get; set; } = new();
    public PayloadLimitOptions PayloadLimits { get; set; } = new();
    public TimeoutOptions Timeouts { get; set; } = new();
    public OpenApiOptions OpenApi { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();
}

public sealed class CorrelationOptions
{
    [Range(8, 128)]
    public int MaximumLength { get; set; } = 64;
}

public sealed class IdempotencyOptions
{
    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int TtlMinutes { get; set; } = 15;

    [Range(8, 128)]
    public int MaximumKeyLength { get; set; } = 128;
}

public sealed class PayloadLimitOptions
{
    [Range(1024, 104857600)]
    public long MaximumBodyBytes { get; set; } = 5 * 1024 * 1024;
}

public sealed class TimeoutOptions
{
    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; set; } = 30;
}

public sealed class OpenApiOptions
{
    public bool Enabled { get; set; } = true;
    public bool EnableUi { get; set; }
}

public sealed class SecurityOptions
{
    public bool EnableHttpsRedirection { get; set; } = true;
    public bool EnableSecurityHeaders { get; set; } = true;
}

public sealed class DatabaseOptions
{
    public bool MigrateOnStartup { get; set; }
}
