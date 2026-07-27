using TradeMind.Identity.Domain.ApiKeys;

namespace TradeMind.Identity.Application;

public sealed class IdentityConcurrencyException(string resource, string id) : InvalidOperationException($"The {resource} '{id}' was changed by another operation.")
{
    public string Resource { get; } = resource;
    public string ResourceId { get; } = id;
}

public sealed class ApiKeyNotFoundException(ApiKeyId id) : KeyNotFoundException($"API key '{id}' was not found.");
public sealed class ApiKeyLimitExceededException : InvalidOperationException
{
    public ApiKeyLimitExceededException() : base("The organization has reached its active API key limit.") { }
}

public sealed class IdentityAuthenticationException(string message) : InvalidOperationException(message);
public sealed class IdentityAuthorizationException(string permission) : UnauthorizedAccessException($"Permission '{permission}' is required.")
{
    public string Permission { get; } = permission;
}
