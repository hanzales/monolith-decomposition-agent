namespace Migration.Intelligence.Design.Models;

public sealed class DataOwnershipPlan
{
    public required string DomainCandidate { get; init; }
    public List<TableOwnershipDecision> OwnedTables { get; init; } = new();
    public List<TableOwnershipDecision> SharedTables { get; init; } = new();
    public List<TableOwnershipDecision> ReferencedTables { get; init; } = new();
    public List<SharedDependencyDefinition> SharedDependencies { get; init; } = new();
    public bool RequiresSharedDatabasePhase { get; init; }
    public bool SupportsImmediateIsolation { get; init; }
    public string DatabaseSplitStrategy { get; init; } = string.Empty;
    public List<string> MigrationNotes { get; init; } = new();
}
