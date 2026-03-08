namespace Migration.Intelligence.DomainInference.Heuristics;

public sealed class CohesionHeuristics
{
    public int CalculateScore(int sourceHintCount)
    {
        if (sourceHintCount <= 0)
        {
            return 20;
        }

        var score = 35 + Math.Min(50, sourceHintCount * 8);
        return Math.Clamp(score, 0, 100);
    }
}
