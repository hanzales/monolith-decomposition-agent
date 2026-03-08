namespace Migration.Intelligence.Contracts.Orchestration;

public sealed class CodeInsightsContract
{
    public int TotalSourceFileCount { get; init; }
    public int ControllerLikeFileCount { get; init; }
    public int RepositoryLikeFileCount { get; init; }
    public int EndpointCount { get; init; }
    public int DependencyCount { get; init; }
    public int TableUsageCount { get; init; }
    public int ExternalCallCount { get; init; }
    public int LegacyRiskCount { get; init; }
    public List<string> TopLevelSourceFolders { get; init; } = new();
    public List<string> TopRiskFiles { get; init; } = new();
}
