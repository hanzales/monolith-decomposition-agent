using Migration.Intelligence.Contracts.Common;
using Migration.Intelligence.Contracts.Domain;

namespace Migration.Intelligence.DomainInference.Services;

public sealed class RecommendationService
{
    public List<MigrationRecommendationContract> BuildRecommendations(
        IReadOnlyCollection<MigrationScoreContract> scoreContracts)
    {
        return scoreContracts
            .Select(score => new MigrationRecommendationContract
            {
                ServiceName = score.ServiceName,
                Summary = BuildSummary(score),
                RiskLevel = score.RiskLevel,
                ActionItems = BuildActionItems(score)
            })
            .ToList();
    }

    private static string BuildSummary(MigrationScoreContract score)
    {
        return score.RiskLevel switch
        {
            RiskLevel.Low => "Likely business boundary, but shared data and contracts must be hardened first.",
            RiskLevel.Medium => "Initial candidate detected from structural and naming signals; plan staged migration.",
            RiskLevel.High => "Requires validation via dependency and table ownership analysis before extraction.",
            _ => "Initial candidate detected from naming/path signals; do not extract directly before refactor."
        };
    }

    private static List<string> BuildActionItems(MigrationScoreContract score)
    {
        var items = new List<string>();

        if (score.CouplingScore < 55)
        {
            items.Add("Reduce inter-service coupling by isolating shared dependencies.");
        }

        if (score.DataOwnershipScore < 55)
        {
            items.Add("Clarify ownership of data stores and repository boundaries.");
        }

        if (score.DependencyHealthScore < 60)
        {
            items.Add("Reduce dependency fan-out and isolate framework-heavy integrations.");
        }

        if (score.LegacyRiskScore < 60)
        {
            items.Add("Address legacy framework hotspots before extracting this service.");
        }

        if (score.NamingScore < 55)
        {
            items.Add("Rename service boundaries to business-aligned terminology.");
        }

        if (items.Count == 0)
        {
            items.Add("Prepare migration runbook and define rollout milestones.");
        }

        return items;
    }
}
