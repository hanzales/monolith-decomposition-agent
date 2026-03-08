namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class WorkflowAnalysisContract
{
    public required string WorkflowName { get; init; }
    public List<string> RelatedControllers { get; init; } = new();
    public List<string> RelatedDomains { get; init; } = new();
    public int ParticipationCount { get; init; }
    public string BoundaryNote { get; init; } = string.Empty;
}
