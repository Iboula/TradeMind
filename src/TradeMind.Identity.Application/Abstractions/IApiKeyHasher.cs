using TradeMind.Identity.Domain.ApiKeys;

namespace TradeMind.Identity.Application.Abstractions;

public interface IApiKeyHasher
{
    bool Verify(string rawSecret, ApiKeySecretHash storedHash);
}

public interface IApiKeyCredentialValidator
{
    Task<TradeMind.Identity.Domain.Actors.ActorIdentity?> ValidateAsync(string publicKeyId, string rawSecret, CancellationToken cancellationToken);
}

public interface IApiKeyUsageRecorder
{
    Task RecordUseAsync(TradeMind.Identity.Domain.ApiKeys.ApiKeyId id, DateTimeOffset usedAtUtc, CancellationToken cancellationToken);
}
