namespace RP.RAG.Application.Search;

public sealed class CategoryTokenIndex
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> _weights;

    private CategoryTokenIndex(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> weights)
    {
        _weights = weights;
    }

    public static CategoryTokenIndex Empty { get; } = new(new Dictionary<string, IReadOnlyDictionary<string, double>>());

    public int CategoryCount => _weights.Count;

    public string? DetectCategory(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _weights.Count == 0)
        {
            return null;
        }

        var queryTokens = Tokenize(query);

        if (queryTokens.Count == 0)
        {
            return null;
        }

        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, categoryWeights) in _weights)
        {
            var categoryScore = 0.0;

            foreach (var token in queryTokens)
            {
                if (categoryWeights.TryGetValue(token, out var weight))
                {
                    categoryScore += weight;
                }
            }

            if (categoryScore > 0)
            {
                scores[category] = categoryScore;
            }
        }

        if (scores.Count == 0)
        {
            return null;
        }

        var top = scores.OrderByDescending(kv => kv.Value).First();

        var second = scores
            .Where(kv => !string.Equals(kv.Key, top.Key, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value)
            .DefaultIfEmpty(0.0)
            .Max();

        if (top.Value < 0.3 || top.Value < second * 1.5)
        {
            return null;
        }

        return top.Key;
    }

    public static CategoryTokenIndex Create(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> weights)
        => new(weights);

    public static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return text
            .ToLowerInvariant()
            .Split(
                [' ', '-', '_', '/', '\\', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}