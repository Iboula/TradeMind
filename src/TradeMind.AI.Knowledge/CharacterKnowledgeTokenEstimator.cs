namespace TradeMind.AI.Knowledge;

public sealed class CharacterKnowledgeTokenEstimator : IKnowledgeTokenEstimator
{
    private readonly int _charactersPerToken;

    public CharacterKnowledgeTokenEstimator(int charactersPerToken = 4)
    {
        if (charactersPerToken <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(charactersPerToken), "Characters per token must be positive.");
        }

        _charactersPerToken = charactersPerToken;
    }

    public int EstimateTokens(string content)
    {
        return string.IsNullOrEmpty(content)
            ? 0
            : Math.Max(1, (int)Math.Ceiling(content.Length / (double)_charactersPerToken));
    }
}
