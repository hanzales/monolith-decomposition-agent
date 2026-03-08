namespace Migration.Intelligence.Design.Models;

public sealed class MigrationPhaseDefinition
{
    public int PhaseOrder { get; init; }
    public required string Name { get; init; }
    public string Objective { get; init; } = string.Empty;
    public List<string> WorkItems { get; init; } = new();
    public List<string> ExitCriteria { get; init; } = new();
    public bool CanRollback { get; init; }
    public string RollbackStrategy { get; init; } = string.Empty;
}
