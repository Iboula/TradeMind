using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Domain.ApiKeys;

namespace TradeMind.Identity.Infrastructure.Security;

public sealed class ApiKeySecretGenerator(IOptions<IdentityOptions> options) : IApiKeySecretGenerator
{
    public ApiKeySecretMaterial Generate(string environmentName)
    {
        var prefix = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase) ? "tm_live" : "tm_test";
        var publicPart = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var secretPart = Base64Url(RandomNumberGenerator.GetBytes(32));
        var raw = $"{prefix}_{publicPart}_{secretPart}";
        var salt = RandomNumberGenerator.GetBytes(options.Value.ApiKeys.SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secretPart, salt, options.Value.ApiKeys.Pbkdf2Iterations, HashAlgorithmName.SHA512, options.Value.ApiKeys.HashSizeBytes);
        return new ApiKeySecretMaterial(publicPart, raw, new ApiKeySecretHash("PBKDF2-SHA512", salt, hash, options.Value.ApiKeys.Pbkdf2Iterations));
    }

    public static bool TryParse(string raw, out string publicPart, out string secretPart)
    {
        publicPart = string.Empty;
        secretPart = string.Empty;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 256) return false;
        var pieces = raw.Trim().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < 4 || pieces[0] != "tm" || pieces[1] is not ("live" or "test")) return false;
        if (pieces[2].Length != 24) return false;
        publicPart = pieces[2];
        secretPart = string.Join('_', pieces.Skip(3));
        return secretPart.Length >= 32;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
