namespace Liens.Application.Search;

internal readonly record struct FuzzyMatchScore(int Value, bool IsDirectMatch)
{
    public static readonly FuzzyMatchScore Empty = new(0, false);
}

internal static class FuzzySearchScorer
{
    public const int CandidateLimit = 5000;

    public static FuzzyMatchScore ScorePersonName(string? firstName, string? lastName, string keyword)
    {
        var forwardName = BuildPersonName(firstName, lastName);
        var reverseName = BuildPersonName(lastName, firstName);
        var normalizedKeyword = Normalize(keyword);
        var normalizedForward = Normalize(forwardName);
        var normalizedReverse = Normalize(reverseName);

        if (string.IsNullOrWhiteSpace(normalizedKeyword) || string.IsNullOrWhiteSpace(normalizedForward))
            return FuzzyMatchScore.Empty;

        var isDirectMatch = normalizedForward.Contains(normalizedKeyword, StringComparison.Ordinal) ||
                            normalizedReverse.Contains(normalizedKeyword, StringComparison.Ordinal);

        if (normalizedKeyword == normalizedForward)
            return new FuzzyMatchScore(1000, true);

        if (normalizedKeyword == normalizedReverse)
            return new FuzzyMatchScore(950, true);

        var nameTokens = GetNormalizedTokens(forwardName);
        var keywordTokens = GetNormalizedTokens(keyword);
        if (nameTokens.Count == 0 || keywordTokens.Count == 0)
            return FuzzyMatchScore.Empty;

        if (keywordTokens.Count == 1)
        {
            var keywordToken = keywordTokens[0];
            var bestTokenScore = nameTokens
                .Select(token => GetScore(token, keywordToken))
                .DefaultIfEmpty(0)
                .Max();

            if (nameTokens.Contains(keywordToken, StringComparer.Ordinal))
                return new FuzzyMatchScore(850, true);

            if (bestTokenScore >= 70)
                return new FuzzyMatchScore(750 + bestTokenScore, isDirectMatch);

            var partialSingleScore = Math.Max(
                GetKeywordFieldScore(forwardName, keyword),
                GetKeywordFieldScore(reverseName, keyword));

            return partialSingleScore >= 35
                ? new FuzzyMatchScore(400 + partialSingleScore, isDirectMatch)
                : FuzzyMatchScore.Empty;
        }

        var keywordTokenScores = keywordTokens
            .Select(keywordToken => nameTokens
                .Select(nameToken => GetScore(nameToken, keywordToken))
                .DefaultIfEmpty(0)
                .Max())
            .ToList();

        if (keywordTokens.Count == nameTokens.Count && keywordTokenScores.All(score => score >= 70))
            return new FuzzyMatchScore(700 + (int)keywordTokenScores.Average(), isDirectMatch);

        var exactTokenMatches = keywordTokens.Count(keywordToken =>
            nameTokens.Contains(keywordToken, StringComparer.Ordinal));
        if (exactTokenMatches > 0)
            return new FuzzyMatchScore(400 + (exactTokenMatches * 40) + keywordTokenScores.Max(), isDirectMatch);

        var partialScore = Math.Max(
            GetKeywordFieldScore(forwardName, keyword),
            GetKeywordFieldScore(reverseName, keyword));

        return partialScore >= 35
            ? new FuzzyMatchScore(300 + partialScore, isDirectMatch)
            : FuzzyMatchScore.Empty;
    }

    public static FuzzyMatchScore ScoreFields(string keyword, params string?[] sources)
    {
        var normalizedKeyword = Normalize(keyword);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
            return FuzzyMatchScore.Empty;

        var bestScore = 0;
        var hasDirectMatch = false;
        foreach (var source in sources)
        {
            var normalizedSource = Normalize(source);
            if (string.IsNullOrWhiteSpace(normalizedSource))
                continue;

            bestScore = Math.Max(bestScore, GetKeywordFieldScore(source, keyword));
            hasDirectMatch |= normalizedSource.Contains(normalizedKeyword, StringComparison.Ordinal);
        }

        return new FuzzyMatchScore(bestScore, hasDirectMatch);
    }

    public static FuzzyMatchScore Best(params FuzzyMatchScore[] scores)
    {
        var best = FuzzyMatchScore.Empty;
        foreach (var score in scores)
        {
            if (score.Value > best.Value)
                best = score;
            else if (score.IsDirectMatch && !best.IsDirectMatch)
                best = best with { IsDirectMatch = true };
        }

        return best;
    }

    public static bool IsAccepted(FuzzyMatchScore score) =>
        score.IsDirectMatch ? score.Value >= 35 : score.Value >= 70;

    private static string BuildPersonName(string? firstName, string? lastName) =>
        string.Join(" ", new[] { firstName?.Trim(), lastName?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static List<string> GetNormalizedTokens(string input) =>
        input.Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();

    private static int GetKeywordFieldScore(string? source, string keyword)
    {
        var score = GetScore(source, keyword);
        var normalizedSource = Normalize(source);
        var normalizedKeyword = Normalize(keyword);

        if (!string.IsNullOrWhiteSpace(normalizedSource) &&
            !string.IsNullOrWhiteSpace(normalizedKeyword) &&
            normalizedSource.Contains(normalizedKeyword, StringComparison.Ordinal))
        {
            return Math.Max(score, 35);
        }

        return score;
    }

    private static int GetScore(string? source, string? keyword)
    {
        var normalizedSource = Normalize(source);
        var normalizedKeyword = Normalize(keyword);
        if (string.IsNullOrWhiteSpace(normalizedSource) || string.IsNullOrWhiteSpace(normalizedKeyword))
            return 0;

        if (normalizedSource == normalizedKeyword)
            return 100;

        var distance = LevenshteinDistance(normalizedSource, normalizedKeyword);
        var maxLength = Math.Max(normalizedSource.Length, normalizedKeyword.Length);
        if (maxLength == 0)
            return 0;

        var score = (int)((1.0 - ((double)distance / maxLength)) * 100);
        if (normalizedSource.Contains(normalizedKeyword, StringComparison.Ordinal))
            score += 10;
        if (normalizedSource.StartsWith(normalizedKeyword, StringComparison.Ordinal))
            score += 5;

        return score;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (source.Length == 0)
            return target.Length;
        if (target.Length == 0)
            return source.Length;

        var previous = Enumerable.Range(0, target.Length + 1).ToArray();
        var current = new int[target.Length + 1];

        for (var sourceIndex = 1; sourceIndex <= source.Length; sourceIndex++)
        {
            current[0] = sourceIndex;
            for (var targetIndex = 1; targetIndex <= target.Length; targetIndex++)
            {
                var substitutionCost = source[sourceIndex - 1] == target[targetIndex - 1] ? 0 : 1;
                current[targetIndex] = Math.Min(
                    Math.Min(current[targetIndex - 1] + 1, previous[targetIndex] + 1),
                    previous[targetIndex - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = new System.Text.StringBuilder(input.Length);
        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character))
                normalized.Append(char.ToLowerInvariant(character));
        }

        return normalized.ToString();
    }
}
