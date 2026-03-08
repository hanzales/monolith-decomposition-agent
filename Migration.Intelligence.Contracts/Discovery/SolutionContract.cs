namespace Migration.Intelligence.Contracts.Discovery;

public class SolutionContract
{
    public required string RelativePath { get; init; }
    public List<ProjectContract> Projects { get; init; } = new();
}
