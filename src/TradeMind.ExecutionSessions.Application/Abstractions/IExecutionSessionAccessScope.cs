namespace TradeMind.ExecutionSessions.Application.Abstractions;

/// <summary>Provider-neutral ownership scope applied by persistence queries.</summary>
public interface IExecutionSessionAccessScope
{
    string? OrganizationId { get; }
    string? TenantId { get; }
    bool IsRestricted { get; }
    bool IsAdministrativeOverride { get; }
}

public sealed class UnrestrictedExecutionSessionAccessScope : IExecutionSessionAccessScope
{
    public string? OrganizationId => null;
    public string? TenantId => null;
    public bool IsRestricted => false;
    public bool IsAdministrativeOverride => false;
}
