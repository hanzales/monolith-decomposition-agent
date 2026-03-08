using Migration.Intelligence.Contracts.Common;

namespace Migration.Intelligence.Contracts.Domain;

public sealed class MigrationScoreContract
{
    public required string ServiceName { get; init; }
    public int NamingScore { get; init; }
    public int CohesionScore { get; init; }
    public int CouplingScore { get; init; }
    public int DataOwnershipScore { get; init; }
    public int DependencyHealthScore { get; init; }
    public int LegacyRiskScore { get; init; }
    public int EstimatedDependencyCount { get; init; }
    public int EstimatedLegacyRiskCount { get; init; }
    public int OverallScore { get; init; }
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;
}
