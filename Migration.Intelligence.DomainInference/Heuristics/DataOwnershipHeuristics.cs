namespace Migration.Intelligence.DomainInference.Heuristics;

public sealed class DataOwnershipHeuristics
{
    public int CalculateScore(IEnumerable<string> sourceHints)
    {
        var hints = sourceHints.ToList();
        if (hints.Count == 0)
        {
            return 30;
        }

        var ownershipSignals = hints.Count(hint =>
            hint.Contains("data", StringComparison.OrdinalIgnoreCase)
            || hint.Contains("repository", StringComparison.OrdinalIgnoreCase)
            || hint.Contains("project:", StringComparison.OrdinalIgnoreCase));

        var ratio = (double)ownershipSignals / hints.Count;
        var score = 40 + (int)Math.Round(ratio * 55, MidpointRounding.AwayFromZero);

        return Math.Clamp(score, 0, 100);
    }
}
