namespace Migration.Intelligence.Contracts.Analysis;

public sealed class DependencyContract
{
    public required string SourceType { get; init; }
    public required string TargetType { get; init; }
    public bool IsExternalDependency { get; init; }
}
