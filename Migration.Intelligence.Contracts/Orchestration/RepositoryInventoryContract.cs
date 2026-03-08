using Migration.Intelligence.Contracts.Discovery;

namespace Migration.Intelligence.Contracts.Orchestration;

public sealed class RepositoryInventoryContract
{
    public required string SourceRootPath { get; init; }
    public List<SolutionContract> Solutions { get; init; } = new();
    public List<ProjectContract> Projects { get; init; } = new();
    public List<FileContract> SourceFiles { get; init; } = new();
    public List<FileContract> MarkdownFiles { get; init; } = new();
}
