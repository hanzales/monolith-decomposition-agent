namespace Migration.Intelligence.DomainInference.Heuristics;

public sealed class CouplingHeuristics
{
    public int CalculateScore(int relatedServiceCount, int totalServiceCount)
    {
        if (totalServiceCount <= 1)
        {
            return 85;
        }

        var couplingRatio = (double)relatedServiceCount / totalServiceCount;
        var score = 100 - (int)Math.Round(couplingRatio * 70, MidpointRounding.AwayFromZero);

        return Math.Clamp(score, 10, 100);
    }
}
