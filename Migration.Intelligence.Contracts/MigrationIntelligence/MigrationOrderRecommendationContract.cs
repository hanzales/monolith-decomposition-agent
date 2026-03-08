namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class MigrationOrderRecommendationContract
{
    public required int Rank { get; init; }
    public required string CandidateName { get; init; }
    public required string WhyFirstOrLater { get; init; }
    public List<string> MajorBlockers { get; init; } = new();
    public bool ReadOnlyFirstExtractionPossible { get; init; }
    public bool StagedMigrationRecommended { get; init; }
}
