namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class LegacyRiskDetailContract
{
    public required string RiskType { get; init; }
    public required string WhyRisky { get; init; }
    public List<string> ImpactedFiles { get; init; } = new();
    public required string MigrationImpact { get; init; }
    public required string RecommendedRemediation { get; init; }
    public List<string> AffectedDomains { get; init; } = new();
    public bool BlocksExtraction { get; init; }
    public bool RequiresAntiCorruptionLayerOrRefactor { get; init; }
}
