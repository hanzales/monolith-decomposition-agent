using Migration.Intelligence.Contracts.Common;

namespace Migration.Intelligence.Contracts.Domain;

public sealed class MigrationRecommendationContract
{
    public required string ServiceName { get; init; }
    public required string Summary { get; init; }
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;
    public List<string> ActionItems { get; init; } = new();
}
