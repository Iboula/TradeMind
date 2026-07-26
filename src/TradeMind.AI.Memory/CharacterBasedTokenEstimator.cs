namespace TradeMind.AI.Memory;

public sealed class CharacterBasedTokenEstimator : ITokenEstimator
{
    private readonly int _charactersPerToken;

    public CharacterBasedTokenEstimator(int charactersPerToken = 4)
    {
        if (charactersPerToken <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(charactersPerToken), "Characters per token must be positive.");
        }

        _charactersPerToken = charactersPerToken;
    }

    public int EstimateTokens(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(content.Length / (double)_charactersPerToken));
    }
}
