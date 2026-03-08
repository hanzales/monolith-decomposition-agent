namespace Migration.Intelligence.Design.Models;

public sealed class ServiceBlueprint
{
    public required string CandidateName { get; init; }
    public required string BoundedContextName { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> CoreCapabilities { get; init; } = new();
    public List<string> PrimaryControllers { get; init; } = new();
    public List<string> PrimaryRepositories { get; init; } = new();
    public List<string> PrimaryTables { get; init; } = new();
    public int CohesionScore { get; init; }
    public int CouplingScore { get; init; }
    public int MigrationReadinessScore { get; init; }
    public string MigrationReadinessLevel { get; init; } = "Unknown";
}
