namespace Migration.Intelligence.DomainInference.Heuristics;

public sealed class NamingHeuristics
{
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Core", "Common", "Shared", "Base", "Platform", "Default"
    };

    public int CalculateScore(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return 0;
        }

        var score = 50;

        if (serviceName.Length >= 5)
        {
            score += 20;
        }

        if (serviceName.Any(char.IsDigit))
        {
            score -= 15;
        }

        if (GenericNames.Contains(serviceName))
        {
            score -= 25;
        }

        if (serviceName.All(char.IsUpper))
        {
            score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }
}
