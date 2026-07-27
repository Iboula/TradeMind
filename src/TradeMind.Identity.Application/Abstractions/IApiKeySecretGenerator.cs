using TradeMind.Identity.Domain.ApiKeys;

namespace TradeMind.Identity.Application.Abstractions;

public interface IApiKeySecretGenerator
{
    ApiKeySecretMaterial Generate(string environmentName);
}

public sealed record ApiKeySecretMaterial(string PublicKeyId, string RawSecret, ApiKeySecretHash Hash);
