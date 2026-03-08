namespace Migration.Intelligence.Contracts.Discovery;

public class ProjectContract
{
    public required string Name { get; init; }
    public required string RelativePath { get; init; }
    public string TargetFramework { get; init; } = string.Empty;
}
