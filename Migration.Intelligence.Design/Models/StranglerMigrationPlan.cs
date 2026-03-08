namespace Migration.Intelligence.Design.Models;

public sealed class StranglerMigrationPlan
{
    public required string DomainCandidate { get; init; }
    public required ExtractionStrategy ExtractionStrategy { get; init; }
    public bool ReadOnlyFirstCandidate { get; init; }
    public bool StagedMigrationRecommended { get; init; }
    public List<MigrationPhaseDefinition> Phases { get; init; } = new();
    public List<string> MonolithRetentionItems { get; init; } = new();
    public List<string> RollbackConsiderations { get; init; } = new();
    public List<string> MigrationBlockers { get; init; } = new();
    public List<string> MigrationNotes { get; init; } = new();
}
