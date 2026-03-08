using Migration.Intelligence.Contracts.Analysis;

namespace Migration.Intelligence.CodeAnalysis.Models;

public sealed class AnalysisSummary
{
    public int ControllerCount { get; init; }
    public int RepositoryCount { get; init; }
    public int EndpointCount { get; init; }
    public int DependencyCount { get; init; }
    public int LegacyRiskCount { get; init; }
    public int TableUsageCount { get; init; }
    public int ExternalCallCount { get; init; }
    public List<string> TopFolders { get; init; } = new();
    public List<LegacyRiskContract> LegacyRisks { get; init; } = new();
}
