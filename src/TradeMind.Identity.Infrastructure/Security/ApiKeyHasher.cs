using System.Security.Cryptography;
using System.Text;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.ApiKeys;

namespace TradeMind.Identity.Infrastructure.Security;

public sealed class ApiKeyHasher : IApiKeyHasher
{
    public bool Verify(string rawSecret, ApiKeySecretHash storedHash)
    {
        if (string.IsNullOrWhiteSpace(rawSecret)) return false;
        var calculated = Rfc2898DeriveBytes.Pbkdf2(rawSecret, storedHash.Salt, storedHash.Iterations, HashAlgorithmName.SHA512, storedHash.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(calculated, storedHash.Hash);
    }
}
