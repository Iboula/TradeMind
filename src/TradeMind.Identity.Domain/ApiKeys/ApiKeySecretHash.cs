namespace TradeMind.Identity.Domain.ApiKeys;

public sealed record ApiKeySecretHash
{
    public ApiKeySecretHash(string algorithm, byte[] salt, byte[] hash, int iterations)
    {
        if (string.IsNullOrWhiteSpace(algorithm)) throw new ArgumentException("Hash algorithm is required.", nameof(algorithm));
        if (salt is null || salt.Length < 16) throw new ArgumentException("A strong salt is required.", nameof(salt));
        if (hash is null || hash.Length < 16) throw new ArgumentException("A strong hash is required.", nameof(hash));
        if (iterations < 100_000) throw new ArgumentOutOfRangeException(nameof(iterations));
        Algorithm = algorithm.Trim();
        _salt = salt.ToArray();
        _hash = hash.ToArray();
        Iterations = iterations;
    }

    public string Algorithm { get; }
    private readonly byte[] _salt;
    private readonly byte[] _hash;
    public byte[] Salt => _salt.ToArray();
    public byte[] Hash => _hash.ToArray();
    public int Iterations { get; }
}
