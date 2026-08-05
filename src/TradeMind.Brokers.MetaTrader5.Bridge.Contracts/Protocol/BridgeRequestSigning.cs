using System.Security.Cryptography;
using System.Text;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;

public static class BridgeRequestSigning
{
    public const string TimestampHeader = "X-TradeMind-Timestamp";
    public const string NonceHeader = "X-TradeMind-Nonce";
    public const string SignatureHeader = "X-TradeMind-Signature";
    public const string SignatureVersionHeader = "X-TradeMind-Signature-Version";
    public const string SignatureVersion = "1";

    public static string CreateSignature(string secret, string method, string path, string timestamp, string nonce, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(timestamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        body ??= string.Empty;

        var canonical = string.Join('\n', method.ToUpperInvariant(), path, timestamp, nonce, body);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool VerifySignature(string secret, string method, string path, string timestamp, string nonce, string body, string suppliedSignature)
    {
        if (string.IsNullOrWhiteSpace(suppliedSignature)) return false;
        var expected = CreateSignature(secret, method, path, timestamp, nonce, body);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(suppliedSignature.Trim().ToLowerInvariant()));
    }
}
