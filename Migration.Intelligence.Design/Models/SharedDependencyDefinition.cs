namespace Migration.Intelligence.Design.Models;

public sealed class SharedDependencyDefinition
{
    public required string ComponentName { get; init; }
    public required string ComponentType { get; init; }
    public required string Recommendation { get; init; }
    public string Rationale { get; init; } = string.Empty;
}
