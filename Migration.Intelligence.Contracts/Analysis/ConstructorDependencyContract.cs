namespace Migration.Intelligence.Contracts.Analysis;

public sealed class ConstructorDependencyContract
{
    public required string OwnerType { get; init; }
    public required string DependencyType { get; init; }
    public bool IsOptional { get; init; }
}
