namespace Migration.Intelligence.Core.Utilities;

public static class StringSimilarityUtility
{
    public static double CalculateJaccardSimilarity(string left, string right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);

        if (leftTokens.Count == 0 && rightTokens.Count == 0)
        {
            return 1.0;
        }

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    public static int CalculateLevenshteinDistance(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 0;
        }

        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var matrix = new int[left.Length + 1, right.Length + 1];

        for (var i = 0; i <= left.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = char.ToLowerInvariant(left[i - 1]) == char.ToLowerInvariant(right[j - 1]) ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }

    public static double CalculateNormalizedSimilarity(string left, string right)
    {
        var distance = CalculateLevenshteinDistance(left, right);
        var maxLen = Math.Max(left.Length, right.Length);

        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .Split([' ', '.', '-', '_', '/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
